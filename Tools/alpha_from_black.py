#!/usr/bin/env python3
r"""Turn glowing art drawn on pure black into a transparent PNG, for effect sprites (portals, auras, flecks).

Alpha comes from brightness (the brightest channel), then colour is divided by alpha ("un-premultiplied"), so a
soft violet glow becomes violet at low opacity instead of a dark smudge. A small floor (--black) treats near-black
noise as fully transparent. Optionally crops to the content and resizes onto a square canvas.

    python Tools/alpha_from_black.py raw.png --out final.png --size 256 --pad 0.04

--solid-core makes everything enclosed by the bright outline fully opaque, keeping its original dark colour, so a
portal's void reads as a hole on light ground instead of a tinted window. The glow outside the outline stays soft.
"""
import argparse

import cv2
import numpy as np
from PIL import Image, ImageFilter


def fill_holes(mask):
    # Flood the background from a corner; whatever it can't reach is inside the outline.
    flooded = mask.copy()
    h, w = mask.shape
    cv2.floodFill(flooded, np.zeros((h + 2, w + 2), np.uint8), (0, 0), 255)
    return mask | cv2.bitwise_not(flooded)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("src")
    ap.add_argument("--out", required=True)
    ap.add_argument("--size", type=int, help="square output size; omit to keep the source size")
    ap.add_argument("--pad", type=float, default=0.04, help="margin around the cropped content, as a fraction")
    ap.add_argument("--black", type=float, default=0.03, help="brightness at or below this is fully transparent")
    ap.add_argument("--solid-core", type=float, metavar="T",
                    help="make the area enclosed by pixels brighter than T (0-1) opaque, e.g. 0.35")
    args = ap.parse_args()

    rgb = np.asarray(Image.open(args.src).convert("RGB"), dtype=np.float32) / 255.0
    alpha = rgb.max(axis=2)
    alpha = np.clip((alpha - args.black) / (1.0 - args.black), 0.0, 1.0)
    safe = np.maximum(alpha, 1e-4)[..., None]
    color = np.clip(rgb / safe, 0.0, 1.0)
    if args.solid_core is not None:
        core = fill_holes((rgb.max(axis=2) > args.solid_core).astype(np.uint8) * 255)
        core = cv2.erode(core, np.ones((5, 5), np.uint8))
        soft = np.asarray(Image.fromarray(core).filter(ImageFilter.GaussianBlur(3)),
                          dtype=np.float32)[..., None] / 255.0
        color = color * (1 - soft) + rgb * soft
        alpha = np.maximum(alpha, soft[..., 0])
    rgba = np.dstack([color, alpha])
    img = Image.fromarray((rgba * 255).round().astype(np.uint8), "RGBA")

    if args.size:
        box = img.getchannel("A").point(lambda a: 255 if a > 8 else 0).getbbox()
        if box:
            img = img.crop(box)
        side = int(max(img.size) * (1 + 2 * args.pad))
        canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
        canvas.alpha_composite(img, ((side - img.width) // 2, (side - img.height) // 2))
        img = canvas.resize((args.size, args.size), Image.LANCZOS)
    img.save(args.out)
    print(f"OK {args.out} {img.size}")


if __name__ == "__main__":
    main()
