#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
require dotnet
cd "$ROOT"

step "Restore"
dotnet restore nest.slnx

step "Build (Release, warnings are errors)"
dotnet build nest.slnx --configuration Release --no-restore
dotnet list package
dotnet list package --outdated
