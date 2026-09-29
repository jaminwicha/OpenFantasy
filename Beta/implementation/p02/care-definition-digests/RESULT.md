# Care definition digests: worker result

Status: **implemented; build and Care-filtered tests pass**. Unstaged and uncommitted for Astra's
review. No measured saving is claimed: no profile, recovery or crash run was executed here.

## Owned changes

| Path | Change |
| --- | --- |
| `Beta/Runtime/src/OpenFantasy.Core/Care/RestorativeRemedyDefinition.cs` | `Version` retains a lazy digest in `_version` |
| `Beta/Runtime/src/OpenFantasy.Core/Care/RestorativeUseDefinition.cs` | `Version` retains a lazy digest in `_version` |
| `Beta/Runtime/src/OpenFantasy.Core/Care/CareAgreement.cs` | `Version` retains a lazy digest in `_version` |
| `Beta/Runtime/src/OpenFantasy.Core/Care/CarePermission.cs` | `GrantVersion` and `Version` retain separate lazy digests |
| `Beta/Runtime/tests/OpenFantasy.Core.Tests/Care/CareDefinitionDigestTests.cs` | New; six tests |
| `Beta/implementation/p02/care-definition-digests/RESULT.md` | This record |

Each getter follows the `WorldCanonicalFrame` idiom: `Volatile.Read`, a visible guard block, the
unchanged encoder call (`CareCodecV1.EncodeRemedy`/`EncodeAgreement`,
`CareCodecV2.EncodeUseDefinition`/`EncodePermissionGrant`/`EncodePermission`), then
`Interlocked.CompareExchange`. Nothing is computed in a constructor. A throwing first read publishes
nothing, so a later read encodes again. Constructors, validation, input copies, derived properties
and every codec are untouched. There is no static, identity-keyed or shared cache and no new type.

`CarePermission.Revoke` still constructs a new owner through the public constructor. That owner
starts with both digests empty and encodes its own facts; no digest is passed from the original.
The complete `Version` is encoded by `EncodePermission` from bytes, not from the retained grant digest.

## Evidence

| Step | Outcome |
| --- | --- |
| Solution build, exact assigned command | Succeeded, 0 warnings, 0 errors, reported elapsed 00:03:52.50 |
| Care-filtered Core tests, exact assigned command | **137 passed, 0 failed, 0 skipped**, 8 s |

TRX: `qualification/care-worker-01.trx`. It is the only run; no test failed, so no repair or
incremented log exists. All six `CareDefinitionDigestTests` cases appear in it as `Passed`.
The build produced console output only; no build log file was retained.

Tests take expected digests from record bytes cut out of complete snapshots written by the public
`CareCodec`/`CareCodecV1`, because Core exposes no internals to its tests. They cover:

- cold and repeated reads of all five getters, active and revoked, in both getter orders;
- eight barrier-released readers over 32 rounds per getter returning one published instance;
- revocation: complete version changes, grant version preserved, original object unchanged,
  a second revocation position differs, and a revoked owner with an equal grant digest still
  refuses later assistance;
- list and array mutation before the first read and after publication;
- same identity with differing HP, range, item, delivery, forms, remedy pin, patient, helper or
  definition pin yielding pairwise distinct digests;
- encoded bytes identical before and after digests are retained, and restored owners matching.

## Mutation and invalidation inspection

No real concern found. Every encoded fact is get-only. `EntityId` is a readonly record struct.
`ContentHash`, `CountedItemDefinition` and `RestorativeFormBinding` are sealed records with get-only
properties. `CarePermissionRevocation` is a sealed class with get-only properties. `SupportedForms`
wraps a private array copied in the constructor. The four owners are sealed classes, not records,
so no `with` or copy constructor can carry a retained digest into an owner with other facts.

## Residual risks

- **Reference identity.** Repeated reads now return the same `ContentHash` instance instead of a
  new equal one. `ContentHash` has value equality. Callers outside the owned paths were not
  searched for reference comparisons of these versions.
- **Failure retry is untested.** With validated constructors no encoder failure was reachable
  through public types, so the no-cached-failure path is established by reading the code only.
- **Contention is probabilistic.** The concurrent test cannot force a particular interleaving.
- **Unmeasured.** Getter call counts, allocation, recovery time and the unchanged 30-second
  deadline remain unqualified. Full Core, Hosting, native, profile and crash runs were not executed.
- **Retained memory.** Each owner keeps one digest, a permission two, for its lifetime.
- **Architecture reference.** `ordinary-care.md` and `aftermath-care.md` do not describe how
  versions are computed. Canonical bytes and state are unchanged; Astra owns any note.

## Scope, permissions and limits

No permission denial occurred. Only owned paths were edited; the assigned test command created
`qualification/`. Nothing was staged, reset, committed or subdelegated.

The build alone reported 3 minutes 52 seconds. Total elapsed time and client cost are not visible
to the worker, so the run may have exceeded its 900-second allowance; take both from client evidence.
