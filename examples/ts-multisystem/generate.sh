#!/bin/bash
# Builds the whole system from the describe files in describe/ (one per app, e.g. from
# `haya describe -f yaml -o describe/orders.yaml orders` in each repository).
# Run from this directory. Uses the Bun CLI from this repository.
set -euo pipefail
cd "$(dirname "$0")"
haya() { bun run ../../src/Haya.Bun/haya.ts "$@"; }

mkdir -p output
# Describe files can be mixed with projects, e.g. `describe/orders.yaml ../other/tsconfig.json`.
haya crc -c -o output/CRC.md describe/storefront.yaml describe/orders.yaml describe/payments.yaml
haya diagram -o output/system.mermaid describe/storefront.yaml describe/orders.yaml describe/payments.yaml
haya backstage -o output/backstage.yaml describe/storefront.yaml describe/orders.yaml describe/payments.yaml
