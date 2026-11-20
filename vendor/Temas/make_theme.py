"""Genera el tema BloquesColor para turing-smart-screen-python (3.5", vertical).

Crea background.png, theme.yaml y preview.png. La vista previa reproduce cómo
dibuja el programa cada elemento (textos, barras y gráficos de línea).
"""
import math
import sys
from pathlib import Path

import yaml
from PIL import Image, ImageDraw, ImageFont

OUT = Path(sys.argv[1]) if len(sys.argv) > 1 else None
# Fuentes del código de la pantalla (este archivo está en <proyecto>\vendor\Temas)
FONTS = Path(__file__).resolve().parents[2] / "src" / "turing-smart-screen-python" / "res" / "fonts"
W, H = 320, 480

F_TITLE = "jetbrains-mono/JetBrainsMono-Medium.ttf"
F_BIG = "jetbrains-mono/JetBrainsMono-Medium.ttf"
F_SMALL = "jetbrains-mono/JetBrainsMono-Medium.ttf"
TITLE_SIZE = 18

BG = (11, 14, 20)
CARD = (21, 26, 36)
WHITE = (255, 255, 255)
GRAPH_BG = (15, 19, 27)
GRAPH_GRID = (78, 88, 110)  # línea central discontinua (50 %)
GRAPH_GRID_SOFT = (50, 58, 75)  # resto de la cuadrícula: visible en la pantalla sin tapar la línea
CPU = dict(accent=(255, 138, 61), light=(255, 196, 155))
GPU = dict(accent=(126, 227, 90), light=(185, 240, 166), dim=(143, 174, 132), track=(38, 48, 34))
NET = dict(accent=(255, 111, 174), light=(255, 194, 220))
RAM = dict(accent=(180, 140, 255), light=(217, 200, 255), track=(42, 36, 64))
DSK = dict(accent=(56, 208, 232), light=(168, 238, 248), track=(23, 48, 56))

CPU_GRAPH = dict(x=20, y=88, w=280, h=38)
CPU_GRAPH_V_DIVISIONS = 6  # con HISTORY_SIZE 30 y lecturas cada 1 s: una división cada 5 s
# Margen de la escala del gráfico de CPU (en puntos de %): sin él, el 0 % se dibuja justo en el borde
# inferior y la línea queda fuera de la imagen. Con -6..106 el 0 % queda ~2 px por encima del borde.
CPU_GRAPH_PAD = 6
CPU_GRAPH_MIN, CPU_GRAPH_MAX = -CPU_GRAPH_PAD, 100 + CPU_GRAPH_PAD
NET_MAX = 125_000_000  # 1 Gbit/s en bytes/s; con AUTOSCALE la escala se ajusta al tráfico reciente

# Tarjetas de arriba abajo (alto de cada una; 8 px de separación). Las posiciones de su contenido se
# calculan desde la parte de arriba de cada tarjeta, así que el orden se puede cambiar (set_layout).
CARD_HEIGHT = {"CPU": 128, "GPU": 120, "NET": 80, "RAM": 52, "DSK": 52}
CARD_COLOR = {"CPU": CPU["accent"], "GPU": GPU["accent"], "NET": NET["accent"], "RAM": RAM["accent"],
              "DSK": DSK["accent"]}
ORDER = ("CPU", "GPU", "NET", "RAM", "DSK")
# Temperatura de cada módulo de RAM junto a su barra (datos CUSTOM RamTemperature1/2 del parche 6)
RAM_TEMPS = False
RAM_TEMP_LABELS = ("M1", "M2")


def set_layout(order=ORDER, ram_temps=False):
    """Recalcula TOP, CARDS, NET_GRAPHS y BARS para un orden de tarjetas."""
    global TOP, CARDS, NET_GRAPHS, BARS, ORDER, RAM_TEMPS
    ORDER, RAM_TEMPS = tuple(order), ram_temps
    TOP, y = {}, 8
    for key in ORDER:
        TOP[key] = y
        y += CARD_HEIGHT[key] + 8
    CARDS = [(8, TOP[k], 312, TOP[k] + CARD_HEIGHT[k], CARD_COLOR[k]) for k in ORDER]  # (x0, y0, x1, y1, color)
    n = TOP["NET"]
    NET_GRAPHS = {"down": dict(x=20, y=n + 52, w=130, h=20), "up": dict(x=170, y=n + 52, w=130, h=20)}
    ram_w = ram_bar_width() if ram_temps else 280
    BARS = {  # nombre: (x, y, w, h, color_pista, color_barra) — tamaño visual de la barra completa
        "gpu_uso": (170, 191, 130, 8, GPU["track"], GPU["accent"]),
        "gpu_vram": (170, 220, 130, 8, GPU["track"], GPU["accent"]),
        "gpu_vent": (170, 249, 130, 8, GPU["track"], GPU["accent"]),
        "ram": (20, TOP["RAM"] + 34, ram_w, 10, RAM["track"], RAM["accent"]),
        "disco": (20, TOP["DSK"] + 34, 280, 8, DSK["track"], DSK["accent"]),
    }
# El programa rellena las barras con un rectángulo. Para que se vean redondeadas, el fondo lleva la pista
# redondeada y el extremo izquierdo ya pintado con el color de la barra; la zona que rellena el programa
# empieza y termina BAR_RADIUS px hacia dentro.
BAR_RADIUS = 4


def font(path, size):
    return ImageFont.truetype(str(FONTS / path), size)


def width(text, path, size):
    return font(path, size).getlength(text)


def rgb(c):
    return f"{c[0]}, {c[1]}, {c[2]}"


RAM_TEMP_SIZE = 15
RAM_TEMP_GAP = 5     # px entre la etiqueta ("M1") y su temperatura
RAM_TEMP_SEP = 14    # px entre las dos temperaturas, y entre la barra y la primera


def ram_temp_positions():
    """Fila de la barra de RAM: [barra] M1 38°C  M2 39°C, alineado a la derecha en x=300.
    Devuelve [(x_etiqueta, borde_derecho_valor), ...] para M1 y M2."""
    w = lambda t: width(t, F_SMALL, RAM_TEMP_SIZE)
    right, pos = 300, []
    for label in reversed(RAM_TEMP_LABELS):
        value_right = right
        label_x = value_right - w("00°C") - RAM_TEMP_GAP - w(label)
        pos.insert(0, (label_x, value_right))
        right = label_x - RAM_TEMP_SEP
    return pos


def ram_bar_width():
    return round(ram_temp_positions()[0][0] - RAM_TEMP_SEP - 20)


set_layout()


# --- Fondo -------------------------------------------------------------------

def draw_graph_area(d, g):
    d.rounded_rectangle((g["x"], g["y"], g["x"] + g["w"] - 1, g["y"] + g["h"] - 1), radius=3, fill=GRAPH_BG)


def draw_background(base=None):
    """Fondo completo; con `base`, dibuja gráficos y barras sobre las tarjetas ya pintadas en esa imagen."""
    img = base.copy() if base is not None else Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(img)
    if base is None:
        for x0, y0, x1, y1, color in CARDS:
            d.rounded_rectangle((x0, y0, x1, y1), radius=10, fill=CARD, outline=color, width=2)

    # Gráfico de uso de CPU con cuadrícula: horizontales al 25/50/75 % y verticales cada 5 s
    g = CPU_GRAPH
    gx0, gy0, gx1, gy1 = g["x"], g["y"], g["x"] + g["w"] - 1, g["y"] + g["h"] - 1
    draw_graph_area(d, g)
    for i in range(1, CPU_GRAPH_V_DIVISIONS):
        x = round(gx0 + i * g["w"] / CPU_GRAPH_V_DIVISIONS)
        d.line((x, gy0 + 1, x, gy1 - 1), fill=GRAPH_GRID_SOFT)
    for pct in (25, 50, 75):
        # Misma escala que usa el programa para la línea, para que la cuadrícula cuadre con los valores
        y = round(gy0 + g["h"] - (pct - CPU_GRAPH_MIN) / (CPU_GRAPH_MAX - CPU_GRAPH_MIN) * g["h"])
        if pct == 50:  # línea central discontinua y algo más visible
            for x in range(gx0 + 1, gx1, 7):
                d.line((x, y, min(x + 3, gx1 - 1), y), fill=GRAPH_GRID)
        else:
            d.line((gx0 + 1, y, gx1 - 1, y), fill=GRAPH_GRID_SOFT)

    for g in NET_GRAPHS.values():
        draw_graph_area(d, g)

    r = BAR_RADIUS
    for x, y, w, h, track, color in BARS.values():
        d.rounded_rectangle((x, y, x + w - 1, y + h - 1), radius=r, fill=track)
        # Extremo izquierdo redondeado con el color de la barra (solo la parte fuera de la zona del programa)
        d.rounded_rectangle((x, y, x + 2 * r, y + h - 1), radius=r, fill=color)
        d.rectangle((x + r, y, x + 2 * r, y + h - 1), fill=track)
    return img


# --- Tema --------------------------------------------------------------------

def text(x, y, size, color, anchor="ls", fnt=F_SMALL, chars=None, descenders=False, **extra):
    """Valor dinámico con la línea base en (x, y), alineado a la izquierda ("ls") o a la derecha ("rs").

    La versión 3.10.0 no borra el texto anterior si el nuevo es más corto, así que cada valor tiene un
    recuadro fijo de `chars` caracteres que se repinta entero. Con recuadro, el programa coloca el texto
    en la esquina superior: se ancla por la línea de ascendentes ("la"/"ra"), que no depende del texto.
    Los números, "%", "°C" o "GHz" no bajan de la línea base, así que el recuadro solo baja 2 px salvo
    que el valor tenga trazos descendentes (`descenders`, p. ej. la "/" de "K/s").
    """
    ascent, descent = font(fnt, size).getmetrics()
    box_w = math.ceil(width("0" * chars, fnt, size))
    box_x = x if anchor == "ls" else x - box_w
    box_h = ascent + (descent if descenders else 2)
    data = dict(SHOW=True, X=round(box_x), Y=round(y - ascent), WIDTH=box_w, HEIGHT=box_h,
                FONT=fnt, FONT_SIZE=size, FONT_COLOR=rgb(color), BACKGROUND_IMAGE="background.png",
                ANCHOR="la" if anchor == "ls" else "ra")
    data.update(extra)
    return data


def static(label, x, y, size, color, anchor="ls", fnt=F_SMALL):
    # Los textos fijos se dibujan una sola vez: no necesitan recuadro y conservan el anclaje por la línea base
    return dict(TEXT=label, X=round(x), Y=round(y), FONT=fnt, FONT_SIZE=size, FONT_COLOR=rgb(color),
                BACKGROUND_IMAGE="background.png", ANCHOR=anchor)


def bar(name):
    x, y, w, h, _, color = BARS[name]
    return dict(SHOW=True, X=x + BAR_RADIUS, Y=y, WIDTH=w - 2 * BAR_RADIUS, HEIGHT=h, MIN_VALUE=0, MAX_VALUE=100,
                BAR_COLOR=rgb(color), BAR_OUTLINE=False, BACKGROUND_IMAGE="background.png")


def line_graph(g, color, history, max_value, autoscale, line_width=2, min_value=0):
    return dict(SHOW=True, X=g["x"], Y=g["y"], WIDTH=g["w"], HEIGHT=g["h"], MIN_VALUE=min_value, MAX_VALUE=max_value,
                HISTORY_SIZE=history, AUTOSCALE=autoscale, LINE_COLOR=rgb(color), LINE_WIDTH=line_width,
                AXIS=False, BACKGROUND_IMAGE="background.png")


def build_theme():
    s15 = lambda t: width(t, F_SMALL, 15)
    s16 = lambda t: width(t, F_SMALL, 16)
    # Líneas base desde la parte de arriba de cada tarjeta
    net_title, net_text = TOP["NET"] + 22, TOP["NET"] + 46
    ram_line, dsk_line = TOP["RAM"] + 24, TOP["DSK"] + 24

    # GPU, columna derecha: etiqueta fija + porcentaje ("uso 35 %", "vram 34 %", "vent. 42 %")
    gpu_col = 170
    # VRAM absoluta bajo la temperatura, alineada a la derecha de su hueco (x=160): "4123 / 12227 MB"
    vram_end = 160
    vram_total_right = vram_end - s15(" MB")
    vram_used_right = vram_total_right - s15("00000 / ")
    # RAM y disco: "usado / total" alineado a la derecha antes del porcentaje (x=240)
    used_total_end = 240
    ram_total_right = used_total_end - s15(" MB")
    ram_used_right = ram_total_right - s15("00000 / ")  # total de 5 cifras (32768 MB)
    dsk_total_right = used_total_end - s15(" GB")
    dsk_used_right = dsk_total_right - s15("000 / ")  # disco C: de 1 TB, total de 3 cifras

    static_text = {
        "CPU_TITULO": static("CPU", 20, 30, TITLE_SIZE, CPU["accent"], fnt=F_TITLE),
        "CPU_USO": static("uso", 300, 47, 15, CPU["light"], "rs"),
        "GPU_TITULO": static("GPU", 20, 166, TITLE_SIZE, GPU["accent"], fnt=F_TITLE),
        "GPU_MODELO": static("RTX 5070", 20 + width("GPU", F_TITLE, TITLE_SIZE) + 10, 166, 16, GPU["dim"]),
        "GPU_USO": static("uso", gpu_col, 186, 15, GPU["light"]),
        "GPU_VRAM": static("vram", gpu_col, 215, 15, GPU["light"]),
        "GPU_VENT": static("vent.", gpu_col, 244, 15, GPU["light"]),
        "GPU_VRAM_SEP": static(" / ", vram_used_right, 250, 15, GPU["light"]),
        "GPU_VRAM_MB": static(" MB", vram_end, 250, 15, GPU["light"], "rs"),
        "RED_TITULO": static("Red", 20, net_title, TITLE_SIZE, NET["accent"], fnt=F_TITLE),
        "RED_BAJADA": static("\u2193", 20, net_text, 16, NET["light"]),
        "RED_SUBIDA": static("\u2191", 170, net_text, 16, NET["light"]),
        "RAM_TITULO": static("RAM", 20, ram_line, TITLE_SIZE, RAM["accent"], fnt=F_TITLE),
        "RAM_SEP": static(" / ", ram_used_right, ram_line, 15, RAM["light"]),
        "RAM_MB": static(" MB", used_total_end, ram_line, 15, RAM["light"], "rs"),
        "DISCO_TITULO": static("Disco C:", 20, dsk_line, TITLE_SIZE, DSK["accent"], fnt=F_TITLE),
        "DISCO_SEP": static(" / ", dsk_used_right, dsk_line, 15, DSK["light"]),
        "DISCO_GB": static(" GB", used_total_end, dsk_line, 15, DSK["light"], "rs"),
    }

    def percent(key, right, baseline, size, color):
        """Porcentaje con el "%" separado del número: el valor se muestra sin unidad, alineado a la
        derecha en un hueco de 3 cifras, y el "%" es un texto fijo a `gap` px. `right` es el borde
        derecho del "%"."""
        gap = max(3, round(size * 0.2))
        num_right = right - width("%", F_SMALL, size) - gap
        static_text[key] = static("%", right, baseline, size, color, "rs")
        return text(num_right, baseline, size, color, "rs", chars=3, MIN_SIZE=0, SHOW_UNIT=False)

    # Columna derecha de la GPU: números alineados a la derecha tras la etiqueta más larga ("vent. ")
    gpu_pct_right = gpu_col + s15("vent. 000") + max(3, round(15 * 0.2)) + s15("%")

    stats = {
        "CPU": {
            "PERCENTAGE": {
                "INTERVAL": 1,
                "TEXT": percent("CPU_PCT", 300, 78, 28, CPU["accent"]),
                "LINE_GRAPH": line_graph(CPU_GRAPH, CPU["accent"], 30, CPU_GRAPH_MAX, False, min_value=CPU_GRAPH_MIN),
            },
            "FREQUENCY": {"INTERVAL": 2, "TEXT": text(300, 30, 16, CPU["light"], "rs", chars=8, MIN_SIZE=0)},
            "TEMPERATURE": {"INTERVAL": 2, "TEXT": text(20, 78, 42, WHITE, fnt=F_BIG, chars=5, MIN_SIZE=0)},
        },
        "GPU": {
            "INTERVAL": 1,
            "PERCENTAGE": {"TEXT": percent("GPU_USO_PCT", gpu_pct_right, 186, 15, GPU["light"]),
                           "GRAPH": bar("gpu_uso")},
            "MEMORY_PERCENT": {"TEXT": percent("GPU_VRAM_PCT", gpu_pct_right, 215, 15, GPU["light"]),
                               "GRAPH": bar("gpu_vram")},
            "FAN_SPEED": {"TEXT": percent("GPU_VENT_PCT", gpu_pct_right, 244, 15, GPU["light"]),
                          "GRAPH": bar("gpu_vent")},
            "MEMORY_USED": {"TEXT": text(vram_used_right, 250, 15, GPU["light"], "rs", chars=5, MIN_SIZE=0, SHOW_UNIT=False)},
            "MEMORY_TOTAL": {"TEXT": text(vram_total_right, 250, 15, GPU["light"], "rs", chars=5, MIN_SIZE=0, SHOW_UNIT=False)},
            "TEMPERATURE": {"TEXT": text(20, 214, 42, WHITE, fnt=F_BIG, chars=5, MIN_SIZE=0)},
            "FREQUENCY": {"TEXT": text(300, 166, 16, GPU["light"], "rs", chars=8, MIN_SIZE=0)},
        },
        "NET": {
            "INTERVAL": 1,
            "ETH": {
                "DOWNLOAD": {"TEXT": text(20 + s16("\u2193 "), net_text, 16, NET["light"], chars=10, descenders=True, MIN_SIZE=0),
                             "LINE_GRAPH": line_graph(NET_GRAPHS["down"], NET["accent"], 20, NET_MAX, True)},
                "UPLOAD": {"TEXT": text(170 + s16("\u2191 "), net_text, 16, NET["light"], chars=10, descenders=True, MIN_SIZE=0),
                           "LINE_GRAPH": line_graph(NET_GRAPHS["up"], NET["accent"], 20, NET_MAX, True)},
            },
        },
        "MEMORY": {
            "INTERVAL": 3,
            "VIRTUAL": {
                "GRAPH": bar("ram"),
                "PERCENT_TEXT": percent("RAM_PCT", 300, ram_line, 20, RAM["accent"]),
                "USED": text(ram_used_right, ram_line, 15, RAM["light"], "rs", chars=5, MIN_SIZE=0, SHOW_UNIT=False),
                "TOTAL": text(ram_total_right, ram_line, 15, RAM["light"], "rs", chars=5, MIN_SIZE=0, SHOW_UNIT=False),
            },
        },
        "DISK": {
            "INTERVAL": 30,
            "USED": {
                "GRAPH": bar("disco"),
                "PERCENT_TEXT": percent("DISCO_PCT", 300, dsk_line, 20, DSK["accent"]),
                "TEXT": text(dsk_used_right, dsk_line, 15, DSK["light"], "rs", chars=3, MIN_SIZE=0, SHOW_UNIT=False),
            },
            "TOTAL": {"TEXT": text(dsk_total_right, dsk_line, 15, DSK["light"], "rs", chars=3, MIN_SIZE=0, SHOW_UNIT=False)},
        },
    }

    if RAM_TEMPS:
        # Fila de la barra de RAM: "M1 38°C  M2 39°C". Valores del parche 6 (sensors_custom.RamTemperature1/2).
        temp_line = TOP["RAM"] + 45
        stats["CUSTOM"] = {"INTERVAL": 3}
        for i, (label_x, value_right) in enumerate(ram_temp_positions(), start=1):
            static_text[f"RAM_M{i}"] = static(RAM_TEMP_LABELS[i - 1], label_x, temp_line, RAM_TEMP_SIZE, RAM["accent"])
            stats["CUSTOM"][f"RamTemperature{i}"] = {
                "TEXT": text(value_right, temp_line, RAM_TEMP_SIZE, RAM["light"], "rs", chars=4, MIN_SIZE=0)}

    return {
        "author": "McGregor",
        "display": {"DISPLAY_SIZE": '3.5"', "DISPLAY_ORIENTATION": "portrait",
                    "DISPLAY_RGB_LED": rgb(CPU["accent"])},
        "static_images": {"BACKGROUND": dict(PATH="background.png", X=0, Y=0, WIDTH=W, HEIGHT=H)},
        "static_text": static_text,
        "STATS": stats,
    }


# --- Vista previa (misma lógica de dibujo que library/lcd/lcd_comm.py) -------

def color_of(value):
    return tuple(int(c) for c in value.split(","))


def render_text(canvas, data, value, background=None):
    """Igual que DisplayText de la 3.10.0: con recuadro, repinta el recuadro entero desde el fondo."""
    fnt = font(data["FONT"], data["FONT_SIZE"])
    color = color_of(data["FONT_COLOR"])
    if "WIDTH" not in data:
        ImageDraw.Draw(canvas).text((data["X"], data["Y"]), value, font=fnt, fill=color, anchor=data["ANCHOR"])
        return
    left, top = data["X"], data["Y"]
    right, bottom = left + data["WIDTH"], top + data["HEIGHT"]
    x = right if data["ANCHOR"].startswith("r") else left
    layer = (background or canvas).copy()
    ImageDraw.Draw(layer).text((x, top), value, font=fnt, fill=color, anchor=data["ANCHOR"])
    canvas.paste(layer.crop((left, top, right, bottom)), (left, top))


def check_no_ghosting(theme, background):
    """Pinta en cada recuadro un valor largo y luego uno corto: el resultado debe ser idéntico
    a pintar solo el corto (no quedan restos del valor anterior)."""
    fallos = []
    for ruta, data in iter_texts(theme["STATS"]):
        a, b = background.copy(), background.copy()
        render_text(a, data, "8" * 12, background)
        render_text(a, data, "1", background)
        render_text(b, data, "1", background)
        if a.tobytes() != b.tobytes():
            fallos.append(ruta)
    return fallos


def iter_texts(node, ruta=""):
    if isinstance(node, dict):
        if "FONT" in node and "SHOW" in node:
            yield ruta, node
        for k, v in node.items():
            yield from iter_texts(v, f"{ruta}/{k}")


def render_bar(canvas, data, value):
    x, y, w, h = data["X"], data["Y"], data["WIDTH"], data["HEIGHT"]
    filled = max(value / 100 * w - 1, 0)
    color = color_of(data["BAR_COLOR"])
    ImageDraw.Draw(canvas).rectangle((x, y, x + filled, y + h - 1), fill=color, outline=color)


def render_line(canvas, data, values):
    lo, hi = data["MIN_VALUE"], data["MAX_VALUE"]
    if data["AUTOSCALE"]:
        lo, hi = max(min(values) - 5, lo), min(max(values) + 5, hi)
    step = data["WIDTH"] / len(values)
    scale = data["HEIGHT"] / (hi - lo)
    pts = [(data["X"] + i * step, data["Y"] + data["HEIGHT"] - (v - lo) * scale) for i, v in enumerate(values)]
    ImageDraw.Draw(canvas).line(pts, fill=color_of(data["LINE_COLOR"]), width=data["LINE_WIDTH"])


def render_preview(theme, background):
    canvas = background.copy()
    for item in theme["static_text"].values():
        render_text(canvas, item, item["TEXT"])
    st = theme["STATS"]
    eth = st["NET"]["ETH"]
    samples = [
        (st["CPU"]["TEMPERATURE"]["TEXT"], "62°C"),
        (st["CPU"]["PERCENTAGE"]["TEXT"], "23"),
        (st["CPU"]["FREQUENCY"]["TEXT"], "4.85 GHz"),
        (st["GPU"]["TEMPERATURE"]["TEXT"], "48°C"),
        (st["GPU"]["FREQUENCY"]["TEXT"], "2.61 GHz"),
        (st["GPU"]["PERCENTAGE"]["TEXT"], "35"),
        (st["GPU"]["MEMORY_PERCENT"]["TEXT"], "34"),
        (st["GPU"]["FAN_SPEED"]["TEXT"], "42"),
        (st["GPU"]["MEMORY_USED"]["TEXT"], "4123"),
        (st["GPU"]["MEMORY_TOTAL"]["TEXT"], "12227"),
        (eth["DOWNLOAD"]["TEXT"], "85.3 M/s"),
        (eth["UPLOAD"]["TEXT"], "2.1 M/s"),
        (st["MEMORY"]["VIRTUAL"]["PERCENT_TEXT"], "47"),
        (st["MEMORY"]["VIRTUAL"]["USED"], "15360"),
        (st["MEMORY"]["VIRTUAL"]["TOTAL"], "32768"),
        (st["DISK"]["USED"]["PERCENT_TEXT"], "44"),
        (st["DISK"]["USED"]["TEXT"], "412"),
        (st["DISK"]["TOTAL"]["TEXT"], "932"),
    ]
    for i, temp in ((1, "38°C"), (2, "39°C")):
        custom = st.get("CUSTOM", {}).get(f"RamTemperature{i}")
        if custom:
            samples.append((custom["TEXT"], temp))
    for data, value in samples:
        render_text(canvas, data, value, background)
    render_bar(canvas, st["GPU"]["PERCENTAGE"]["GRAPH"], 35)
    render_bar(canvas, st["GPU"]["MEMORY_PERCENT"]["GRAPH"], 34)
    render_bar(canvas, st["GPU"]["FAN_SPEED"]["GRAPH"], 42)
    render_bar(canvas, st["MEMORY"]["VIRTUAL"]["GRAPH"], 47)
    render_bar(canvas, st["DISK"]["USED"]["GRAPH"], 44)
    render_line(canvas, st["CPU"]["PERCENTAGE"]["LINE_GRAPH"],
                [20 + 10 * math.sin(i / 3) + 6 * math.sin(i * 1.7) + i * 0.4 for i in range(30)])
    render_line(canvas, eth["DOWNLOAD"]["LINE_GRAPH"],
                [60e6 + 25e6 * math.sin(i / 2.5) + 8e6 * math.sin(i * 1.3) for i in range(20)])
    render_line(canvas, eth["UPLOAD"]["LINE_GRAPH"],
                [1.8e6 + 0.6e6 * math.sin(i / 2) + 0.3e6 * math.sin(i * 2.1) for i in range(20)])
    return canvas


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    background = draw_background()
    background.save(OUT / "background.png", optimize=True)
    theme = build_theme()
    header = ("# Tema BloquesColor para Turing Smart Screen 3.5\" (vertical)\n"
              "# CPU naranja, GPU verde, red rosa, RAM violeta y disco cian.\n"
              "# La red usa la tarjeta Ethernet elegida en el configurador (Ethernet interface).\n"
              "# Generado automáticamente: las posiciones de los textos dependen de la fuente JetBrains Mono.\n\n")
    with open(OUT / "theme.yaml", "w", encoding="utf-8") as f:
        f.write(header)
        yaml.safe_dump(theme, f, sort_keys=False, allow_unicode=True, default_flow_style=False, width=200)
    render_preview(theme, background).save(OUT / "preview.png", optimize=True)
    fallos = check_no_ghosting(theme, background)
    print("Restos de texto anterior:", fallos or "ninguno")
    print("Tema generado en", OUT)
