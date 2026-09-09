# Counted-lot custody recommendation

Reviewer: portable-core agent, independent of the coordinator implementing custody. Date: 2026-09-09. This is a bounded design review before implementation; executed implementation evidence will be recorded separately.

## Recommendation

Implement a pure branch-local transfer planner over an immutable, complete custody snapshot. A pristine counted lot with one source interval can support partial transfer without inventing provenance. Keep its scope explicit: it represents unchanged indivisible inventory units, not every crafted material, liquid, assembly or cross-branch entitlement.

A useful retained model contains stable lot identity, immutable definition reference, current custodian, positive checked quantity, source interval, revision and supported protection/reservation state. Its source interval is half-open and names the source identity assigned before extraction or a particular production cycle. It does not derive a new origin from the transfer command or current branch. The source's quantity unit is explicit; for this primitive only, interval length equals current count. Snapshot construction rejects duplicate identities, overlapping source intervals and impossible supplied occupancy/capacity.

Full transfer retains the lot identity and source interval. Partial transfer selects a canonical prefix, leaves the original identity on the remaining suffix, and creates a destination child from the command identity and a documented spawn-ordinal allocation. For the present single-transfer command, ordinal zero can be reserved for that one child. Reject a collision with any existing identity. Preserve definitions, units and supported protection metadata without inferring permission to override them. Do not silently merge different source intervals because their item definitions match.

The plan validates world/branch, required expected revisions, current custody, positive available quantity, relevant locks and receiver capacity before publishing one immutable candidate. A stale command or capacity failure must leave the input snapshot byte-for-byte unchanged. Competing candidates can both be computed from the same base; only a future serialized authority commit may accept one against that base revision. The plan's result is not a durable receipt, actor authentication, legal ownership proof or delivery acknowledgement.

If the increment cannot enforce this interval/collision/metadata/capacity boundary, accepting full-lot transfers and explicitly rejecting partial requests is preferable to fabricating provenance. Splitting is cohesive enough to retain now if its invariants are tested.

## Real source examples

- `data/quests.json`, `wren_orchard`: Wren requests six wood for a windbreak. `data/npcs.json`, `farmer_wren`, binds that quest. An eight-unit wood lot split into six transferred units and two retained units is a suitable first fixture. A successful transfer alone does not mean the quest is accepted/completed, rewards are granted, a windbreak exists or Wren has performed work.
- `data/recipes.json`, `craft_rations`: two berries plus one wood produce two rations. That transformation requires a union of heterogeneous input claims and an explicit apportionment/loss/process contract. Do not map the output count of two onto a made-up two-unit source interval or drop the wood claim. A pristine single-origin counted-lot primitive can be retained as one case beneath a later general provenance model.
- `src/Sim/World.Systems.cs`, `AddItem`: a partial existing stack is mutated before inventory-slot exhaustion can return false, despite the comment saying nothing was added. `src/Sim/World.Gathering.cs`, `CraftItem`, removes inputs, calls `AddItem`, and tries refunds on failure. These lines supply a concrete reason to prefer candidate construction and all-or-nothing adoption; they should not be copied into the new planner.

## Essential acceptance cases

1. Full transfer preserves identity, quantity, definition and exact source claim; only custody/revisions change.
2. Six-of-eight transfer creates disjoint intervals whose union equals the original, preserving the source ID and unit. Repeat identical planning yields identical child ID and candidate.
3. One-unit, all-but-one, zero, excess-quantity and maximum-endpoint cases handle exact arithmetic and overflow.
4. Source-claim overlap and duplicate lot/child identities reject; adjacent intervals remain legal. Different origins do not falsely collide.
5. Unknown/wrong custodian, world/branch mismatch, missing/stale expected revisions, protected/reserved input and full receiver reject without mutation.
6. Candidate construction does not expose mutable dictionaries, lists, arrays or nested state from the source.
7. Two candidates competing for the same stock cannot both be applied through a stale base. State change followed by an identical submitted command is not silently treated as a new transfer; durable retry/receipt adjudication remains future host work.
8. Transfers conserve per-origin intervals and global quantity across several partial transfers and different enumeration orders. No counter or wall-clock value affects selection or child identity.
9. Cross-branch transfer and transformation remain unsupported, without granting export allowances from the mere presence of origin metadata.

## Requirements grounding

The current technical design requires checked typed quantities (§3), immutable planning against committed revisions and atomic durable publication (§4), actual sourced mining/production lots (§7), reservations/protection and heterogeneous process graphs (§8), and source-interval conservation plus a separate world transit ledger for cross-branch cargo (§9). This increment supplies only the expressly tested local custody mechanism. It does not qualify those broader systems.
