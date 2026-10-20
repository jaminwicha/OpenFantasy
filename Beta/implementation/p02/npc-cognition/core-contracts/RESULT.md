# NPC-01 core cognition contracts

**Status:** storage-independent Core contracts implemented and tested. This is step 1 of the
[NPC-01 implementation sequence](../../../NPC_01_CAUSAL_EPISODE.md#implementation-sequence-and-acceptance):
lawful evidence and typed actor cognition, defined against [the PLAN-STATE exit decision](../../../PLAN_STATE_EXIT_DECISION.md)
as accepted after independent review. Its findings R4–R7 changed these contracts while they were written: decisions
carry a policy version, memory keeps its evidence links, a reconsideration wake persists, and overflow stops the
authority. No world profile, save field, host policy or native behavior changes. NPC-01, P02 and every gate stay open.

## What exists

Code lives in `Runtime/src/OpenFantasy.Core/Cognition/`.

- **Evidence** is one fact an actor acquired from a committed outcome. It records the command that caused it,
  the counterpart for a received item or delivery, the subject, the quantity and the world instant.
  A received item or delivery must name someone other than the knower and a positive quantity.
  Identities derive from the causing command and a knower ordinal in a reserved spawn range.
- **Actor cognition** holds the actor's own evidence in canonical acquisition order, one current intention,
  chronological memory and a pending wake. Foreign evidence, an intention or memory citing evidence the actor does
  not hold, and out-of-order entries are rejected. An intention cites at most eight pieces of evidence.
- **Memory explains the episode.** Each adoption keeps the evidence it rested on; every entry keeps its outcome,
  reason and the policy version that decided it. Gathering that stops because the stock target is met is
  remembered as completed, not superseded.
- **Bounds never forget.** Sixty-four evidence entries and sixty-four memories are finite development bounds. A
  change past either throws a capacity exception and leaves the cognition unchanged; the world binding must turn it
  into a boundary rollback and authority stop, never a rejected action. They are not character-depth caps.
- **Wakes** live in the advancing-step domain. Acquired evidence makes reconsideration due at its step unless an
  earlier wake is pending; own-resource changes and finished actions schedule the same way. Suppression keeps the
  due step and cause, and survives later acquisitions until lifted. A decision made during a reading hold services
  nothing, so a delivery received inside a held conversation keeps its wake for after the hold.
- **Beliefs are derived, not stored.** Known sources, believed depletion, delivered quests and gathered
  quantity are computed from held evidence, each pointing at its supporting evidence. A changed fact elsewhere
  changes nothing until new evidence arrives. A newer observation replaces an older depletion belief.
  A zero-yield attempt at an unobserved source does not create a known source.
- **Recorded decisions.** A decision names its policy version, intention, deferral and next fallback wake, one
  hundred advancing steps later. Applying an unchanged decision moves only the wake; an identical one changes
  nothing. Replaying recorded decisions without the planner reproduces the same bytes.
- **Decision policy** `cognition.delayed-supply.v1` is deterministic. A reading hold keeps the current intention.
  Known delivery of the funded project's quest means working the project, citing that delivery.
  Otherwise Wren waits with the first honest reason: stock target met, missing tool, no known source, all known
  sources depleted, none reachable, or no effort. Otherwise she gathers at the first reachable, undepleted known
  source in identity order. Re-deciding keeps the original adoption time, and an unchanged decision changes nothing.
  The policy proposes; ordinary commands still decide every effect.
- **The facet** holds one cognition owner per actor, at most sixty-four actors, matching the local spatial bound.
- **Codec** `cognition.snapshot.v1` is strict and finite. Optional values are empty fields, the knower is implied
  by the owning actor, and decoding accepts only the canonical byte form.

## Verification

| Check | Result |
|---|---|
| Cognition tests, focused | 28 of 28 pass |
| Full Core suite | 2,069 of 2,069 pass, no build warnings |
| Independent Python oracle | 2 valid vectors match byte for byte; 27 invalid vectors all refused |

The [oracle](../../../../Runtime/tests/OpenFantasy.Core.Tests/Cognition/generate_cognition_vectors.py) writes frames
without the C# code. A one-off diagnostic, since removed, confirmed each invalid vector fails for its own reason:
unknown version, actor or evidence order, duplicates, unknown kind, missing or self counterpart, empty delivery,
unheld intention or memory basis, a basis on an ending, unsupported work, memory order, unknown reason, empty or
invalid policy text, unknown wake cause or suppression, an out-of-range instant, the evidence bound, a short
identity, and trailing or truncated bytes. A fully bounded facet of sixty-four actors with maximum values fits the
declared byte bounds and round-trips: an actor record is at most 48 KiB, below the 64 KiB resident record bound.

The pre-commit verification pass found one defect, now fixed with a regression test: a held decision replaced a
pending evidence wake with a later fallback, delaying the response and losing its cause. A scan found no wall
clock, randomness, hashing or unordered iteration feeding any output.

The focused report is committed under `managed/`. The full report is 3.1 MB, so it stays local and is hashed in
[the evidence manifest](EVIDENCE_MANIFEST.json).

## Not done here

- No world interpretation after profile 41, admission, save field or migration. That binding follows the
  reviewed decision and needs evidence acquired atomically inside committed steps.
- No host policy reads cognition yet. The existing local NPC and construction controllers still read the whole
  world snapshot; replacing that read is the next binding step.
- Disclosure to other audiences and references to accepted commitments owned elsewhere are not modeled, nor
  are the design's "occupied opportunity" and "existing commitment" deferrals. They are added with the binding
  test that needs them.
- Whether servicing an unchanged decision rides an advancing step that commits anyway or stays transient is a
  binding choice; the decision forbids an extra commit for it.
- The lossless retention path for step 3, belief history and closed memory segments, is not built.
- Observation range is not defined. A source-observed fact must come from an admitted perception rule, which
  does not exist yet.

## Documentation review

- **Plan/spec:** implements step 1 of NPC-01's sequence only. No requirement, phase exit or gate changes.
- **Reference:** the ownership contract's extension boundary records the cognition facet once the decision is
  accepted; this record names the Core types and their bounds.
- **Accuracy/completeness:** counts come from the committed focused report and the hashed full report; the
  oracle and the codec were run as committed.
- **Checks:** `python3 Beta/tools/check_documentation.py --work-record Beta/implementation/p02/npc-cognition/core-contracts/RESULT.md --files`
  with affected pages, and `git diff --check`.
