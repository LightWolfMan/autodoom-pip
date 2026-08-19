#!/bin/bash
# Compila o AutoDoom com o patch PIP + copiloto no Linux.
#
# A engine e multiplataforma por projeto (o Eternity traz CMakeLists completo) e
# o patch e C++ comum sobre a biblioteca padrao. O `b_vote.cpp` entra no build
# sozinho porque `source/CMakeLists.txt` monta a lista com
# FILE (GLOB autodoom/*.cpp).
#
# Verificado em 19/08/2026 numa Ubuntu 24.04: cmake 3.28.3, GCC 13.3, SDL2 2.30.
# Compilou de primeira, zero erros, zero mudanca de codigo.
set -e

REPO=${1:-https://github.com/ioan-chera/AutoDoom.git}
PATCH=${2:-$(dirname "$0")/../dist/autodoom-pip/autodoom-pip.patch}
DEST=${3:-$HOME/AutoDoom}

echo "== dependencias =="
sudo apt-get update -qq
sudo apt-get install -y build-essential cmake git pkg-config \
     libsdl2-dev libsdl2-mixer-dev libsdl2-net-dev

echo "== fonte =="
[ -d "$DEST" ] || git clone --branch AutoDoom "$REPO" "$DEST"
cd "$DEST"
# Sem isto o CMake para em "adlmidi not found".
git submodule update --init --recursive

echo "== patch =="
git apply --check "$PATCH"   # falha aqui e melhor do que falhar no meio
git apply "$PATCH"

echo "== build =="
mkdir -p build && cd build
cmake .. -DCMAKE_BUILD_TYPE=Release
make -j"$(nproc)"

echo
echo "Pronto: $DEST/build/source/eternity"
echo "O diretorio base/ precisa estar ao lado do executavel:"
echo "  ln -s $DEST/base $DEST/build/source/base"
echo
echo "Exemplo, com 1 companheiro, copiloto ligado e o quadrinho:"
echo "  ./eternity -iwad /caminho/doom.wad -bots 1 -copilot 1 -pip -warp 1 1"
