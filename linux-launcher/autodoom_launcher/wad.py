"""Leitura de WAD: o suficiente para reconhecer um IWAD e nomear o jogo.

Porte direto do IwadCatalog.cs/WadValidator.cs do launcher Windows. O formato e
o mesmo em qualquer sistema: 12 bytes de cabecalho (magica, numero de lumps,
posicao do diretorio) e um diretorio de entradas de 16 bytes.
"""

from __future__ import annotations

import os
import struct
from dataclasses import dataclass

# Nome de arquivo -> jogo. Vale o mesmo mapa do Windows; o que muda e onde
# procurar, nao o que procurar.
KNOWN_IWADS = {
    "doom.wad": "DOOM",  # refinado depois: o Ultimate tem episodio 4
    "doom1.wad": "DOOM (Shareware)",
    "doomu.wad": "The Ultimate DOOM",
    "doom2.wad": "DOOM II",
    "tnt.wad": "Final DOOM: TNT - Evilution",
    "plutonia.wad": "Final DOOM: The Plutonia Experiment",
    "heretic.wad": "Heretic",
    "heretic1.wad": "Heretic (Shareware)",
    "hacx.wad": "HACX",
    "freedoom1.wad": "Freedoom (Ultimate Doom)",
    "freedoom2.wad": "Freedoom (Doom II)",
    "freedm.wad": "FreeDM",
    "rekkr.wad": "Rekkr",
}

# Ordem de lancamento, nao alfabetica: e assim que a serie e lembrada.
RELEASE_ORDER = [
    "The Ultimate DOOM", "DOOM (Registered)", "DOOM (Shareware)",
    "DOOM II", "Final DOOM: TNT - Evilution",
    "Final DOOM: The Plutonia Experiment",
    "Heretic", "Heretic (Shareware)", "HACX", "Rekkr",
    "Freedoom (Ultimate Doom)", "Freedoom (Doom II)", "FreeDM",
]


@dataclass(frozen=True)
class Iwad:
    label: str
    path: str

    def __str__(self) -> str:
        return self.label


def lump_names(path: str, limit: int = 65536) -> set[str]:
    """Nomes do diretorio de lumps. Conjunto vazio se o arquivo nao for um WAD."""
    try:
        with open(path, "rb") as f:
            magic, count, offset = struct.unpack("<4sii", f.read(12))
            if magic not in (b"IWAD", b"PWAD") or not 0 < count <= limit or offset < 12:
                return set()

            f.seek(offset)
            data = f.read(count * 16)

        return {
            data[i + 8:i + 16].split(b"\0")[0].decode("ascii", "ignore").upper()
            for i in range(0, len(data) - 15, 16)
        }
    except (OSError, struct.error, ValueError):
        return set()


def describe(path: str) -> Iwad | None:
    """Rotula um arquivo, ou devolve None se ele nao for um IWAD conhecido."""
    name = os.path.basename(path).lower()
    label = KNOWN_IWADS.get(name)
    if label is None:
        return None

    # doom.wad pode ser o registrado ou o Ultimate; o episodio 4 decide.
    if label == "DOOM":
        label = "The Ultimate DOOM" if "E4M1" in lump_names(path) else "DOOM (Registered)"

    return Iwad(label, os.path.abspath(path))


def release_index(label: str) -> int:
    for i, known in enumerate(RELEASE_ORDER):
        if label.startswith(known):
            return i
    return len(RELEASE_ORDER)
