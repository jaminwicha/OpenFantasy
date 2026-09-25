# Accountable item consumption

This dependency supplies portable C# material candidates for the
[finite-aid design](../encounter-choice-validation/RECOVERY_DESIGN.md). It does not yet treat a
character, change HP, publish a world, expose a Use/Aid action or close a recovery gate.
The owner's pure-C# decision remains fixed: portable Core, in-process Hosting and Godot C# client.

## Contract

An authenticated acting actor selects an exact owned, unprotected, unreserved raw or crafted lot
prefix with current storage/production/lot revisions and an exact definition pin. The candidate
retains the consumed interval, command-derived identity, acting actor, recipient and custodian.
It returns both material owners together; a refusal preserves both original owners and creates no
claim. It fabricates no bottle, residue, source entitlement or output to keep accounting balanced.

Production V2 explicitly retains consumption alongside recipes, batches and live products. Produced
budgets remain completely partitioned; raw claims remain finite and disjoint. Raw issuance completeness
still belongs to complete source/history authority, because a generic source's capacity is not an
issued cursor. Subsequent crafting retains every consumed claim. V1 bytes and interpretation remain
separate, and V1 refuses a consumption-enabled owner even when its claim collection is empty.
Existing `WorldSnapshot` profiles reject that owner until complete care admission exists.

The [architecture reference](../../../Runtime/docs/architecture/item-consumption.md) describes the
exact types, invariants, limits and publication boundary. The
[review](REVIEW.md) records the critic finding and bounded disposition.

## Qualification

The [qualification report](qualification/results.json) records a clean solution build and **1,849
passing managed cases: 1,200 Core and 649 Hosting/client**, zero failed/skipped. This includes 54 new
cases. The focused production and source-supply checks passed 115 + 4 cases before the full serialized
regression. Hosting took 10 m 15 s; this is correctness evidence, not a production performance claim.
The two initial test-code build failures are retained alongside the corrected build.

Both new V2 and existing V1 independent generators reproduce their retained fixtures byte-for-byte.
All 47 prior milestones, 22 frozen specification hashes and the exact unrelated 19-file predecessor
index are preserved. No native or external-process recovery experiment accompanies this domain-only
increment. No external model was invoked. The development checkout remains `codex/runtime-foundation`.
Qualified commit `04e6dba14351b9cf0836a9ac76aa785a2003e364` is preserved on `codex/milestone-item-consumption`.

The retained-content fixture uses the original Potion payload (40 HP / 0 MP) and actual three-berry
recipe. Physical mass is explicitly authored for this test: 20,000 mg per berry and 60,000 mg per
Potion. Its finite two-Potion/six-berry supply has 240,000 mg, conserved through raw consumption,
crafting, crafted consumption, component archive restoration and another craft. Ancestral recipe
ingredients are backing, not a second mass contribution. These are component operations using real
source content; no Host process, saved game or physiological effect is implied.

An independent Python generator constructs four exact V2 byte/hash vectors using retained independent
V1 recipe bytes as data: enabled-empty, raw consumption, produced consumption and interleaved
raw-consume/craft/produced-consume. It also constructs overlapping-raw and dropped-output archives
that must reject. The C# tests compare actual planner candidates against those bytes.

## Remaining integration

Implement fresh profile25 with prior care agreement, exact defeat acknowledgment, actual nearby
neutral helper, body/reach/readiness checks, finite supply and one atomic health/debt/phase effect.
Keep the lethal attack record; add the explicit resource supersession witness required by admission.
Bind every consumed claim to an actual care event, complete identity reservations and issuance
history. Reconcile construction/terrain/excavation/watershed material consumers before admitting V2
in a complete world. Add versioned world encoding and durable replay, then real crash/reopen/native
acceptance. The original profile24 and older saves retain their interpretation.

This foundation does not imply a general metabolism, food loop, disposal process, medical system,
source-mass simulation, infinite history capacity or accepted balance. The bounded 256-claim owner
must not silently discard history when full. All production scale, campaign, art/device/platform and
full P00/P01/P02 gates remain open; the full plan through P14 remains authoritative.
