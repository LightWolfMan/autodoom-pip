"""A janela, em GTK4 com libadwaita.

Esqueleto: ja monta a linha de comando certa e sobe o jogo, mas ainda nao tem o
seletor de PWAD, a ficha do IWAD com o logo lido de dentro do WAD, nem a
varredura com barra de progresso que o launcher Windows tem.

A escolha de GTK4/libadwaita esta no README deste diretorio; em uma linha: e o
que o Debian e o Arch ja empacotam, sem runtime extra para o usuario instalar.
"""

from __future__ import annotations

import os
import threading

import gi

gi.require_version("Gtk", "4.0")
gi.require_version("Adw", "1")

from gi.repository import Adw, GLib, Gtk  # noqa: E402

from . import catalog, config, game  # noqa: E402
from .game import Options  # noqa: E402

APP_ID = "net.autodoom.launcher"


class LauncherWindow(Adw.ApplicationWindow):
    def __init__(self, app: Adw.Application, game_dir: str):
        super().__init__(application=app, title="AutoDoom Launcher",
                         default_width=760, default_height=620)

        self._game_dir = game_dir
        self._settings = config.load_settings()
        self._iwads: list = []

        toolbar = Adw.ToolbarView()
        toolbar.add_top_bar(Adw.HeaderBar())
        self.set_content(toolbar)

        page = Adw.PreferencesPage()
        toolbar.set_content(page)

        # ---------------------------------------------------------- modo
        mode = Adw.PreferencesGroup(title="Modo", description="O copiloto e os companheiros sao independentes")
        page.add(mode)

        self._copilot = Adw.SwitchRow(
            title="Copiloto",
            subtitle="O bot joga o seu personagem e devolve o controle quando voce mexe",
            active=self._settings.get("copilot", True),
        )
        mode.add(self._copilot)

        self._companions = Adw.SpinRow.new_with_range(0, 3, 1)
        self._companions.set_title("Bots companheiros")
        self._companions.set_subtitle("Entram como jogadores 2 a 4")
        self._companions.set_value(self._settings.get("companions", 1))
        mode.add(self._companions)

        # --------------------------------------------------------- extras
        extras = Adw.PreferencesGroup(title="Extras")
        page.add(extras)

        self._pip = Adw.SwitchRow(
            title="Ver a tela dos bots num quadrinho",
            subtitle="Precisa do binario com o patch",
            active=self._settings.get("pip", True),
        )
        extras.add(self._pip)

        self._weapons = Adw.SwitchRow(
            title="Armas somem ao pegar",
            subtitle="Como no single player, em vez do padrao de coop",
            active=self._settings.get("weapons", True),
        )
        extras.add(self._weapons)

        # ----------------------------------------------------------- iwad
        self._iwad_group = Adw.PreferencesGroup(title="IWAD")
        page.add(self._iwad_group)

        self._iwad_row = Adw.ComboRow(title="Jogo")
        self._iwad_group.add(self._iwad_row)

        scan = Gtk.Button(label="Procurar no disco", margin_top=8, halign=Gtk.Align.START)
        scan.connect("clicked", self._on_scan)
        self._iwad_group.add(scan)

        # ---------------------------------------------------------- rodape
        play = Gtk.Button(label="Jogar", css_classes=["suggested-action", "pill"],
                          halign=Gtk.Align.CENTER, margin_top=18, margin_bottom=12)
        play.connect("clicked", self._on_play)
        toolbar.add_bottom_bar(play)

        self._status = Gtk.Label(css_classes=["dim-label"], margin_bottom=8)
        toolbar.add_bottom_bar(self._status)

        self._reload_iwads(use_locate=False)

    # ------------------------------------------------------------ acoes

    def _reload_iwads(self, use_locate: bool) -> None:
        self._iwads = catalog.build(self._game_dir, use_locate=use_locate)
        model = Gtk.StringList()
        for iwad in self._iwads:
            model.append(iwad.label)
        self._iwad_row.set_model(model)

        last = self._settings.get("iwad")
        for i, iwad in enumerate(self._iwads):
            if iwad.path == last:
                self._iwad_row.set_selected(i)
                break

        if not self._iwads:
            self._status.set_text("Nenhum IWAD encontrado. Use 'Procurar no disco'.")

    def _on_scan(self, _button: Gtk.Button) -> None:
        if not catalog.has_plocate():
            self._status.set_text(
                "plocate nao instalado: apt install plocate (Debian) ou pacman -S plocate (Arch)")
            return

        self._status.set_text("Procurando...")
        # O locate responde em milissegundos, mas nao e desculpa para travar a
        # interface: o trabalho vai para uma thread e volta pelo idle_add.
        def worker() -> None:
            found = catalog.build(self._game_dir, use_locate=True)
            # idle_add porque o GTK so aceita mexer na interface pela thread
            # principal; devolver o resultado por aqui e a regra, nao enfeite.
            GLib.idle_add(self._finish_scan, found)

        threading.Thread(target=worker, daemon=True).start()

    def _finish_scan(self, found: list) -> bool:
        self._iwads = found
        model = Gtk.StringList()
        for iwad in found:
            model.append(iwad.label)
        self._iwad_row.set_model(model)
        self._status.set_text(f"{len(found)} IWAD(s) encontrados")
        return False

    def _on_play(self, _button: Gtk.Button) -> None:
        index = self._iwad_row.get_selected()
        if index == Gtk.INVALID_LIST_POSITION or index >= len(self._iwads):
            self._status.set_text("Escolha um IWAD primeiro")
            return

        engine = game.find_engine(self._game_dir)
        if engine is None:
            self._status.set_text(
                f"Binario da engine nao encontrado em {self._game_dir} nem no PATH")
            return

        opts = Options(
            iwad=self._iwads[index].path,
            copilot=self._copilot.get_active(),
            companions=int(self._companions.get_value()),
            pip=self._pip.get_active(),
            weapons_stay_off=self._weapons.get_active(),
        )

        # Config antes de subir: a engine le no inicio e reescreve ao sair.
        config.ensure_project_keys(self._game_dir)

        config.save_settings({
            "copilot": opts.copilot,
            "companions": opts.companions,
            "pip": opts.pip,
            "weapons": opts.weapons_stay_off,
            "iwad": opts.iwad,
        })

        game.launch(engine, opts, self._game_dir)
        self.close()


class LauncherApp(Adw.Application):
    def __init__(self, game_dir: str):
        super().__init__(application_id=APP_ID)
        self._game_dir = game_dir

    def do_activate(self) -> None:
        window = self.props.active_window or LauncherWindow(self, self._game_dir)
        window.present()


def default_game_dir() -> str:
    for candidate in (os.getcwd(), os.path.expanduser("~/AutoDoom"),
                      os.path.expanduser("~/.local/share/autodoom")):
        if os.path.isdir(candidate):
            return candidate
    return os.getcwd()
