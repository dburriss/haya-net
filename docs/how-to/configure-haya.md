# Configure Haya

Keep the flags you always pass in a file, per project or for your whole machine. The full list of settings is in the
[configuration reference](../reference/configuration.md).

## Set a value for this project

```bash
haya config set output.path docs/haya
haya config set diagram.format html
haya config set crc.diagram apps
```

This writes `.haya/config.json` in the current directory (or updates the nearest one above it). Commit it so the team and CI share it.
From then on `haya crc ...`, `haya diagram ...` and the others write into `docs/haya`, and `-o`, `-f` and `-c` still override it.

## Set a value for all your projects

```bash
haya config set viewer.detail apps --global
```

The global file is `haya/config.json` in your OS config directory. A project's file overrides it, one setting at a time.

## See what is in effect

```bash
haya config list --show-origin
```

## Change the explorer's colours

The complexity colours start at four stops. Raise them for larger systems:

```bash
haya config set viewer.complexity.systems 10,14,20,30
haya config set viewer.complexity.links 4,6,9,14
haya diagram -f html -o diagram.html Example.sln
```

## Use an environment variable in CI

```bash
HAYA_OUTPUT__PATH=build/docs HAYA_VIEWER__DETAIL=apps haya diagram -f html Example.sln
```

Variables beat both files and are never written by `haya config set`.
