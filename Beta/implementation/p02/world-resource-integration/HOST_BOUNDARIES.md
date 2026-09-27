# Persistence seam review

Read against `049408e` plus the preserved care WIP. These are source findings and required integration
proofs, not accepted storage changes. The worker owns world composition; the lead owns host storage.

| Existing binding | Required consequence for care/resource publication |
| --- | --- |
| `WorldGenesisAdmission.Validate` refuses every care owner. | Keep this gate until fresh care/resource anchors, pending-obligation recovery and complete replay are qualified. A canonical root alone is insufficient. |
| `SqliteWorldAuthority.CreateNew` maps profile24 to schema6 and currently falls back for profile25. | Add explicit fresh storage interpretation; never let a new profile fall through to an old schema. Do not rewrite existing schema constants/bytes. |
| Schema6's `world_commits` accepts kinds1,2,3,5,6,7,8; draft Care is kind9. | Admit the new kind only in the explicit new schema; preserve strict schema validation and old schema constraints. |
| `WorldCommitPreparation.Map` has no Care case; `ValidateDispatch` exempts lifecycle boundaries from its tactical-kind equality, but not Care. | Add the precise validated care dispatch path and kind mapping. Do not broaden tactical exceptions for arbitrary batch kinds. |
| `WorldJournalRecovery` checks exclusive profile/schema combinations before and after replay. | Add the explicit new pair consistently to creation, opening, recovery and exact schema tables. A checkpoint or edited head cannot bypass replay. |
| Recovery starts at admitted genesis, reprocesses every accepted batch, verifies before/after hashes, exact outcomes, identity/command tables and scheduled checkpoint bytes. | This is the actual deterministic-history proof. New resource records must be reproduced by those writers and compared, not accepted as independently authoritative command evidence. It still does not authenticate an externally rewritten database's provenance. |
| `WorldCommitPreparation` catches argument, operation and overflow exceptions from the processor as dispatch refusal. | Classify storage-envelope exhaustion deliberately. Never let a per-command catch convert it into an ordinary rejected action, repeatedly preventing all future resource advancement. Preserve the world on failure. |

The existing journal has development stops at 250,000 commits, 65,536 command outcomes and 128 MiB of
accepted batch/outcome bytes; it raises `WorldRecoveryLimitException` as a host stop. This is already a
known development boundary, not the production lifetime design. A new 256-batch resource segment must
not silently become a much smaller gameplay lifetime cap. Continuous storage needs an explicit
archival continuation before exhaustion, retaining retrievable causal and branch evidence.

Because the resource component anchors only at completed world steps, rollover must reserve enough
capacity for the whole next step (up to64 ordered actors/actions, potentially several effects each).
It cannot split one atomic command or relocate an effect across its actual commit boundary. A retained
last attack may predate the new segment: its resource relationship still needs accessible proof;
changing the basis must not erase or weaken that comparison. Merely hashing the discarded segment
would not satisfy this requirement.

Pending care differs from pending excavation. The acknowledged command already has its terminal
receipt; treatment is a separate helper action. Recovery must retain the acknowledged obligation,
materials and readiness exactly, but must not fabricate a second acknowledgment receipt or interpret
player recovery acknowledgment as permission to control/spend for the helper. Host/NPC policy still
selects and revalidates a real treatment action after readiness.

Required retained proof before enabling durable care: actual source-backed attack→acknowledgment→
treatment→later attack/reward, exact reopen at each boundary, crash around publication, duplicate
requests, stale offers, and component-valid fabricated evidence rejected by world/replay checks.
Then prove archive continuation across a real capacity boundary and the native ordinary/aftermath
flows at their existing gates. Managed component success cannot close those acceptance requirements.
