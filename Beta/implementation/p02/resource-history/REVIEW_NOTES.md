# Lead review and integration constraints

The native read-only writer cross-check found no contradiction between the candidate and current
resource writers. This is source review, not execution evidence or an independent Fable verdict.

- `WorldStepProcessor` chooses one world sequence before iterating deterministically ordered actions.
  Multiple successful rewards can advance progression within that step. History positions use the
  actual ordered action index; rejected or non-resource actions may leave gaps.
- Encounter resolution is an actionless step. `WorldEncounterActionProcessor` resolves the retained
  earlier decision command at a later world sequence. A history batch must cite that command, not
  invent an action from the empty step action list.
- Damage and caster MP cost revise two actors together but advance progression once. Count batches,
  not actor effects, for the progression lower bound.
- Rewards revise the actor even when XP is zero or HP/MP do not change. Their entire revision belongs
  in the chain; a level gain includes refill and capacities in that one revision.
- Later material/geography admission may reject a prepared reward. History must roll back with all
  owners. Preparing an effect is not permission to append it to durable history.
- A command identity is unique within this segment. Delayed/repeated effects need distinct owned
  process-occurrence identities referencing their continuing cause, not repeated original commands.

The lead additionally inspected the codec diff: the old progression codec changes only three
`private` members to `internal`, allowing canonical actor-frame reuse. Existing formats and public
entrypoints retain their implementation. The new archive requires separately admitted basis bytes
and world boundary; a caller cannot use the archive as its own trust root.

The mathematical-consistency test deliberately admits a fabricated external cause reference. This
establishes the standalone component's limit, not a weakness permitted at complete-world admission.
Causal evidence, exact amounts/rules, permissions, RNG and journal replay must be authenticated by the
world integration. Currency, definition/roster migration, process scheduling and archive continuation
are also outside this component. No complete-world profile, durable storage schema or gameplay gate
is enabled by this increment.

## Independent critic disposition

The [Fable report](fable/REVIEW.md) found no implementation defect in the six runtime files. Accept
its two P2 test gaps: a same-record evidence/rule alias was rejected before reaching the intended
cross-record check, and zero-XP plus forged level-up refill lacked direct coverage. Five additional
cases now cover both rule-collection orders, zero-XP archive continuity, forged refill/after-level,
basis-sequence equality, insufficient MP and numeric overflow.

Accept the P3 documentation clarifications: only batch command identity uniqueness is enforced
locally; causal evidence can repeat and process-occurrence authentication belongs to the world.
Segment anchors are completed-step boundaries. Direct domain callers must handle argument, unpaid
magic and overflow refusals. These corrections do not change any runtime behavior or archive bytes.
No second ceremonial critic is needed for the new tests and wording; the lead reviews them and
reruns the affected Core suite. Hosting qualification retains the identical runtime candidate.

The read-only run completed in 442.041 seconds with verified pre/post context, no permission denials,
and an estimated list-price cost of $4.6534395. The returned primary model was `claude-fable-5-1`,
1,000,000 context, requested Max effort. Native CLI accounting also reports a small Haiku utility
call; no implementation/review fallback was selected. These estimates do not establish actual
subscription-credit deductions or net savings. The review provided targeted coverage and boundary
clarifications; it did not discover a production-code bug or execute tests.
