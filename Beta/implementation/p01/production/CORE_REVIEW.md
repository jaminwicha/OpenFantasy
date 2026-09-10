# Independent production and world-boundary review

The reviewed production domain and its world integration have no blocking finding within this bounded inspection. The reviewer authored the separate farm content admission, so this verdict concerns the other worker's production implementation and the coordinator's world integration; it is not an independent review of that admission or of the Hosting persistence implementation.

## What was inspected

The exact source, test, fixture, generator and contract hashes are in [CORE_REVIEW_EVIDENCE.json](CORE_REVIEW_EVIDENCE.json). Static inspection covered the immutable material and crafting definitions, counted-item/physical bindings, allocation matrix, ingredient requests and retirements, output budgets, batch dependency graph, identity registry, pure planner and strict canonical decoder. It also covered the world's crafting resolver, all-action capacity admission, retired gathering cursor checks, profile-three wire boundaries and pure profile-two migration. New component tests and the world production tests were read; this reviewer ran no managed build or game process.

The coordinator reported **448 Core Debug tests and 143 Hosting tests passing**, including the new production and admission cases. Those are coordinator execution results, not a claim of separate execution by this reviewer. The native presentation and copy-and-publish save qualification are separate work and are not established by this report.

## Findings and disposition

| Boundary | Result and evidence |
| --- | --- |
| Composition | Each allocation column equals one output unit's mass. Each allocation row, multiplied by output counts, equals the full consumed input material. Item pins remain distinct from physical calibration pins. One item cannot acquire conflicting physical calibrations through another recipe. |
| Raw rights | Retired raw intervals remain disjoint from other retirements and current raw lots. Consumed prefixes cannot be spent again or relabeled as outputs. Partial remainders preserve identity and advance revision. The source admission budget remains unchanged. |
| Derived rights | Every output budget is fully covered by nonoverlapping live or retired intervals. Waste cannot disappear. Chained recipes consume intervals from an earlier batch; dependency cycles cannot bootstrap free stock. Raw material is retired only once even when a product is processed again. |
| Access and rejection | Control is checked before private revisions and ingredients. The actor must be the storage's explicit withdrawal owner; there is no separate omitted withdrawal flag in the current policy. Protection and reservations reject selection. Current/historical identity conflicts, stale selections and capacity failures discard the whole candidate. |
| Shared capacity | Crafting accounts for raw remainders, products and waste together. The world also checks that same combined count capacity after gathering and ordinary inventory actions, preventing a bypass through another action family. |
| Gathering continuity | World admission checks retired raw intervals against the node's issued cursor, in addition to live raw lots. Crafting cannot make a previously issued interval disappear from the evidence used to reject cursor rewind. |
| Identity roles | Item, physical material, physical recipe, craft recipe, batch, lot and command roles are joined explicitly. Pure planner IDs remain subject to the world transaction's current and historical collision checks before publication. |
| Migration | Migration pins the actual old head and admission. It introduces empty production state and fresh definitions, increments commit revision once, and preserves inventory, space, residents, calendar, experienced time, advancing steps, dialogue and gathering state. It does not reset finite sources or generate rations. |
| Wire boundary | Nested byte/count bounds precede expansion. Known versions, tags, widths, enums, pins, ordering and full reconstruction invariants are checked. Exact re-encoding rejects noncanonical order. Component and world profile versions remain separate. |
| Volume scope clarification | Closed by the final [CORE_CONTRACT.md](CORE_CONTRACT.md): checked per-term volume products are not an occupied-volume storage model. Storage is still count-limited, and tools without physical calibration do not acquire invented volumes. No runtime change was needed. |

## Independent executable check

[verify_core_review.py](verify_core_review.py) reproduced the frozen ledger generator's complete stdout byte for byte, checked each positive vector's declared byte length and SHA-256, and independently decoded the positive recipe/ledger frames. Its separate integer checks verified allocation rows and columns, each batch's mass balance, raw-interval disjointness, and complete contiguous output budgets across the first craft and chained recipe. Both nonempty examples preserve **90,000 mg** from retired raw material into live product/waste lots, including the chained case where an intermediate unit is retired rather than counted twice.

This Python check evaluates the positive fixtures. The malformed vector count in the evidence is an inventory of declared test inputs; rejection of those frames by the C# decoder belongs to the coordinator's passing tests. The reviewer did not claim to execute C# from Python or prove an unbounded graph space.

## Practical limits retained

The current ledger has explicit recipe, batch, lot, selection and wire limits. Reaching them must reject or apply backpressure; neither retirement history nor waste may be pruned to make room. The component verifies local consistency, while authenticated replay and durable admission establish that the recorded ingredients were actually available. Native crafting feedback, eating/healing, packaging return, volume-based placement, timed jobs, production-scale history storage and cross-platform persistence need their own later qualification. These limits do not weaken the mass and source-rights checks established here.

`CraftRequest.ExpectedProductionRevision` currently pins the global production ledger. Two independently controlled actors crafting from the same public snapshot in one batch can therefore receive first-success/second-revision-conflict even when their inventories are disjoint. That is safe rejection under the existing rules, but is not a satisfactory fairness guarantee for the planned 1–4-player world or crafting NPCs. The present local client has one player and the NPC controller does not craft. No inspected production test claims that simultaneous independent crafts both succeed; the unrelated counted-authority concurrency test proves retry deduplication, not crafting fairness.

Before admitting multiple active crafters, a later explicit rules profile should qualify either a batch-start production expectation while planning each action against sequential candidates, or a suitably scoped actor/work revision. It must still check exact recipe pins, owned ingredient/storage revisions, shared capacity, identity collisions and production limits, and retain historical profile-three behavior during replay. Simply ignoring the existing expectation would change recorded command semantics. This is a future multiplayer/NPC acceptance requirement, not a reason to alter the qualified local profile silently.
