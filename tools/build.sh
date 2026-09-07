#!/usr/bin/env bash
# Builds the solution (Core, every program DLL, then the host), first clearing
# any instance left running by a previous screenshot run.
set -u
taskkill //F //IM MiminusOS.exe >/dev/null 2>&1 || true
cd "$(dirname "$0")/../src" || exit 1
"/c/PROGRAM FILES/DOTNET/dotnet" build MiminusOS.slnx -v q --nologo "$@" 2>&1 \
  | grep -E "error|warning|Build succ" | sed 's/\[G:.*//' | sort -u | head -40
