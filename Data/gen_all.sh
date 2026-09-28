#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
LUBAN_DLL="$PROJECT_ROOT/Tools/Luban/Luban.dll"
OUTPUT_CODE_DIR="$PROJECT_ROOT/Assets/01_Scripts/Generated/Luban"
OUTPUT_DATA_DIR="$PROJECT_ROOT/Assets/StreamingAssets/GameData/bytes"
CUSTOM_TEMPLATE_DIR="$SCRIPT_DIR/Templates"

export DOTNET_ROLL_FORWARD="${DOTNET_ROLL_FORWARD:-Major}"

dotnet "$LUBAN_DLL" \
  -t client \
  -c cs-bin \
  -d bin \
  --conf "$SCRIPT_DIR/luban.conf" \
  --customTemplateDir "$CUSTOM_TEMPLATE_DIR" \
  -x outputCodeDir="$OUTPUT_CODE_DIR" \
  -x outputDataDir="$OUTPUT_DATA_DIR" \
  -x tableImporter.name=default
