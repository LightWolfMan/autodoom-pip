"""Onde os IWADs moram no Linux, e como achar os que estao fora do caminho.

O launcher do Windows varre o journal do NTFS para achar WAD espalhado pelo
disco. Isso nao existe aqui: o ext4 nao guarda um diario de nomes consultavel, e
o `fanotify`, que e o mais parecido em espirito, so ve eventos ao vivo -- nao
serve para descobrir o que ja esta no disco, e ainda exige CAP_SYS_ADMIN.

O substituto e o `plocate`: banco indexado, `locate '*.wad'` responde em
milissegundos, roda sem privilegio nenhum e ja vem instalado na maioria dos
desktops Debian e Arch. Quando ele nao existe, sobra varrer as pastas conhecidas
-- que e o que a maioria das instalacoes precisa de qualquer forma.
"""

from __future__ import annotations

import os
import shutil
import subprocess
from collections.abc import Iterable, Iterator

from . import wad
from .wad import Iwad

# Caminhos que qualquer instalacao Linux de Doom costuma usar. O XDG_DATA_HOME
# entra primeiro porque e onde o usuario poe o que e dele.
def default_folders(game_dir: str) -> Iterator[str]:
    yield game_dir
    yield os.path.join(game_dir, "iwads")

    data_home = os.environ.get("XDG_DATA_HOME") or os.path.expanduser("~/.local/share")
    for sub in ("doom", "games/doom", "iwads"):
        yield os.path.join(data_home, sub)

    yield os.path.expanduser("~/.doom")
    yield "/usr/share/doom"
    yield "/usr/share/games/doom"
    yield "/usr/local/share/doom"
    yield "/usr/local/share/games/doom"


def scan_folders(folders: Iterable[str]) -> Iterator[Iwad]:
    for folder in folders:
        try:
            entries = os.scandir(folder)
        except OSError:
            continue

        with entries:
            for entry in entries:
                if not entry.is_file():
                    continue
                found = wad.describe(entry.path)
                if found is not None:
                    yield found


def has_plocate() -> bool:
    return shutil.which("locate") is not None or shutil.which("plocate") is not None


def locate_wads(timeout: float = 8.0) -> Iterator[Iwad]:
    """IWADs conhecidos que o banco do plocate ja tenha visto."""
    binary = shutil.which("plocate") or shutil.which("locate")
    if binary is None:
        return

    try:
        # -i porque WAD, wad e Wad convivem; -0 porque nome de arquivo aceita
        # tudo menos NUL, inclusive quebra de linha.
        result = subprocess.run(
            [binary, "-i", "-0", "--", "*.wad"],
            capture_output=True, timeout=timeout, check=False,
        )
    except (OSError, subprocess.TimeoutExpired):
        return

    for raw in result.stdout.split(b"\0"):
        if not raw:
            continue
        path = os.fsdecode(raw)
        if os.path.basename(path).lower() in wad.KNOWN_IWADS:
            found = wad.describe(path)
            if found is not None:
                yield found


def build(game_dir: str, extra: Iterable[str] = (), use_locate: bool = True) -> list[Iwad]:
    """Lista final: um jogo por linha, na ordem de lancamento."""
    by_game: dict[str, Iwad] = {}

    sources: list[Iterator[Iwad]] = [scan_folders([*default_folders(game_dir), *extra])]
    if use_locate:
        sources.append(locate_wads())

    for source in sources:
        for found in source:
            # A primeira fonte vence: a lista e de JOGOS, nao de arquivos, e ver
            # "DOOM II" tres vezes nao ajuda ninguem.
            by_game.setdefault(found.label, found)

    return sorted(by_game.values(), key=lambda i: (wad.release_index(i.label), i.label.lower()))
