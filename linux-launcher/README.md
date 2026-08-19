# AutoDoom Launcher para Linux

**Versão 1.0-rc1.** Python 3 + GTK 4 + libadwaita (PyGObject). Escolhe IWAD e PWAD, liga o
copiloto, define de 0 a 3 companheiros, escreve o que precisa na configuração da engine e sobe
o jogo. Acompanha o idioma do sistema, em português ou inglês.

<img src="../docs/launcher-pt.png" alt="A janela do launcher" width="620">

## Por que esta stack, e não outra

O alvo é **Debian e Arch**, e é isso que decide. Três candidatos reais foram pesados:

| Stack | A favor | Contra |
| --- | --- | --- |
| **Python + GTK4/libadwaita** | Está nos repositórios das duas distros (`python3-gi` / `python-gobject`), sem runtime extra para o usuário instalar. Visual nativo no GNOME e aceitável no KDE. Empacotar é um `.deb` ou `PKGBUILD` de poucas linhas, sem bundle. | A lógica precisou ser escrita do zero — deu cerca de 400 linhas. |
| **Avalonia (C#/.NET)** | Reaproveitaria lógica pronta quase intacta. | Empurra o .NET para o usuário: ou ele instala o runtime, ou o pacote carrega ~70 MB de binário self-contained. Nenhuma das duas distros empacota Avalonia. |
| **Qt6/QML** | Excelente no KDE, e Qt está em ambas as distros. | Mais cerimônia para uma janela só, e o visual destoa no GNOME, que é o padrão do Debian. |

Ganhou o **GTK4** porque o custo cai no lado certo: escrever a lógica é trabalho que acontece
uma vez, enquanto exigir runtime seria custo de todo mundo que instalar, para sempre.

## Dependências

**Debian / Ubuntu**

```
sudo apt install python3-gi python3-gi-cairo gir1.2-gtk-4.0 gir1.2-adw-1 plocate
```

**Arch**

```
sudo pacman -S --needed python-gobject gtk4 libadwaita plocate
```

Verificado na Ubuntu 24.04 com GTK 4.14, libadwaita 1.5 e PyGObject 3.48.

## Rodando

```
./autodoom-launcher [pasta-do-jogo]
```

Sem argumento ele procura, nessa ordem: o diretório atual, `~/AutoDoom` e
`~/.local/share/autodoom`. Para ver a janela no outro idioma sem trocar a sessão:

```
LC_ALL=en_US.UTF-8 ./autodoom-launcher
```

## O que a janela faz

| Seção | O que decide |
| --- | --- |
| **Modo** | O copiloto (o bot dirige o *seu* personagem) e a contagem de companheiros, de 0 a 3. São independentes: dá para ter um companheiro e o copiloto ao mesmo tempo, o que vira `-bots 1 -copilot 1` |
| **Extras** | Quadrinho com a visão dos bots, armas sumindo ao pegar, quem a câmera segue, pulo liberado e o placar no `F` |
| **IWAD** | Lista o que achou nas pastas conhecidas; `Detectar IWADs...` usa o `plocate` para varrer o disco |
| **PWAD** | Um mapa avulso, opcional |

O que mora na configuração da engine (`pip_follow`, `comp_aircontrol`, `show_scores`) é escrito
no momento de jogar, com o jogo fechado — ela lê no início e reescreve o arquivo ao sair.

## Arquivos

| Arquivo | Papel |
| --- | --- |
| `autodoom_launcher/wad.py` | Lê o diretório de lumps do WAD; reconhece IWAD e distingue Doom registrado do Ultimate pelo `E4M1` |
| `autodoom_launcher/catalog.py` | Onde procurar IWAD, mais a busca por `plocate` |
| `autodoom_launcher/game.py` | Monta `-bots`, `-copilot`, `-pip`, `-dmflags` e sobe o processo |
| `autodoom_launcher/config.py` | `settings.json` em `$XDG_CONFIG_HOME`, perfis da engine e o teclado do projeto |
| `autodoom_launcher/strings.py` | Os textos, em português e inglês, escolhidos pelo locale |
| `autodoom_launcher/icons.py` | Ícones Fluent tingidos pela cor do tema |
| `autodoom_launcher/window.py` | A janela |
| `data/net.autodoom.launcher.desktop` | Entrada de menu |

## Por que RC e não 1.0

Tudo que a janela oferece funciona e foi visto funcionando, mas duas coisas ainda faltam para
ficar no mesmo nível do resto do projeto:

- A ficha do IWAD mostra nome, arquivo, tipo e tamanho, mas não o logo lido de dentro do
  próprio WAD (`M_DOOM`/`TITLEPIC`).
- O PWAD é escolhido na mão, sem o teste de compatibilidade que avisa quando o mapa não
  combina com o IWAD selecionado.

Faltam também os pacotes `.deb` e `PKGBUILD` — hoje se roda direto do diretório.
