# Format qualification integration

This increment accepts additional independent vectors and decoder/archive regression coverage.
It changes no runtime behavior, byte contract or acceptance threshold. Formats remain unfrozen;
this is not durable Host, native, retained-save, phase or production acceptance. The immutable
[worker report](WORKER_RESULT.md) and [worker coverage matrix](COVERAGE.md) describe the delivered
state before Astra's corrections below. [Final checks](qualification/FINAL_CHECKS.json) record the
accepted build and selections.

## Integration findings and corrections

- The worker's three malformed V10 approval/result cases allowed either a decoding exception or
  any authority exception. Source review and the corrected executable proof establish the exact
  boundary: all three frames decode structurally, then `ValidateForAuthority` throws the specific
  ordered-consent `ArgumentException`. Wrong moved lot, non-binding acceptance and swapped recorded
  delivery are checked separately against a valid two-gift control. An unrelated exception cannot pass.
- `ProductionCodecV10`'s summary incorrectly said decoding re-decided approvals. Its XML comment now
  distinguishes structural decoding from the mandatory inventory replay. The maintained
  [architecture reference](../../../Runtime/docs/architecture/consented-production.md#ordered-evidence-and-replay)
  already states this contract correctly. No architecture or executable behavior changed.
- Added the omitted profile-28 movement bound: 64 actual distinct samples encode/decode, 65 fail
  the exact decoder and constructor guards, and reordered/duplicate actors fail the ordering guard.
  The worker's independent vector separately covers signed movement. Existing limits are unchanged.
- Both project changes only copy/link the new JSON fixture. Existing source inclusion, references,
  tests, fixtures and runtime dependencies remain unchanged. The Python generator reproduces its
  fixture without importing/invoking runtime codecs or reading their output.

The worker independently read the previously excluded V10 operation codec/history. Its reported
transitive exclusions remain exclusions; Astra additionally traced approval validation,
`ProductionMaterialCodec`, snapshot/continuation validation and ordered replay when resolving the
weak test. This is integration review, not a claim of another independent full-system critic.

## Scope of the format evidence

Eleven complete vectors independently pin terms, V15 commands, the V28 step, world evidence/history
and the V10 consent operation. Seven compositions independently pin framing around explicit opaque
imports. Evidence constructors are internal, so those vectors prove decoding, every named semantic
field, hashing and re-encoding; real planner evidence is covered by the existing world journeys.

The complete-root composition imports legacy payloads by position. It proves envelope structure,
not independent semantic ordering or fixed hashes of those payloads. The worker names their fixture
origin but does **not** provide the assignment's requested fixed import hashes. Production context
hashes are likewise opaque. Do not describe these checks as an independent encoder of a whole world
or a complete format freeze. Preserve this explicit residual rather than manufacturing goldens
from runtime output and calling them independent.

The retained matrix still has these follow-ups before a final format/authority qualification:

- Maximum-size root boundaries and fixed provenance for the opaque legacy imports.
- Protected stock through a cut and attempted retirement (reservation/construction coverage is
  separate and already executed).
- V10 input bytes under a complete-world context; current tests exercise the converse and exact
  context capability guards.
- Durable journal/archive identity reconstruction, genesis/profile pinning and archive reopen cost.
  These need the actual Host owner, not more in-memory round trips.

Other reader-shape negatives assert the narrow format exception type rather than a particular
message. They remain useful corruption checks but do not establish a unique inner failure reason.
No new mandatory gate is invented here and no existing required gate is waived. The four previously
completed whole-world proof groups must not be redispatched.

## Verification and delegation economics

The final build and both relevant selections are pinned to identical sources and assemblies.
All existing 77 consented Core and 24 complete-world cases are retained; the added tests total
11 Core and five world cases. The prior full 1,883-case integration remains historical evidence,
not a newly run full suite. Broad reruns are unnecessary for additive tests, fixture copying and an
XML-comment correction; they remain appropriate when the next Host implementation changes shared
behavior. Native tests are not run for this format-only increment.

Fable returned `claude-fable-5-1`, context 1,000,000; requested effort was Max. Context postflight
verified, with no permission denials, retry or substitution. The run took 1,317.195 seconds with a
$12.83281075 client list-price estimate (not billing or demonstrated net credit savings). It delivered
useful coverage but required the integration correction above and left explicit assignment gaps.
Keep future delegation focused on substantial implementation units rather than repeated broad reviews.

The two original worker failures remain recorded: invalid revision-zero cut setup and a helper that
prematurely decoded deliberately invalid evidence. Both were test corrections; runtime rules were
not relaxed. The worker's original reports remain unchanged.

## Next implementation boundary

Use [durable binding notes](DURABLE_BINDING_NOTES.md) and the
[full integration plan](../consented-custody/INTEGRATION_PLAN.md) to scope a substantial SQLite Host
unit. It must preserve old profile meanings and bind archives, exact receipts, permanent identities
and publication to actual replay. A privileged selected batch does not authenticate two public
participants or implement persisted asynchronous proposals. Track those separate obligations;
do not conceal them behind numeric profile allowlist changes. The original saved personal-dose
world and its 520-dose history remain unchanged and unqualified for the new route.
