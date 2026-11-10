# Contributing

How to build and test Haya. Agents and humans should also read [AGENTS.md](../AGENTS.md) for layout and conventions.

## .NET (C# tool, core, tests)

```bash
dotnet tool restore
dotnet build
dotnet test
dotnet run --project src/Haya.Tool -- describe -o out.json examples/dotnet/Example.sln   # input path goes last
```

## Bun (TypeScript tool)

Prerequisites: the .NET SDK from `global.json` (with `dotnet tool restore` for Fable) and Bun.

```bash
cd src/Haya.Bun
bun install
bun run fable        # compile Haya.Core to TypeScript
bun run typecheck
bun test             # analyser unit tests
bun run parity       # diff against Haya.Tool for the examples (needs .NET)
bun run build        # fable + typecheck + compile -> dist/haya
```

`bun run parity` must pass after changes to `Haya.Core`, either analyser, or the examples.

## Docs

Docs follow [Diataxis](https://diataxis.fr): put a page under `docs/tutorials`, `docs/how-to`, `docs/reference` or
`docs/explanation` by what the reader is doing, keep each page to one mode, and link from [the index](index.md).
Record design decisions as ADRs in [docs/adr](adr/README.md) and user-visible changes in the [changelog](../CHANGELOG.md).

## Releasing

Releases are driven by a `v*` tag. CI never bumps versions or commits; it reacts to the tag.

1. Add user-facing entries under `## [Unreleased]` in the [changelog](../CHANGELOG.md).
2. Run `./publish.sh` (`./publish.sh --dry-run` first to preview). It builds and tests, asks for the bump (and whether
   it is a prerelease), updates `<Version>` and `<PackageReleaseNotes>` in `Directory.Build.props`, rolls the changelog
   for a stable release, commits, tags `vX.Y.Z`, and offers to push `main` and the tag.
3. Pushing the tag runs [`publish-nuget.yml`](../.github/workflows/publish-nuget.yml), which builds, tests, packs and
   pushes `Haya` and `Haya.Tool` at the tag's version. Both packages share one version.

Publishing uses NuGet [Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing): there is no
API key. The workflow gets a short-lived key through OIDC, using the `NUGET_USER` repository secret (the nuget.org
profile name, not the email) and a policy on nuget.org bound to the workflow file name `publish-nuget.yml`. Renaming
that file breaks publishing until the policy is updated. A published version cannot be taken back, so try a prerelease
tag such as `v0.1.0-alpha.1` first.

To try the latest prerelease of the tool locally, run `./install-alpha.sh`.
