#!/usr/bin/env python3
"""Generates MSIX visual assets (tiles, store logo) from the Vihor icon renderer."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_icon import render
from PIL import Image

out = sys.argv[1] if len(sys.argv) > 1 else "packaging/msix/Assets"
os.makedirs(out, exist_ok=True)
BG = (13, 27, 52, 255)

def square(name, size, scale_icon=0.72, plated=True):
    img = Image.new("RGBA", (size, size), BG if plated else (0, 0, 0, 0))
    ic = render(max(16, int(size * scale_icon)))
    img.paste(ic, ((size - ic.width) // 2, (size - ic.height) // 2), ic)
    img.save(os.path.join(out, name))

def wide(name, w, h):
    img = Image.new("RGBA", (w, h), BG)
    ic = render(int(h * 0.72))
    img.paste(ic, ((w - ic.width) // 2, (h - ic.height) // 2), ic)
    img.save(os.path.join(out, name))

# App list / taskbar icon: unplated full icon
square("Square44x44Logo.png", 44, 1.0, plated=False)
for t in (16, 24, 32, 48, 256):
    square(f"Square44x44Logo.targetsize-{t}.png", t, 1.0, plated=False)
    square(f"Square44x44Logo.targetsize-{t}_altform-unplated.png", t, 1.0, plated=False)
square("Square71x71Logo.png", 71)
square("Square150x150Logo.png", 150)
square("Square310x310Logo.png", 310)
wide("Wide310x150Logo.png", 310, 150)
square("StoreLogo.png", 50, 1.0, plated=False)
wide("SplashScreen.png", 620, 300)
print("ok")
