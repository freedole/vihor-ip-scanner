#!/usr/bin/env python3
"""Generates the Vihor icon: a whirlwind (spiral) made of network nodes on a rounded square."""
import math, sys
from PIL import Image, ImageDraw

def render(size: int) -> Image.Image:
    S = size * 4  # supersample
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    r = int(S * 0.22)
    d.rounded_rectangle([0, 0, S - 1, S - 1], radius=r, fill=(13, 27, 52, 255))
    cx = cy = S / 2
    turns, n = 2.35, 26
    pts = []
    for k in range(n):
        t = k / (n - 1)
        ang = -math.pi / 2 + t * turns * 2 * math.pi
        rad = S * (0.05 + 0.31 * t)
        pts.append((cx + rad * math.cos(ang), cy + rad * math.sin(ang), t))
    # links
    for (x1, y1, t1), (x2, y2, t2) in zip(pts, pts[1:]):
        w = max(1, int(S * (0.012 + 0.018 * t2)))
        d.line([x1, y1, x2, y2], fill=(56, 189, 248, int(120 + 100 * t2)), width=w)
    # nodes
    for i, (x, y, t) in enumerate(pts):
        if size <= 24 and i % 2:
            continue
        rad = S * (0.018 + 0.040 * t)
        col = (125, 211, 252, 255) if i != n - 1 else (52, 211, 153, 255)
        d.ellipse([x - rad, y - rad, x + rad, y + rad], fill=col)
    return img.resize((size, size), Image.LANCZOS)

if __name__ == "__main__":
    ico, png = sys.argv[1], sys.argv[2]
    sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
    big = render(256)
    big.save(png)
    imgs = [render(s) for s in sizes]
    imgs[-1].save(ico, format="ICO", sizes=[(s, s) for s in sizes], append_images=imgs[:-1])
    print("ok")
