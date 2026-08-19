# Contribuindo — branch `linux`

*Escrito para quem chega sem contexto: outra pessoa, outra máquina, outra IA. O `README.md`
explica o produto; este arquivo explica **o estado do trabalho, as decisões já tomadas e as
armadilhas que já custaram tempo**, para ninguém pagar duas vezes pela mesma descoberta.*

## O que esta branch é

O lado Linux do projeto. A branch `main` é a versão Windows e **não se olha para ela aqui**:
esta branch parte do princípio de que você não usa Windows, e nada nela deve citar aquele
sistema. Se algo só faz sentido lá, o lugar é a `main`.

Duas partes:

| Parte | Estado |
| --- | --- |
| **Engine** (patch em `autodoom-pip.patch`) | Pronta. Compila e roda; nenhuma linha precisou mudar para o Linux |
| **Launcher** (`linux-launcher/`) | **1.0-rc1**. Faz tudo que a janela oferece; falta o logo do IWAD e a validação do PWAD |

## Começando

```
./tools/build_linux.sh                       # dependências, clone, patch e build da engine
sudo apt install python3-gi python3-gi-cairo gir1.2-gtk-4.0 gir1.2-adw-1 plocate
./linux-launcher/autodoom-launcher ~/AutoDoom
```

Verificado na Ubuntu 24.04 com CMake 3.28.3, GCC 13.3, SDL2 2.30, GTK 4.14, libadwaita 1.5 e
PyGObject 3.48.

## Decisões já tomadas, com o motivo

Mexer nelas é possível, mas saiba o que está desfazendo.

**Copiloto e companheiros são independentes.** A engine amarrava as duas coisas num número só:
`-bots` de 1 a 3 desligava o bot que dirige o *seu* personagem, e só `-bots 4` o trazia de
volta. O patch acrescentou `-copilot <0|1>`, aplicado **depois** da regra do `-bots` dentro de
`G_AdjustNetBotSettings`. Sem o parâmetro, o comportamento antigo continua idêntico — isso
importa porque o patch é oferecido rio acima.

**O launcher é GTK4 + libadwaita, em Python.** O alvo são Debian e Arch, e as duas distros já
empacotam `python3-gi`/`python-gobject`, então **nenhum runtime cai no colo do usuário**.
Avalonia reaproveitaria lógica pronta, mas custaria .NET instalado ou ~70 MB de bundle por
download. A comparação completa está em [`linux-launcher/README.md`](linux-launcher/README.md).

**A busca de WADs usa `plocate`, e não uma varredura própria.** Não existe aqui um diário de
nomes de arquivo consultável; o `fanotify` do kernel só vê eventos ao vivo, não conta o que já
está no disco, e ainda exige `CAP_SYS_ADMIN`. O `plocate` responde em milissegundos e não pede
privilégio nenhum. O preço: um WAD criado depois do último `updatedb` fica invisível até o
próximo.

**Os ícones são máscaras, não arte.** Os PNG em `linux-launcher/icons/` são pretos sobre
transparente; o `icons.py` reescreve o RGB e mantém o alfa, pintando com a cor que o tema
pedir. Um arquivo serve para o tema claro e o escuro. São Fluent UI System Icons (MIT).

**O teclado do projeto (`keys/autodoom-modern.csc`) nunca sobrescreve o de ninguém.** Ele é
escrito só em perfil que ainda **não tem** `keys.csc`. Uma instalação nova precisa disso: a
engine sobe com o teclado de 1993, setas para andar e sem WASD.

## Armadilhas que já custaram tempo

- **A configuração da engine é escrita antes de subir o jogo.** Ela lê no início e reescreve o
  arquivo ao sair, então mexer com o jogo aberto é trabalho perdido.
- **O `adlmidi` precisa estar populado** (`git submodule update --init --recursive`), senão o
  CMake para em "adlmidi not found".
- **`cmake_minimum_required (VERSION 2.6)`** é só um aviso no CMake 3.x, mas vira **erro no
  CMake 4**, que as distribuições novas já trazem. Se o configure falhar logo de cara, é isso.
- **O binário quer o `base/` ao lado dele**: `ln -s ../../base source/base` dentro de `build/`.
- **No GTK, só a thread principal mexe na interface.** Trabalho de fundo volta por
  `GLib.idle_add`, como faz a varredura de WADs.
- **O `Gtk.Scale` trabalha em ponto flutuante.** Sem `round_digits(0)` a alça descansa entre
  duas marcas e o spinner mostra outro número. O slider e o spinner compartilham um único
  `Gtk.Adjustment` de propósito: são vistas do mesmo valor, e não há o que sincronizar.
- **Efeito rápido demais para fotografar pede log, não screenshot.** Foi assim que a votação de
  saída foi diagnosticada: a mensagem dura 4 segundos. Ligue `bot_exitvote_log 1` e leia o
  `vote.log`.

## Como provar que algo funciona

O bot não precisa de teclado para se mexer, e isso dá um teste barato e honesto: **diferença
de quadros**. Suba o jogo, tire duas capturas com alguns segundos de intervalo sem tocar em
nada e conte os pixels que mudaram. Foi assim que o `-copilot` foi verificado — **76,4%** dos
pixels mudando em cinco segundos com `-copilot 1` contra **0,0%** com `-copilot 0`.

Sirva-se do mesmo método antes de afirmar que qualquer coisa funciona. "Compilou" não é
"funciona", e "o processo está vivo" não é "a tela pinta".

## O que falta

- Ficha do IWAD com o logo lido de dentro do próprio WAD (`M_DOOM`, com `TITLEPIC` de reserva).
- Validação de compatibilidade do PWAD — avisar quando o mapa não combina com o IWAD escolhido.
- Empacotamento: `.deb` e `PKGBUILD`.
- Nunca testado fora do WSLg: falta rodar num desktop Linux de verdade, e no KDE.

## Estilo

Comentário explica **por quê**, não o quê — o código já diz o que faz. Quando algo foi medido,
o número entra no comentário ou na mensagem de commit; quando não foi, diga que não foi.
Mensagens de commit em inglês, no imperativo, explicando a razão da mudança.
