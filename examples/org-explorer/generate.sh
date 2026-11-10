#!/bin/bash
# Builds the picture of a whole organisation from the describe files in describe/. Each team publishes the describe file for
# its own system (e.g. `haya describe -o describe/orders.yaml Orders/Orders.sln` in its repository); this reads them all.
# Run from anywhere. Uses the .NET tool from this repository; open output/explorer.html in a browser.
set -euo pipefail
cd "$(dirname "$0")"
haya() { dotnet run --no-build --project ../../src/Haya.Tool -- "$@"; }

dotnet build ../../src/Haya.Tool --nologo -v q

mkdir -p output
# The input paths go last. JSON and YAML describe files can be mixed in one call, and with solutions or tsconfig projects.
haya diagram -f html -o output/explorer.html describe/*
# The static diagrams of the same system, for comparison: level 1 is readable, level 2 is the one that does not scale.
haya diagram -o output/system.mermaid describe/*
haya diagram -t c4container -o output/containers.mermaid describe/*
