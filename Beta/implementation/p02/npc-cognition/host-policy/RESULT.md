# NPC-01 host cognition policy

**Status:** implemented and tested in a real local session over the reviewed
[profile-42 binding](../world-binding/RESULT.md). Its review fixes are verified with this increment. For the first
time a hosted session drives Wren's cognition: she sees the oak, waits honestly without a tool, accepts an outright
gift, chooses, approaches and gathers a small personal stock, survives a restart midway, and starts the original
funded guard only after the player's real delivery reaches her in person. Profile 42 remains internal and
non-default; no native client, dialogue, presentation or baseline exists yet. NPC-01, P02 and every gate stay open.

## What exists

- **[LocalCognitionController](../../../../Runtime/src/OpenFantasy.Hosting/Simulation/LocalCognitionController.cs)**
  drives host-controlled residents that have an unfinished project of their own. For each such resident, in order,
  it proposes:
  1. a due decision, computed by `CognitionPolicy` from the resident's own tools, stock, effort and known sources,
     and only when it changes something;
  2. a gathering swing preflighted by the ordinary gather planner, plus ordinary approach movement toward a
     standing cell, for a gathering intention;
  3. a look at the first source in sight that the resident has never observed.

  Route and planner checks are physical-feasibility advice that keeps commands from being futile. They never become
  beliefs. The authored motive, a personal stock of two, is a named constant of the host policy
  `local.delayed-supply.v1`.
- **[LocalNpcController](../../../../Runtime/src/OpenFantasy.Hosting/Simulation/LocalNpcController.cs)** takes a
  profile-42 builder's funded commitment from its own `WorkFundedProject` intention. It no longer reads quest
  participation for this, so Wren never starts work she has not learned of. Earlier profiles keep the construction
  controller's quest read unchanged. Cognition actions come before ordinary funded work.
- **Player adapter.** `LocalWorldSession` refuses a recorded decision as a player request: reconsideration is the
  host's autonomous proposal. Players may still inspect sources.
- **Policy v2.** `CognitionPolicy` keeps a gathering plan when only effort is spent, deferring the swing. Version
  `cognition.delayed-supply.v2` is recorded on its decisions; replay applies the recorded v1 or v2 bytes unchanged.

## What the session journey proves

`CognitionSessionTests` submits only the player's requests: the gift, speech, acceptance, delivery and close. It
ticks the real session loop, and every one of Wren's actions and movements comes from the host policy. The test shows:

- the first look and the "missing tool" wait;
- gathering adopted after the gift;
- one swing, then a durable close and reopen with the gathering wake pending and identical after reopening;
- the stock target met under the new session;
- no guard work while undelivered;
- the delivery's reading hold suppressing her wake;
- the funded intention after the hold closes;
- the guard's first work unit, with her two units of personal wood still hers.

## Verification

| Check | Result |
|---|---|
| [Profile-42 world, authority and session tests](managed/hosting-cognition.trx) | 17 of 17 pass, including the review-required refusal cases and the hosted journey |
| [Core cognition tests](managed/core-cognition.trx) | 31 of 31 pass |
| Full Core suite | 2,072 of 2,072 pass in 1 m 20 s |
| Full Hosting suite, with `OPENFANTASY_DOTNET` set | 2,253 passed, 25 opt-in skipped and 10 failed, in 20 m 57 s |
| Independent Python oracle | Regenerated with policy text v2; a second run reproduces the vectors byte for byte |
| Server tests and Godot client builds | No warnings or errors |

The 10 Hosting failures belong to classes already established [in the binding](../world-binding/RESULT.md#verification):

- **6 manifest-dependent result verifiers.**
- **1 fixture exporter** that requires an explicit destination.
- **3 `ControllerPermissionAdapterTests` cases**, which fail identically at unchanged HEAD `69646306`. They are
  pre-existing and undiagnosed here.

With the variable set, the six real-process withdrawal tests pass inside the full run. Raw reports stay local and
are hashed in the [evidence manifest](EVIDENCE_MANIFEST.json). The host exposes no API call count or per-call context.

## Not done here

- **Decision-3 baseline:** profiles 40, 41 and 42 with every NPC autonomous. It is blocked on the evidence-bound
  precondition in the binding's gaps.
- **Player-absent detailed continuation** with a changed-place return.
- **Dynamic dialogue** explaining the episode, native presentation, and human understanding.
- **Other residents and players:** they are not driven, and their due wakes age without service. There is no
  starvation bound.
- **Fallback after completion:** after the guard completes, the final decision clears the intention. A later
  fallback wake is never serviced, because no unfinished project remains.
- **Uncompared hosted runs:** two hosted runs use random command identities, so the restart proof compares cognition
  facts and outcomes, not root bytes. The byte-identical restart comparison is the Core proof in the binding.

## Documentation review

- **Plan/spec:** completes NPC-01 step 2's host side. The roadmap and handoff move the next work to the evidence-bound
  precondition, the baseline and offscreen continuation. No requirement, phase exit or gate changes.
- **Reference:** `actors-and-society.md` describes the host policy, its residents, its proposal order and the
  intention-based commitment. `due-work-and-scheduling.md` states who services wakes and that others age.
- **Accuracy/completeness:** checked against the controller, the NPC controller's profile gate, the session refusal
  and the session journey's assertions.
- **Checks:** `python3 Beta/tools/check_documentation.py --work-record Beta/implementation/p02/npc-cognition/host-policy/RESULT.md --files`
  with the affected pages.
