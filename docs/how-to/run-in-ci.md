# How to run Haya in CI

Use this to keep generated documentation in sync with the code.

## Behaviour to rely on

- Exit code `0` on success, `1` on any error (input not found, annotation errors, solution fails to load).
- Warnings (for example merge conflicts) go to stderr and do not fail the run.
- Output is deterministic with LF line endings, so a diff means a real change.

## GitHub Actions example: fail when docs are stale

```yaml
- uses: actions/setup-dotnet@v6
  with:
    global-json-file: global.json
- run: dotnet tool restore            # if you pin haya in dotnet-tools.json
- run: haya crc -cc -o CRC.md ./YourApp.sln
- run: git diff --exit-code CRC.md    # fails if the committed file is out of date
```

To publish instead of fail, commit the regenerated files from the workflow, or upload them as artifacts.
For a multi-repository system, upload each repository's `haya describe` output as an artifact and run the
[central step](document-multiple-systems.md) from one repository.

## Notes

- Component paths in the output are relative to the working directory, so run `haya` from the same folder each time.
- Only C# projects in a solution are analysed; F# projects are skipped.
