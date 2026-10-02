# World resource history

## Ownership and authority

[WorldResourceHistory](../../src/OpenFantasy.Core/Worlds/WorldResourceHistory.cs) joins the
[actor continuity component](actor-resources.md) to named world operations. Current HP, MP,
level and XP remain owned by `ActorProgressionState`; history is immutable causal evidence.
The care-enabled complete-world profile owns this history together with progression, quests,
material consumption and encounter lifecycle. Earlier profiles retain their own bytes and rules.

The initial anchor contains the complete fresh progression basis, world/branch scope, sequence
zero and world admission pin. A [continued segment](resource-archive-continuation.md) instead binds
the exact retrieved closing progression and world sequence. The complete actor set replays from
that basis. Actor effects require one progression revision per recorded command batch above the
basis revision, including rewards of zero XP. The [paid world](world-paid-exchange.md) additionally
requires one revision per fill; payment changes the currency ledger without inventing an actor
resource batch, actor revision, HP, MP or XP. Comparing
only final HP, or accepting an unexplained later revision, does not establish continuity.

An embedded basis and matching admission label establish structural consistency, not provenance.
The host must admit genesis and replay the actual accepted commands before trusting such a root.
Historical authentication, range, body capability, input selection, permissions and publication
cannot be established from resource labels alone. Durable admission requires fresh unearned care,
material and resource owners under the explicit schema-seven contract, or schema eight for the
[coordinated material/care world](coordinated-world-continuation.md). Continued roots are restored
through actual journal/archive retrieval, never admitted as a new genesis.

## Named causes and complete transactions

[WorldResourceEvidence](../../src/OpenFantasy.Core/Worlds/WorldResourceEvidence.cs) has four
domain-specific forms, each attached to one ordered command batch:

| Cause | Retained evidence and checks | Coupled publication |
| --- | --- | --- |
| Encounter ability | Exact selected request, scenario, episode and full original resolution; replayed seeded draw, historical attacker/target stats, granted ability, damage and complete MP cost | Target damage, caster MP, random state, readiness and choices |
| Quest reward | Actual once-only completion command and pinned reward; earned level and XP law; finite reserve payment replayed through `ActorRewardPlanner` | Delivered material, reserved project stock, currency, XP/level/refill and quest completion |
| Ordinary restorative use | Exact selected request, use, policy, historical patient resources and scoped permission before any revocation | Actual dose retirement, capped HP restoration and one ordinary exploration step |
| Treatment | Actual treatment, acknowledged defeat, prior care agreement, remedy and separately retained one-dose consumption | Source/crafted dose retirement, restoration, geography/material reconciliation, helper debt and encounter resumption |

[WorldResourceAdmission](../../src/OpenFantasy.Core/Worlds/WorldResourceAdmission.cs) reconstructs
the expected complete effects from each cause. A component-valid edit that removes a caster's
MP cost, substitutes a remedy rule for damage, changes a random draw or omits reward payment
still fails world admission. It checks every completed quest, treatment and ordinary use has its resource
batch, and compares the exact replayed currency ledger including account revisions.

In the paid interpretation, admission merges book fills and actor batches by world sequence and
command ordinal, refusing a tie. Each reward and payment consumes the ledger produced by the prior
cause. The book owns the quote and settlement evidence; progression remains the sole balance owner.
The replay checks exact account revisions and finite supply, not just net balances. After a joint
cut the actual closing progression is the basis and only successor fills add to its revision.

Encounter draws are replayed independently from each authority-derived episode's seed and
identity. Episode order cannot run backwards, and each ordinal has one scenario-derived identity
across historical attacks and the retained current episode. The retained active or released episode's RNG
position must equal its successful attacks. The original last attack and each acknowledged
defeat remain byte-exact witnesses at their historical boundaries. Healing and XP refills
explain later resources through the ordered history; they never rewrite past damage or require
past target HP to stay current. Historical action delay uses the attacker's stats at that time.

Resource effects have contiguous ordinals within a command. Quest commands use their actual
position in the world batch's deterministic actor order, including gaps for other actions.
Actionless encounter resolution records the earlier selected command at the resolution's
world sequence. Rejected requests produce no resource batch. The complete-world preflight
rolls history back with progression, quests and material owners after a later rejection.
The ordinary-use witness retains its exact world-command ordinal and a distinct evidence tag; profile25 rejects that cause.
Acknowledging recovery creates no resource change and is distinct from permission, helper
commitment, treatment and the command's receipt.

## Serialization and continuation

[WorldResourceHistoryCodec](../../src/OpenFantasy.Core/Worlds/WorldResourceHistoryCodec.cs)
preserves the original inline V1 frame and explicitly selects V2 for an archive-backed anchor.
V3 binds a joint material/care anchor and its exact closing progression basis. Each interpretation
retains bounded active continuity and typed causal witnesses. Parsing refuses unknown evidence
and requires canonical re-encoding. Complete-world decoding additionally validates each cause
against the other restored owners, including actor membership before accessing historical resources.
Malformed cross-owner references are admission errors and decode as `FormatException`.
The complete profile includes the entire active owner and its exact archive reference in its hash.

The underlying limits of 256 batches and 1,024 effects describe one archive segment, not character
lifetime. [Continuation](resource-archive-continuation.md) reserves the complete next batch and seals
the exact closing world before capacity is exceeded. Appending without available capacity still
raises `WorldResourceContinuationRequiredException`, outside ordinary gameplay-rejection catches.
The coordinated profile reserves all potential writers plus future actionable headroom and closes
resource, material and care windows together. A material-only cut can retain no new resource batches;
it still starts the next causal segment at the complete closing world revision. Pending care carries
its exact original defeat witness, while completed quest and encounter evidence seed the next segment.

Continuous durable play also requires authenticated end-of-step archive publication, retrievable
prior cause/branch evidence and host replay. Retaining only a hash, silently deleting history or
treating overflow as a permanent gameplay refusal does not satisfy that contract. Actor roster and
definition changes require explicit admitted boundaries. Ordinary treatment, delivery modes and
broader medical policies compose through the same resource/material laws under their own rules.
