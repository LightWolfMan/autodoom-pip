"""A janela, em GTK4 com libadwaita.

O desenho segue o mockup do projeto: secoes com titulo e icone (MODO, EXTRAS,
IWAD, PWAD), cartoes de opcao com o icone a esquerda do texto, lista de IWAD com
ficha ao lado, e um rodape com Sair e Jogar.

Uma diferenca proposital em relacao ao mockup: la o modo era um par de opcoes
exclusivas, Copiloto **ou** Coop. Aqui o copiloto e um marcador e os
companheiros sao uma contagem de 0 a 3, porque as duas coisas passaram a ser
independentes na engine -- da para jogar com um companheiro e o copiloto ligado
ao mesmo tempo, combinacao que o par exclusivo nao sabia expressar.

Falta ainda: validacao de compatibilidade do PWAD, o logo do jogo lido de dentro
do proprio WAD e os textos em ingles.
"""

from __future__ import annotations

import os
import threading

import gi

gi.require_version("Gtk", "4.0")
gi.require_version("Adw", "1")

from gi.repository import Adw, Gdk, Gio, GLib, Gtk  # noqa: E402

from . import catalog, config, game, icons  # noqa: E402
from .game import Options  # noqa: E402
from .wad import describe  # noqa: E402

APP_ID = "net.autodoom.launcher"

CSS = b"""
.section-title {
   font-size: 0.82rem;
   font-weight: 700;
   letter-spacing: 0.08em;
}
.card-surface {
   background-color: @card_bg_color;
   border: 1px solid alpha(@borders, 0.8);
   border-radius: 12px;
}
.option-card {
   background-color: @card_bg_color;
   border: 1px solid alpha(@borders, 0.8);
   border-radius: 10px;
   padding: 10px 12px;
}
.info-card {
   background-color: alpha(@accent_bg_color, 0.10);
   border: 1px solid alpha(@accent_bg_color, 0.30);
   border-radius: 10px;
   padding: 10px 14px;
}
.hint { font-size: 0.86rem; }
"""


def accent_rgb(dark: bool) -> tuple[int, int, int]:
    """Azul que sobrevive nos dois temas: um so tom sumiria em um deles."""
    return (118, 178, 255) if dark else (28, 113, 216)


def ink_rgb(dark: bool) -> tuple[int, int, int]:
    return (230, 230, 230) if dark else (60, 60, 60)


class LauncherWindow(Adw.ApplicationWindow):
    def __init__(self, app: Adw.Application, game_dir: str):
        super().__init__(application=app, title="AutoDoom Launcher",
                         default_width=1000, default_height=940)

        self._game_dir = game_dir
        self._settings = config.load_settings()
        self._iwads: list = []
        self._pwad_path: str | None = None

        dark = Adw.StyleManager.get_default().get_dark()
        self._accent = accent_rgb(dark)
        self._ink = ink_rgb(dark)

        provider = Gtk.CssProvider()
        provider.load_from_data(CSS)
        Gtk.StyleContext.add_provider_for_display(
            Gdk.Display.get_default(), provider, Gtk.STYLE_PROVIDER_PRIORITY_APPLICATION)

        view = Adw.ToolbarView()
        view.add_top_bar(Adw.HeaderBar())
        self.set_content(view)

        body = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=14,
                       margin_top=12, margin_bottom=8, margin_start=18, margin_end=18)
        body.append(self._build_mode())
        body.append(self._build_extras())
        body.append(self._build_iwad())
        body.append(self._build_pwad())

        scroller = Gtk.ScrolledWindow(hscrollbar_policy=Gtk.PolicyType.NEVER, vexpand=True)
        scroller.set_child(body)
        view.set_content(scroller)
        view.add_bottom_bar(self._build_footer())

        self._fill_list(catalog.build(self._game_dir, use_locate=False))
        self._update_hint()

    # ------------------------------------------------------------- pecas

    def _section(self, title: str, icon: str, content: Gtk.Widget) -> Gtk.Widget:
        header = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=8, margin_bottom=6)
        header.append(icons.image(icon, 18, self._accent))
        label = Gtk.Label(label=title.upper(), xalign=0, css_classes=["section-title", "accent"])
        header.append(label)

        content.set_margin_top(12)
        content.set_margin_bottom(12)
        content.set_margin_start(12)
        content.set_margin_end(12)

        frame = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, css_classes=["card-surface"])
        frame.append(content)

        box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL)
        box.append(header)
        box.append(frame)
        return box

    def _option_card(self, check: Gtk.CheckButton | None, icon: str, title: str,
                     subtitle: str | None, trailing: Gtk.Widget | None = None) -> Gtk.Widget:
        row = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=12,
                      css_classes=["option-card"], hexpand=True)

        if check is not None:
            check.set_valign(Gtk.Align.CENTER)
            row.append(check)

        row.append(icons.image(icon, 26, self._accent))

        if trailing is not None:
            caption = Gtk.Label(label=title, xalign=0, valign=Gtk.Align.CENTER,
                                css_classes=["heading"])
            row.append(caption)
            trailing.set_hexpand(True)
            trailing.set_valign(Gtk.Align.CENTER)
            row.append(trailing)
            return row

        text = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=2,
                       valign=Gtk.Align.CENTER, hexpand=True)
        text.append(Gtk.Label(label=title, xalign=0, css_classes=["heading"]))
        if subtitle:
            text.append(Gtk.Label(label=subtitle, xalign=0, wrap=True,
                                  css_classes=["dim-label", "hint"]))
        row.append(text)
        return row

    def _icon_button(self, label: str, icon: str, handler, width: int = 200) -> Gtk.Button:
        content = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=8,
                          halign=Gtk.Align.CENTER)
        content.append(icons.image(icon, 16, self._ink))
        content.append(Gtk.Label(label=label))
        button = Gtk.Button(child=content)
        button.set_size_request(width, 38)
        button.connect("clicked", handler)
        return button

    # -------------------------------------------------------------- modo

    def _build_mode(self) -> Gtk.Widget:
        grid = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=16)
        rows = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=10, hexpand=True)

        # A caixa de dica nasce antes de tudo: ajustar o slider abaixo ja dispara
        # o handler que escreve nela, e criar depois daria AttributeError.
        info = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=10,
                       css_classes=["info-card"], valign=Gtk.Align.CENTER)
        info.set_size_request(310, -1)
        info.append(icons.image("info", 20, self._accent))
        self._hint = Gtk.Label(xalign=0, wrap=True, hexpand=True, css_classes=["hint"])
        info.append(self._hint)

        self._copilot = Gtk.CheckButton(active=self._settings.get("copilot", True))
        self._copilot.connect("toggled", lambda _b: self._update_hint())
        rows.append(self._option_card(
            self._copilot, "bot", "Copiloto",
            "O bot joga o seu personagem e devolve o controle quando você mexe"))

        self._companions = Gtk.Scale.new_with_range(Gtk.Orientation.HORIZONTAL, 0, 3, 1)
        self._companions.set_draw_value(False)
        self._companions.set_hexpand(True)
        for i in range(4):
            self._companions.add_mark(i, Gtk.PositionType.BOTTOM, str(i))

        self._spin = Gtk.SpinButton.new_with_range(0, 3, 1)
        self._spin.set_valign(Gtk.Align.CENTER)

        # Uma fonte de verdade: o slider. O spinner espelha, e a trava impede
        # que um chame o outro em cascata.
        self._syncing = False
        self._companions.connect("value-changed", self._on_slider)
        self._spin.connect("value-changed", self._on_spin)
        self._companions.set_value(self._settings.get("companions", 1))
        self._spin.set_value(self._companions.get_value())

        slider_row = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=12)
        slider_row.append(self._companions)
        slider_row.append(self._spin)
        slider_row.append(Gtk.Label(label="bots", valign=Gtk.Align.CENTER))

        rows.append(self._option_card(None, "people", "Bots companheiros:", None, slider_row))
        grid.append(rows)
        grid.append(info)

        return self._section("Modo", "gamepad", grid)

    def _on_slider(self, scale: Gtk.Scale) -> None:
        if self._syncing:
            return
        self._syncing = True
        self._spin.set_value(round(scale.get_value()))
        self._syncing = False
        self._update_hint()

    def _on_spin(self, spin: Gtk.SpinButton) -> None:
        if self._syncing:
            return
        self._syncing = True
        self._companions.set_value(spin.get_value())
        self._syncing = False
        self._update_hint()

    def _update_hint(self) -> None:
        count = int(self._spin.get_value())
        first = ("O bot dirige o seu personagem e solta o controle a cada toque seu nas teclas."
                 if self._copilot.get_active()
                 else "Sem copiloto: você joga o seu personagem do início ao fim.")
        second = ("Sem companheiros: só você no mapa." if count == 0
                  else f"{count} companheiro(s) de bot, nos slots 2 a {count + 1}. Máximo de 3.")
        self._hint.set_text(f"{first}\n{second}")

    # ------------------------------------------------------------ extras

    def _build_extras(self) -> Gtk.Widget:
        grid = Gtk.Grid(column_spacing=12, row_spacing=12, column_homogeneous=True)

        self._pip = Gtk.CheckButton(active=self._settings.get("pip", True))
        grid.attach(self._option_card(
            self._pip, "pip", "Ver a tela dos bots num quadrinho",
            "Precisa do binário com o patch."), 0, 0, 1, 1)

        self._weapons = Gtk.CheckButton(active=self._settings.get("weapons", True))
        grid.attach(self._option_card(
            self._weapons, "target", "Armas somem ao pegar, como no single player",
            "No coop o padrão é a arma ficar no chão para todo mundo pegar."), 1, 0, 1, 1)

        self._follow = Gtk.DropDown.new_from_strings(
            ["quem mais mata", "quem está mais perto da saída"])
        self._follow.set_selected(self._settings.get("follow", 0))
        grid.attach(self._option_card(None, "camera", "A câmera segue:", None, self._follow),
                    0, 1, 1, 1)

        self._jump = Gtk.CheckButton(active=self._settings.get("jump", True))
        grid.attach(self._option_card(
            self._jump, "jump", "Liberar o pulo",
            "A engine vem com o pulo desativado."), 1, 1, 1, 1)

        self._scores = Gtk.CheckButton(active=self._settings.get("scores", True))
        grid.attach(self._option_card(
            self._scores, "shield", "Placar de abates ao segurar F",
            "Mostra o ranking de kills enquanto a tecla estiver pressionada."), 0, 2, 1, 1)

        return self._section("Extras", "settings", grid)

    # -------------------------------------------------------------- iwad

    def _build_iwad(self) -> Gtk.Widget:
        row = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=14)

        self._list = Gtk.ListBox(css_classes=["boxed-list"])
        self._list.connect("row-selected", lambda _b, _r: self._update_details())
        scroller = Gtk.ScrolledWindow(hexpand=True, min_content_height=150,
                                      hscrollbar_policy=Gtk.PolicyType.NEVER)
        scroller.set_child(self._list)
        row.append(scroller)

        buttons = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=10,
                          valign=Gtk.Align.CENTER)
        buttons.append(self._icon_button("Abrir...", "folder", self._on_open_iwad))
        buttons.append(self._icon_button("Detectar IWADs...", "scan", self._on_scan))
        row.append(buttons)

        self._details = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=6,
                                valign=Gtk.Align.CENTER)
        self._details.set_size_request(250, -1)
        row.append(self._details)

        return self._section("IWAD", "document", row)

    def _update_details(self) -> None:
        while (child := self._details.get_first_child()) is not None:
            self._details.remove(child)

        iwad = self._selected_iwad()
        if iwad is None:
            return

        self._details.append(Gtk.Label(label=iwad.label, xalign=0, wrap=True,
                                       css_classes=["title-4", "accent"]))
        try:
            size = os.path.getsize(iwad.path) / (1024 * 1024)
        except OSError:
            size = 0.0

        for key, value in (("Arquivo:", os.path.basename(iwad.path)),
                           ("Tipo:", "IWAD"),
                           ("Tamanho:", f"{size:.2f} MB".replace(".", ","))):
            line = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=8)
            name = Gtk.Label(label=key, xalign=0, css_classes=["heading"])
            name.set_size_request(90, -1)
            line.append(name)
            line.append(Gtk.Label(label=value, xalign=0, wrap=True, css_classes=["dim-label"]))
            self._details.append(line)

    def _selected_iwad(self):
        row = self._list.get_selected_row()
        if row is None:
            return None
        index = row.get_index()
        return self._iwads[index] if 0 <= index < len(self._iwads) else None

    def _fill_list(self, found: list) -> None:
        self._iwads = found
        while (child := self._list.get_first_child()) is not None:
            self._list.remove(child)

        for iwad in found:
            row = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=10,
                          margin_top=8, margin_bottom=8, margin_start=10, margin_end=10)
            row.append(icons.image("wad", 22, self._accent))
            row.append(Gtk.Label(label=iwad.label, xalign=0))
            self._list.append(row)

        last = self._settings.get("iwad")
        chosen = next((i for i, w in enumerate(found) if w.path == last), 0 if found else None)
        if chosen is not None:
            self._list.select_row(self._list.get_row_at_index(chosen))

        if not found and hasattr(self, "_status"):
            self._status.set_text("Nenhum IWAD encontrado. Use 'Detectar IWADs...' ou 'Abrir...'.")

    # -------------------------------------------------------------- pwad

    def _build_pwad(self) -> Gtk.Widget:
        row = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=12)
        self._pwad = Gtk.DropDown.new_from_strings(["(nenhum)"])
        self._pwad.set_hexpand(True)
        row.append(self._pwad)
        row.append(self._icon_button("Abrir...", "folder", self._on_open_pwad))
        return self._section("PWAD de mapa (opcional)", "puzzle", row)

    # ------------------------------------------------------------ rodape

    def _build_footer(self) -> Gtk.Widget:
        bar = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=12,
                      halign=Gtk.Align.CENTER, margin_top=10)
        bar.append(self._icon_button("Sair", "exit", lambda _b: self.close(), width=180))

        content = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=8,
                          halign=Gtk.Align.CENTER)
        content.append(icons.image("play", 16, (255, 255, 255)))
        content.append(Gtk.Label(label="Jogar"))
        play = Gtk.Button(child=content, css_classes=["suggested-action"])
        play.set_size_request(220, 42)
        play.connect("clicked", self._on_play)
        bar.append(play)

        self._status = Gtk.Label(css_classes=["dim-label", "hint"],
                                 margin_top=6, margin_bottom=10)

        wrapper = Gtk.Box(orientation=Gtk.Orientation.VERTICAL)
        wrapper.append(bar)
        wrapper.append(self._status)
        return wrapper

    # ------------------------------------------------------------ acoes

    def _on_scan(self, _button: Gtk.Button) -> None:
        if not catalog.has_plocate():
            self._status.set_text(
                "plocate não instalado: apt install plocate (Debian) ou pacman -S plocate (Arch)")
            return

        self._status.set_text("Procurando...")

        def worker() -> None:
            found = catalog.build(self._game_dir, use_locate=True)
            # O GTK so aceita mexer na interface pela thread principal; voltar
            # pelo idle_add e a regra, nao enfeite.
            GLib.idle_add(self._finish_scan, found)

        threading.Thread(target=worker, daemon=True).start()

    def _finish_scan(self, found: list) -> bool:
        self._fill_list(found)
        self._status.set_text(f"{len(found)} IWAD(s) encontrados")
        return False

    def _on_open_iwad(self, _button: Gtk.Button) -> None:
        self._open_file("Escolher IWAD", self._add_iwad)

    def _on_open_pwad(self, _button: Gtk.Button) -> None:
        self._open_file("Escolher PWAD", self._add_pwad)

    def _open_file(self, title: str, done) -> None:
        chooser = Gtk.FileDialog(title=title)
        filters = Gio.ListStore.new(Gtk.FileFilter)
        wads = Gtk.FileFilter(name="WAD e PK3")
        for pattern in ("*.wad", "*.WAD", "*.pk3", "*.pke", "*.zip"):
            wads.add_pattern(pattern)
        filters.append(wads)
        chooser.set_filters(filters)

        def answered(dialog, result):
            try:
                file = dialog.open_finish(result)
            except GLib.Error:
                return  # o usuario cancelou
            if file is not None and file.get_path():
                done(file.get_path())

        chooser.open(self, None, answered)

    def _add_iwad(self, path: str) -> None:
        found = describe(path)
        if found is None:
            self._status.set_text(f"{os.path.basename(path)} não é um IWAD conhecido")
            return
        self._fill_list([found, *[i for i in self._iwads if i.path != found.path]])

    def _add_pwad(self, path: str) -> None:
        self._pwad_path = path
        model = Gtk.StringList()
        model.append(os.path.basename(path))
        model.append("(nenhum)")
        self._pwad.set_model(model)
        self._pwad.set_selected(0)

    def _on_play(self, _button: Gtk.Button) -> None:
        iwad = self._selected_iwad()
        if iwad is None:
            self._status.set_text("Escolha um IWAD primeiro")
            return

        engine = game.find_engine(self._game_dir)
        if engine is None:
            self._status.set_text(
                f"Binário da engine não encontrado em {self._game_dir} nem no PATH")
            return

        opts = Options(
            iwad=iwad.path,
            copilot=self._copilot.get_active(),
            companions=int(self._spin.get_value()),
            pip=self._pip.get_active(),
            weapons_stay_off=self._weapons.get_active(),
            pwad=self._pwad_path if self._pwad.get_selected() == 0 else None,
        )

        # Tudo que mora na config da engine se escreve agora, com o jogo fechado:
        # ela le no inicio e reescreve o arquivo ao sair.
        config.ensure_project_keys(self._game_dir)
        config.write_cvar(self._game_dir, "pip_follow", str(self._follow.get_selected()))
        config.write_cvar(self._game_dir, "comp_aircontrol", "0" if self._jump.get_active() else "1")
        config.write_cvar(self._game_dir, "show_scores", "1" if self._scores.get_active() else "0")

        config.save_settings({
            "copilot": opts.copilot,
            "companions": opts.companions,
            "pip": opts.pip,
            "weapons": opts.weapons_stay_off,
            "jump": self._jump.get_active(),
            "scores": self._scores.get_active(),
            "follow": self._follow.get_selected(),
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
