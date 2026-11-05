# Builds Codex reference images from VPE textures (local reference only, never shipped).
import sys
from PIL import Image
W = "C:/Program Files (x86)/Steam/steamapps/workshop/content/294100/2842502659/Textures/"
out = sys.argv[1]
icons = ["Skipdoor", "Farskip", "ChaosSkip", "Wallraise", "Skip", "ResourceSkip"]
sheet = Image.new("RGB", (3 * 300, 2 * 300), "white")
for i, n in enumerate(icons):
    im = Image.open(W + f"Abilities/Skipmaster/{n}.png").convert("RGBA").resize((256, 256), Image.LANCZOS)
    sheet.paste(im, ((i % 3) * 300 + 22, (i // 3) * 300 + 22), im)
sheet.save(out + "/ref_vpe_icons.png")
bg = Image.new("RGB", (2 * 950 + 40, 1515), "white")
for i, n in enumerate(["SkipmasterPath", "NightstalkerPath"]):
    bg.paste(Image.open(W + f"UI/Backgrounds/{n}.png").convert("RGB"), (i * 990, 0))
bg.save(out + "/ref_vpe_backgrounds.png")
print("ok")
