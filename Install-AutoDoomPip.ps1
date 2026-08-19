<#
.SYNOPSIS
Instala o AutoDoom PIP e o launcher em qualquer pasta do AutoDoom.

.DESCRIPTION
Copia o executavel com picture-in-picture e o launcher para a pasta indicada,
liga o placar no F e, se voce quiser, o pulo. Nao apaga nem sobrescreve o
AutoDoom.exe original: os dois convivem lado a lado.

.PARAMETER Target
Pasta onde mora o AutoDoom.exe.

.PARAMETER EnableJump
Liga o pulo (comp_aircontrol 0). O padrao da engine e pulo desativado.

.PARAMETER WhatIf
Mostra o que faria, sem escrever nada.

.EXAMPLE
.\Install-AutoDoomPip.ps1 -Target "D:\Jogos\AutoDoom"

.EXAMPLE
.\Install-AutoDoomPip.ps1 -Target "D:\Jogos\AutoDoom" -EnableJump -WhatIf
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string]$Target,
    [switch]$EnableJump
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

function Write-Step { param([string]$Text) Write-Host "  $Text" }

# ---------------------------------------------------------------- validacao

if (-not (Test-Path -LiteralPath $Target -PathType Container)) {
    throw "Pasta nao encontrada: $Target"
}

$gameExe = Join-Path $Target 'AutoDoom.exe'
if (-not (Test-Path -LiteralPath $gameExe)) {
    throw "Nao achei AutoDoom.exe em $Target. Aponte para a pasta do jogo."
}

# O executavel com PIP e 32 bits e usa as DLLs que ja estao na pasta do jogo.
foreach ($dll in 'SDL2.dll', 'SDL2_mixer.dll', 'SDL2_net.dll') {
    if (-not (Test-Path -LiteralPath (Join-Path $Target $dll))) {
        throw "Falta $dll em $Target. Essa pasta nao parece uma instalacao completa."
    }
}

Write-Host "Instalando em: $Target"

# ------------------------------------------------------------------ copias

foreach ($file in 'autodoom_pip.exe', 'AutoDoom Launcher.exe') {
    $src = Join-Path $here $file
    if (-not (Test-Path -LiteralPath $src)) {
        Write-Warning "$file nao esta ao lado do script; pulando."
        continue
    }

    if ($PSCmdlet.ShouldProcess((Join-Path $Target $file), 'copiar')) {
        Copy-Item -LiteralPath $src -Destination $Target -Force
        Write-Step "copiado: $file"
    }
}

# --------------------------------------------------------------- config

# O Eternity guarda a config do jogo em user\doom\ (ou user\<mod>\). Mexemos
# em todos os perfis que existirem, para o F valer em qualquer um.
$userDirs = @()
$userRoot = Join-Path $Target 'user'
if (Test-Path -LiteralPath $userRoot) {
    $userDirs = Get-ChildItem -LiteralPath $userRoot -Directory -ErrorAction SilentlyContinue
}

if (-not $userDirs) {
    Write-Step 'sem pasta user\ ainda; abra o jogo uma vez e rode de novo para o placar no F'
}

foreach ($dir in $userDirs) {
    $keys = Join-Path $dir.FullName 'keys.csc'
    $cfg  = Join-Path $dir.FullName 'eternity.cfg'

    # F segurado mostra o placar: a acao "frags" ja existe na engine.
    if (Test-Path -LiteralPath $keys) {
        # -notmatch num array FILTRA, nao devolve booleano: sem o Where-Object
        # abaixo o script re-adicionaria o bind a cada execucao.
        $bind = @(Get-Content -LiteralPath $keys)
        $already = @($bind | Where-Object { $_ -match '^bind f "frags"' }).Count -gt 0
        if (-not $already) {
            if ($PSCmdlet.ShouldProcess($keys, 'ligar o placar no F')) {
                Add-Content -LiteralPath $keys -Value 'bind f "frags"' -Encoding utf8
                Write-Step "placar no F: $($dir.Name)"
            }
        }
    }

        # Backspace solta os bots que travaram; a tecla so e usada no console.
        $hasUnstick = @($bind | Where-Object { $_ -match 'bot_unstick' }).Count -gt 0
        if (-not $hasUnstick) {
            if ($PSCmdlet.ShouldProcess($keys, 'destravar bots no Backspace')) {
                Add-Content -LiteralPath $keys -Value 'bind backspace "bot_unstick"' -Encoding utf8
                Write-Step "destravar no Backspace: $($dir.Name)"
            }
        }

    if (-not (Test-Path -LiteralPath $cfg)) { continue }

    $lines = Get-Content -LiteralPath $cfg

    # show_scores precisa estar ligado, senao o F nao desenha nada.
    if (@($lines | Where-Object { $_ -match '^show_scores' }).Count -gt 0) {
        $lines = $lines -replace '^show_scores.*$', 'show_scores                   1'
    } else {
        $lines += 'show_scores                   1'
    }

    if ($EnableJump) {
        # comp_aircontrol e alias de comp_jump: 1 desativa o pulo.
        $lines = $lines -replace '^comp_aircontrol.*$', 'comp_aircontrol               0'
    }

    if ($PSCmdlet.ShouldProcess($cfg, 'ajustar config')) {
        Set-Content -LiteralPath $cfg -Value $lines -Encoding utf8
        Write-Step "config ajustada: $($dir.Name)"
    }
}

Write-Host ''
Write-Host 'Pronto. Abra o "AutoDoom Launcher.exe" na pasta do jogo.'
Write-Host 'Marque "Ver a tela dos bots num quadrinho" para usar o build com PIP.'
Write-Host 'Segure F em jogo para o placar. F12 troca quem aparece no quadrinho.'
