"""
Balon fontu (Play testi 13): Pixelify Sans'ın karakteri kalsın, ama zor okunan 'e' ve 'E' VT323'teki gibi
düz olsun. Pixelify'ın kendi piksel ızgarasında (yatay ~93, dikey ~92,4 birim) yeniden çizilir; piksel boyu
diğer harflerle aynı kalır.

Çalıştırma (fontTools gerekli):  python Tools/bubble_font.py
Girdi:  Assets/Art/Fonts/PixelifySans.ttf (değişken font, wght 400 örneği alınır)
Çıktı:  Assets/Art/Fonts/DepremBalon-Regular.ttf  (OFL; Pixelify Sans'ta korunan font adı yok)
"""
from pathlib import Path

from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont
from fontTools.varLib import instancer

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "Assets/Art/Fonts/PixelifySans.ttf"
TARGET = ROOT / "Assets/Art/Fonts/DepremBalon-Regular.ttf"

# Pixelify'ın ızgarası: 'o' ve 'e' x 60..525 (5 piksel), y -12..450 (5 satır)
X0, PX = 60, 93
Y0, PY = -12, 92.4
RIGHT_BEARING = 61

# Üstten alta; '#' dolu piksel. VT323'ün e ve E'si bu ızgaraya indirilmiş hali.
GLYPHS = {
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


def draw(rows):
    pen = TTGlyphPen(None)
    height = len(rows)
    for i, row in enumerate(rows):
        r = height - 1 - i  # alttan satır no
        y0, y1 = round(Y0 + r * PY), round(Y0 + (r + 1) * PY)
        c = 0
        while c < len(row):
            if row[c] != "#":
                c += 1
                continue
            start = c
            while c < len(row) and row[c] == "#":
                c += 1
            x0, x1 = X0 + start * PX, X0 + c * PX
            # Saat yönünde dikdörtgen (TrueType dış kontur)
            pen.moveTo((x0, y0))
            pen.lineTo((x0, y1))
            pen.lineTo((x1, y1))
            pen.lineTo((x1, y0))
            pen.closePath()
    return pen.glyph()


def main():
    font = instancer.instantiateVariableFont(TTFont(SOURCE), {"wght": 400})
    cmap = font.getBestCmap()
    glyf = font["glyf"]
    for char, rows in GLYPHS.items():
        name = cmap[ord(char)]
        glyph = draw(rows)
        glyph.recalcBounds(glyf)
        glyf[name] = glyph
        width = len(rows[0])
        font["hmtx"][name] = (X0 + width * PX + RIGHT_BEARING, glyph.xMin)

    # Kaynakla karışmasın diye yeni ad (Unity aynı adlı iki fontu karıştırabilir)
    names = font["name"]
    for record in list(names.names):
        if record.nameID in (1, 4, 16):
            names.setName("Deprem Balon" if record.nameID != 4 else "Deprem Balon Regular",
                          record.nameID, record.platformID, record.platEncID, record.langID)
        elif record.nameID == 6:
            names.setName("DepremBalon-Regular", 6, record.platformID, record.platEncID, record.langID)
        elif record.nameID == 3:
            names.setName("DepremBalon-Regular;Pixelify Sans e/E değişikliği", 3,
                          record.platformID, record.platEncID, record.langID)
    names.setName("Pixelify Sans'tan türetildi (OFL): e ve E harfleri VT323 biçiminde yeniden çizildi.",
                  10, 3, 1, 0x409)

    font.save(TARGET)
    print("Kaydedildi:", TARGET)


if __name__ == "__main__":
    main()
