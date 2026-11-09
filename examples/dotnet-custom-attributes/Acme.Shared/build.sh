#!/bin/sh
# Rebuilds ../lib/Acme.Shared.dll, the compiled library that Checkout references.
set -e
cd "$(dirname "$0")"
dotnet build -c Release -o ../lib/build
cp ../lib/build/Acme.Shared.dll ../lib/Acme.Shared.dll
rm -rf ../lib/build
