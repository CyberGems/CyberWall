"""Generates the 55x58 wizard small image (BMP) used by the Inno Setup installer.

Inno Setup only accepts a real bitmap for WizardSmallImageFile, so the app
icon is rendered into a 55x58 24-bit BMP on a white background that matches
the wizard page.

Usage:
    python scripts/generate-installer-wizard-image.py <icon.ico> [output.bmp]
"""

import os
import sys

from PIL import Image

DEFAULT_ICON = os.path.join("src", "CyberSnap", "CyberSnap.ico")
DEFAULT_OUTPUT = os.path.join("installer", "installerWizardSmall.bmp")


def build(ico_path: str, out_path: str) -> None:
    icon = Image.open(ico_path)
    sizes = sorted(icon.info.get("sizes", []), key=lambda s: s[0] * s[1])
    if sizes:
        icon.size = sizes[-1]
        icon = icon.convert("RGBA")
        icon.load()
    else:
        icon = icon.convert("RGBA")

    size = 55
    canvas = Image.new("RGB", (size, 58), "#FFFFFF")
    scale = min((size - 4) / icon.width, (58 - 4) / icon.height)
    w = max(1, int(icon.width * scale))
    h = max(1, int(icon.height * scale))
    resized = icon.resize((w, h), Image.Resampling.LANCZOS)
    # Composite over white so the BMP has no alpha channel
    flat = Image.new("RGBA", resized.size, (255, 255, 255, 255))
    flat.alpha_composite(resized)
    canvas.paste(flat.convert("RGB"), ((size - w) // 2, (58 - h) // 2))

    os.makedirs(os.path.dirname(out_path) or ".", exist_ok=True)
    canvas.save(out_path, format="BMP")
    print(f"Saved {out_path} ({os.path.getsize(out_path)} bytes)")


if __name__ == "__main__":
    ico = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_ICON
    out = sys.argv[2] if len(sys.argv) > 2 else DEFAULT_OUTPUT
    build(ico, out)
