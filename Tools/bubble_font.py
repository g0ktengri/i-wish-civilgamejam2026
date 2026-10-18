"""
Pixelify Sans'tan tÃ¼retilen iki font. Pixelify'Ä±n kendi piksel Ä±zgarasÄ±nda (yatay ~93, dikey ~92,4 birim)
harfler yeniden Ã§izilir; piksel boyu diÄŸer harflerle aynÄ± kalÄ±r.

- DepremBalon-Regular (balonlar ve oyun iÃ§i HUD). Play testi 13: zor okunan 'e' ve 'E' VT323'teki gibi dÃ¼z.
- DepremMenu-Regular (menÃ¼, keÅŸke, final ekranÄ±, kabin tabelasÄ±): Pixelify'Ä±n kendisi.

Ä°kisinde de play testi 33'Ã¼n dÃ¼zeltmeleri var: bÃ¼yÃ¼k B G gibi, 5 S gibi, Z 2 gibi, C ile G birbiri gibi
okunuyordu. B'nin sol kÃ¶ÅŸeleri dik, 5'in Ã¼stÃ¼ dÃ¼z, Z'nin Ã§aprazÄ± belirgin, C saÄŸÄ± aÃ§Ä±k, G Ã§engelli.

Ã‡alÄ±ÅŸtÄ±rma (fontTools gerekli):  python Tools/bubble_font.py
Girdi:  Assets/Art/Fonts/PixelifySans.ttf (deÄŸiÅŸken font, wght 400 Ã¶rneÄŸi alÄ±nÄ±r)
Ã‡Ä±ktÄ±:  Assets/Art/Fonts/DepremBalon-Regular.ttf, Assets/Art/Fonts/DepremMenu-Regular.ttf
        (OFL; Pixelify Sans'ta korunan font adÄ± yok)
"""
from pathlib import Path

from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont
from fontTools.varLib import instancer

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "Assets/Art/Fonts/PixelifySans.ttf"
FONTS_DIR = ROOT / "Assets/Art/Fonts"

# Pixelify'Ä±n Ä±zgarasÄ±: 'o' ve 'e' x 60..525 (5 piksel), y -12..450 (5 satÄ±r)
X0, PX = 60, 93
Y0, PY = -12, 92.4
RIGHT_BEARING = 61

# Ãœstten alta; '#' dolu piksel. VT323'Ã¼n e ve E'si bu Ä±zgaraya indirilmiÅŸ hali.
BUBBLE_GLYPHS = {
    "e": [
        ".###.",
        "#...#",
        "#####",
        "#....",
        ".###.",
    ],
    "E": [
        "####",
        "#...",
        "#...",
        "####",
        "#...",
        "#...",
        "####",
    ],
}

# Play testi 33: Pixelify'da B'nin sol kÃ¶ÅŸeleri yuvarlak (G gibi), 5 ile S ve Z ile 2 aynÄ± Ã§iziliyordu
CLEAR_GLYPHS = {
    "B": [
        "####.",
        "#...#",
        "#...#",
        "####.",
        "#...#",
        "#...#",
        "####.",
    ],
    "5": [
        "#####",
        "#....",
        "####.",
        "....#",
        "....#",
        "#...#",
        ".###.",
    ],
    "Z": [
        "#####",
        "....#",
        "...#.",
        "..#..",
        ".#...",
        "#....",
        "#####",
    ],
    # C ve G birbirine benziyordu ("Civil Game"): C saÄŸÄ± aÃ§Ä±k, G'nin iÃ§inde belirgin Ã§engel
    "C": [
        ".####",
        "#....",
        "#....",
        "#....",
        "#....",
        "#....",
        ".####",
    ],
    "G": [
        ".####",
        "#....",
        "#....",
        "#.###",
        "#...#",
        "#...#",
        ".###.",
    ],
}


def runs(row):
    """SatÄ±rdaki dolu piksel dizileri: (baÅŸlangÄ±Ã§, bitiÅŸ) sÃ¼tunlarÄ±"""
    result, c = [], 0
    while c < len(row):
        if row[c] != "#":
            c += 1
            continue
        start = c
        while c < len(row) and row[c] == "#":
            c += 1
        result.append((start, c))
    return result


def draw(rows):
    # Alt alta aynÄ± dizi tek dikdÃ¶rtgen olur (satÄ±r aralarÄ±nda ince dikiÅŸ gÃ¶rÃ¼nmesin)
    pen = TTGlyphPen(None)
    height = len(rows)
    row_runs = [runs(row) for row in rows]
    used = set()
    for i in range(height):
        for run in row_runs[i]:
            if (i, run) in used:
                continue
            j = i
            while j + 1 < height and run in row_runs[j + 1]:
                j += 1
            for k in range(i, j + 1):
                used.add((k, run))
            # i Ã¼st satÄ±r, j alt satÄ±r; alttan satÄ±r no
            y0, y1 = round(Y0 + (height - 1 - j) * PY), round(Y0 + (height - i) * PY)
            x0, x1 = X0 + run[0] * PX, X0 + run[1] * PX
            # Saat yÃ¶nÃ¼nde dikdÃ¶rtgen (TrueType dÄ±ÅŸ kontur)
            pen.moveTo((x0, y0))
            pen.lineTo((x0, y1))
            pen.lineTo((x1, y1))
            pen.lineTo((x1, y0))
            pen.closePath()
    return pen.glyph()


def build(glyphs, family, style_note, description, target):
    font = instancer.instantiateVariableFont(TTFont(SOURCE), {"wght": 400})
    cmap = font.getBestCmap()
    glyf = font["glyf"]
    for char, rows in glyphs.items():
        name = cmap[ord(char)]
        glyph = draw(rows)
        glyph.recalcBounds(glyf)
        glyf[name] = glyph
        width = len(rows[0])
        font["hmtx"][name] = (X0 + width * PX + RIGHT_BEARING, glyph.xMin)

    # Kaynakla karÄ±ÅŸmasÄ±n diye yeni ad (Unity aynÄ± adlÄ± iki fontu karÄ±ÅŸtÄ±rabilir)
    postscript = family.replace(" ", "") + "-Regular"
    names = font["name"]
    for record in list(names.names):
        if record.nameID in (1, 4, 16):
            names.setName(family if record.nameID != 4 else family + " Regular",
                          record.nameID, record.platformID, record.platEncID, record.langID)
        elif record.nameID == 6:
            names.setName(postscript, 6, record.platformID, record.platEncID, record.langID)
        elif record.nameID == 3:
            names.setName(f"{postscript};{style_note}", 3, record.platformID, record.platEncID, record.langID)
    names.setName(description, 10, 3, 1, 0x409)

    font.save(target)
    print("Kaydedildi:", target)


def main():
    build({**BUBBLE_GLYPHS, **CLEAR_GLYPHS}, "Deprem Balon", "Pixelify Sans e/E/B/5/Z/C/G deÄŸiÅŸikliÄŸi",
          "Pixelify Sans'tan tÃ¼retildi (OFL): e ve E VT323 biÃ§iminde; B, 5, Z, C ve G daha okunaklÄ± yeniden Ã§izildi.",
          FONTS_DIR / "DepremBalon-Regular.ttf")
    build(CLEAR_GLYPHS, "Deprem Menu", "Pixelify Sans B/5/Z/C/G deÄŸiÅŸikliÄŸi",
          "Pixelify Sans'tan tÃ¼retildi (OFL): B, 5, Z, C ve G daha okunaklÄ± yeniden Ã§izildi.",
          FONTS_DIR / "DepremMenu-Regular.ttf")


if __name__ == "__main__":
    main()
