from pathlib import Path
import textwrap

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parent
OUT = ROOT / "carousel-export"
W, H = 1080, 1350

BG = "#101418"
PANEL = "#171d23"
PANEL_2 = "#1f2830"
TEXT = "#f2f5f7"
MUTED = "#a6b0b8"
LINE = "#33404a"
ACCENT = "#58d68d"
ACCENT_2 = "#61b6ff"
WARN = "#ffcf6a"
TERMINAL = "#0c1116"
DARK_TEXT = "#08100c"


def font(size, bold=False, mono=False):
    if mono:
        names = [
            "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf",
            "/usr/share/fonts/truetype/liberation2/LiberationMono-Regular.ttf",
        ]
    elif bold:
        names = [
            "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
            "/usr/share/fonts/truetype/liberation2/LiberationSans-Bold.ttf",
        ]
    else:
        names = [
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/truetype/liberation2/LiberationSans-Regular.ttf",
        ]
    for name in names:
        if Path(name).exists():
            return ImageFont.truetype(name, size)
    return ImageFont.load_default()


FONTS = {
    "brand": font(24, bold=True),
    "kicker": font(17, mono=True),
    "eyebrow": font(21, mono=True),
    "h1": font(92, bold=True),
    "h2": font(68, bold=True),
    "h3": font(32, bold=True),
    "body": font(31),
    "body_small": font(24),
    "mono": font(25, mono=True),
    "mono_small": font(19, mono=True),
    "button": font(24, bold=True),
    "footer": font(20, mono=True),
}


def draw_wrapped(draw, xy, text, face, fill, width, line_gap=10):
    x, y = xy
    words = text.split()
    lines = []
    current = ""
    for word in words:
        trial = f"{current} {word}".strip()
        if draw.textbbox((0, 0), trial, font=face)[2] <= width or not current:
            current = trial
        else:
            lines.append(current)
            current = word
    if current:
        lines.append(current)

    for line in lines:
        draw.text((x, y), line, font=face, fill=fill)
        bbox = draw.textbbox((x, y), line, font=face)
        y += bbox[3] - bbox[1] + line_gap
    return y


def draw_header(draw, kicker):
    draw.rectangle((72, 85, 86, 99), fill=ACCENT)
    draw.text((98, 78), "Revit MCP", font=FONTS["brand"], fill=TEXT)
    kw = draw.textlength(kicker.upper(), font=FONTS["kicker"])
    draw.text((W - 72 - kw, 82), kicker.upper(), font=FONTS["kicker"], fill=MUTED)


def draw_footer(draw, num):
    draw.line((72, H - 122, W - 72, H - 122), fill=LINE, width=1)
    draw.text((72, H - 83), f"{num:02d}", font=FONTS["footer"], fill=MUTED)
    label = "Revit MCP"
    lw = draw.textlength(label, font=FONTS["footer"])
    draw.text((W - 72 - lw, H - 83), label, font=FONTS["footer"], fill=MUTED)


def draw_window(draw, box, title, rows=None, terminal=None):
    x1, y1, x2, y2 = box
    draw.rectangle(box, fill=PANEL, outline=LINE)
    draw.rectangle((x1, y1, x2, y1 + 64), fill=PANEL, outline=LINE)
    draw.rectangle((x1 + 22, y1 + 24, x1 + 37, y1 + 39), fill=WARN)
    draw.text((x1 + 54, y1 + 19), title, font=FONTS["body_small"], fill=MUTED)
    if terminal:
        draw.rectangle((x1, y1 + 64, x2, y2), fill=TERMINAL)
        y = y1 + 96
        for line in terminal.splitlines():
            draw.text((x1 + 34, y), line, font=FONTS["mono"], fill=TEXT)
            y += 42
    if rows:
        cell_w = (x2 - x1 - 64) // 2
        cell_h = 74
        for i, row in enumerate(rows):
            cx = x1 + 24 + (i % 2) * (cell_w + 16)
            cy = y1 + 88 + (i // 2) * (cell_h + 16)
            fill = ACCENT if row.get("primary") else PANEL_2
            color = DARK_TEXT if row.get("primary") else MUTED
            draw.rectangle((cx, cy, cx + cell_w, cy + cell_h), fill=fill, outline=ACCENT if row.get("primary") else LINE)
            if row.get("dot"):
                draw.rectangle((cx + 18, cy + 30, cx + 31, cy + 43), fill=ACCENT)
                draw.text((cx + 44, cy + 23), row["text"], font=FONTS["body_small"], fill=color)
            else:
                tw = draw.textlength(row["text"], font=FONTS["button"])
                draw.text((cx + (cell_w - tw) / 2, cy + 21), row["text"], font=FONTS["button"], fill=color)


def draw_status_panel(draw, box, title, items):
    x1, y1, x2, y2 = box
    draw.rectangle(box, fill=PANEL, outline=LINE)
    draw.rectangle((x1, y1, x2, y1 + 64), fill=PANEL, outline=LINE)
    draw.rectangle((x1 + 22, y1 + 24, x1 + 37, y1 + 39), fill=WARN)
    draw.text((x1 + 54, y1 + 19), title, font=FONTS["body_small"], fill=MUTED)
    yy = y1 + 92
    for item in items:
        draw.rectangle((x1 + 28, yy, x2 - 28, yy + 76), fill=PANEL_2, outline=LINE)
        draw.rectangle((x1 + 52, yy + 30, x1 + 68, yy + 46), fill=ACCENT)
        draw.text((x1 + 92, yy + 20), item, font=FONTS["body"], fill=TEXT)
        yy += 94


def base(kicker, num):
    img = Image.new("RGB", (W, H), BG)
    draw = ImageDraw.Draw(img)
    draw.rectangle((0, 0, W, 240), fill="#121a20")
    draw_header(draw, kicker)
    draw_footer(draw, num)
    return img, draw


def card(draw, box, title, body=None, num=None):
    x1, y1, x2, y2 = box
    draw.rectangle(box, fill=PANEL, outline=LINE)
    if num is not None:
        draw.rectangle((x1 + 28, y1 + 28, x1 + 80, y1 + 80), fill=ACCENT)
        draw.text((x1 + 45, y1 + 39), str(num), font=FONTS["button"], fill=DARK_TEXT)
        tx = x1 + 104
    else:
        tx = x1 + 28
    y = draw_wrapped(draw, (tx, y1 + 24), title, FONTS["h3"], TEXT, x2 - tx - 28, 8)
    if body:
        draw_wrapped(draw, (tx, y + 8), body, FONTS["body_small"], MUTED, x2 - tx - 28, 8)


SLIDES = [
    {
        "kicker": "Autodesk Revit + IA",
        "eyebrow": "AUTOMAÇÃO PARA O DIA A DIA",
        "title": "Use IA dentro do Revit, sem programar.",
        "body": "Conecte seu projeto a um assistente e transforme pedidos simples em consultas, revisões e automações.",
    },
    {
        "kicker": "Problema",
        "eyebrow": "MUITO CLIQUE, POUCA DECISÃO",
        "title": "Quanto tempo sua equipe perde em tarefas repetitivas?",
    },
    {
        "kicker": "MCP simples",
        "eyebrow": "O QUE E MCP?",
        "title": "MCP é a ponte entre a IA e suas ferramentas.",
        "body": "Ele permite que uma IA converse com sistemas externos, como o Revit, com permissão e controle do usuário.",
    },
    {
        "kicker": "Produto",
        "eyebrow": "ONDE O REVIT MCP ENTRA",
        "title": "Ele conecta essa ponte ao projeto aberto no Revit.",
    },
    {
        "kicker": "Exemplos",
        "eyebrow": "PEDIDOS EM LINGUAGEM NATURAL",
        "title": "Voce pede como falaria com alguem da equipe.",
    },
    {
        "kicker": "Beneficio",
        "eyebrow": "MENOS TRABALHO MANUAL",
        "title": "Automação onde ela mais pesa: no repetitivo.",
    },
    {
        "kicker": "Controle",
        "eyebrow": "VOCÊ CONTINUA NO COMANDO",
        "title": "A IA não trabalha sozinha no seu modelo.",
    },
    {
        "kicker": "Chamada final",
        "eyebrow": "PARA ARQUITETOS, BIM MANAGERS E EQUIPES",
        "title": "Quer testar IA conectada ao Revit de verdade?",
        "body": "Conheça o Revit MCP e comece conectando o add-in pelo Ribbon do Revit.",
    },
]


def render():
    OUT.mkdir(exist_ok=True)
    for idx, data in enumerate(SLIDES, 1):
        img, draw = base(data["kicker"], idx)
        draw.text((72, 208), data["eyebrow"], font=FONTS["eyebrow"], fill=ACCENT)
        y = draw_wrapped(draw, (72, 260), data["title"], FONTS["h1"] if idx == 1 else FONTS["h2"], TEXT, 900, 12)
        if data.get("body"):
            y = draw_wrapped(draw, (72, y + 24), data["body"], FONTS["body"], MUTED, 850, 12)

        if idx == 1:
            draw_window(
                draw,
                (72, 835, 890, 1110),
                "Revit MCP Ribbon",
                rows=[
                    {"text": "Conexão MCP ativa", "dot": True},
                    {"text": "Projeto.rvt conectado", "dot": True},
                    {"text": "Iniciar MCP", "primary": True},
                    {"text": "Pronto para pedidos"},
                ],
            )
        elif idx == 2:
            card(draw, (72, 610, 1008, 735), "Conferir informações espalhadas pelo modelo.", num=1)
            card(draw, (72, 760, 1008, 885), "Criar ou ajustar elementos em sequencia.", num=2)
            card(draw, (72, 910, 1008, 1035), "Revisar padrões, tipos, parâmetros e quantidades.", num=3)
        elif idx == 3:
            draw.rectangle((72, 790, 1008, 1055), fill=PANEL, outline=LINE)
            draw.rectangle((110, 828, 320, 1018), fill=PANEL_2, outline=LINE)
            draw.rectangle((760, 828, 970, 1018), fill=PANEL_2, outline=LINE)
            draw.text((180, 895), "IA", font=font(54, bold=True), fill=TEXT)
            draw.text((795, 895), "Revit", font=font(48, bold=True), fill=TEXT)
            draw.line((320, 923, 760, 923), fill=LINE, width=4)
            draw.rectangle((465, 887, 615, 958), fill=ACCENT)
            draw.text((506, 906), "MCP", font=FONTS["button"], fill=DARK_TEXT)
        elif idx == 4:
            draw_status_panel(
                draw,
                (72, 680, 930, 1052),
                "Status da conexão",
                ["Revit aberto", "Projeto ativo identificado", "Conexão MCP pronta"],
            )
            draw_wrapped(draw, (72, 1080), "A IA entende o contexto do projeto antes de sugerir ou executar uma ação.", FONTS["body_small"], MUTED, 790)
        elif idx == 5:
            card(draw, (72, 625, 1008, 750), '"Liste todas as portas do projeto por tipo."', "Pedido")
            card(draw, (72, 775, 1008, 900), '"Crie 5 níveis com pé-direito de 3 metros."', "Pedido")
            card(draw, (72, 925, 1008, 1050), '"Encontre paredes sem tipo correto."', "Pedido")
        elif idx == 6:
            card(draw, (72, 650, 1008, 790), "Consultar", "Elementos, vistas, famílias, parâmetros e quantidades.")
            card(draw, (72, 815, 1008, 955), "Revisar", "Padrões do modelo e informações que precisam de atenção.")
            card(draw, (72, 980, 1008, 1120), "Executar", "Ações no Revit aberto, dentro do contexto correto do projeto.")
        elif idx == 7:
            draw_window(draw, (72, 665, 1008, 1045), "Fluxo seguro")
            items = [
                "O Revit precisa estar aberto.",
                "Um projeto ou família fica ativo.",
                "Você escolhe o pedido.",
                "Você revisa resultados e ações importantes.",
            ]
            yy = 760
            for n, item in enumerate(items, 1):
                draw.text((122, yy), f"{n}.", font=FONTS["h3"], fill=ACCENT)
                draw_wrapped(draw, (180, yy), item, FONTS["body"], MUTED, 760)
                yy += 70
        elif idx == 8:
            draw.rectangle((72, 820, 1008, 970), fill=PANEL, outline=LINE)
            draw.text((102, 850), "PRÓXIMO PASSO", font=FONTS["mono_small"], fill=ACCENT)
            draw.text((102, 892), "Acesse o link na bio", font=FONTS["h3"], fill=TEXT)
            draw.rectangle((736, 875, 970, 938), fill=ACCENT)
            draw.text((772, 894), "Link na bio", font=FONTS["button"], fill=DARK_TEXT)

        img.save(OUT / f"slide-{idx:02d}.png", quality=95)


if __name__ == "__main__":
    render()
