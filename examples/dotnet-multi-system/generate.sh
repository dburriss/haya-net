#!/bin/bash
# Builds the whole system from the describe files in describe/ (one per app, e.g. from
# `haya describe -o describe/Orders.json Orders/Orders.sln` in each repository).
# Run from this directory. Uses the .NET tool from this repository.
set -euo pipefail
cd "$(dirname "$0")"
haya() { dotnet run --no-build --project ../../src/Haya.Tool -- "$@"; }

dotnet build ../../src/Haya.Tool --nologo -v q

mkdir -p output
# The input paths go last. Describe files can be mixed with solutions, e.g. `describe/Orders.json ../other/Other.sln`.
haya crc -cc -o output/CRC.md describe/Storefront.json describe/Orders.json describe/Payments.json
haya diagram -o output/system.mermaid describe/Storefront.json describe/Orders.json describe/Payments.json
haya backstage -o output/backstage.yaml describe/Storefront.json describe/Orders.json describe/Payments.json
haya datadog -o output/entity.datadog.yaml describe/Storefront.json describe/Orders.json describe/Payments.json
