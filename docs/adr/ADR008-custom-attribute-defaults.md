# ADR008: Defaults for custom attributes and annotations

Date: 2026-11-09  
Status: Accepted  
Author: dburriss

### Context
People want their own attributes with some values baked in (`[CallsPayments]` instead of repeating `AppName`,
`System` and `Protocol` on every class, or an organisation-wide `[AcmeMeta]`). A subclass of a Haya attribute was
silently ignored: the analyser matched the attribute class name exactly, and only read the named arguments written at the
usage site. Haya never runs user code, so defaults have to be readable statically.

### Decision
- A custom attribute derives from `ResponsibilityAttribute`, `CollaboratorAttribute` or `MetaAttribute`. The analyser
  matches the attribute class or any of its base classes.
- The defaults are declared with a marker attribute on the custom attribute class: `ResponsibilityDefaults`,
  `CollaboratorDefaults` or `MetaDefaults`, which carry the same properties as the attributes they default. The
  analyser merges the marker's named arguments (base class first, so derived overrides) underneath the named arguments
  at the usage site.
- The markers are separate types, not subclasses of the attributes: the analyser scans every class, and a marker that
  was itself a `Collaborator` would report the custom attribute class as a collaborator.

### Alternatives considered
Assigning the properties in the custom attribute's constructor is the most natural C#, but the analyser would have to
read the constructor body from syntax. That is only possible for source, so defaults from a compiled attribute library
(a NuGet package, the likely home of organisation-wide attributes) would be silently lost, and only literals could be
supported. Marker attributes are ordinary attribute metadata that Roslyn reads the same way for source and compiled
symbols, and it evaluates the constants itself.

### Consequences
- Output for existing code is unchanged.
- Three new public types in the netstandard2.0 `Haya` package, with property lists that must be kept in step with the
  attributes they default.
- Reflection at runtime does not see the defaults; Haya is the only consumer.
- TypeScript has no attribute inheritance, so its counterpart is declarative too: `defineResponsibility`,
  `defineCollaborator` and `defineMeta` in `@haya/annotations` take the default options, and the result is used like the
  annotation it wraps. The analyser follows a callee to a `const` initialised with a `define...` call from the Haya
  package and reads its options statically, with the same rules as for the annotations. Usage-site options win. The
  types make options that are already defaults optional.
- A hand-written wrapper function is not recognised in TypeScript: reading it would need evaluating code.
- A `define...` declaration that is only available as compiled `.d.ts` is not supported, because declaration files do not
  keep the initialiser. (The C# marker attributes do not have this limit.)
