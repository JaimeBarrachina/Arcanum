#!/usr/bin/env python3
r"""Resize an image to exactly WxH, scaling to cover and centre-cropping the overflow (no letterboxing).

For textures with a fixed size, e.g. VPE path backgrounds (950x1515) or Workshop previews.

    python Tools/fit_cover.py raw.png --out final.png --width 950 --height 1515
"""
import argparse

from PIL import Image


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("src")
    ap.add_argument("--out", required=True)
    ap.add_argument("--width", type=int, required=True)
    ap.add_argument("--height", type=int, required=True)
    ap.add_argument("--rgb", action="store_true", help="drop alpha (opaque output)")
    args = ap.parse_args()

    img = Image.open(args.src).convert("RGB" if args.rgb else "RGBA")
    scale = max(args.width / img.width, args.height / img.height)
    img = img.resize((round(img.width * scale), round(img.height * scale)), Image.LANCZOS)
    left = (img.width - args.width) // 2
    top = (img.height - args.height) // 2
    img = img.crop((left, top, left + args.width, top + args.height))
    img.save(args.out)
    print(f"OK {args.out} {img.size}")


if __name__ == "__main__":
    main()
