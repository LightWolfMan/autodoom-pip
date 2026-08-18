<div align="center">

# AutoDoom PIP + Launcher

**Picture-in-picture and a friendly launcher for [AutoDoom](https://github.com/ioan-chera/AutoDoom) — Ioan Chera's Eternity Engine fork with a pathfinding bot.**

[![Release](https://img.shields.io/github/v/release/LightWolfMan/autodoom-pip?label=release)](https://github.com/LightWolfMan/autodoom-pip/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/LightWolfMan/autodoom-pip/total)](https://github.com/LightWolfMan/autodoom-pip/releases)
[![Engine](https://img.shields.io/badge/engine-GPL--3.0-blue)](COPYING-GPLv3)
[![Launcher](https://img.shields.io/badge/launcher-MIT-green)](LICENSE-launcher.md)
[![Platform](https://img.shields.io/badge/platform-Windows-lightgrey)](#requirements)

**English** · [Português](#português)

<img src="docs/pip.png" alt="AutoDoom with the picture-in-picture box in the top-left corner, showing another bot's view" width="820">

</div>

---

Watching a bot play is more fun when you can see what *it* sees. This project adds a corner box
rendering another player's view **in the same frame** as your own, plus a launcher that ties the
loose ends of running AutoDoom together.

## Table of contents

- [Features](#features)
- [Requirements](#requirements)
- [Installation](#installation)
- [Usage](#usage)
- [Configuration](#configuration)
- [Building from source](#building-from-source)
- [How it works](#how-it-works)
- [Known limitations](#known-limitations)
- [Contributing](#contributing)
- [Licence](#licence)
- [Credits](#credits)

## Features

- **Picture-in-picture** — one to three corner boxes with other players' views, rendered in the
  same frame. Off by default.
- **Follows the leader** — with a single box it tracks whoever has the most kills. `F12` borrows
  the box for ten seconds, then it goes back on its own.
- **Launcher** — IWAD and PWAD pickers, Copilot and Co-op modes, bot count, and a WAD finder
  that reads the NTFS journal instead of walking the disk.
- **Co-op scoreboard** — hold `F` for a ranking by kills. Eternity's scoreboard was deathmatch
  only; a small patch opens it up.
- **Bilingual** — the launcher follows your Windows language (English or Portuguese).
- **Non-destructive** — installs *beside* your `AutoDoom.exe`, never replacing it.

<img src="docs/launcher-en.png" alt="The AutoDoom Launcher window" width="520">

## Requirements

| | |
| --- | --- |
| OS | Windows 10 or 11 |
| Game | An existing [AutoDoom](https://github.com/ioan-chera/AutoDoom) install, with `AutoDoom.exe` and the SDL2 DLLs |
| IWAD | Any Doom, Doom II, Final Doom, Heretic or Freedoom IWAD |
| Runtime | [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) — for the launcher only; the game itself needs nothing extra |
| Optional | Administrator rights and an NTFS volume with an active USN journal, for the WAD finder |

## Installation

1. Download **`AutoDoomPip-Setup.exe`** from the [latest release](https://github.com/LightWolfMan/autodoom-pip/releases/latest).
2. Run it and point it at the folder that contains `AutoDoom.exe`. The installer refuses to
   continue anywhere else.
3. Launch **AutoDoom Launcher** from the Start menu.

Prefer the command line? The release also ships a PowerShell installer:

```powershell
.\Install-AutoDoomPip.ps1 -Target "D:\Games\AutoDoom"
```

To uninstall, use *Apps & features* in Windows, or the shortcut in the Start menu folder. Your
original `AutoDoom.exe` is never touched.

## Usage

Open the launcher, pick an IWAD, choose a mode and press **Play**.

| Mode | What happens |
| --- | --- |
| **Copilot** | The bot plays your character and hands control back the moment you touch the keys, taking it again a second after you stop. |
| **Co-op** | You play alongside 1–4 bots. Four is the engine ceiling (`MAXPLAYERS`), and at four your own character goes on autopilot too, because no slot is left. |

In game:

| Key | Action |
| --- | --- |
| `F` (hold) | Kill scoreboard |
| `F12` | Move the PIP box to the next player, for ten seconds |
| `Space` | Jump, when enabled in the launcher |

<img src="docs/scoreboard.png" alt="The co-op scoreboard ranked by kills, with the PIP box visible" width="720">

## Configuration

Picture-in-picture is off by default. Turn it on with the launcher checkbox, with `-pip` on the
command line, or with the console variables below — all of them are saved in `eternity.cfg`.

| cvar | What it does | Default | Range |
| --- | --- | --- | --- |
| `pip` | Turns the box on | `0` | `0`–`1` |
| `pip_size` | Size of each box, as a percentage of the screen | `25` | `10`–`50` |
| `pip_count` | How many boxes, laid out left to right | `1` | `1`–`3` |
| `pip_bottom` | `0` = top row, `1` = bottom row | `0` | `0`–`1` |

The launcher also flips two engine settings that have nothing to do with PIP:

- **Weapons disappear when picked up** — Doom's co-op default leaves weapons on the floor for
  everyone (`DM_WEAPONSTAY`), which makes the game noticeably easier. The launcher passes
  `-dmflags 0`.
- **Jumping** — supported by the engine since forever, disabled behind `comp_aircontrol`.

## Building from source

```
msbuild vc2019\Eternity.sln /p:Configuration=Release /p:Platform=Win32 ^
  /p:PlatformToolset=v143 /p:SDL2_0=<sdl2> /p:SDLMIXER2_0=<mixer> /p:SDLNET2_0=<net>
```

Two things that cost an afternoon:

- The **`adlmidi` submodule must be populated** (`git submodule update --init`), otherwise the
  build fails on missing source files.
- **Build Win32.** If the binary is going to sit next to the 32-bit DLLs that ship with
  AutoDoom, an x64 build dies with `0xc000007b`.

The launcher builds with `dotnet publish -c Release -r win-x64 --self-contained false
-p:PublishSingleFile=true`; its source is in [`launcher-src/`](launcher-src).

## How it works

`R_RenderPlayerView` clears every buffer it uses on entry — clip segs, draw segs, planes,
portals, sprites — so **calling it twice in one frame is safe**. The geometry is what needs
care: `R_RenderPipView` saves `viewwindow`, the `cb_view_t`, the centers and the per-column
height table, swaps in the box rectangle, renders, and puts everything back.

The cost is not double. The BSP walk and thing setup repeat in full while only the pixel work
scales with the box area, so a 25% box costs noticeably less than a second full render — but it
is not free, and on a software renderer that time competes with the bot's own thinking.

Eternity already renders alternate viewpoints through skybox and anchored portals; those are
always tied to map geometry. This is the same renderer pointed at a screen-space rectangle and
a player instead.

## Known limitations

- Windows only, tested on a 32-bit Release build.
- The WAD finder needs administrator rights and an active NTFS journal. Without them the button
  stays disabled and says why, instead of falling back to an hours-long disk sweep.
- `pip_count` above `1` is implemented but has had little real use.
- No crouching: there is no crouch code anywhere in Eternity, only an unused ACS constant.

## Contributing

Issues and pull requests are welcome. The engine change is offered upstream at
[ioan-chera/AutoDoom](https://github.com/ioan-chera/AutoDoom); the modified source lives in
[LightWolfMan/AutoDoom, branch `pip-view`](https://github.com/LightWolfMan/AutoDoom/tree/pip-view).

## Licence

This repository carries two works with different origins.

| Part | Licence | Why |
| --- | --- | --- |
| `autodoom_pip.exe`, `autodoom-pip.patch` | [GPL-3.0](COPYING-GPLv3) | Derived from the Eternity Engine and AutoDoom. The modified source is published [here](https://github.com/LightWolfMan/AutoDoom/tree/pip-view). |
| Launcher, installer script | [MIT](LICENSE-launcher.md) | Written from scratch; does not derive from Eternity. |

## Credits

- [Ioan Chera](https://github.com/ioan-chera) — AutoDoom and its pathfinding bot.
- [Team Eternity](https://github.com/team-eternity/eternity) — the Eternity Engine.
- id Software — Doom.

---

<div align="center">

# Português

**Picture-in-picture e um launcher amigável para o [AutoDoom](https://github.com/ioan-chera/AutoDoom) — o fork do Eternity Engine com o bot do Ioan Chera.**

[English](#autodoom-pip--launcher) · **Português**

</div>

Ver um bot jogar fica bem melhor quando dá para ver o que *ele* vê. Este projeto acrescenta um
quadrinho no canto com a visão de outro jogador, desenhado **no mesmo quadro** que a sua, mais um
launcher que amarra as pontas soltas de rodar o AutoDoom.

## Índice

- [Recursos](#recursos)
- [Requisitos](#requisitos)
- [Instalação](#instalação)
- [Como usar](#como-usar)
- [Configuração](#configuração)
- [Compilando](#compilando)
- [Como funciona](#como-funciona)
- [Limitações conhecidas](#limitações-conhecidas)
- [Licença](#licença-1)

## Recursos

- **Picture-in-picture** — de um a três quadrinhos com a visão de outros jogadores, desenhados no
  mesmo quadro. Vem desligado.
- **Segue o líder** — com um quadrinho só, ele acompanha quem mais matou. O `F12` empresta o
  quadrinho por dez segundos e depois ele volta sozinho.
- **Launcher** — escolha de IWAD e PWAD, modos Copiloto e Coop, número de bots, e um detector de
  WADs que lê o journal do NTFS em vez de varrer o disco.
- **Placar no coop** — segure `F` para o ranking por abates. O placar do Eternity só valia em
  deathmatch; um patch pequeno abriu.
- **Bilíngue** — o launcher acompanha o idioma do Windows (português ou inglês).
- **Não destrutivo** — instala *ao lado* do seu `AutoDoom.exe`, nunca por cima.

<img src="docs/launcher-pt.png" alt="A janela do AutoDoom Launcher" width="520">

## Requisitos

| | |
| --- | --- |
| Sistema | Windows 10 ou 11 |
| Jogo | Uma instalação do [AutoDoom](https://github.com/ioan-chera/AutoDoom), com `AutoDoom.exe` e as DLLs do SDL2 |
| IWAD | Qualquer IWAD de Doom, Doom II, Final Doom, Heretic ou Freedoom |
| Runtime | [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) — só para o launcher; o jogo não precisa de nada |
| Opcional | Direitos de administrador e volume NTFS com journal ativo, para o detector de WADs |

## Instalação

1. Baixe o **`AutoDoomPip-Setup.exe`** na [última release](https://github.com/LightWolfMan/autodoom-pip/releases/latest).
2. Rode e aponte para a pasta que contém o `AutoDoom.exe`. O instalador se recusa a continuar em
   qualquer outro lugar.
3. Abra o **AutoDoom Launcher** pelo Menu Iniciar.

Pela linha de comando:

```powershell
.\Install-AutoDoomPip.ps1 -Target "D:\Jogos\AutoDoom"
```

Para desinstalar, use *Aplicativos e recursos* do Windows. O seu `AutoDoom.exe` original nunca é
tocado.

## Como usar

| Modo | O que acontece |
| --- | --- |
| **Copiloto** | O bot joga o seu personagem e devolve o controle no instante em que você toca nas teclas, retomando um segundo depois que você para. |
| **Coop** | Você joga ao lado de 1 a 4 bots. Quatro é o teto da engine (`MAXPLAYERS`), e nele o seu personagem também entra no piloto automático, porque não sobra vaga. |

No jogo:

| Tecla | Ação |
| --- | --- |
| `F` (segurando) | Placar de abates |
| `F12` | Passa o quadrinho para o próximo jogador, por dez segundos |
| `Espaço` | Pular, quando ligado no launcher |

## Configuração

| cvar | O que faz | Padrão | Faixa |
| --- | --- | --- | --- |
| `pip` | Liga o quadrinho | `0` | `0`–`1` |
| `pip_size` | Tamanho de cada quadrinho, em % da tela | `25` | `10`–`50` |
| `pip_count` | Quantos quadrinhos, da esquerda para a direita | `1` | `1`–`3` |
| `pip_bottom` | `0` = fila em cima, `1` = embaixo | `0` | `0`–`1` |

O launcher também mexe em duas opções da engine que nada têm a ver com o PIP:

- **Armas somem ao pegar** — o padrão do coop do Doom deixa a arma no chão para todo mundo
  (`DM_WEAPONSTAY`), o que facilita bastante. O launcher passa `-dmflags 0`.
- **Pulo** — a engine sempre teve, desativado atrás do `comp_aircontrol`.

## Compilando

```
msbuild vc2019\Eternity.sln /p:Configuration=Release /p:Platform=Win32 ^
  /p:PlatformToolset=v143 /p:SDL2_0=<sdl2> /p:SDLMIXER2_0=<mixer> /p:SDLNET2_0=<net>
```

Duas pedras que custaram uma tarde: o submódulo **`adlmidi` precisa estar populado**
(`git submodule update --init`), e **compile em Win32** — um build x64 ao lado das DLLs de 32
bits do AutoDoom morre com `0xc000007b`.

## Como funciona

O `R_RenderPlayerView` limpa todos os buffers que usa logo na entrada — clip segs, draw segs,
planos, portais, sprites —, então **chamar duas vezes no mesmo quadro é seguro**. O que precisa
de cuidado é a geometria: o `R_RenderPipView` salva `viewwindow`, o `cb_view_t`, os centros e a
tabela de altura por coluna, troca pelo retângulo do quadrinho, renderiza e devolve tudo.

O custo não é o dobro. O passeio pela BSP e o processamento de coisas se repetem inteiros, e só
o trabalho de pixel escala com a área, então um quadrinho de 25% custa bem menos que um segundo
render completo — mas custa, e num renderizador por software esse tempo disputa com o
pensamento do bot.

## Limitações conhecidas

- Só Windows, testado em build Release de 32 bits.
- O detector de WADs exige administrador e journal NTFS ativo. Sem isso o botão fica desabilitado
  e explica o motivo, em vez de cair numa varredura de horas.
- `pip_count` maior que `1` está implementado, mas foi pouco usado de verdade.
- Não há agachamento: não existe código de crouch no Eternity, apenas uma constante de ACS sem uso.

## Licença

| Parte | Licença | Por quê |
| --- | --- | --- |
| `autodoom_pip.exe`, `autodoom-pip.patch` | [GPL-3.0](COPYING-GPLv3) | Derivado do Eternity Engine e do AutoDoom. O fonte modificado está publicado [aqui](https://github.com/LightWolfMan/AutoDoom/tree/pip-view). |
| Launcher, script de instalação | [MIT](LICENSE-launcher.md) | Escrito do zero; não deriva do Eternity. |
