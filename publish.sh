#!/bin/bash

# Wrapper script for the F# publish script.
#
# Arguments:
#   --dry-run    Simulate the process without making any changes to disk or git.
#
# Usage:
#   ./publish.sh              # Standard execution
#   ./publish.sh --dry-run    # Test run to verify changes

dotnet fsi scripts/publish.fsx \
  --allow-dirty \
  --fsproj "Directory.Build.props" \
  --solution "Haya-net.sln" \
  "$@"
