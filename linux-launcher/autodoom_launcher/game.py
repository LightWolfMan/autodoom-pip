"""Montar a linha de comando e subir o jogo.

O mapeamento e o mesmo do launcher Windows, e a razao dele vale aqui igual:
`-bots N` diz quantos companheiros entram, e `-copilot 0|1` diz se o bot dirige
o SEU personagem. Sao coisas separadas desde que a engine ganhou o parametro --
com `-bots` sozinho, qualquer numero de 1 a 3 desligava o copiloto.
"""

from __future__ import annotations

import os
import shutil
import subprocess
from dataclasses import dataclass, field

# O binario com o patch. Sem ele o `-copilot` e o `-pip` sao ignorados em
# silencio, que e a pior forma de falhar: a opcao aparece marcada e nao faz nada.
PATCHED_BINARIES = ("autodoom-pip", "eternity")


@dataclass
class Options:
    iwad: str
    copilot: bool = True
    companions: int = 0
    pip: bool = False
    weapons_stay_off: bool = True
    pwad: str | None = None
    warp: str | None = None
    extra: list[str] = field(default_factory=list)


def find_engine(game_dir: str) -> str | None:
    """Primeiro o binario ao lado do jogo, depois o que estiver no PATH."""
    for name in PATCHED_BINARIES:
        candidate = os.path.join(game_dir, name)
        if os.path.isfile(candidate) and os.access(candidate, os.X_OK):
            return candidate

    for name in PATCHED_BINARIES:
        found = shutil.which(name)
        if found:
            return found

    return None


def build_command(engine: str, opts: Options) -> list[str]:
    argv = [engine, "-iwad", opts.iwad]

    if opts.companions > 0:
        argv += ["-bots", str(opts.companions)]

    # Sempre explicito: o padrao da engine sem o parametro depende do -bots, e
    # depender disso e o que tornava "1 companheiro com copiloto" impossivel.
    argv += ["-copilot", "1" if opts.copilot else "0"]

    if opts.pip:
        argv.append("-pip")

    # dmflags 0 tira o DM_WEAPONSTAY que o coop liga por padrao, fazendo a arma
    # sumir ao ser pega, como no single player.
    if opts.weapons_stay_off:
        argv += ["-dmflags", "0"]

    if opts.pwad:
        argv += ["-file", opts.pwad]

    if opts.warp:
        argv += ["-warp", *opts.warp.split()]

    return argv + opts.extra


def launch(engine: str, opts: Options, game_dir: str) -> subprocess.Popen[bytes]:
    return subprocess.Popen(build_command(engine, opts), cwd=game_dir)
