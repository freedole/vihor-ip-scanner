import sys
import os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_icon import render
from PIL import Image, ImageDraw, ImageFont
W, H = 1200, 630
F = '/usr/share/fonts/opentype/inter/'
img = Image.new('RGB', (W, H), (10, 20, 38))
d = ImageDraw.Draw(img)
# subtle grid of nodes on the right
import random, math
random.seed(7)
pts = [(random.randint(860, 1180), random.randint(30, 600)) for _ in range(26)]
for i, a in enumerate(pts):
    for b in pts[i+1:]:
        if math.dist(a, b) < 120:
            d.line([a, b], fill=(26, 52, 86), width=2)
for p in pts:
    d.ellipse([p[0]-4, p[1]-4, p[0]+4, p[1]+4], fill=(34, 74, 120))
icon = render(240)
img.paste(icon, (90, 120), icon)
title = ImageFont.truetype(F + 'Inter-Bold.otf', 76)
sub = ImageFont.truetype(F + 'Inter-Medium.otf', 32)
small = ImageFont.truetype(F + 'Inter-SemiBold.otf', 26)
d.text((372, 150), "Vihor", font=title, fill=(255, 255, 255))
d.text((372, 238), "IP Scanner", font=title, fill=(125, 211, 252))
d.text((372, 345), "Besplatan skener lokalne mreže", font=sub, fill=(203, 213, 225))
d.text((372, 388), "otvorenog koda za Windows", font=sub, fill=(203, 213, 225))
# footer bar
d.rectangle([0, 520, W, H], fill=(7, 14, 27))
d.text((90, 558), "cyber-security.ba", font=small, fill=(255, 255, 255))
t = "MIT licenca  ·  GitHub  ·  bez telemetrije"
w = d.textlength(t, font=small)
d.text((W - 90 - w, 558), t, font=small, fill=(148, 163, 184))
img.save('assets/vihor-banner.jpg', quality=90, optimize=True)

