# World resource history

## Ownership and authority

[WorldResourceHistory](../../src/OpenFantasy.Core/Worlds/WorldResourceHistory.cs) joins the
[actor continuity component](actor-resources.md) to named world operations. Current HP, MP,
level and XP remain owned by `ActorProgressionState`; history is immutable causal evidence.
The care-enabled complete-world profile owns this history together with progression, quests,
material consumption and encounter lifecycle. Earlier profiles retain their own bytes and rules.

The anchor contains the complete fresh progression basis, world/branch scope, sequence zero
and world admission pin. The complete actor set replays from that basis. The profile requires
one progression revision per recorded command batch, including rewards of zero XP. Comparing
only final HP, or accepting an unexplained later revision, does not establish continuity.

An embedded basis and matching admission label establish structural consistency, not provenance.
The host must admit genesis and replay the actual accepted commands before trusting such a root.
Historical authentication, range, body capability, input selection, permissions and publication
cannot be established from resource labels alone. Durable host admission excludes care-enabled
roots; canonical encoding and in-memory world transactions do not confer storage admission.

## Named causes and complete transactions

[WorldResourceEvidence](../../src/OpenFantasy.Core/Worlds/WorldResourceEvidence.cs) has three
domain-specific forms, each attached to one ordered command batch:

| Cause | Retained evidence and checks | Coupled publication |
| --- | --- | --- |
| Encounter ability | Exact selected request, scenario, episode and full original resolution; replayed seeded draw, historical attacker/target stats, granted ability, damage and complete MP cost | Target damage, caster MP, random state, readiness and choices |
| Quest reward | Actual once-only completion command and pinned reward; earned level and XP law; finite reserve payment replayed through `ActorRewardPlanner` | Delivered material, reserved project stock, currency, XP/level/refill and quest completion |
| Treatment | Actual treatment, acknowledged defeat, prior care agreement, remedy and separately retained one-dose consumption | Source/crafted dose retirement, restoration, geography/material reconciliation, helper debt and encounter resumption |

[WorldResourceAdmission](../../src/OpenFantasy.Core/Worlds/WorldResourceAdmission.cs) reconstructs
the expected complete effects from each cause. A component-valid edit that removes a caster's
MP cost, substitutes a remedy rule for damage, changes a random draw or omits reward payment
still fails world admission. It checks every completed quest and treatment has its resource
batch, and compares the exact replayed currency ledger including account revisions.

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
Acknowledging recovery creates no resource change and is distinct from permission, helper
commitment, treatment and the command's receipt.

## Serialization and continuation

[WorldResourceHistoryCodecV1](../../src/OpenFantasy.Core/Worlds/WorldResourceHistoryCodecV1.cs)
frames the admission anchor, basis, standalone continuity archive and typed causal witnesses.
Parsing bounds collection counts and field sizes, refuses unknown evidence, and requires
canonical re-encoding. Complete-world decoding additionally validates each cause against
the other restored owners, including actor membership before accessing historical resources.
Malformed cross-owner references are admission errors and decode as `FormatException`.
The complete profile includes this entire owner in its hash.

The underlying limits of 256 batches and 1,024 effects describe one archive segment, not
character lifetime. Before appending beyond a segment, `WorldResourceContinuationRequiredException`
escapes the catches that translate ordinary action failures into rejected receipts. Resource
progress cannot silently become permanently unavailable because an archive is full.

Continuous durable play requires authenticated end-of-step segment continuation, retrievable
prior cause/branch evidence, and an exact admitted successor anchor before publication.
Retaining only a hash, silently deleting history, or treating overflow as a gameplay cap does
not satisfy that contract. Actor roster and definition changes also require explicit admitted
boundaries. The retained treatment policy covers aftermath recovery; ordinary treatment,
delivery modes and broader medical policies compose through the same resource/material laws
under their own complete-world admission rules.
