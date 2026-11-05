#!/usr/bin/env python3
r"""Generate one image with Codex's built-in image generation, non-interactively, and land it at --out.

Codex gives better character art than the image API, and `codex exec` can run it from a script: this finds
the Codex binary (the VS Code extension bundles it), sends the job with any reference images attached, and
makes sure the picture ends up at --out (if Codex could not copy it there, the newest image Codex generated
during the run is copied instead). Uses the signed-in Codex account and its allowance.

    python Tools/IconGenerator/codex_image.py --job Tools/IconGenerator/output/portraits/Smuggler/jobs/proof_v1.md \
        --out Tools/IconGenerator/output/portraits/Smuggler/proof_neutral_v1.png \
        --ref "Style/Characters/Markus Lake/Character study.png"

Copied from the Necro project (Tools/IconGenerator) 2026-11-01; adds --skip-git-repo-check because the mod folders are not git repos.

--job is a file (or literal text) describing the picture; this script adds the standing instructions
(one image, save to --out, touch nothing else). Exit code 0 only when --out exists and is new.
Stdlib only, so it runs with any Python 3.9+.
"""
import argparse
import glob
import os
import shutil
import subprocess
import sys
import time
from pathlib import Path

FOOTER = """

---
Standing instructions for this job:
- Use your built-in image generation tool to create exactly ONE image. Do not write code to draw it.
- Save it as `{out}` (create the folders). If you cannot copy it out of your generated-images folder, say so and give its full path.
- Do not edit, create or delete any other file, and do not commit.
- Finish with one line: the saved path, or why it failed.
"""


def find_codex() -> str:
    if os.environ.get("CODEX_EXE"):
        return os.environ["CODEX_EXE"]
    on_path = shutil.which("codex")
    if on_path:
        return on_path
    home = Path.home()
    pattern = str(home / ".vscode" / "extensions" / "openai.chatgpt-*" / "bin" / "*" / ("codex.exe" if os.name == "nt" else "codex"))
    found = sorted(glob.glob(pattern), key=os.path.getmtime)
    if not found:
        sys.exit("Codex not found: install the Codex VS Code extension or the CLI, or set CODEX_EXE.")
    return found[-1]


def newest_generated(since: float):
    root = Path.home() / ".codex" / "generated_images"
    images = [p for p in root.glob("**/*.png") if p.stat().st_mtime >= since]
    return max(images, key=lambda p: p.stat().st_mtime) if images else None


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--job", required=True, help="Job description: a .md/.txt file path, or the text itself")
    ap.add_argument("--out", required=True, help="Where the image must end up (relative to --repo)")
    ap.add_argument("--ref", action="append", default=[], help="Reference image to attach (repeatable)")
    ap.add_argument("--repo", default=".", help="Workspace Codex runs in (default: current folder)")
    ap.add_argument("--log", help="Write Codex's full output here (default: next to --out, .codex.log)")
    ap.add_argument("--timeout", type=int, default=900, help="Seconds before giving up (default 900)")
    args = ap.parse_args()

    repo = Path(args.repo).resolve()
    out = (repo / args.out).resolve()
    job = Path(args.job)
    text = job.read_text(encoding="utf-8") if job.is_file() else args.job
    prompt = text.rstrip() + FOOTER.format(out=out.relative_to(repo).as_posix())
    for r in args.ref:
        if not Path(r).is_file():
            sys.exit(f"reference image not found: {r}")
    out.parent.mkdir(parents=True, exist_ok=True)
    log_path = Path(args.log) if args.log else out.with_suffix(".codex.log")

    codex = find_codex()
    cmd = [codex, "exec", "--skip-git-repo-check", "-C", str(repo), "-s", "workspace-write"]
    for r in args.ref:
        cmd += ["-i", str(Path(r).resolve())]
    # The prompt goes in on stdin ("-"): -i takes any number of files and would swallow a trailing prompt
    # argument as another image, and stdin also avoids Windows' command-line length limit.
    cmd.append("-")

    start = time.time()
    print(f"codex: {codex}\njob: {args.job}\nout: {out}\nrunning (usually 1-3 minutes)...", flush=True)
    try:
        run = subprocess.run(cmd, input=prompt, capture_output=True, text=True, encoding="utf-8", errors="replace",
                             timeout=args.timeout)
    except subprocess.TimeoutExpired:
        sys.exit(f"Codex did not finish within {args.timeout}s")
    log_path.write_text((run.stdout or "") + "\n--- stderr ---\n" + (run.stderr or ""), encoding="utf-8")

    if out.is_file() and out.stat().st_mtime >= start:
        print(f"OK {out}")
        return
    fallback = newest_generated(start)
    if fallback:
        shutil.copy2(fallback, out)
        print(f"OK {out} (copied from Codex's folder: {fallback})")
        return
    tail = "\n".join((run.stdout or "").strip().splitlines()[-8:])
    sys.exit(f"No image produced (codex exit {run.returncode}). Log: {log_path}\n{tail}")


if __name__ == "__main__":
    main()
