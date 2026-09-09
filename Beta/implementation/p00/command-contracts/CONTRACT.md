# Command and receipt contract, version 1

This retained P00 increment turns the [transaction design](../../../spec-v1_1/TECHNICAL_DESIGN.md#4-transactions-saves-and-recovery) into bounded portable C# values and canonical byte formats. It implements parsing, invariant diagnostics and comparison of supplied retry data. It does **not** implement authentication, permission checks, inventory mutation, reservations, scheduling, receipt storage, SQLite commit or durable exactly-once behavior. P01 must prove those operations together against actual state.

Application types live in `OpenFantasy.Core.Commands`. Each immutable type has one responsibility: submitted intent, revision expectations, content fingerprint, commit identity or recorded result. Pure codecs own framing. No Godot reference or transport/session service enters the core.

## Envelope and identity decision

`CommandEnvelope` contains protocol version, command identity, principal claim, original submission-session claim, controlled actor claim, world, branch, expected revisions and one typed `TransferLotIntent`. These are **claims**, not verified host context.

The retry key is **(world identity, principal identity, command identity)**. Its content fingerprint is full SHA-256 of the complete canonical envelope, including the original session, branch, actor, every revision and every payload field. The constructor canonicalizes revision input order; changing only input enumeration order does not change content. A repeated revision owner rejects even if its value is identical. Decoders require already-canonical order instead of silently rewriting wire input. Distinct definition, entity and authority namespaces must not be inferred from a bare string or field position.

The coordinator reviewed and accepted this reconnect rule before implementation:

- **Initial admission:** the host authenticates the current delivery session and principal and binds the submitted original-session claim to that verified context. It separately checks world access, actor control, knowledge and gameplay permissions.
- **Reconnect/retry:** resend the original envelope unchanged. A new authenticated delivery session is external to the envelope; rewriting `OriginSessionId` changes the content and conflicts. The host authorizes receipt lookup using the authenticated principal and permitted world/receipt scope. Losing or transferring actor control after the original attempt does not automatically prevent the principal from reading its own permitted prior receipt. The original session need not remain connected.
- **Existing receipt:** an exact authorized retry returns the original recorded receipt, including a committed rejection. Different content under the same key rejects as a conflict and cannot replace the old receipt, reroll the command or issue a new result. Another world, principal or command identity is a different key, not a retry match.
- **No receipt:** absence can be established only by the durable authority; this contract has no dictionary or invented durability layer. A retry identifier must not be reused for a fresh intended action. The host must retain the deduplication identity/receipt for the supported retry lifetime; pruning requires an explicit expired-key protocol instead of executing an ancient retry again.

`CommandReceipt.CompareRetry` compares a supplied immutable receipt and command only. It never performs authentication, receipt lookup or publication. Canonical bytes/hashes likewise do not authenticate commands or receipts. A decoded receipt can be forged outside trusted storage/transport. The host must validate provenance and access before returning its contents. Parsing failure diagnostics are not committed gameplay rejections and must not be used to disclose hidden state.

## Revision and payload scope

`ExpectedRevision` identifies an **entity (1)** or **branch (2)** revision owner, plus an unsigned 64-bit expected revision. Targets are scoped to the envelope's world and branch. Branch expectations must name the envelope's own branch. Ordering is numeric kind followed by canonical entity-ID byte order. The immutable set permits zero through **64** owners, defensively copies the input and stops an overlong enumerable on its 65th element. Revision zero is representable; an omitted expectation is not an automatic authorization or successful compare-and-swap.

`TransferLotIntent` carries a lot identity, source custodian identity, destination custodian identity and a positive unsigned 64-bit **count of indivisible inventory units**. Source and destination must differ. This narrow P00 fixture is an actual typed intent, not unrestricted JSON or executable code. It makes no claim that the lot exists, belongs to either actor, has sufficient units or is splittable. Physical mass/volume, unit compatibility, multi-owner reservations and permitted transformations need their own typed rule/quantity contracts; a count is not silently treated as kilograms or litres. Other command kinds require an explicit supported tag/version and their own bounded validation, not reflection-based dispatch.

## Exact canonical byte layout

The existing [portable primitive framing](../../../Runtime/src/OpenFantasy.Core/CONTRACTS.md#canonical-binary-encoding) remains unchanged: `4f 46 43 00`, UInt32 big-endian **framing version 1**, UInt32 big-endian count including tag, then UInt32 big-endian byte length and bytes for each field. Tags are exact UTF-8 bytes. Every numeric field below is exactly **eight unsigned big-endian bytes**, including the envelope protocol and nested contract versions. Identities are exactly **16 canonical big-endian bytes**; hashes are all **32 SHA-256 bytes**. There are no optional positional fields, native-endian integers, separator concatenation or implicit Unicode normalization.

| Tag | Ordered fields after tag |
|---|---|
| `command.envelope` | protocol `1`; command ID; principal ID; original session ID; controlled actor ID; world ID; branch ID; entire expected-revisions frame; entire transfer-lot frame |
| `command.expected-revisions` | contract `1`; zero or more entire expected-revision frames in canonical order |
| `command.expected-revision` | contract `1`; target kind; target ID; expected revision |
| `command.transfer-lot` | contract `1`; lot ID; source custodian ID; destination custodian ID; positive count |
| `command.receipt` | contract `1`; command ID; principal ID; world ID; branch ID; command content hash; positive world commit sequence; receipt status; rejection reason |

The envelope maximum is **16,384 bytes**; expected-revision set **12,288**, individual expected revision **128**, transfer payload **256**, receipt **1,024**. These caps are checked before copying/decomposing untrusted payloads; each nested reader also limits field counts and checks its exact required fields. A tag is at most 64 bytes. Canonical framing/schema versions and unknown kinds/statuses reject. Duplicate extra positional fields, unsorted/repeated revision targets, bad widths, contradictory payload/receipt fields, trailing bytes and truncated lengths/content reject. No decoder salvages partial commands or discards unknown fields. Exact lengths written by current known layouts can be smaller than their safety caps.

`CommandFormatException.Failure` supplies one of `LimitExceeded`, `Truncated`, `InvalidFrame`, `UnsupportedVersion`, `UnknownType`, `InvalidValue`, `NonCanonical`, or `TrailingData`. These classify bounded contract failure; they do not establish gameplay legality or freeze external user-facing wording. Changing a format or enum meaning requires explicit version handling and retained migration vectors.

## Receipts and immutable result identity

`CommandCommitIdentity` contains world identity and a positive unsigned 64-bit sequence. Sequence zero is not a committed result. A receipt's world must match its commit. The sequence is authority ordering, not calendar time, a client clock or an assertion that merely constructing the value persisted anything.

`CommandReceiptStatus` encodes **Applied (1)** or **Rejected (2)**. Applied requires reason **None (0)**. Rejected requires exactly one supported reason: **RevisionConflict (1)**, **NotAuthorized (2)**, **ResourceUnavailable (3)** or **InvalidAction (4)**. This is a bounded initial outcome vocabulary, not a claim that all eventual domain failure details already exist. `NotAuthorized` is usable only after the host has established whether a safely scoped receipt can be recorded/exposed; it is not permission to echo attacker-selected identifiers or hidden knowledge.

The command key, branch, content hash, commit sequence, status and reason are immutable. `ResultId` is the first 16 bytes of SHA-256 over the entire canonical receipt; it is derived rather than embedded in its own hash input. The original canonical bytes and full command hash remain available. As with entity identity, an authority must reject a detected identity collision instead of conflating results. A pure comparison also checks receipt branch against the submitted command, so a malformed stored result cannot match merely by carrying the original content hash alongside a different branch.

An applied/rejected factory or receipt decoder constructs a **value describing a supplied commit**. Only the future durable writer can establish that the commit occurred. P01 must atomically store receipt, ordered events, entity deltas, scheduler/RNG changes and outbox before publishing success; a crash-before/after-commit test and replay continuation must prove retry behavior. This increment does not reduce those exit gates.

## Independent vectors and executed checks

[Python fixture generator](../../../Runtime/tests/OpenFantasy.Core.Tests/Fixtures/generate_command_vectors.py) uses only `struct`, `hashlib`, `json` and other Python standard-library utilities. It independently spells every frame and field, then prints JSON to stdout for comparison with the frozen [commands-v1.json](../../../Runtime/tests/OpenFantasy.Core.Tests/Fixtures/commands-v1.json). Verification never overwrites the expected fixture. Tests consume those constants rather than calling the production codec to produce expected values.

The retained command is 649 bytes; applied and rejected receipts are each 195 bytes. Fixtures include **23 malformed command cases and 8 malformed receipt cases**, plus a shape-valid receipt with an inconsistent branch for comparison diagnostics. Tests cover every incomplete prefix of the 649-byte command and 195-byte receipt, over-limit frames, 64/65 revision boundaries, early termination of an unbounded source enumerable, duplicate targets, canonical permutation equality, maximum quantity/revision, empty expectations, immutable input/output copies, unchanged rejection receipt, changed-content conflicts, distinct retry keys and receipt-status/commit consistency.

Local qualification uses the pinned `/Users/alucinor/.local/share/openfantasy/toolchains/dotnet-10.0.401/dotnet`, matching `DOTNET_ROOT`, with `dotnet test Runtime/tests/OpenFantasy.Core.Tests/OpenFantasy.Core.Tests.csproj --no-restore --filter FullyQualifiedName~Commands` from the planning directory. Test results are recorded by the coordinator's integrated evidence packet; a targeted initial run passed 22 tests before adding the branch-consistency regression. The coordinator subsequently passed all 92 core tests in both Debug and Release, including all 23 Commands tests; see the [integrated increment evidence](../contract-increment/README.md). The original targeted result remains an earlier 22-test observation.

Remaining evidence: durable storage/recovery, authenticated delivery and receipt permissions, real lot transfer/custody/conservation, state revision conflicts, common player/NPC action legality, migration-derived objects and cross-platform execution. None is passed by a round-trip fixture or a diagnostic host demonstration.
