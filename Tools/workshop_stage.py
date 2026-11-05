#!/usr/bin/env python3
r"""Prepare one of our mods for RimWorld's in-game Workshop uploader, and put things back afterwards.

RimWorld uploads the mod's whole folder with no filter (Verse.Steam.Workshop.SetWorkshopItemDataFrom ->
SteamUGC.SetItemContent(RootDir)). Our mods are junctions from the game's Mods folder to C:\Projects\RimworldMods\<Mod>,
which also holds Art/, Source/ (with obj/), and design docs. So before uploading, the junction is swapped for a clean
copy holding only what the game loads, and swapped back afterwards.

    python Tools/workshop_stage.py check Spatialist     # read-only: About.xml, preview, stale DLL, XML, state
    python Tools/workshop_stage.py stage Spatialist     # junction -> clean copy (RimWorld must be closed)
      ... start RimWorld, dev mode on, Mods, the mod's menu, "Upload to Steam Workshop", quit RimWorld ...
    python Tools/workshop_stage.py unstage Spatialist   # copy PublishedFileId.txt back, clean copy -> junction

Safety: the junction is removed with os.rmdir, which removes only the link and refuses a real folder that has contents.
unstage only deletes a real folder (never a junction or symlink, and none inside it) whose About.xml has the dev
mod's packageId. Both refuse while RimWorld is running. Stdlib only, Python 3.12+ (os.path.isjunction).
"""
import argparse
import os
import re
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

DEV_ROOT = Path(r"C:\Projects\RimworldMods")
GAME_MODS = Path(r"C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods")
STEAM_PREVIEW_LIMIT = 1024 * 1024

# What the game loads. Anything else at the mod root is reported as skipped, never shipped silently.
INCLUDE_NAMES = {"About", "LoadFolders.xml", "Languages", "Textures", "Sounds", "Defs", "Patches", "Assemblies", "Common"}
VERSION_DIR = re.compile(r"^\d+\.\d+$")
# Never shipped, even inside included folders.
EXCLUDE_DIRS = {"obj", "bin", ".vs", ".git"}
EXCLUDE_SUFFIXES = {".pdb", ".user", ".md"}


class Problem(Exception):
    pass


def is_included(name: str) -> bool:
    return name in INCLUDE_NAMES or bool(VERSION_DIR.match(name))


def rimworld_running() -> bool:
    out = subprocess.run(["tasklist", "/FI", "IMAGENAME eq RimWorldWin64.exe", "/NH"], capture_output=True, text=True)
    return "RimWorldWin64.exe" in out.stdout


def same_path(a: Path, b: Path) -> bool:
    return os.path.normcase(os.path.realpath(a)) == os.path.normcase(os.path.realpath(b))


def read_about(mod_dir: Path) -> ET.Element:
    about = mod_dir / "About" / "About.xml"
    if not about.is_file():
        raise Problem(f"no {about}")
    return ET.parse(about).getroot()


def state_of(mod: str) -> str:
    link = GAME_MODS / mod
    if os.path.isjunction(link):
        return "linked" if same_path(link, DEV_ROOT / mod) else "junction-elsewhere"
    if link.is_symlink():
        return "symlink"
    if link.is_dir():
        return "staged"
    return "missing"


def check(mod: str) -> list[str]:
    """Return a list of errors; print warnings and facts along the way."""
    dev = DEV_ROOT / mod
    errors = []
    if not dev.is_dir():
        return [f"no dev folder {dev}"]

    root = read_about(dev)
    name = root.findtext("name", "").strip()
    package = root.findtext("packageId", "").strip()
    versions = [li.text for li in root.findall("supportedVersions/li")]
    description = root.findtext("description", "").strip()
    print(f"name:        {name}")
    print(f"packageId:   {package}")
    print(f"versions:    {', '.join(versions) or '(none)'}")
    print(f"depends on:  {', '.join(li.findtext('packageId', '?') for li in root.findall('modDependencies/li'))}")
    if not name or not package:
        errors.append("About.xml needs <name> and <packageId>")
    if not versions:
        errors.append("About.xml needs <supportedVersions>")
    if len(description) < 80:
        errors.append("About.xml <description> is missing or very short")

    pfid = dev / "About" / "PublishedFileId.txt"
    if pfid.is_file():
        print(f"workshop id: {pfid.read_text().strip()} (an update: Steam keeps the page's current description)")
    else:
        print("workshop id: none yet (a FIRST upload: the About.xml description becomes the page description)")

    preview = dev / "About" / "Preview.png"
    if not preview.is_file():
        errors.append("missing About/Preview.png (the Workshop thumbnail)")
    elif preview.stat().st_size >= STEAM_PREVIEW_LIMIT:
        errors.append(f"About/Preview.png is {preview.stat().st_size // 1024} KB; Steam's limit is 1 MB")
    else:
        print(f"preview:     {preview.stat().st_size // 1024} KB")
    if not (dev / "About" / "ModIcon.png").is_file():
        print("warning: no About/ModIcon.png (optional; shown in the 1.6 mod list)")

    # Gizmos and the mod list draw icons into square rects, so a trimmed 128x112 icon comes out squashed.
    icons = list(dev.glob("Textures/**/Abilities/*.png")) + list(dev.glob("About/ModIcon.png"))
    for icon in icons:
        w, h = png_size(icon)
        if w != h:
            errors.append(f"icon not square ({w}x{h}): {icon.relative_to(dev)}")

    # A DLL older than its source means the last change was never built.
    sources = list((dev / "Source").rglob("*.cs")) if (dev / "Source").is_dir() else []
    sources = [s for s in sources if not (EXCLUDE_DIRS & set(s.parts))]
    dlls = list(dev.glob("*/Assemblies/*.dll"))
    if sources:
        newest_src = max(s.stat().st_mtime for s in sources)
        if not dlls:
            errors.append("C# source exists but no built DLL in */Assemblies")
        elif max(d.stat().st_mtime for d in dlls) < newest_src:
            errors.append("the DLL is older than the newest .cs file: rebuild first")

    bad_xml = 0
    for xml in dev.rglob("*.xml"):
        rel = xml.relative_to(dev)
        if not is_included(rel.parts[0]) or EXCLUDE_DIRS & set(rel.parts):
            continue
        try:
            ET.parse(xml)
        except ET.ParseError as e:
            bad_xml += 1
            errors.append(f"malformed XML: {rel}: {e}")
    print(f"xml:         {'all well-formed' if not bad_xml else f'{bad_xml} malformed'}")

    shipped, skipped = plan_copy(dev)
    size = sum(f.stat().st_size for f in shipped)
    print(f"would ship:  {len(shipped)} files, {size / 1024 / 1024:.1f} MB")
    print(f"skipped:     {', '.join(sorted(skipped)) or '(nothing)'}")
    print(f"state:       {state_of(mod)}")
    return errors


def png_size(path: Path):
    """Width and height from the PNG header (stdlib only)."""
    with open(path, "rb") as f:
        head = f.read(24)
    if head[:8] != b"\x89PNG\r\n\x1a\n":
        return (0, -1)
    return int.from_bytes(head[16:20], "big"), int.from_bytes(head[20:24], "big")


def plan_copy(dev: Path):
    shipped, skipped = [], set()
    for entry in dev.iterdir():
        if not is_included(entry.name):
            skipped.add(entry.name + ("/" if entry.is_dir() else ""))
            continue
        files = [entry] if entry.is_file() else [p for p in entry.rglob("*") if p.is_file()]
        for f in files:
            rel = f.relative_to(dev)
            if EXCLUDE_DIRS & set(rel.parts[:-1]) or f.suffix.lower() in EXCLUDE_SUFFIXES:
                skipped.add(str(rel.parent / "*") if EXCLUDE_DIRS & set(rel.parts[:-1]) else str(rel))
                continue
            shipped.append(f)
    return shipped, skipped


def stage(mod: str):
    errors = check(mod)
    if errors:
        raise Problem("fix these first:\n  - " + "\n  - ".join(errors))
    if rimworld_running():
        raise Problem("RimWorld is running; close it first (it reads the mod list at startup)")
    dev, link = DEV_ROOT / mod, GAME_MODS / mod
    if state_of(mod) != "linked":
        raise Problem(f"{link} is '{state_of(mod)}', not a junction to {dev}; not touching it")

    tmp = GAME_MODS / f"{mod}.staging-tmp"
    if tmp.exists():
        raise Problem(f"leftover {tmp}: inspect and delete it by hand")
    shipped, _ = plan_copy(dev)
    for f in shipped:
        dest = tmp / f.relative_to(dev)
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(f, dest)

    os.rmdir(link)  # removes the junction itself; never recurses into the dev folder
    try:
        os.rename(tmp, link)
    except OSError:
        make_junction(link, dev)
        raise
    print(f"\nstaged: {link} is now a clean copy ({len(shipped)} files).")
    print("Next: start RimWorld, Options > Development mode on, Mods, open this mod's menu, 'Upload to Steam Workshop'.")
    print(f"Then quit RimWorld and run: python Tools/workshop_stage.py unstage {mod}")


def unstage(mod: str):
    if rimworld_running():
        raise Problem("RimWorld is running; close it first (it may hold files in the staged copy)")
    dev, link = DEV_ROOT / mod, GAME_MODS / mod
    state = state_of(mod)
    if state == "linked":
        print("already linked; nothing to do")
        return
    if state != "staged":
        raise Problem(f"{link} is '{state}'; expected a staged copy")
    if read_about(link).findtext("packageId", "").strip() != read_about(dev).findtext("packageId", "").strip():
        raise Problem(f"{link} has a different packageId from the dev folder; not deleting it")
    for walk_root, dirs, _ in os.walk(link):
        for d in dirs:
            p = Path(walk_root) / d
            if os.path.isjunction(p) or p.is_symlink():
                raise Problem(f"{p} is a link; not deleting anything")

    staged_id = link / "About" / "PublishedFileId.txt"
    if staged_id.is_file():
        pid = staged_id.read_text().strip()
        dev_id = dev / "About" / "PublishedFileId.txt"
        if not dev_id.is_file() or dev_id.read_text().strip() != pid:
            shutil.copy2(staged_id, dev_id)
            print(f"copied PublishedFileId.txt ({pid}) back to the dev folder: keep it, later uploads update this item")
        print(f"workshop page: https://steamcommunity.com/sharedfiles/filedetails/?id={pid}")
    else:
        print("note: no PublishedFileId.txt in the staged copy, so the upload probably didn't happen")

    shutil.rmtree(link)
    make_junction(link, dev)
    print(f"unstaged: {link} -> {dev} (junction restored)")


def make_junction(link: Path, target: Path):
    subprocess.run(["cmd", "/c", "mklink", "/J", str(link), str(target)], check=True, capture_output=True)
    if not (os.path.isjunction(link) and same_path(link, target)):
        raise Problem(f"failed to recreate the junction {link} -> {target}; run: mklink /J \"{link}\" \"{target}\"")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("command", choices=["check", "stage", "unstage"])
    ap.add_argument("mod", help="folder name, e.g. Spatialist or PhantomHand")
    args = ap.parse_args()
    try:
        if args.command == "check":
            errors = check(args.mod)
            print("\nready to stage" if not errors else "\nNOT ready:\n  - " + "\n  - ".join(errors))
            sys.exit(1 if errors else 0)
        {"stage": stage, "unstage": unstage}[args.command](args.mod)
    except Problem as e:
        sys.exit(f"stopped: {e}")


if __name__ == "__main__":
    main()
