"""Generates the installer artwork for the CyberGems apps.

Produces, from the app icon:
  - <name>.bmp / <name>.png   (164x314) NSIS sidebar and Inno Setup wizard image
  - installerWizardSmall.bmp  (55x58)   Inno Setup small wizard image (--small)

The plate (pale gradient, brand arc and constellation dots) reproduces the
look used by the apps that ship their own illustration, so every installer
in the suite shares the same visual language.

Usage:
    python scripts/generate-installer-art.py <icon> <outdir> <name> [--small]
"""

import math
import os
import sys

from PIL import Image, ImageDraw, ImageFilter

SIDEBAR_SIZE = (164, 314)
SMALL_SIZE = (55, 58)

PLATE_TOP = (255, 255, 255)
PLATE_BOTTOM = (232, 241, 252)
ARC_COLOR = (218, 232, 249)
DOT_COLOR = (201, 220, 240)
LINE_COLOR = (222, 235, 250)
GLOW_COLOR = (34, 211, 238)


def draw_plate(size):
    """Pale plate with the brand arc and a faint constellation."""
    width, height = size
    plate = Image.new("RGB", size, PLATE_TOP)
    draw = ImageDraw.Draw(plate)

    for y in range(height):
        t = y / max(1, height - 1)
        r = int(PLATE_TOP[0] + (PLATE_BOTTOM[0] - PLATE_TOP[0]) * t)
        g = int(PLATE_TOP[1] + (PLATE_BOTTOM[1] - PLATE_TOP[1]) * t)
        b = int(PLATE_TOP[2] + (PLATE_BOTTOM[2] - PLATE_TOP[2]) * t)
        draw.line([(0, y), (width, y)], fill=(r, g, b))

    # Brand arc: a large circle whose centre sits off canvas to the right, so
    # the curve enters at the top and leaves at the bottom, bulging left.
    cx = width * 1.30
    cy = height * 0.50
    radius = width * 1.05
    for offset, color, line_width in ((0, ARC_COLOR, 3), (-14, ARC_COLOR, 2)):
        points = []
        for step in range(0, 3610):
            angle = math.radians(step / 10)
            x = cx + (radius + offset) * math.cos(angle)
            y = cy + (radius + offset) * math.sin(angle)
            if -20 <= x <= width + 20 and -20 <= y <= height + 20:
                points.append((x, y))
        if len(points) > 1:
            draw.line(points, fill=color, width=line_width, joint="curve")

    # Faint constellation on the right
    dots = []
    for i in range(26):
        angle = (i * 137.5) % 360
        radius = 0.30 + 0.22 * ((i % 5) / 4)
        x = width * 0.78 + math.cos(math.radians(angle)) * width * radius * 0.62
        y = height * 0.52 + math.sin(math.radians(angle)) * height * radius * 0.42
        if 0 <= x <= width and 0 <= y <= height:
            dots.append((x, y))
            dot_size = 2 if i % 4 == 0 else 1
            draw.ellipse([x - dot_size, y - dot_size, x + dot_size, y + dot_size], fill=DOT_COLOR)
    for i, (x1, y1) in enumerate(dots):
        for x2, y2 in dots[i + 1 : i + 3]:
            if math.dist((x1, y1), (x2, y2)) < 34:
                draw.line([(x1, y1), (x2, y2)], fill=LINE_COLOR)

    return plate


def load_icon(icon_path):
    icon = Image.open(icon_path)
    sizes = sorted(icon.info.get("sizes", []), key=lambda s: s[0] * s[1])
    if sizes:
        icon.size = sizes[-1]
        icon = icon.convert("RGBA")
        icon.load()
    else:
        icon = icon.convert("RGBA")
    return icon


def compose_sidebar(icon_path):
    plate = draw_plate(SIDEBAR_SIZE).convert("RGBA")
    icon = load_icon(icon_path)

    target = 92
    scale = target / max(icon.width, icon.height)
    icon = icon.resize((max(1, int(icon.width * scale)), max(1, int(icon.height * scale))), Image.Resampling.LANCZOS)

    # Soft glow behind the icon
    glow = Image.new("RGBA", SIDEBAR_SIZE, (0, 0, 0, 0))
    glow_draw = ImageDraw.Draw(glow)
    cx = SIDEBAR_SIZE[0] // 2
    cy = 118
    for i in range(14, 0, -1):
        radius = 26 + i * 3
        alpha = int(9 * (14 - i) / 14)
        glow_draw.ellipse([cx - radius, cy - radius, cx + radius, cy + radius], fill=GLOW_COLOR + (alpha,))
    glow = glow.filter(ImageFilter.GaussianBlur(7))
    plate = Image.alpha_composite(plate, glow)

    plate.alpha_composite(icon, (cx - icon.width // 2, cy - icon.height // 2))
    return plate.convert("RGB")


def compose_small(icon_path):
    canvas = Image.new("RGB", SMALL_SIZE, "#FFFFFF")
    icon = load_icon(icon_path)
    scale = min((SMALL_SIZE[0] - 4) / icon.width, (SMALL_SIZE[1] - 4) / icon.height)
    icon = icon.resize((max(1, int(icon.width * scale)), max(1, int(icon.height * scale))), Image.Resampling.LANCZOS)
    flat = Image.new("RGBA", icon.size, (255, 255, 255, 255))
    flat.alpha_composite(icon)
    canvas.paste(flat.convert("RGB"), ((SMALL_SIZE[0] - icon.width) // 2, (SMALL_SIZE[1] - icon.height) // 2))
    return canvas


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    flags = {a for a in sys.argv[1:] if a.startswith("--")}
    if len(args) < 3:
        print(__doc__)
        sys.exit(1)
    icon_path, out_dir, name = args[0], args[1], args[2]
    os.makedirs(out_dir, exist_ok=True)

    sidebar = compose_sidebar(icon_path)
    bmp_path = os.path.join(out_dir, f"{name}.bmp")
    png_path = os.path.join(out_dir, f"{name}.png")
    sidebar.save(bmp_path, format="BMP")
    sidebar.save(png_path)
    print(f"Saved {bmp_path} ({os.path.getsize(bmp_path)} bytes) and {png_path}")

    if "--small" in flags:
        small = compose_small(icon_path)
        small_path = os.path.join(out_dir, "installerWizardSmall.bmp")
        small.save(small_path, format="BMP")
        print(f"Saved {small_path} ({os.path.getsize(small_path)} bytes)")

    if "--ico" in flags:
        icon = load_icon(icon_path)
        ico_path = os.path.join(out_dir, "icon.ico")
        icon.save(ico_path, format="ICO", sizes=[(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (16, 16)])
        print(f"Saved {ico_path} ({os.path.getsize(ico_path)} bytes)")


if __name__ == "__main__":
    main()
