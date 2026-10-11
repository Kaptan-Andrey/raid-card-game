# -*- coding: utf-8 -*-
"""Фон игрового стола «Рейд»: ночная земля между лагерем (внизу) и изнанкой (вверху).
Чистый PIL. Светлота держится низкой: карты должны оставаться самым ярким на экране."""
import math, random, os, sys
from PIL import Image, ImageDraw, ImageChops, ImageFilter, ImageStat

W, H = 2560, 1440
LW, LH = 384, 216
AR = W / H

def sstep(a, b, t):
    if b == a: return 0.0
    t = max(0.0, min(1.0, (t - a) / (b - a)))
    return t * t * (3 - 2 * t)

def g(dx, dy, r):
    return math.exp(-(dx * dx + dy * dy) / (2 * r * r))

PRESETS = {
    "chasha": dict(
        top=(10, 8, 14), bot=(16, 13, 13),
        fire=((128, 68, 30), 0.52), core=((206, 128, 56), 0.28),
        cold=[((0.06, -0.16, 0.34), ( 68, 46, 108), 0.52),
              ((0.94, -0.14, 0.32), ( 82, 40,  78), 0.44),
              ((0.50, -0.30, 0.42), ( 44, 32,  66), 0.30)],
        lift=((34, 28, 44), 0.42),
        moss=(26, 44, 22), mist=(96, 66, 140), embers=(236, 150, 68),
        leaves=0.9, seed=7),
    "pepel": dict(
        top=(13, 9, 11), bot=(20, 14, 12),
        fire=((150, 64, 26), 0.58), core=((214, 118, 44), 0.32),
        cold=[((0.05, -0.17, 0.33), ( 78, 36,  74), 0.44),
              ((0.95, -0.15, 0.31), ( 96, 42,  52), 0.38),
              ((0.50, -0.30, 0.42), ( 52, 28,  46), 0.26)],
        lift=((44, 30, 30), 0.40),
        moss=(34, 32, 20), mist=(124, 72, 86), embers=(244, 136, 56),
        leaves=0.25, seed=21),
    "iznanka": dict(
        top=(8, 8, 16), bot=(12, 13, 18),
        fire=((54, 76, 116), 0.40), core=((104, 150, 186), 0.22),
        cold=[((0.06, -0.16, 0.34), ( 56, 40, 122), 0.54),
              ((0.94, -0.14, 0.32), ( 38,  80, 112), 0.44),
              ((0.50, -0.30, 0.42), ( 38, 34,  88), 0.30)],
        lift=((28, 34, 56), 0.44),
        moss=(18, 40, 40), mist=(80, 92, 170), embers=(126, 196, 230),
        leaves=0.8, seed=33),
}

def vnoise(gx, gy, seed):
    r = random.Random(seed)
    return [[r.random() for _ in range(gx + 1)] for _ in range(gy + 1)]

def vsample(grid, u, v):
    """Билинейная выборка со сглаживанием → мягкое поле в [-1, 1]."""
    gy = len(grid) - 1; gx = len(grid[0]) - 1
    x = max(0.0, min(1.0, u)) * gx; y = max(0.0, min(1.0, v)) * gy
    x0 = min(int(x), gx - 1); y0 = min(int(y), gy - 1)
    fx = x - x0; fy = y - y0
    fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy)
    a = grid[y0][x0] * (1 - fx) + grid[y0][x0 + 1] * fx
    b = grid[y0 + 1][x0] * (1 - fx) + grid[y0 + 1][x0 + 1] * fx
    return (a * (1 - fy) + b * fy) * 2 - 1

def light_field(p):
    img = Image.new("RGB", (LW, LH)); px = img.load()
    wob_a = vnoise(7, 4, p["seed"] + 101)
    wob_b = vnoise(5, 3, p["seed"] + 202)
    wob_c = vnoise(9, 5, p["seed"] + 303)
    for y in range(LH):
        v = y / (LH - 1)
        for x in range(LW):
            u0 = x / (LW - 1)
            # свет ложится по неровной земле, а не по идеальной сетке
            u = u0 + vsample(wob_a, u0, v) * 0.075
            vv = v + vsample(wob_b, u0, v) * 0.085
            t = sstep(0.0, 1.0, v)
            r = p["top"][0] + (p["bot"][0] - p["top"][0]) * t
            gc = p["top"][1] + (p["bot"][1] - p["top"][1]) * t
            b = p["top"][2] + (p["bot"][2] - p["top"][2]) * t
            # костёр лагеря за нижней кромкой
            w1 = g((u - 0.5) * AR * 1.45, (vv - 1.08) * 1.55, 0.50) * p["fire"][1]
            w2 = g((u - 0.5) * AR * 1.9, (vv - 1.16) * 2.3, 0.30) * p["core"][1]
            r += p["fire"][0][0]*w1 + p["core"][0][0]*w2
            gc += p["fire"][0][1]*w1 + p["core"][0][1]*w2
            b += p["fire"][0][2]*w1 + p["core"][0][2]*w2
            # холод изнанки за верхней кромкой
            for (cx, cy, cr), col, amp in p["cold"]:
                k = g((u - cx) * AR * 0.80, (vv - cy) * 1.15, cr) * amp
                r += col[0]*k; gc += col[1]*k; b += col[2]*k
            # едва заметный подъём середины — там ляжет Рейд, но без рамки
            k = g((u - 0.5) * 0.85, (vv - 0.47) * 2.7, 0.46) * p["lift"][1]
            # холодный подбой в нижних углах — тепло не должно заливать всю свою половину
            k2 = (g((u - 0.02) * AR * 0.9, (vv - 1.02) * 1.3, 0.40)
                  + g((u - 0.98) * AR * 0.9, (vv - 1.02) * 1.3, 0.40)) * 0.34
            r += 28 * k2; gc += 26 * k2; b += 44 * k2
            r += p["lift"][0][0]*k; gc += p["lift"][0][1]*k; b += p["lift"][0][2]*k
            d = math.hypot((u0 - 0.5) * 1.12, (v - 0.5) * 1.02)
            m = 1.0 - 0.80 * sstep(0.26, 0.84, d)
            px[x, y] = (max(0, min(255, int(r*m))), max(0, min(255, int(gc*m))), max(0, min(255, int(b*m))))
    return img.resize((W, H), Image.BICUBIC)

def fnoise(scale, sigma, blur=0, seed=0):
    random.seed(seed)
    n = Image.effect_noise((max(2, W // scale), max(2, H // scale)), sigma).resize((W, H), Image.BICUBIC)
    return n.filter(ImageFilter.GaussianBlur(blur)) if blur else n

def vgrad(top_val, bot_val, gamma=1.0):
    s = Image.new("L", (1, 256)); d = s.load()
    for i in range(256):
        t = (i / 255) ** gamma
        d[0, i] = int(top_val + (bot_val - top_val) * t)
    return s.resize((W, H), Image.BICUBIC)

def leaf(dr, cx, cy, L, ang, col):
    pts = []
    for i in range(11):
        t = i / 10; wd = math.sin(t * math.pi) * L * 0.30
        pts.append((t * L, -wd))
    for i in range(11):
        t = 1 - i / 10; wd = math.sin(t * math.pi) * L * 0.30
        pts.append((t * L, wd))
    ca, sa = math.cos(ang), math.sin(ang)
    dr.polygon([(cx + x*ca - y*sa, cy + x*sa + y*ca) for x, y in pts], fill=col)

def build(p):
    img = light_field(p)
    rnd = random.Random(p["seed"])

    # ---- земля: три частоты, самая мелкая несёт «зерно» грунта
    for sc, sg, bl, amt in ((2, 52, 0, 0.42), (6, 80, 1.5, 0.40), (18, 96, 8, 0.34)):
        n = fnoise(sc, sg, bl, p["seed"] + sc).convert("RGB")
        img = Image.blend(img, ImageChops.soft_light(img, n), amt)

    # ---- борозды: земля слегка «расчёсана» к центру стола
    sc_img = Image.new("L", (W, H), 128); d = ImageDraw.Draw(sc_img)
    for i in range(220):
        y0 = rnd.uniform(0, H); x0 = rnd.uniform(-100, W)
        ln = rnd.uniform(120, 620); amp = rnd.uniform(3, 16)
        ph = rnd.uniform(0, 6.28); val = int(128 + rnd.uniform(-26, 26))
        pts = [(x0 + t*ln/14, y0 + math.sin(ph + t*0.5)*amp) for t in range(15)]
        d.line(pts, fill=val, width=int(rnd.uniform(2, 7)))
    sc_img = sc_img.filter(ImageFilter.GaussianBlur(3))
    img = Image.blend(img, ImageChops.soft_light(img, sc_img.convert("RGB")), 0.38)

    # ---- мох пятнами по шуму, а не полосой у кромки
    mn = fnoise(22, 120, 10, p["seed"] + 91)
    mmask = ImageChops.multiply(mn, vgrad(20, 255, 1.5)).point(lambda v: 0 if v < 150 else int((v-150)/105*120))
    mmask = mmask.filter(ImageFilter.GaussianBlur(14))
    img = Image.composite(ImageChops.soft_light(img, Image.new("RGB", (W, H), p["moss"])), img, mmask)
    img = Image.composite(Image.blend(img, Image.new("RGB", (W, H), p["moss"]), 0.22), img, mmask)

    # ---- тёмная масса чащи по верхней кромке: край задаёт шум, не полигоны
    tn = fnoise(34, 130, 16, p["seed"] + 5)
    tmask = ImageChops.multiply(tn, vgrad(255, 0, 0.55)).point(lambda v: 255 if v > 132 else int(v/132*90))
    tmask = tmask.filter(ImageFilter.GaussianBlur(26))
    img = Image.composite(Image.new("RGB", (W, H), (5, 4, 9)), img, tmask)

    # ---- намёки на стволы: сильно размыты, малый контраст
    tr = Image.new("RGBA", (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(tr)
    for i in range(7):
        x = rnd.uniform(-80, W + 80); wd = rnd.uniform(40, 120)
        hg = rnd.uniform(H*0.16, H*0.46); lean = rnd.uniform(-40, 40)
        d.polygon([(x, -20), (x+wd, -20), (x+wd*0.55+lean, hg), (x+wd*0.10+lean, hg*0.96)],
                  fill=(4, 3, 8, int(rnd.uniform(90, 160))))
        for b in range(3):
            by = rnd.uniform(hg*0.2, hg*0.8); bl = rnd.uniform(60, 200)
            bx = x + wd*0.5 + lean*by/hg
            d.line([(bx, by), (bx + rnd.choice([-1, 1])*bl, by - rnd.uniform(20, 90))],
                   fill=(4, 3, 8, 120), width=int(rnd.uniform(4, 11)))
    tr = tr.filter(ImageFilter.GaussianBlur(14))
    img = Image.alpha_composite(img.convert("RGBA"), tr).convert("RGB")

    # ---- туман изнанки
    mist_n = fnoise(30, 110, 24, p["seed"] + 3)
    mask = ImageChops.multiply(mist_n, vgrad(255, 0, 1.0)).point(lambda v: int((v/255)**1.8 * 96))
    img = Image.composite(ImageChops.screen(img, Image.new("RGB", (W, H), p["mist"])), img, mask)

    # ---- тёплая дымка над костром
    wn = fnoise(34, 100, 28, p["seed"] + 4)
    wmask = ImageChops.multiply(wn, vgrad(0, 255, 2.6)).point(lambda v: int((v/255)**1.8 * 84))
    img = Image.composite(ImageChops.screen(img, Image.new("RGB", (W, H), p["embers"])), img, wmask)

    # ---- корни, вползающие с краёв
    rt = Image.new("RGBA", (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(rt)
    for i in range(14):
        side = rnd.choice([0, 1, 2, 3])
        if side == 0: x, y, dx, dy = -30, rnd.uniform(0, H), 1, rnd.uniform(-.5, .5)
        elif side == 1: x, y, dx, dy = W+30, rnd.uniform(0, H), -1, rnd.uniform(-.5, .5)
        elif side == 2: x, y, dx, dy = rnd.uniform(0, W), -30, rnd.uniform(-.5, .5), 1
        else: x, y, dx, dy = rnd.uniform(0, W), H+30, rnd.uniform(-.5, .5), -1
        pts = [(x, y)]
        for s in range(26):
            dx += rnd.uniform(-.22, .22); dy += rnd.uniform(-.22, .22)
            n = math.hypot(dx, dy) or 1; dx, dy = dx/n, dy/n
            x += dx*rnd.uniform(18, 46); y += dy*rnd.uniform(18, 46)
            pts.append((x, y))
        d.line(pts, fill=(6, 5, 9, 120), width=int(rnd.uniform(5, 16)), joint="curve")
    rt = rt.filter(ImageFilter.GaussianBlur(5))
    img = Image.alpha_composite(img.convert("RGBA"), rt).convert("RGB")

    # ---- трещины в подсохшей земле
    ck = Image.new("RGBA", (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(ck)
    for i in range(70):
        x, y = rnd.uniform(0, W), rnd.uniform(H*0.15, H*0.95)
        a = rnd.uniform(0, 6.28); pts = [(x, y)]
        for s in range(rnd.randint(4, 11)):
            a += rnd.uniform(-.6, .6); ln = rnd.uniform(14, 52)
            x += math.cos(a)*ln; y += math.sin(a)*ln; pts.append((x, y))
        d.line(pts, fill=(3, 2, 5, rnd.randint(40, 90)), width=rnd.choice([1, 1, 2]))
    ck = ck.filter(ImageFilter.GaussianBlur(.8))
    img = Image.alpha_composite(img.convert("RGBA"), ck).convert("RGB")

    # ---- камни: неровные, с мягкой тенью и слабым бликом сверху-снизу
    st = Image.new("RGBA", (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(st)
    for i in range(34):
        cx, cy = rnd.uniform(0, W), rnd.uniform(H*0.12, H*0.98)
        depth = sstep(0.05, 1.0, cy/H)
        rr = rnd.uniform(10, 34) * (0.55 + depth*0.8)
        poly = []
        for k in range(9):
            a = k/9*6.283; rad = rr*rnd.uniform(0.72, 1.18)
            poly.append((cx + math.cos(a)*rad*1.45, cy + math.sin(a)*rad*0.78))
        d.polygon(poly, fill=(12, 11, 13, int(120 + 60*depth)))
        d.polygon([(px_, py_ - rr*0.26) for px_, py_ in poly],
                  fill=(int(26 + 40*depth), int(23 + 30*depth), int(24 + 24*depth), 70))
    st = st.filter(ImageFilter.GaussianBlur(3.4))
    img = Image.alpha_composite(img.convert("RGBA"), st).convert("RGB")

    # ---- листва у нижней кромки, в тени, с тёплым рефлексом от костра
    if p["leaves"] > 0:
        lv = Image.new("RGBA", (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(lv)
        cnt = int(150 * p["leaves"])
        for i in range(cnt):
            side = rnd.random()
            cx = rnd.uniform(-60, W*0.34) if side < .5 else rnd.uniform(W*0.66, W+60)
            cy = H - abs(rnd.gauss(0, H*0.050)) + rnd.uniform(-10, 10)
            L = rnd.uniform(26, 78); ang = rnd.uniform(-math.pi, math.pi)
            k = rnd.uniform(0.25, 0.8)
            col = (int(9 + 26*k), int(14 + 34*k), int(8 + 16*k), 250)
            leaf(d, cx, cy, L, ang, col)
            if rnd.random() < .4:
                leaf(d, cx, cy - 2, L*0.92, ang, (int(44 + 50*k), int(34 + 32*k), int(20 + 14*k), 46))
        lv = lv.filter(ImageFilter.GaussianBlur(1.4))
        img = Image.alpha_composite(img.convert("RGBA"), lv).convert("RGB")

    # ---- искры костра и светляки изнанки
    sp = Image.new("RGB", (W, H), (0, 0, 0)); d = ImageDraw.Draw(sp)
    for i in range(90):
        cx = rnd.gauss(W*0.5, W*0.24); cy = H - abs(rnd.gauss(0, H*0.30)); rr = rnd.uniform(1.0, 2.6)
        d.ellipse([cx-rr, cy-rr, cx+rr, cy+rr], fill=p["embers"])
    for i in range(46):
        cx = rnd.uniform(0, W); cy = abs(rnd.gauss(0, H*0.26)); rr = rnd.uniform(0.9, 2.2)
        d.ellipse([cx-rr, cy-rr, cx+rr, cy+rr], fill=p["mist"])
    img = ImageChops.screen(img, sp.filter(ImageFilter.GaussianBlur(11)).point(lambda v: int(v*0.55)))
    img = ImageChops.screen(img, sp.filter(ImageFilter.GaussianBlur(1.3)).point(lambda v: int(v*0.75)))

    # ---- финальная виньетка и зерно
    vg = Image.new("L", (LW, LH)); vp = vg.load()
    for y in range(LH):
        for x in range(LW):
            dd = math.hypot((x/(LW-1)-0.5)*1.08, (y/(LH-1)-0.5)*1.0)
            vp[x, y] = int(255 * (1.0 - 0.58*sstep(0.30, 0.78, dd)))
    img = ImageChops.multiply(img, vg.resize((W, H), Image.BICUBIC).convert("RGB"))
    img = Image.blend(img, ImageChops.soft_light(img, Image.effect_noise((W, H), 6).convert("RGB")), 0.26)
    img = Image.blend(img, img.convert("L").convert("RGB"), 0.17)   # сбить лобовую насыщенность
    return img

if __name__ == "__main__":
    out = sys.argv[1]; os.makedirs(out, exist_ok=True)
    for name, p in PRESETS.items():
        im = build(p)
        im.save(os.path.join(out, "table_%s.png" % name), optimize=True)
        im.resize((1536, 864), Image.LANCZOS).save(os.path.join(out, "prev_%s.jpg" % name), quality=86)
        s = ImageStat.Stat(im.convert("L"))
        print("%-8s mean=%.1f  max=%d" % (name, s.mean[0], s.extrema[0][1]))
