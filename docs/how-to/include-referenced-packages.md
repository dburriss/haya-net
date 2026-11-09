# Include attributes from referenced packages

Use this when a shared library (a NuGet package or DLL) is annotated with Haya attributes and you want its
responsibilities and collaborators in your app's docs. C# only: TypeScript decorators and `meta()` calls do not survive
into `.d.ts` files, so `Haya.Bun` cannot read them from `node_modules`.

## Steps

1. Restore the packages (`dotnet restore`) so the solution's references resolve.
2. Run any command with one of the flags:

   ```bash
   haya crc --include-references --verbose -o CRC.md MyApp.sln
   haya describe --include-assembly "Acme.*" -o haya.json MyApp.sln
   ```

3. `--verbose` prints `Including <assembly>: <type>` for every type picked up, so you can check the scope.

A runnable sample is in `examples/dotnet-custom-attributes`: `Checkout` references the prebuilt `lib/Acme.Shared.dll`
(source in `Acme.Shared`, rebuild with `Acme.Shared/build.sh`). Compare
its [`CRC.md`](../../examples/dotnet-custom-attributes/CRC.md) with
[`CRC.with-references.md`](../../examples/dotnet-custom-attributes/CRC.with-references.md), generated with
`haya crc -cc --include-references` (the two extra rows and the `Geocoding` container come from the DLL).

## What is included

- Only assemblies that reference `Haya` are scanned. `--include-references` scans all of those; `--include-assembly`
  limits it to names matching a glob.
- Only **public** classes (and public nested classes) are read.
- The assembly-level `[Meta]` of a package is ignored: a package is not your app, and a solution describes one app.
- Projects referenced inside the solution are read from source as usual, not twice.
- A component from a package has the source `Assembly::Namespace.Type`, shown as plain text in the CRC instead of a file link.
- Custom attributes with defaults declared in a package apply as they do in source.

Without the flags, output is unchanged. See the [CLI reference](../reference/cli.md) and
[ADR009](../adr/ADR009-referenced-assembly-analysis.md).
