"""Genera el tema BloquesColorPestanas: BloquesColor con el título de cada tarjeta en una pestaña de color.

Usa make_theme.py (en esta misma carpeta) para todo lo común: posiciones, colores, gráficos, barras,
textos y vista previa. Cambia las cabeceras (pestaña de color detrás del título, título a 16 px en
color oscuro, y "RTX 5070" alineado con el título y fuera de la pestaña), el orden de las tarjetas
(CPU, GPU, RAM, disco, red) y añade la temperatura de los dos módulos de RAM junto a su barra.

Uso:  python make_theme_pestanas.py <carpeta de salida>      (requiere Pillow y PyYAML)
"""
import sys
from pathlib import Path

import yaml
from PIL import Image, ImageDraw

import make_theme as m

ORDEN = ("CPU", "GPU", "RAM", "DSK", "NET")  # de arriba abajo
TITULO = {"CPU": "CPU_TITULO", "GPU": "GPU_TITULO", "NET": "RED_TITULO", "RAM": "RAM_TITULO", "DSK": "DISCO_TITULO"}
# Línea base de los títulos en BloquesColor, desde la parte de arriba de su tarjeta
BASE_TITULO = {"CPU": 22, "GPU": 22, "NET": 22, "RAM": 24, "DSK": 24}
TITULO_SIZE = 16
SUBIR = 1      # px que sube el título para quedar centrado en la pestaña
MARGEN = 5     # px de pestaña por debajo de la línea base del título
TEXTO_OSCURO = (14, 16, 24)


def mix(a, b, t):
    return tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(3))


def build():
    m.set_layout(ORDEN, ram_temps=True)
    img = Image.new("RGB", (m.W, m.H), m.BG)
    d = ImageDraw.Draw(img)
    theme = m.build_theme()
    for (x0, y0, x1, y1, color), card in zip(m.CARDS, m.ORDER):
        base, key = y0 + BASE_TITULO[card], TITULO[card]
        d.rounded_rectangle((x0, y0, x1, y1), radius=10, fill=m.CARD, outline=color, width=2)
        st = theme["static_text"][key]
        st["FONT_SIZE"] = TITULO_SIZE
        st["Y"] = base - SUBIR
        st["FONT_COLOR"] = m.rgb(TEXTO_OSCURO)
        fin = st["X"] + m.width(st["TEXT"], st["FONT"], TITULO_SIZE) + 9
        d.rounded_rectangle((x0, y0, round(fin), base - SUBIR + MARGEN), radius=9,
                            fill=mix(color, m.CARD, 0.12), corners=(True, False, True, False))
    # "RTX 5070" fuera de la pestaña de la GPU y en la misma línea base que el título
    gpu = theme["static_text"]["GPU_TITULO"]
    modelo = theme["static_text"]["GPU_MODELO"]
    modelo["X"] = round(gpu["X"] + m.width("GPU", gpu["FONT"], TITULO_SIZE) + 9 + 8)
    modelo["Y"] = gpu["Y"]
    return theme, m.draw_background(img)


if __name__ == "__main__":
    out = Path(sys.argv[1])
    out.mkdir(parents=True, exist_ok=True)
    theme, background = build()
    background.save(out / "background.png", optimize=True)
    header = ("# Tema BloquesColorPestanas para Turing Smart Screen 3.5\" (vertical)\n"
              "# Variante de BloquesColor: título de cada tarjeta en una pestaña de color (16 px).\n"
              "# De arriba abajo: CPU naranja, GPU verde, RAM violeta, disco cian y red rosa.\n"
              "# La RAM muestra la temperatura de cada módulo (M1, M2): necesita el parche 6 de la pantalla.\n"
              "# La red usa la tarjeta Ethernet elegida en el configurador (Ethernet interface).\n\n")
    with open(out / "theme.yaml", "w", encoding="utf-8") as f:
        f.write(header)
        yaml.safe_dump(theme, f, sort_keys=False, allow_unicode=True, default_flow_style=False, width=200)
    m.render_preview(theme, background).save(out / "preview.png", optimize=True)
    print("Restos de texto anterior:", m.check_no_ghosting(theme, background) or "ninguno")
    print("Tema generado en", out)
