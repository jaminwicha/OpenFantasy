# P00 package identity and dependency contract

This increment implements portable package metadata and dependency resolution in `OpenFantasy.Core.Content`. The existing C# core remains engine-free. The host can inspect a real retained source packet through the same metadata contract. No package type compiles or executes the original game, authorizes commands, creates mutable world state, or proves gameplay migration.

## Identity and immutable ownership

`PackageId` is a lowercase ASCII, dot-separated namespace of 1–128 characters. Every segment starts with a letter and may then contain letters, digits, `_` or `-`. `ContentHash` is a full SHA-256 value expressed as exactly 64 lowercase hexadecimal characters. These value objects reject ambiguous/noncanonical spellings; text normalization and current culture do not enter identity.

A `PackageManifest` owns its copied, read-only dependency list. Each dependency names one namespace and pins that dependency's **manifest version**, not merely its payload hash. A manifest permits at most 64 dependencies and rejects repeated namespaces. Constructor enumeration stops and rejects at the first excessive entry. Callers receive fresh canonical byte arrays. The version binds the package namespace, its payload hash and every dependency pin; changing any of these changes the version. Manifest dependency order is ordinal and independent of caller enumeration.

Canonical framing reuses the existing version-1 core frame without changing it: `OFC\0`, unsigned big-endian 32-bit frame version and field count, followed by unsigned big-endian 32-bit byte lengths and exact field bytes. The field count includes the leading tag. A manifest uses tag `package.manifest.v1`, namespace UTF-8, raw 32-byte payload hash, then zero or more sorted nested dependency frames. Each dependency frame uses tag `package.dependency.v1`, namespace UTF-8 and raw 32-byte dependency manifest version. The full manifest frame's SHA-256 is its version. These tags explicitly version the package contract.

`PackageManifestJson` is the bounded interchange representation, with exact properties `schemaVersion`, `id`, `payloadSha256`, and `dependencies`; dependency records have `id` and `versionSha256`. Unknown, missing and duplicate properties reject, as do unsupported schema versions. The input limit is 65,536 bytes and JSON nesting depth is at most eight. JSON formatting/property order does not define identity; canonical binary bytes do. Malformed JSON syntax throws `JsonException`; malformed metadata values throw format/argument exceptions, all handled by the diagnostic host. The binary frame is currently an encoder/hash contract; an independent binary manifest decoder is not implemented or implied.

`PackageResolver.Resolve` takes a complete candidate set of at most 256 manifests and returns immutable deterministic dependency-first order. Missing dependencies, cycles, duplicate namespaces and mismatched pins reject the entire plan. Both root and dependency traversal order are explicit. A cyclic package graph cannot be legitimized by mutually chosen hashes; graph validation remains mandatory. Resolution checks metadata only; the caller must separately validate each payload and later its schema, references, rules and presentation coverage before activation.

## Executable diagnostic boundary

From `Beta/Runtime`, after building Debug:

```sh
dotnet run --project src/OpenFantasy.Host --no-build -- --inspect-package content/packages/aldermere-first-loop.manifest.json content/legacy/aldermere-first-loop/closure-manifest.json
```

The inspector reports the package version, exact payload hash/size and dependency pins. It verifies only the supplied manifest and payload bytes. The command does not recursively open a file list embedded in a payload or resolve a dependency set. The integrated verification tool separately checks every retained closure file's path, byte count and SHA-256 and requires an exact file set. Source-to-source semantic/reference preservation is checked by the migration closure builder and its tests.

The package wrapper hashes `closure-manifest.json` as the opaque source-preservation payload. That closure manifest hashes every retained output and records the original source hashes. The wrapper currently has no cross-package dependencies: all enumerated source closure dependencies are retained inside the packet. This must not be carried forward as evidence that a future playable Aldermere package has no rule, animation, sound or material dependencies.

The host enforces a 16 MiB diagnostic payload limit before allocation, reads from one captured bounded length, and rejects shrink/growth while reading. This is a diagnostic input budget, not a production archive, chunk, save, network or streaming limit. The host's command inspector similarly enforces the command codec's 16 KiB limit and reports structure only. Return codes are zero for successful inspection/help, one for malformed/unreadable input, and two for unsupported argument shapes. It issues no gameplay receipt.

## Validation and subsequent ownership

The independent Python oracle defines payload bytes, manifest/dependency frame bytes and full version hashes without importing production C#. Core tests check those exact bytes plus payload tampering, dependency pinning, caller mutation, namespace syntax, JSON ambiguity/limits, ordinal behavior under Turkish culture, input permutations, shared dependencies, missing/version-mismatched dependencies, cycles and boundary counts. Executable host probes check positive and malformed command/package inputs, exact independent fixture projections, bounded file rejection, and the real retained closure packet. See the integrated increment evidence for executed counts and source hashes.

P01 must add authenticated admission, atomic durable command/state/receipt writes, replay and receipt-access rules. The content compiler must add typed executable schemas, complete cross-package rule/presentation dependencies, finite custody and source-unit transformations, native content validation and migration scenarios. None of those responsibilities belongs in these immutable metadata values. New schemas or wire representations need explicit versions and independent fixtures; existing vectors must remain readable or receive a deliberate migration contract.
