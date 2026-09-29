# Worker result — consented format qualification

**Outcome: delivered with open gaps. No runtime defect was exposed.** Runtime sources, old tests,
goldens, fixtures and runners were not edited. No git, network, nested agent or extra Bash use.
This is codec/archive test evidence only; it closes no Host, native, retained-save, phase or
production gate and does not freeze the formats. New test code still needs Astra's integration review.

Run facts: model `claude-fable-5-1`. Context size and effort are not verifiable from inside the
session. Dispatch 08:46:49Z; last accepted check finished about 09:06:40Z. No permission denial.

## Files

| Path | Change |
| --- | --- |
| `format_vectors.py` | new standalone generator, `--write` / `--check` |
| `Core.Tests/Fixtures/consented-world-formats-v1.json` | new; SHA-256 `51bf661f…c372f2`; 11 vectors, 7 compositions |
| `Core.Tests/Worlds/ConsentedWorldFormatTests.cs` | new, 6 cases (+ fixture reader) |
| `Core.Tests/Production/ConsentedMaterialFormatTests.cs` | new, 4 cases |
| `Hosting.Tests/Care/ConsentedWorldCodecQualificationTests.cs` | new, 5 cases in the existing partial class |
| `OpenFantasy.Core.Tests.csproj` | one added `None Update` copy item |
| `OpenFantasy.Hosting.Tests.csproj` | one added linked `None Include` copy item |
| `COVERAGE.md`, `WORKER_RESULT.md` | new |

## Executed checks (all under `qualification/`)

| Check | Evidence | Result |
| --- | --- | --- |
| `--write`, then `--check` ×2 | console | match |
| build | `format-build-20260929T090356Z.json` | accepted, 26.2 s |
| focused | `format-focused-20260929T090431Z.json` | 87 passed |
| journey | `format-journey-20260929T090457Z.json` / `.log` | **failed: 27 passed, 2 failed** (retained) |
| build | `format-build-20260929T090549Z.json` | accepted, 9.6 s |
| journey | `format-journey-20260929T090604Z.json` | 29 passed (24 existing + 5 new) |
| focused | `format-focused-20260929T090624Z.json` | 87 passed (77 existing + 10 new) |

The two failures were errors in my new test code, not runtime behaviour: a cut was attempted at
revision 0, and a helper assumed a position-reversed record still decodes as a standalone history
(the history contract already refuses it). Both were corrected in the test file only.

**Not run:** the full Core (1,664) and managed Hosting (196) suites. They are outside the permitted
invocations, so their pass is not re-verified by this assignment. The changes are additive, but
both project files were edited; Astra should run the full regressions before integrating.

## V10 operation-frame review (previously excluded)

No defect found. Observations for Astra, none verified beyond the files listed in COVERAGE.md §2:

1. `MaterialOperationCodec.cs:115-130` decodes the consent payload without checking that the
   acceptance binds the command or that the moved lot is the approved lot or its derived child.
   The world evidence constructor enforces both (`WorldConsentedTransferEvidence.cs:26-37`). Three
   hostile frames of this kind are refused by structure or by `ValidateForAuthority`; the test does
   not pin which layer, and I did not read `ConsentedMaterialApproval` or the replay.
2. `ProductionCodecV10.cs:4-6` says decoding re-decides every approval. The existing balanced
   custody test shows decode admits frames that only `ValidateForAuthority` refuses. The comment
   may overstate; decode alone is not sufficient for any consumer. Suggested correction is wording only.
3. `MaterialOperationHistory.cs:25-30` keeps command and acceptance identities distinct across
   roles, but not spawned child identities, unlike `WorldConsentedMaterialHistory.cs:31-32`.
   Uniqueness of children rests on the derivation and the permanent identity lookup.

## Recorded boundaries (executed, deliberate)

- Pre-cut profile-27 bytes re-framed as 28 with empty history are admitted as structure (critic L1 confirmed by execution).
- A re-hashed closing root with a recomputed reference is admitted with a different anchor identity.

Both are Host authentication duties, not migrations. Details: COVERAGE.md §4.

## Remaining gaps

- Evidence vectors enter through the decoder; there is no construct-from-facts encoder proof.
- Compositions leave every legacy owner payload and the production context frame opaque.
- Open: 65 movement samples, maximum-size roots, protected stock across a cut (critic 5),
  identity lookup from archive and journal (critic 6, Host scope), V10 bytes under a complete-world context.
- Several refusals are asserted by exception type only where I had not read the reader's messages (listed in COVERAGE.md §2–3).
- Archive reopen cost (critic L2) and durable profile-28 Host binding remain unmeasured and unimplemented.
