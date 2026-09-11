# Admitted watershed persistence

11 September 2026. This P01 increment makes the [prepared basin](../watershed-preparation/README.md)
a retained component of an explicit new composed world. It supports the established-fact and bounded
macro dependencies in [geological design](../../../spec-v1_1/GEOLOGICAL_WORLD_DESIGN.md); it does not
close GEOR-02, RQ-151/RQ-159, a phase or any production gate.

## Contract

`WorldWatershedAdmission.CreateGenesis` accepts an unplayed construction-profile root and exactly one
immutable `PreparedWatershed`. World, branch and realm must match local space. The admission pin
incorporates the previous admission version and exact watershed hash. A played root or already admitted
profile is refused. Existing saves are never upgraded, rewritten or chosen as fallback by this operation.
The ordinary game still selects its existing farm profile and terrain.

Profile six contains a complete profile-five basis plus the canonical watershed archive. Profiles one
through five keep their existing byte contracts. The new step version carries the existing legal actions;
there is no new action codec, clock, authority or database schema. The same SQLite owner commits the
world root, commands, receipts and identity reservations. Publication remains after transaction commit.
Exact recorded-commit history reconstructs the same watershed alongside lived actors and held dialogue.

Basin, initial formation, substrate parcels, internal faces and external terminal receive separate
reserved macro roles. None may alias actors, lots, materials, local terrain, batch causes or commands.
They do not grant extraction rights or own physical water. The immutable identity layer is reused only
when both the watershed and terrain references match; scope is still checked. Ordinary steps retain
the same prepared object rather than rerunning generation.

The host exposes offline `--create-watershed-world <new-save> <construction-genesis> <prepared-basin>`
and `--execute-world-step <save> <host-selected-step>` operations. These require privileged local access
and the existing exclusive lease; they are not authenticated network endpoints. A stale or duplicate
batch fails. `--inspect-recorded-world` now includes the retained watershed's identities/hash/counts.
The existing standalone counted-authority `--create-world` command retains its meaning.

## Evidence

The [qualification summary](qualification/verification-summary.json) records exact inputs, builds,
executed test counts and native reports. The final bounded run passed **560 Core + 323 Hosting
tests** (883 total, zero skips), **74 existing host inspections**, 27 new operator invocations,
both real crash boundaries, and the native foundation/farm checks. The final build had zero warnings
and errors. This is one successful bounded integration run, not the broader full verifier. The new checks include:

- Complete world/step round trips, unchanged legacy bytes, wrong world/branch/realm, identity aliases,
  malformed nested state, and 200 real construction work steps ending in retained physical geometry.
- Actual imported farm content, legal movement into speaking range, addressed dialogue, held-time
  closure, reverse-order exact history, restart and post-restart cause/command collision rejection.
- Valid but independently changed geography in genesis, checkpoint or head, with recalculated row
  hashes, refused by full replay. This is internal consistency, not authenticated whole-history origin.
- The maximum 4,096-column basin through the existing world byte envelope and restart. This is a
  bounded capacity check, not a production throughput, allocation or restore-latency qualification.
- Actual host processes killed at the reported before/after transaction boundaries, then reopened.
  Before commit restores zero; after commit restores exactly one; repeating the batch is refused.
  Remaining real movement/dialogue batches reproduce their expected states in separate host processes.
- Python independently checks the new outer archive and admission-pin composition, retaining every
  inner basis payload byte except the deliberate pin change. This does not independently reimplement
  all existing world rules or replace the earlier watershed algorithm oracles.

The first Core build failed on missing new APIs and a test's attempted use of an internal encoder;
the test now uses its own small frame writer. The first Hosting pass failed an object-identity assertion
across initial serialization: the authority deliberately decodes accepted genesis. The fixture now
compares those canonical bytes, then checks reference identity from the authority's actual state
across commits. No runtime contract, timeout or assertion about within-authority reuse was relaxed.
Both original reports remain retained.

The bounded [critic review](REVIEW.md) found no blocking defect. Its suggested narrow handling of the
existing recovery-limit exception was applied to the new operator; the limit remains a hard stop.
Other native journeys, rendered art/UI, editor import, physical devices and exports are outside this
increment. Earlier failed editor/focus/probe reports remain in their original packets.

## Next dependencies and limits

This world currently admits one finite basin at genesis. There is no dynamic atlas insertion, played-save
migration, province composition, cross-basin drainage or terminal capacity witness yet. The bound is
not a global world-size limit. Published geography must not later be silently replaced with a new seed,
profile or extent. Future admissions need versioned commands, causal history and compatibility proofs.

The macro coordinates still have no admitted mapping into local terrain. The existing farm's physical
strata remain authoritative there; these separate macro parcels cannot be mined. Mapping, compatible
shared macro boundaries, conservative material/fluid custody, actual relief/river/cave realization and
historical geological changes are the next relevant dependencies. The terminal only ends preparation;
it supplies neither water nor storage. No accepted full geological dependency set is implied.

The full journal and nested snapshots remain the existing development baseline. Representative workloads,
archive/checkpoint growth, exact-time preparation, cancellation, cross-platform replay and production
performance require their planned measurements. All 181 requirements and 14 full UI cases remain intact.
