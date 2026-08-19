"""Os icones da janela.

Sao os Fluent UI System Icons (MIT), rasterizados em 256x256 preto sobre
transparente e guardados em `icons/`. O preto nao e a cor final: e mascara. O
alfa carrega o desenho e o RGB e reescrito na hora de usar, com a cor que o tema
pedir -- um arquivo serve para o claro e para o escuro, e o mesmo desenho sai
cinza num botao e azul de acento num cartao.

Reduzir de 256 para 16-32 pixels sai limpo em qualquer escala de tela; o
caminho contrario, ampliar, borraria.
"""

from __future__ import annotations

import os

import gi

gi.require_version("Gtk", "4.0")

from gi.repository import Gdk, GdkPixbuf, GLib, Gtk  # noqa: E402

ICON_DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "icons")

_cache: dict[tuple[str, int, tuple[int, int, int]], Gdk.Texture] = {}


def texture(name: str, size: int, color: tuple[int, int, int]) -> Gdk.Texture | None:
    key = (name, size, color)
    if key in _cache:
        return _cache[key]

    path = os.path.join(ICON_DIR, f"{name}.png")
    if not os.path.isfile(path):
        return None

    try:
        pixbuf = GdkPixbuf.Pixbuf.new_from_file_at_size(path, size, size)
    except GLib.Error:
        return None

    if not pixbuf.get_has_alpha():
        pixbuf = pixbuf.add_alpha(False, 0, 0, 0)

    data = bytearray(pixbuf.get_pixels())
    width, height = pixbuf.get_width(), pixbuf.get_height()
    stride, channels = pixbuf.get_rowstride(), pixbuf.get_n_channels()
    r, g, b = color

    for y in range(height):
        row = y * stride
        for x in range(width):
            offset = row + x * channels
            data[offset] = r
            data[offset + 1] = g
            data[offset + 2] = b

    tinted = GdkPixbuf.Pixbuf.new_from_bytes(
        GLib.Bytes.new(bytes(data)), pixbuf.get_colorspace(), True,
        pixbuf.get_bits_per_sample(), width, height, stride,
    )
    result = Gdk.Texture.new_for_pixbuf(tinted)
    _cache[key] = result
    return result


def image(name: str, size: int, color: tuple[int, int, int]) -> Gtk.Widget:
    """Um `Gtk.Image` pronto para entrar no layout, ja no tamanho pedido."""
    picture = Gtk.Image()
    tex = texture(name, size, color)
    if tex is not None:
        picture.set_from_paintable(tex)
    picture.set_pixel_size(size)
    return picture
