"""A janela, em GTK4 com libadwaita.

O desenho segue o mockup do projeto: secoes com titulo e icone (MODO, EXTRAS,
IWAD, PWAD), cartoes de opcao com o icone a esquerda do texto, lista de IWAD com
ficha ao lado, e um rodape com Sair e Jogar.

Uma diferenca proposital em relacao ao mockup: la o modo era um par de opcoes
exclusivas, Copiloto **ou** Coop. Aqui o copiloto e um marcador e os
companheiros sao uma contagem de 0 a 3, porque as duas coisas passaram a ser
independentes na engine -- da para jogar com um companheiro e o copiloto ligado
ao mesmo tempo, combinacao que o par exclusivo nao sabia expressar.

Falta ainda para sair de RC: validacao de compatibilidade do PWAD e o logo do
jogo lido de dentro do proprio WAD.
"""

from __future__ import annotations

import os
import threading

import gi

gi.require_version("Gtk", "4.0")
gi.require_version("Adw", "1")

from gi.repository import Adw, Gdk, Gio, GLib, Gtk  # noqa: E402

from . import catalog, config, game, icons, strings as t  # noqa: E402
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
            self._copilot, "bot", t.COPILOT, t.COPILOT_HINT))

        # Um unico Gtk.Adjustment para o slider e o spinner. Antes eram dois
        # valores espelhados na mao, com uma trava para nao se chamarem em
        # cascata; compartilhando o adjustment nao ha o que sincronizar -- os
        # dois controles sao vistas do mesmo numero, e a trava sumiu junto.
        self._bots = Gtk.Adjustment(lower=0, upper=3, step_increment=1, page_increment=1,
                                    value=self._settings.get("companions", 1))
        self._bots.connect("value-changed", self._on_bots_changed)

        self._companions = Gtk.Scale(orientation=Gtk.Orientation.HORIZONTAL,
                                     adjustment=self._bots, draw_value=False, hexpand=True)
        # Sem isto o arrasto para no meio do caminho: o Gtk.Scale trabalha em
        # ponto flutuante e a alca ficaria entre duas marcas, mostrando 1 no
        # spinner enquanto aponta para 1,4. Com round_digits 0 ela so descansa
        # em numero inteiro, que e o unico valor que a engine aceita.
        self._companions.set_round_digits(0)
        self._companions.set_digits(0)
        # Rolar o mouse e clicar numa marca passam a valer um passo cheio.
        self._companions.set_increments(1, 1)
        for i in range(4):
            self._companions.add_mark(i, Gtk.PositionType.BOTTOM, str(i))

        self._spin = Gtk.SpinButton(adjustment=self._bots, climb_rate=1, digits=0,
                                    numeric=True, snap_to_ticks=True,
                                    valign=Gtk.Align.CENTER)

        slider_row = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=12)
        slider_row.append(self._companions)
        slider_row.append(self._spin)
        slider_row.append(Gtk.Label(label=t.BOTS_WORD, valign=Gtk.Align.CENTER))

        rows.append(self._option_card(None, "people", t.COMPANIONS, None, slider_row))
        grid.append(rows)
        grid.append(info)

        return self._section(t.SECTION_MODE, "gamepad", grid)

    def _on_bots_changed(self, adjustment: Gtk.Adjustment) -> None:
        # O round_digits arredonda o que o usuario arrasta, mas nao o que o
        # codigo escreve: o valor restaurado das preferencias, por exemplo,
        # poderia entrar quebrado e deixar a alca entre duas marcas com o
        # spinner mostrando outro numero. Arredondar aqui fecha os dois casos.
        # Nao ha recursao: na segunda passada o valor ja e inteiro e o GTK so
        # emite o sinal quando ele muda de verdade.
        value = adjustment.get_value()
        rounded = round(value)
        if value != rounded:
            adjustment.set_value(rounded)
            return

        self._update_hint()

    def _companion_count(self) -> int:
        """O valor que vai para a linha de comando: inteiro, sempre."""
        return int(round(self._bots.get_value()))

    def _update_hint(self) -> None:
        count = self._companion_count()
        first = t.COPILOT_ON if self._copilot.get_active() else t.COPILOT_OFF
        second = t.NO_COMPANIONS if count == 0 else t.companions_hint(count)
        self._hint.set_text(f"{first}\n{second}")

    # ------------------------------------------------------------ extras

    def _build_extras(self) -> Gtk.Widget:
        grid = Gtk.Grid(column_spacing=12, row_spacing=12, column_homogeneous=True)

        self._pip = Gtk.CheckButton(active=self._settings.get("pip", True))
        grid.attach(self._option_card(
            self._pip, "pip", t.PIP, t.PIP_HINT), 0, 0, 1, 1)

        self._weapons = Gtk.CheckButton(active=self._settings.get("weapons", True))
        grid.attach(self._option_card(
            self._weapons, "target", t.WEAPONS, t.WEAPONS_HINT), 1, 0, 1, 1)

        self._follow = Gtk.DropDown.new_from_strings([t.FOLLOW_KILLS, t.FOLLOW_EXIT])
        self._follow.set_selected(self._settings.get("follow", 0))
        grid.attach(self._option_card(None, "camera", t.CAMERA, None, self._follow),
                    0, 1, 1, 1)

        self._jump = Gtk.CheckButton(active=self._settings.get("jump", True))
        grid.attach(self._option_card(
            self._jump, "jump", t.JUMP, t.JUMP_HINT), 1, 1, 1, 1)

        self._scores = Gtk.CheckButton(active=self._settings.get("scores", True))
        grid.attach(self._option_card(
            self._scores, "shield", t.SCORES, t.SCORES_HINT), 0, 2, 1, 1)

        return self._section(t.SECTION_EXTRAS, "settings", grid)

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
        buttons.append(self._icon_button(t.OPEN, "folder", self._on_open_iwad))
        buttons.append(self._icon_button(t.DETECT, "scan", self._on_scan))
        row.append(buttons)

        self._details = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=6,
                                valign=Gtk.Align.CENTER)
        self._details.set_size_request(250, -1)
        row.append(self._details)

        return self._section(t.SECTION_IWAD, "document", row)

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

        size_text = f"{size:.2f} MB"
        if t.PORTUGUESE:
            size_text = size_text.replace(".", ",")

        for key, value in ((t.DETAIL_FILE, os.path.basename(iwad.path)),
                           (t.DETAIL_KIND, "IWAD"),
                           (t.DETAIL_SIZE, size_text)):
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
            self._status.set_text(t.NO_IWAD_FOUND)

    # -------------------------------------------------------------- pwad

    def _build_pwad(self) -> Gtk.Widget:
        row = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=12)
        self._pwad = Gtk.DropDown.new_from_strings([t.NONE_PWAD])
        self._pwad.set_hexpand(True)
        row.append(self._pwad)
        row.append(self._icon_button(t.OPEN, "folder", self._on_open_pwad))
        return self._section(t.SECTION_PWAD, "puzzle", row)

    # ------------------------------------------------------------ rodape

    def _build_footer(self) -> Gtk.Widget:
        bar = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=12,
                      halign=Gtk.Align.CENTER, margin_top=10)
        bar.append(self._icon_button(t.QUIT, "exit", lambda _b: self.close(), width=180))

        content = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=8,
                          halign=Gtk.Align.CENTER)
        content.append(icons.image("play", 16, (255, 255, 255)))
        content.append(Gtk.Label(label=t.PLAY))
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
            self._status.set_text(t.NO_PLOCATE)
            return

        self._status.set_text(t.SEARCHING)

        def worker() -> None:
            found = catalog.build(self._game_dir, use_locate=True)
            # O GTK so aceita mexer na interface pela thread principal; voltar
            # pelo idle_add e a regra, nao enfeite.
            GLib.idle_add(self._finish_scan, found)

        threading.Thread(target=worker, daemon=True).start()

    def _finish_scan(self, found: list) -> bool:
        self._fill_list(found)
        self._status.set_text(t.found_iwads(len(found)))
        return False

    def _on_open_iwad(self, _button: Gtk.Button) -> None:
        self._open_file(t.CHOOSE_IWAD, self._add_iwad)

    def _on_open_pwad(self, _button: Gtk.Button) -> None:
        self._open_file(t.CHOOSE_PWAD, self._add_pwad)

    def _open_file(self, title: str, done) -> None:
        chooser = Gtk.FileDialog(title=title)
        filters = Gio.ListStore.new(Gtk.FileFilter)
        wads = Gtk.FileFilter(name=t.FILE_FILTER)
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
            self._status.set_text(t.not_an_iwad(os.path.basename(path)))
            return
        self._fill_list([found, *[i for i in self._iwads if i.path != found.path]])

    def _add_pwad(self, path: str) -> None:
        self._pwad_path = path
        model = Gtk.StringList()
        model.append(os.path.basename(path))
        model.append(t.NONE_PWAD)
        self._pwad.set_model(model)
        self._pwad.set_selected(0)

    def _on_play(self, _button: Gtk.Button) -> None:
        iwad = self._selected_iwad()
        if iwad is None:
            self._status.set_text(t.PICK_IWAD_FIRST)
            return

        engine = game.find_engine(self._game_dir)
        if engine is None:
            self._status.set_text(t.engine_missing(self._game_dir))
            return

        opts = Options(
            iwad=iwad.path,
            copilot=self._copilot.get_active(),
            companions=self._companion_count(),
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
