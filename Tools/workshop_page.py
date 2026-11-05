#!/usr/bin/env python3
r"""Build a mod's Steam Workshop page assets from its own defs: the psycast tree, one card per psycast, header
banners, and the BBCode that strings them together. Output goes to <Mod>/Art/workshop/.

    python Tools/workshop_page.py PhantomHand

Images are linked from the public Arcanum repo (raw.githubusercontent.com), so commit and push Art/workshop/ before
pasting the BBCode into the Steam page. The tree is drawn with VPE's own card geometry (decompiled 2026-11-05 from
PsycastsUIUtility.DoPathAbilities / DrawPathBackground and ITab_Pawn_Psycasts):
  - card ~190 px wide, background at its own aspect, plus a 30 px name bar below
  - row i (level i+1) at y = (MaxLevel - 1 - i) * height / MaxLevel + 10, level 1 at the bottom
  - 36 px icons; left edges at centre + {-18} | {-47, 11} | {-69, -18, 33}
  - rows sorted by `order`; `spaceAfter` adds a blank slot; prerequisite lines 2 px, drawn under the icons
Ornament art (banner and card frames) is commissioned from Codex into <Mod>/Art/raw/ws_*.png; lettering is drawn here.
Needs Pillow (use Necro's IconSheetSplitter venv).
"""
import argparse
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(r"C:\Projects\RimworldMods")
REPO_RAW = "https://raw.githubusercontent.com/JaimeBarrachina/Arcanum/main"
FONTS = Path(r"C:\Windows\Fonts")
STEAM_BG = (27, 37, 50)  # #1b2532, the page colour the ornaments are drawn on

# VPE geometry, in VPE UI pixels.
CARD_W, NAME_BAR, ICON = 190, 30, 36
X_OFFSETS = {1: [-18], 2: [-47, 11], 3: [-69, -18, 33]}
FRAME_COLOR = (97, 108, 122)
NAME_BAR_COLOR = (21, 25, 29)

# Per-mod look.
THEMES = {
    "PhantomHand": {"accent": (243, 228, 184), "glow": (214, 166, 70), "dim": (150, 120, 70)},
    "Spatialist": {"accent": (217, 201, 242), "glow": (150, 110, 210), "dim": (110, 85, 150)},
}


def font(name, size):
    return ImageFont.truetype(str(FONTS / name), size)


class Psycast:
    def __init__(self, el: ET.Element):
        ext = next(li for li in el.iter("li") if li.get("Class", "").endswith("AbilityExtension_Psycast"))
        self.defName = el.findtext("defName")
        self.label = el.findtext("label")
        self.description = (el.findtext("description") or "").replace("\\n", "\n")
        self.icon_path = el.findtext("iconPath")
        self.ability_class = el.findtext("abilityClass") or ""
        self.level = int(ext.findtext("level"))
        self.order = float(ext.findtext("order") or 0)
        self.space_after = (ext.findtext("spaceAfter") or "").lower() == "true"
        self.prereqs = [li.text for li in ext.findall("prerequisites/li")]
        self.psyfocus = float(ext.findtext("psyfocusCost") or 0)
        self.heat = float(ext.findtext("entropyGain") or 0)
        self.cooldown = int(el.findtext("cooldownTime") or 0)

    @property
    def passive(self):
        return "Passive" in self.ability_class


def load(mod: str):
    dev = ROOT / mod
    path_el = next(ET.parse(f).getroot().find("VanillaPsycastsExpanded.PsycasterPathDef")
                   for f in (dev / "1.6/Defs/PsycasterPathDefs").glob("*.xml"))
    path = {"defName": path_el.findtext("defName"), "label": path_el.findtext("label"),
            "background": dev / "Textures" / (path_el.findtext("background") + ".png")}
    casts = []
    for f in (dev / "1.6/Defs/AbilityDefs").glob("*.xml"):
        for el in ET.parse(f).getroot():
            if el.get("Abstract", "").lower() == "true":
                continue
            if any(li.get("Class", "").endswith("AbilityExtension_Psycast") for li in el.iter("li")):
                casts.append(Psycast(el))
    return dev, path, casts


def rows_of(casts):
    """VPE's abilityLevelsInOrder: per level, sorted by order, a None slot after each spaceAfter."""
    max_level = max(c.level for c in casts)
    rows = []
    for level in range(1, max_level + 1):
        row = []
        for c in sorted((c for c in casts if c.level == level), key=lambda c: c.order):
            row.append(c)
            if c.space_after:
                row.append(None)
        rows.append(row)
    return rows


def icon_image(dev, cast, size):
    return Image.open(dev / "Textures" / (cast.icon_path + ".png")).convert("RGBA").resize((size, size), Image.LANCZOS)


def render_tree(dev, path, casts, scale=3, margin=24):
    s = scale
    bg = Image.open(path["background"]).convert("RGBA")
    w = CARD_W * s
    h = round(w / bg.width * bg.height)
    rows = rows_of(casts)
    max_level = len(rows)

    card = Image.new("RGBA", (w, h + NAME_BAR * s), NAME_BAR_COLOR + (255,))
    card.alpha_composite(bg.resize((w, h), Image.LANCZOS), (0, 0))

    centres = {}
    for i, row in enumerate(rows):
        y = (max_level - 1 - i) * h / max_level + 10 * s
        for j, cast in enumerate(row):
            if cast is None:
                continue
            x = w / 2 + X_OFFSETS[len(row)][j] * s
            centres[cast.defName] = (x + ICON * s / 2, y + ICON * s / 2, x, y)

    draw = ImageDraw.Draw(card)
    for cast in casts:
        for pre in cast.prereqs:
            if cast.defName in centres and pre in centres:
                a, b = centres[cast.defName], centres[pre]
                draw.line([a[:2], b[:2]], fill=(255, 255, 255, 255), width=2 * s)
    for cast in casts:
        if cast.defName not in centres:
            continue
        _, _, x, y = centres[cast.defName]
        box = [round(x), round(y), round(x + ICON * s) - 1, round(y + ICON * s) - 1]
        draw.rectangle(box, fill=(40, 42, 46, 235), outline=(110, 114, 120, 255), width=max(1, s // 2))
        card.alpha_composite(icon_image(dev, cast, ICON * s), (round(x), round(y)))

    label_font = font("arial.ttf", 13 * s)
    draw.text((w / 2, h + NAME_BAR * s / 2), path["label"][0].upper() + path["label"][1:], font=label_font,
              fill=(230, 230, 230), anchor="mm")

    out = Image.new("RGB", (w + 2 * margin * s // 3 + 4 * s, card.height + 2 * margin * s // 3 + 4 * s), STEAM_BG)
    ox, oy = (out.width - w) // 2, (out.height - card.height) // 2
    ImageDraw.Draw(out).rectangle([ox - 2 * s, oy - 2 * s, ox + w + 2 * s - 1, oy + card.height + 2 * s - 1],
                                  outline=FRAME_COLOR, width=max(1, s // 2))
    out.paste(card, (ox, oy), card)
    return out


# ---------------------------------------------------------------- ornaments (Codex frames + our lettering)

OUT_W = 1280  # Steam shows description images at roughly 640 px; text sizes below are chosen for that


def luminance(px):
    return 0.299 * px[0] + 0.587 * px[1] + 0.114 * px[2]


def content_box(im, threshold):
    """Bounding box of pixels brighter than `threshold` (luminance)."""
    mask = im.convert("L").point(lambda v: 255 if v > threshold else 0)
    return mask.getbbox()


def glow_text(base, xy, text, fnt, fill, glow, radius):
    layer = Image.new("RGBA", base.size, (0, 0, 0, 0))
    ImageDraw.Draw(layer).text(xy, text, font=fnt, fill=glow + (255,), anchor="mm")
    base.alpha_composite(layer.filter(ImageFilter.GaussianBlur(radius)))
    ImageDraw.Draw(base).text(xy, text, font=fnt, fill=fill + (255,), anchor="mm")


def render_banner(raw: Image.Image, title: str, theme) -> Image.Image:
    im = raw.convert("RGBA")
    bg = im.getpixel((5, 5))[:3]
    x0, y0, x1, y1 = content_box(im, 150)          # the glowing rules and stars
    fx0, _, fx1, _ = content_box(im, luminance(bg) + 14) or (x0, y0, x1, y1)  # include faint end motifs
    pad = (y1 - y0) // 3
    im = im.crop((max(0, fx0 - pad), max(0, y0 - pad), min(im.width, fx1 + pad), min(im.height, y1 + pad)))
    band_h = y1 - y0
    size = int(band_h * 0.55)
    max_w = im.width * 0.58
    while size > 10:
        fnt = font("CASTELAR.TTF", size)
        if fnt.getlength(title) <= max_w:
            break
        size -= 4
    glow_text(im, (im.width / 2, im.height / 2 + size * 0.04), title, fnt, theme["accent"], theme["glow"], size / 7)
    return im.resize((OUT_W, round(im.height * OUT_W / im.width)), Image.LANCZOS).convert("RGB")


def wrap(text, fnt, width):
    lines = []
    for para in text.split("\n"):
        if not para.strip():
            lines.append("")
            continue
        line = ""
        for word in para.split():
            trial = (line + " " + word).strip()
            if fnt.getlength(trial) <= width:
                line = trial
            else:
                lines.append(line)
                line = word
        lines.append(line)
    while lines and not lines[-1]:
        lines.pop()
    return lines


def meta_line(cast, by_def):
    bits = [f"Level {cast.level}"]
    if cast.passive:
        bits.append("passive")
    else:
        if cast.psyfocus:
            bits.append(f"{cast.psyfocus * 100:g}% psyfocus")
        if cast.heat:
            bits.append(f"{cast.heat:g} heat")
        if cast.cooldown >= 60000:
            bits.append("once per day" if cast.cooldown == 60000 else f"{cast.cooldown / 60000:g}-day cooldown")
    if cast.prereqs:
        names = [by_def[p].label.title() if p in by_def else p for p in cast.prereqs]
        bits.append("after " + " or ".join(names))
    return "  ·  ".join(bits)


def render_card(raw: Image.Image, dev, cast, by_def, theme) -> Image.Image:
    im = raw.convert("RGBA")
    fx0, fy0, fx1, fy1 = content_box(im, 150)  # glowing frame, star included
    pad = 24
    im = im.crop((max(0, fx0 - pad), max(0, fy0 - pad), min(im.width, fx1 + pad), min(im.height, fy1 + pad)))
    # frame without the star: the border rows are the brightest long horizontal runs
    gray = im.convert("L")
    w, h = im.size
    bright_rows = [y for y in range(h) if sum(1 for x in range(0, w, 8) if gray.getpixel((x, y)) > 150) > w / 8 * 0.6]
    top, bottom = bright_rows[0], bright_rows[-1]
    bright_cols = [x for x in range(w) if sum(1 for y in range(top, bottom, 8) if gray.getpixel((x, y)) > 150) > (bottom - top) / 8 * 0.6]
    left, right = bright_cols[0], bright_cols[-1]
    # icon slot: the dimmer square border inside the frame. Search only the left 40% of the panel and below the
    # star ornament that hangs from the top edge, or the star gets swept into the box.
    panel = gray.getpixel(((left + right) // 2, (top + bottom) // 2))
    inset = round((bottom - top) * 0.08)
    region = (left + inset, top + inset, left + round((right - left) * 0.4), bottom - inset)
    inner = gray.crop(region).point(lambda v: 255 if v > panel + 30 else 0)
    sx0, sy0, sx1, sy1 = inner.getbbox()
    slot = (sx0 + region[0], sy0 + region[1], sx1 + region[0], sy1 + region[1])

    s = w / OUT_W  # design sizes are for the 1280-wide output
    title_f = font("CASTELAR.TTF", round(46 * s))
    meta_f = font("arialbd.ttf", round(25 * s))
    body_f = font("arial.ttf", round(29 * s))
    text_x0 = slot[2] + round(36 * s)
    text_w = right - round(40 * s) - text_x0
    lines = wrap(cast.description, body_f, text_w)
    meta_lines = wrap(meta_line(cast, by_def), meta_f, text_w)
    line_h = round(body_f.size * 1.32)
    meta_h = round(meta_f.size * 1.3) * len(meta_lines)
    needed = round(26 * s) + title_f.size + round(14 * s) + meta_h + round(18 * s) + line_h * len(lines) + round(30 * s)
    available = bottom - top

    if needed > available:  # stretch through the plain band between the slot and the frame's bottom edge
        cut = (slot[3] + bottom) // 2
        extra = needed - available
        stretched = Image.new("RGBA", (w, h + extra))
        stretched.paste(im.crop((0, 0, w, cut)), (0, 0))
        strip = im.crop((0, cut, w, cut + 1))
        for i in range(extra):
            stretched.paste(strip, (0, cut + i))
        stretched.paste(im.crop((0, cut, w, h)), (0, cut + extra))
        im, bottom, h = stretched, bottom + extra, h + extra

    slot_size = slot[2] - slot[0]
    icon = icon_image(dev, cast, round(slot_size * 0.86))
    im.alpha_composite(icon, (slot[0] + (slot_size - icon.width) // 2, slot[1] + (slot[3] - slot[1] - icon.height) // 2))

    draw = ImageDraw.Draw(im)
    y = top + round(26 * s)
    draw.text((text_x0, y), cast.label.title(), font=title_f, fill=theme["accent"])
    y += title_f.size + round(14 * s)
    for line in meta_lines:
        draw.text((text_x0, y), line, font=meta_f, fill=theme["dim"])
        y += round(meta_f.size * 1.3)
    y += round(18 * s)
    for line in lines:
        draw.text((text_x0, y), line, font=body_f, fill=(214, 216, 220))
        y += line_h
    return im.resize((OUT_W, round(im.height * OUT_W / im.width)), Image.LANCZOS).convert("RGB")


# ---------------------------------------------------------------- page text

def about_sections(dev):
    """Split About.xml's plain description into intro, psycast list (ignored: cards replace it), notes, outro."""
    root = ET.parse(dev / "About" / "About.xml").getroot()
    text = root.findtext("description").strip()
    blocks = re.split(r"\n\s*\n", text)
    intro = blocks[0]
    notes, outro = [], []
    for block in blocks[1:]:
        head, *rest = block.split("\n")
        if head.rstrip(":").lower() == "good to know":
            notes = [l.lstrip("- ").strip() for l in rest if l.strip()]
        elif head.rstrip(":").lower() == "psycasts":
            continue
        else:
            outro.append(block)
    deps = []
    for li in root.findall("modDependencies/li"):
        name = li.findtext("displayName") or li.findtext("packageId")
        url = li.findtext("steamWorkshopUrl") or ""
        m = re.search(r"(\d{6,})", url)
        deps.append(f"[url=https://steamcommunity.com/sharedfiles/filedetails/?id={m.group(1)}]{name}[/url]" if m else name)
    return intro, notes, outro, deps


def bbcode(mod, dev, casts, files):
    intro, notes, outro, deps = about_sections(dev)
    url = lambda f: f"[img]{REPO_RAW}/{mod}/Art/workshop/{f}[/img]"
    out = [url(files["banner_path"]), "", intro, "", url("tree.png"), "", url(files["banner_psycasts"]), ""]
    out += [url(f) for f in files["cards"]]
    if notes:
        out += ["", url(files["banner_notes"]), "", "[list]"] + [f"[*] {n}" for n in notes] + ["[/list]"]
    out += ["", url(files["banner_requirements"]), "", "[list]"] + [f"[*] {d}" for d in deps] + ["[/list]"]
    if outro:
        out += [""] + outro
    out += ["", f"Source: [url=https://github.com/JaimeBarrachina/Arcanum]github.com/JaimeBarrachina/Arcanum[/url]"]
    return "\n".join(out) + "\n"


def save_png(im, path):
    """256-colour dithered PNG: the page loads several of these, and full colour adds nothing at 640 px."""
    im.convert("RGB").quantize(colors=256, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.FLOYDSTEINBERG).save(path, optimize=True)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("mod")
    ap.add_argument("--only", choices=["tree"], help="build just one part")
    args = ap.parse_args()
    theme = THEMES[args.mod]
    dev, path, casts = load(args.mod)
    by_def = {c.defName: c for c in casts}
    out_dir = dev / "Art" / "workshop"
    out_dir.mkdir(parents=True, exist_ok=True)
    tree = render_tree(dev, path, casts)
    save_png(tree, out_dir / "tree.png")
    print(f"tree.png {tree.size}, {len(casts)} psycasts over {len(rows_of(casts))} levels")
    if args.only == "tree":
        return

    banner_raw = Image.open(dev / "Art" / "raw" / "ws_banner_v1.png")
    card_raw = Image.open(dev / "Art" / "raw" / "ws_card_v1.png")
    files = {}
    for key, title in [("banner_path", "The Path"), ("banner_psycasts", "Psycasts"),
                       ("banner_notes", "Good to Know"), ("banner_requirements", "Requirements")]:
        name = key + ".png"
        save_png(render_banner(banner_raw, title.upper(), theme), out_dir / name)
        files[key] = name
    files["cards"] = []
    for i, cast in enumerate(sorted(casts, key=lambda c: (c.level, c.order)), 1):
        name = f"card_{i:02d}_{cast.defName}.png"
        save_png(render_card(card_raw, dev, cast, by_def, theme), out_dir / name)
        files["cards"].append(name)
    (out_dir / "description.bbcode").write_text(bbcode(args.mod, dev, casts, files), encoding="utf-8")
    total = sum(f.stat().st_size for f in out_dir.glob("*.png")) // 1024
    print(f"{len(files['cards'])} cards, 4 banners, description.bbcode; {total} KB of images in {out_dir}")


if __name__ == "__main__":
    main()
