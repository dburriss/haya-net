# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

> Semantic Versioning will only be applied once the project reaches a stable v1.

## [Unreleased]

### Added

- Add `Tech` field to the `CollaboratorAttribute` attribute.

### Changed

- Json output of `componentSource` is now relative to the executing directory.
- Added `protocol` field to the Descriptor, used in json output.

### Removed

## [0.0.3] - 2024-09-21

### Added

- DocComments on the Attributes
- Added this changelog

### Changed

- Attributes are now published independently in the `Haya` package

### Removed

## [0.0.2] - 2024-09-20

### Added

- Added documentation page for the attributes.

## Changed

- Changes `Tech` field to `Protocol` field on the `CollaboratorAttribute` attribute.

## [0.0.1] - 2024-09-16

Initial release of the project.

### Added

- Added the `MetaAttribute` attribute.
- Added the `ResponsibilityAttribute` attribute.
- Added the `CollaboratorAttribute` attribute.
- Added the `Haya` dotnet tool with the `crc` command.

### Fixed

### Changed

### Removed
