#!/bin/sh
set -eu
cd -- "$(dirname -- "$0")"
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
exec ./Greyhaven.x86_64 "$@"
