# Encounter farm content contract

`EncounterFarmAdmission.CreateEmbedded(seedText, playerName)` and
`Create(contentRoot, seedText, playerName)` admit a separate fresh encounter world.
They do not change the default farm launch or migrate a played save. The directory form
validates the supplied directory throughout the farm, production, quest, construction and
combat composition; it does not silently replace a missing witness with an embedded one.

The composition retains the existing player, Wren, original quest, construction project,
terrain generation seed/rules, finite stock and source intervals. It adds one Bramble Wolf
before the breach builder binds the complete inventory to its water owners. The existing
water owner remains the retained construction owner, Wren. All 6,000 water units and the
unissued finite divider source remain the original breach content. No effort, work,
exposure, rewards, deliveries, construction output or excavation completion is manufactured.

## Retained source and verification

All paths below are under `Runtime/content/legacy/aldermere-first-loop/`. The base farm
loader validates the existing package and closure manifest. `EncounterFarmSources` then
performs bounded exact SHA-256 reads of the selected records and original file witnesses.
Each selected record must retain its target key, path, JSON pointer, inventory identity and
canonical sorted-key payload hash; the original file's selected payload must match it.

| Record | Original pointer | Canonical payload SHA-256 |
| --- | --- | --- |
| `abilities/strike` | `data/abilities.json#/abilities/0` | `974a3c16135eddfc9679deb3929c862476c78b62f38fc79cb7253aba7e02f70a` |
| `classes/squire` | `data/classes.json#/classes/0` | `b6defa5b6c7a49d13fe3297938c41952c78b22de5ba2187138654d84b2507cf9` |
| `monsters/bramble_wolf` | `data/monsters.json#/monsters/1` | `d8639a45b5219163e148fde39537388df45db56148bfcaaa8ebb58ea52e6d967` |

The exact file pins are:

| File | SHA-256 |
| --- | --- |
| `records.json` | `9bfc3372954475402ea2fab30dab9de29b2a7c800f2fb61ab654d2efc81e973b` |
| `source-support/data/abilities.json` | `5cb75f014fef62fdf96d0597e9f51508737c1366ca7f341d5621ad9ca3d764d6` |
| `source-support/data/classes.json` | `27c5956e9b269acdc36ecba339522c426380ea2f35bc33fead49ae778add6234` |
| `source-support/data/monsters.json` | `8a3599545e5ea3e1a8ecfdf396b4bd107d2f2f3c696fcaaa2ed76d9dab511493` |
| `source-support/src/Sim/World.cs.txt` | `fc55db3ec2c8a9602e0432a7359e460b8b6a6e92a4123620df4df5190eaea4e2` |
| `source-support/src/Sim/SimConstants.cs.txt` | `7bdcec4fa6f7017bab1cb9c7ff028ee7e28b15ae37bc4503174f91b89e431742` |

`World.cs.txt` pins the source melee grace and integer damage/variance semantics. The
constants witness pins 32 source units per tile; one tile maps to 256 runtime subunits.
The source packet pin, original farm hash and new body definition pin enter the fresh
admission hash. Each ability also retains its selected payload as `SourceVersion`, and its
canonical definition includes the explicit transformed parameters.

## Admitted definitions and adaptation

The original level-one human Squire remains HP60, MP10, attack12, magic4, defense8,
speed8, with the retained class growth and zero human heritage modifiers. Admission checks
those source fields and the Squire's actual `strike` entry before granting Strike.

The Wolf uses the source level3 and fixed HP62, MP0, attack14, magic0, defense5,
speed10. Its unassigned stat profile applies no invented class growth or heritage. Initial
residual experience is zero; level3 is an authored source initial condition, not an earned
reward. Its new currency account has zero balance and its one-unit-capacity primary
custody is empty. The original finite currency supply and all material lots remain intact.

| Runtime key | Source power | Reach | Readiness ratio | Mana | Required function |
| --- | --- | --- | --- | --- | --- |
| `strike` | 10 tenths | `(48 + 8) × 8 = 448` subunits | `300 / 300` | 0 | Manipulation |
| `bramble_wolf_bite` | 10 tenths | `44 × 8 = 352` subunits | `400 / 300` | 0 | NaturalWeapon |

Strike includes the source player's eight-unit melee grace. The Wolf has no such grace.
Runtime actions additionally use the current common spatial law: equal feet elevation,
bounded XY center reach and a clear three-dimensional center sightline. The old source's
range-only test is not imported as permission to attack through terrain.

The source ATB numbers become explicit cost ratios in the current 10ms readiness units:
`ceil(10000 × numerator / (denominator × normalizedSpeed))`, where normalized speed is
ten times the retained stat. At these initial stats Strike costs125 units and bite134 units.
This is a declared timing adaptation, not the predecessor's ATB accumulator. Damage retains
the source integer physical rule and 900–1100 variance, with the runtime's retained SHA
counter stream in place of the predecessor RNG. Both initial matchups have base damage10
before variance. Actual draw consumption and effects belong to the encounter action owner.

## Authored body and placement

The Wolf is a newly authored quadruped anatomy, not a renamed humanoid. Its clearance box
is192×128×192 subunits. Four leg/paw chains supply ground contact (three functional paws
required), independent eyes and ears provide senses, and teeth under the jaw supply
NaturalWeapon. It has no hands or Manipulation function. These dimensions, slots and
locomotion threshold are explicit calibration content; the source monster record does not
specify anatomy. Source sprite dimensions are not physics evidence.

The existing player stance is unchanged. The Wolf starts one cell east, at the same Y and
feet elevation: for the farm's retained player stance, `(102.5m, 325.5m, 1m)`.
`LocalSpatialSnapshot` admits its full body, known terrain support and exclusive occupancy;
both actual melee sightlines must be reachable. The new creature also adds a construction
access requirement from its home to the dry cell immediately north. The current route is
checked at admission, and construction still checks that witness and current occupant
routes against proposed installed geometry. Existing project fields and access requirements
are retained. These are fresh authored positions and permissions, not played relocations.

## Scope boundaries

Player side1 and Wolf side2 name this confrontation only. Wren is a side0 coupled bystander
with no combat grants; her actual actor, inventory, progression, quest and shared exposure
remain. The Wolf has no admitted human language knowledge or opening dialogue. Fixed
source stats do not grant species-wide hostility, affiliation, morality or conversation.

Only Strike and the source Wolf attack are granted. Cleave, Breaker, Rally, Fire, healing,
equipment effects and broader source records remain outside this action admission. The
Wolf's source XP95, gil10–22, pelt/potion drops and the predecessor's despawn/respawn behavior
are retained evidence, not automatic runtime grants. Zero HP is a retained incapacitated
actor in this increment; death, remains, inheritance, loot and revival need actual lifecycle
and custody transitions. Native combat presentation and full campaign/combat acceptance
are separate evidence obligations.

`EncounterCombatWorldFixture` wraps the actual public admission and captures complete
command controls/revisions. It does not alter vitality, bodies, stock, effort or water to
manufacture a result. Qualification outcomes belong in the packet's execution record;
the existence of this contract is not a passing test claim.
