# Evidence retention review: disposition

The independent critic returned **accept with required changes** for [evidence retention](../evidence-retention/RESULT.md)
at `398e8ecc`. The [returned review](REVIEW.md) and [dispatch record](DISPATCH.json) are committed; the raw session stays
local. Route: exact `claude-opus-5-5[1m]`, Max effort, Read/Glob/Grep only, 1,800 s and $15 limits. It ran 970 s and
79 turns, and the provider reported $5.27. The client's single 16-token Haiku call is auxiliary. No permission was
denied, both context checks verified, and nothing was retried.

The lead re-read each cited path before changing anything. The reviewer's claim that two players can pass an item back
and forth is refuted: a gift from one player to another is refused `NotAuthorized` by the custody transfer rule, as a
probe at genesis showed. Every other checked claim held.

| Finding | Disposition | Where |
|---|---|---|
| 1 Medium-low: a host decision past the memory bound fails the session without stopping the authority | Accepted and fixed. `Decide` proposes a decision whose preflight overflows, so the authority's preview stops it by the documented route. A new session test fills Wren's memory, runs the real session, and shows the session ends with the overflow, the authority refuses further commits until reopened, and the reopened head is unchanged. With the fix removed the test fails: the authority is not stopped. | `LocalCognitionController.cs`; `A_host_decision_past_the_memory_bound_stops_the_authority_through_its_overflow_route` |
| 2 Medium-low: effort-limited gathering adds two reason changes per two swings | Accepted, with the reviewer's planner-only alternative. Policy `cognition.delayed-supply.v3` keeps a gathering plan's own reason, `AwaitingSupply`, while its next swing waits for effort: spent effort delays the swing but does not change why the funded work is deferred. The host policy's reason changes now come only from tool, supply, source and stock events. The rate an alternating planner would reach, and the baseline's duty to report memory growth, are stated in the record, reference and handoff. Recorded v2 decisions replay unchanged. | `CognitionPolicy.cs`; oracle policy text; result "Remaining limits" |
| 3 Low, before freeze: decoding admits unreachable states, one of which can wedge an actor | Accepted for the freeze. The two decoding rules (the held intention equals its latest adoption memory; the current deferral equals the latest memory's reason, policy and instant) and their oracle vectors are scheduled before profile 42 freezes. The memory-segment design must decide whether citations from segments still keep evidence in the record. Not changed now: no lawful transition reaches the wedge. | Result "Remaining limits" |
| 4 Low: what justified a gathering completion is not kept | Accepted as a record change. Decision 2 and the reference say the completion's outcome is uncited and may be replaced, leaving only the journal. Letting a gathering `Completed` memory cite its outcome is an optional freeze item. | Result decision 2; `actors-and-society.md` |
| 5 Low: remembered reasons are unvalidated, and the durable test records false ones | Accepted. The record and fixture call those decisions "admissible planner claims; the host policy never records these". Checking the reasons the actor's own state decides (`InsufficientEffort`, `MissingTool`, `NoKnownSource`, `SourceDepleted`, `AwaitingSupply`) is scheduled before freeze. | `CognitionWorldFixture.cs`; result |
| 6 Low: record wording and test gaps | Accepted. The at-bound root is described as admissible, not reachable, with what admission does not check. The journal-bytes sentence now says the v2 pin governs what decoded decisions do. The `Since` refusal has a test. Deliveries join the growth list. Replaced evidence identities stay reserved in the database's identity table, so the record's identity caveat is corrected. | Result; `A_root_whose_current_deferral_postdates_the_world_does_not_decode` |

## Suggestions

- **Taken:** a test for evidence cited only by a memory after its intention ends, so the citation check cannot quietly
  narrow to the intention; the corrected "empty-handed attempt" comment.
- **Recorded, not taken here:** a decision-frame vector in the Python oracle; a test for a cited observation at a full
  bound; and two items that predate this increment. The host never decides again after a builder's project completes,
  so `WorkFundedProject` gets no live `Completed` memory and that `ActionFinished` wake is never serviced. An unchanged
  intention also ignores a recorded `CompletedCurrent` flag and basis.

## Documentation review

- **Plan/spec:** dispositions the increment's independent review; no requirement, phase exit or gate changes.
- **Reference:** `actors-and-society.md` states the host's overflow route, the gathering plan's kept reason, completion
  provenance and the full growth list.
- **Accuracy/completeness:** every finding and suggestion has a row or bullet.
- **Checks:** `python3 Beta/tools/check_documentation.py --work-record Beta/implementation/p02/npc-cognition/evidence-retention-review/DISPOSITION.md --files`
  with the affected pages.
