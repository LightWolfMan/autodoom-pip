"""Textos da interface, em portugues e ingles.

O idioma vem do locale do ambiente, sem opcao para forcar: uma configuracao a
menos para errar, e o launcher fala a lingua da maquina onde abriu. Sem gettext
de proposito -- sao poucas dezenas de frases, e um `.mo` por idioma sujaria o
pacote para nao ganhar nada.

Para conferir o outro idioma sem trocar a sessao inteira:

    LC_ALL=en_US.UTF-8 ./autodoom-launcher
"""

from __future__ import annotations

import os

_ENV_KEYS = ("LC_ALL", "LC_MESSAGES", "LANG", "LANGUAGE")


def _is_portuguese() -> bool:
    for key in _ENV_KEYS:
        value = os.environ.get(key, "")
        if value:
            # LANGUAGE pode trazer uma lista, "pt_BR:pt:en"; vale a primeira.
            return value.split(":")[0].lower().startswith("pt")
    return False


PORTUGUESE = _is_portuguese()


def pick(pt: str, en: str) -> str:
    return pt if PORTUGUESE else en


# ------------------------------------------------------------------ secoes

SECTION_MODE = pick("Modo", "Mode")
SECTION_EXTRAS = pick("Extras", "Extras")
SECTION_IWAD = "IWAD"
SECTION_PWAD = pick("PWAD de mapa (opcional)", "Map PWAD (optional)")

# -------------------------------------------------------------------- modo

COPILOT = pick("Copiloto", "Copilot")
COPILOT_HINT = pick(
    "O bot joga o seu personagem e devolve o controle quando você mexe",
    "The bot plays your character and hands control back when you move")

COMPANIONS = pick("Bots companheiros:", "Bot companions:")
BOTS_WORD = pick("bots", "bots")

COPILOT_ON = pick(
    "O bot dirige o seu personagem e solta o controle a cada toque seu nas teclas.",
    "The bot drives your character and lets go each time you touch the keys.")
COPILOT_OFF = pick(
    "Sem copiloto: você joga o seu personagem do início ao fim.",
    "No copilot: you play your own character from start to finish.")
NO_COMPANIONS = pick(
    "Sem companheiros: só você no mapa.",
    "No companions: your character alone on the map.")


def companions_hint(count: int) -> str:
    return pick(
        f"{count} companheiro(s) de bot, nos slots 2 a {count + 1}. Máximo de 3.",
        f"{count} bot companion(s), in slots 2 to {count + 1}. 3 at most.")


# ------------------------------------------------------------------ extras

PIP = pick("Ver a tela dos bots num quadrinho",
           "Show the bots' view in a corner box")
PIP_HINT = pick("Precisa do binário com o patch.",
                "Needs the patched binary.")

WEAPONS = pick("Armas somem ao pegar, como no single player",
               "Weapons disappear when picked up, like single player")
WEAPONS_HINT = pick("No coop o padrão é a arma ficar no chão para todo mundo pegar.",
                    "In co-op the default leaves weapons on the floor for everyone.")

CAMERA = pick("A câmera segue:", "The camera follows:")
FOLLOW_KILLS = pick("quem mais mata", "whoever kills the most")
FOLLOW_EXIT = pick("quem está mais perto da saída", "whoever is closest to the exit")

JUMP = pick("Liberar o pulo", "Enable jumping")
JUMP_HINT = pick("A engine vem com o pulo desativado.",
                 "The engine ships with jumping disabled.")

SCORES = pick("Placar de abates ao segurar F", "Kill scoreboard while holding F")
SCORES_HINT = pick("Mostra o ranking de kills enquanto a tecla estiver pressionada.",
                   "Shows the kill ranking while the key is held down.")

# -------------------------------------------------------------- iwad e pwad

OPEN = pick("Abrir...", "Open...")
DETECT = pick("Detectar IWADs...", "Detect IWADs...")
NONE_PWAD = pick("(nenhum)", "(none)")

DETAIL_FILE = pick("Arquivo:", "File:")
DETAIL_KIND = pick("Tipo:", "Type:")
DETAIL_SIZE = pick("Tamanho:", "Size:")

CHOOSE_IWAD = pick("Escolher IWAD", "Choose an IWAD")
CHOOSE_PWAD = pick("Escolher PWAD", "Choose a PWAD")
FILE_FILTER = pick("WAD e PK3", "WAD and PK3")

# ------------------------------------------------------------------ rodape

QUIT = pick("Sair", "Quit")
PLAY = pick("Jogar", "Play")

# ------------------------------------------------------------------ avisos

NO_IWAD_FOUND = pick("Nenhum IWAD encontrado. Use 'Detectar IWADs...' ou 'Abrir...'.",
                     "No IWAD found. Use 'Detect IWADs...' or 'Open...'.")
PICK_IWAD_FIRST = pick("Escolha um IWAD primeiro", "Pick an IWAD first")
NO_PLOCATE = pick(
    "plocate não instalado: apt install plocate (Debian) ou pacman -S plocate (Arch)",
    "plocate is not installed: apt install plocate (Debian) or pacman -S plocate (Arch)")
SEARCHING = pick("Procurando...", "Searching...")


def found_iwads(count: int) -> str:
    return pick(f"{count} IWAD(s) encontrados", f"{count} IWAD(s) found")


def not_an_iwad(name: str) -> str:
    return pick(f"{name} não é um IWAD conhecido", f"{name} is not a known IWAD")


def engine_missing(game_dir: str) -> str:
    return pick(f"Binário da engine não encontrado em {game_dir} nem no PATH",
                f"Engine binary not found in {game_dir} or on PATH")
