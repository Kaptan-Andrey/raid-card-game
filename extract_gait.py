# -*- coding: utf-8 -*-
"""Съём походки с референса: по кадрам GIF находим силуэт тёмного бойца и
вынимаем числа — таз, стопы, наклон, подъём корпуса. В кадр заставки пиксели
референса не попадают, он работает только источником движения."""
import json, math, os, sys
from PIL import Image, ImageDraw

SRC = "/root/NIK/concepts/самураи 1.gif"
F0, F1 = 0, 46          # сегмент сближения

def mask_of(fr, thr=62):
    g = fr.convert("L"); w, h = g.size; p = g.load()
    m = Image.new("L", (w, h), 0); mp = m.load()
    for y in range(int(h * .02), h):
        for x in range(w):
            if p[x, y] < thr: mp[x, y] = 255
    return m

def biggest(m, min_h=55):
    w, h = m.size; p = m.load()
    seen = [[False] * w for _ in range(h)]
    best = None
    for y in range(h):
        for x in range(w):
            if p[x, y] and not seen[y][x]:
                st = [(x, y)]; seen[y][x] = True; cur = []
                while st:
                    cx, cy = st.pop(); cur.append((cx, cy))
                    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                        nx, ny = cx + dx, cy + dy
                        if 0 <= nx < w and 0 <= ny < h and not seen[ny][nx] and p[nx, ny]:
                            seen[ny][nx] = True; st.append((nx, ny))
                ys = [q[1] for q in cur]; xs = [q[0] for q in cur]
                hh = max(ys) - min(ys); ww = max(xs) - min(xs)
                if hh >= min_h and ww < w * .45 and (best is None or len(cur) > len(best)):
                    best = cur
    return best

def analyse(pts):
    xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
    x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
    hgt = y1 - y0
    rows = {}
    for x, y in pts: rows.setdefault(y, []).append(x)
    # таз — строка наибольшей ширины в средней трети
    hip_y, best = y0 + hgt * .55, -1
    for y in range(y0 + int(hgt * .35), y0 + int(hgt * .75)):
        r = rows.get(y)
        if r and (max(r) - min(r)) > best: best = max(r) - min(r); hip_y = y
    # стопы — две группы в нижних 14 % силуэта
    low = [p for p in pts if p[1] > y1 - hgt * .14]
    feet = []
    if low:
        lx = sorted(p[0] for p in low)
        gaps = [(lx[i + 1] - lx[i], i) for i in range(len(lx) - 1)]
        g, gi = max(gaps) if gaps else (0, 0)
        if g > hgt * .10:
            a = lx[:gi + 1]; b = lx[gi + 1:]
            feet = [(sum(a) / len(a), y1), (sum(b) / len(b), y1)]
        else:
            feet = [(sum(lx) / len(lx), y1)] * 2
    top = [p for p in pts if p[1] < y0 + hgt * .30]
    bot = [p for p in pts if p[1] > y0 + hgt * .70]
    lean = 0.0
    if top and bot:
        tx = sum(p[0] for p in top) / len(top); bx = sum(p[0] for p in bot) / len(bot)
        lean = math.degrees(math.atan2(tx - bx, hgt * .7))
    return dict(x0=x0, x1=x1, y0=y0, y1=y1, h=hgt,
                cx=sum(xs) / len(xs), hip_y=hip_y, head_y=y0,
                feet=sorted(feet), lean=lean)

def smooth(seq, k=2):
    out = []
    for i in range(len(seq)):
        a = max(0, i - k); b = min(len(seq), i + k + 1)
        out.append(sum(seq[a:b]) / (b - a))
    return out

def main():
    g = Image.open(SRC)
    recs = []
    for i in range(F0, F1):
        g.seek(i)
        fr = g.convert("RGB")
        pts = biggest(mask_of(fr))
        if pts is None:
            recs.append(None); continue
        recs.append(analyse(pts))
    ok = [r for r in recs if r]
    print("кадров разобрано:", len(ok), "из", len(recs))

    for key in ("cx", "hip_y", "head_y", "lean"):
        vals = smooth([r[key] for r in ok], 2)
        for r, v in zip(ok, vals): r[key] = v
    for j in (0, 1):
        fx = smooth([r["feet"][j][0] for r in ok], 1)
        for r, v in zip(ok, fx): r["feet"][j] = (v, r["feet"][j][1])

    json.dump(ok, open("/root/NIK/concepts/Клод/gait.json", "w"), ensure_ascii=False, indent=1)

    # проверочная раскладка: разметка поверх референса
    g.seek(F0)
    cw, chh = g.size
    cols, rows_n = 6, 3
    sheet = Image.new("RGB", (cw * cols // 2, chh * rows_n // 2), "black")
    for k, i in enumerate(range(F0, F0 + cols * rows_n * 2, 2)):
        if k >= cols * rows_n or i - F0 >= len(recs) or recs[i - F0] is None: break
        g.seek(i); fr = g.convert("RGB")
        d = ImageDraw.Draw(fr); r = recs[i - F0]
        d.rectangle([r["x0"], r["y0"], r["x1"], r["y1"]], outline=(255, 90, 40))
        d.line([(r["x0"], r["hip_y"]), (r["x1"], r["hip_y"])], fill=(90, 200, 255), width=2)
        for fx, fy in r["feet"]:
            d.ellipse([fx - 5, fy - 5, fx + 5, fy + 5], outline=(120, 255, 120), width=2)
        d.line([(r["cx"], r["y1"]), (r["cx"] + math.tan(math.radians(r["lean"])) * r["h"] * .7, r["y0"])],
               fill=(255, 230, 80), width=2)
        sheet.paste(fr.resize((cw // 2, chh // 2)), ((k % cols) * cw // 2, (k // cols) * chh // 2))
    sheet.save("/tmp/claude-0/-root-NIK/1ab3f299-4193-4bdb-86f0-8ec8f895b6f9/scratchpad/gait_check.jpg", quality=90)
    print("разметка сохранена")

if __name__ == "__main__":
    main()
