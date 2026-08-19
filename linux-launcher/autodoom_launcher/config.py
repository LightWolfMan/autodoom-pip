"""Configuracao: a da engine (que e dela) e a do launcher (que e nossa).

A engine guarda um perfil por jogo em `<game_dir>/user/<jogo>/`. O launcher nao
mistura o que e dele com o que e da engine: as preferencias da janela vao para
`$XDG_CONFIG_HOME/autodoom-launcher/settings.json`, como manda a XDG.

Uma regra vale para tudo que se escreve na config da engine: escrever **antes**
de subir o jogo. Ela le no inicio e reescreve o arquivo ao sair, entao mexer com
o jogo aberto e trabalho perdido.
"""

from __future__ import annotations

import json
import os
import re
from collections.abc import Iterator

APP = "autodoom-launcher"

# O teclado do projeto, versionado na raiz do repositorio. Procurado aqui e nos
# caminhos de instalacao usuais.
KEYS_FILE = "autodoom-modern.csc"


def config_dir() -> str:
    base = os.environ.get("XDG_CONFIG_HOME") or os.path.expanduser("~/.config")
    return os.path.join(base, APP)


def settings_path() -> str:
    return os.path.join(config_dir(), "settings.json")


def load_settings() -> dict:
    try:
        with open(settings_path(), encoding="utf-8") as f:
            return json.load(f)
    except (OSError, json.JSONDecodeError):
        return {}


def save_settings(data: dict) -> None:
    try:
        os.makedirs(config_dir(), exist_ok=True)
        with open(settings_path(), "w", encoding="utf-8") as f:
            json.dump(data, f, indent=2)
    except OSError:
        pass  # falha ao salvar nao pode impedir o jogo de subir


def profiles(game_dir: str) -> Iterator[str]:
    root = os.path.join(game_dir, "user")
    try:
        with os.scandir(root) as entries:
            for entry in entries:
                if entry.is_dir():
                    yield entry.path
    except OSError:
        return


def keys_source(repo_root: str | None = None) -> str | None:
    """Onde esta o keys do projeto: no repositorio ou instalado no sistema."""
    candidates = []
    if repo_root:
        candidates.append(os.path.join(repo_root, "keys", KEYS_FILE))
    candidates += [
        os.path.join(os.path.dirname(__file__), "..", "..", "keys", KEYS_FILE),
        f"/usr/share/{APP}/{KEYS_FILE}",
        f"/usr/local/share/{APP}/{KEYS_FILE}",
    ]
    for path in candidates:
        if os.path.isfile(path):
            return os.path.abspath(path)
    return None


def ensure_project_keys(game_dir: str, repo_root: str | None = None) -> list[str]:
    """
    Escreve o teclado do projeto em perfil que ainda nao tem `keys.csc`.

    Nunca sobrescreve: perfil existente tem as teclas do dono. Instalacao nova
    precisa disso, porque a engine comeca de um perfil zerado e sobe com o
    teclado de 1993 -- setas para andar, sem WASD.
    """
    source = keys_source(repo_root)
    if source is None:
        return []

    with open(source, encoding="utf-8") as f:
        layout = f.read()

    written = []
    for profile in profiles(game_dir):
        target = os.path.join(profile, "keys.csc")
        if os.path.exists(target):
            continue
        try:
            with open(target, "w", encoding="utf-8") as f:
                f.write(layout)
            written.append(target)
        except OSError:
            continue

    return written


def read_cvar(cfg: str, key: str) -> str | None:
    try:
        with open(cfg, encoding="utf-8", errors="replace") as f:
            for line in f:
                match = re.match(rf"^{re.escape(key)}\s+(\S+)\s*$", line)
                if match:
                    return match.group(1)
    except OSError:
        pass
    return None


def write_cvar(game_dir: str, key: str, value: str) -> None:
    for profile in profiles(game_dir):
        cfg = os.path.join(profile, "eternity.cfg")
        if not os.path.isfile(cfg):
            continue
        try:
            with open(cfg, encoding="utf-8", errors="replace") as f:
                lines = f.readlines()

            found = False
            for i, line in enumerate(lines):
                if re.match(rf"^{re.escape(key)}\s", line):
                    lines[i] = f"{key:<29} {value}\n"
                    found = True
            if not found:
                lines.append(f"{key:<29} {value}\n")

            with open(cfg, "w", encoding="utf-8") as f:
                f.writelines(lines)
        except OSError:
            continue
