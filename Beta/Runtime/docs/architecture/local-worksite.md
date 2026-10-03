# Local worksite

## Content and save ownership

[WorksiteFarmAdmission](../../src/OpenFantasy.Hosting/Content/WorksiteFarmAdmission.cs) composes an
explicit fresh local world using the farm's source inventory, gathering, production, progression,
quest, construction and event-settlement owners. It introduces two unstarted projects, distinct
delivery commitments and an owned three-unit excavation. No construction output, completed work,
extracted terrain or reward is supplied by admission. The original farm content remains a separate
selection.

[WorksiteFarmConstruction](../../src/OpenFantasy.Hosting/Content/WorksiteFarmConstruction.cs) defines
stairs with a fixed deck and an adjacent supported deck. Each requires six wood and 200 construction
work units. The second quest has an explicitly admitted currency reserve. Initial carried stock and
finite gathering provide the inputs; delivery reserves them and ordinary NPC work performs fabrication.
Material mass is conserved, but the large demonstration members and small inherited wood units are
authored calibration, not physical timber density or production balance.

[WorksiteFarmTerrain](../../src/OpenFantasy.Hosting/Content/WorksiteFarmTerrain.cs) performs a bounded
deterministic search for a fresh terrain seed under one exact stratified relief rule. It checks a
natural lower work stance, adjacent support and approach, then the level cells required by authored
walking and construction corridors. Only a matching candidate generates the full locality. Search
exhaustion refuses admission; it does not change the rules or introduce a pre-excavated cell. Existing
terrain never participates in this selection. This is constrained local content, not global geological
generation or frontier streaming.

[WorksiteFarmPolicy](../../src/OpenFantasy.Hosting/Content/WorksiteFarmPolicy.cs) pins explicit member,
body, carried-item and terrain-bearing ratings. The complete world admission and action processors
still decide support, material conservation, clearance and harmless settlement. Completing excavation
uses the existing durable consequence continuation and publishes the final terrain, material, member
positions and receipt together.

[FarmClientSession.OpenWorksiteAsync](../../client/scripts/FarmClientSession.cs) uses a separate save
entry, verifies world/branch/profile/admission identity and attaches the ordinary local session. It
does not copy or upgrade a farm save. An incompatible existing path is refused and its lease released.
The `--worksite` application argument selects this entry; the default farm retains its own save chain.

[`HumanConstructionWorldAdmission`](../../src/OpenFantasy.Hosting/Content/HumanConstructionWorldAdmission.cs)
defines a separate, explicitly selected playable qualification scenario. Three travelers join the
retained builder Wren as a fourth human through the declared fresh calibration contract. Her permanent
identity, project, kit, work requirement, placement and supply quest stay intact; another human supplies
the original materials through that quest. The Host's `--create-human-construction` command creates a
new database and refuses an existing one. This does not change the default population, qualify
production balance or convert a lived NPC. Subsequent recovery/permission admissions add their declared
laws without granting treatment, resources, funding, work or elapsed time.

## Human work and completed structures

An unfinished project is inspectable by its designated builder as well as its participating supplier.
The builder need not be a participant in their own supply quest. The shared construction work view
provides purpose-specific funding, readiness, manipulation, stance and reach facts, plus exact legal
singleton tool choices. It exposes no project-stock ledger or full private inventory. Home characters
and Production-authorized operated characters use the same projection and ordinary command path.

[`ConstructionActionOffers`](../../client/scripts/ConstructionActionOffers.cs) requires an explicit
project target. The field work control and wheel review one `WorkConstructionIntent` with its captured
actor, project revision, project-storage revision, tool identity/revision and selection witness. A
changed review cannot become another project or tool. Each confirmation requests one ordinary work
step; it is not consent to an unattended job. The Core checks actual ownership, completed supply
commitment, reserved materials, body, exact work stance, reachable site and safe placement/access.
Final work converts the real reserved input and installs geometry atomically. Previously accepted
partial work remains when control changes, while further work requires current authority.

[`LocalPublicStructureView`](../../src/OpenFantasy.Hosting/Simulation/LocalPublicStructureView.cs)
separately exposes actual installed identity, origin and member geometry/collision from the same
observation. Seeing a completed structure does not require Production or private history and exposes
neither funding nor backing lots. An absent older facet differs from a present empty collection.
The renderer applies its current region/floor and projection to these members. Physical construction
retains its condition/displacement owner and renderer; public geometry cannot invent condition or
reconstruct a removed collider from a definition.

## Public geometry and interactions

[LocalWorksiteView](../../src/OpenFantasy.Hosting/Simulation/LocalWorksiteView.cs) exposes committed
terrain occupancy and installed member geometry within the explicitly sight-complete locality. A worksite view exists whenever excavation is admitted, independently of physical construction.
`HasPhysicalConstruction` identifies ownership of retained member condition/displacement; without it,
`Members` is empty and ordinary completed farm structures retain their separate appearance. It
also exposes only the controlled actor's owned excavation targets, request revisions, matching tools,
effort and manipulation capability. Raw source claims, unissued output, another actor's custody and
pending calculation cursors remain private. Unknown terrain outside the supplied bounds is not empty.
During consequence resolution, geometry remains the committed pre-publication state and the view
explicitly reports resolution in progress.

[WorksiteActionOffers](../../client/scripts/WorksiteActionOffers.cs) offers mining and assisted ledge
traversal only for an explicitly selected excavation target. `SupportsAssistedTraversal` reflects the
admitted world interpretation; extraction alone does not grant a Climb offer. The terrain-bound liquid
profile exposes mining without assisted traversal. Each target preserves its declared reach mode and
stance, including a raised-bank approach. A nearby known terrain contour can suggest
one bounded ledge request; it does not reproduce authoritative body-clearance, sweep or reach rules.
The captured intent includes the actual target/tool/revisions or exact traversal displacement. The
host remains the sole judge of acceptance. Wrong-floor mining, insufficient effort, absent tools and
completed excavation have inspectable blockers. Normal stairs use ordinary walking.

For [selected characters](selected-player-protocol.md), the shared
[`FarmProductionPresentation`](../../client/scripts/FarmProductionPresentation.cs) passes only the
operated Production worksite, feet and known conditions to the same offer builder. Excavation targets
participate in ordinary explicit target selection. Mining permission does not grant climbing: both
world support and the selected character's separate Traversal permission are required for that offer.
An unsupported interpretation keeps assisted traversal absent even when Traversal is granted.
The HUD presents operated effort and selected work progress alongside the permanent home obligation.

Regional canvas frames carry the disclosed excavation targets and gathering nodes through the same
floor/region filters as their observation. Resource sprites, work cues and the operated actor use the
canvas's current geometry projection; fixed-farm coordinates cannot place a regional resource. Camera
framing uses the actual unobscured field between HUD panels. Physical members remain supplied geometry;
an unfinished project record or hidden historical record is not a substitute for public structure
geometry. The renderer cannot borrow a different character's home observations to fill that gap.

The existing four-sector wheel substitutes Mine and, where admitted, Climb for the selected worksite
context. An unsupported traversal sector remains absent. Opening
selects nothing; direction or pointer input inspects and explicit confirmation submits. An open slot
cannot change a traversal destination or quest definition during refresh. A changed choice requires
reopening. Captured offers retain the existing session binding, stale-revision refusal and idempotent
submission behavior. Focus loss, modal cancellation and world readiness retain their normal owners.

Addressed tasks select an accepted commitment before new offers and completed records, in stable
definition order within each category. Every visible offer names its quest. Completing one task cannot
turn an already captured acceptance into acceptance of another task from the same person.

## Finite multilevel presentation

[WorksiteCoordinates](../../client/scripts/Presentation/WorksiteCoordinates.cs) uses a bounded oblique
projection with 32 pixels per metre on each axis. The one-metre farm floor aligns with the original
flat projection. Actors, target markers and camera focus share this projection; it changes presentation
only. The legacy flat coordinate contract remains explicit.

[WorksiteView](../../client/scripts/Presentation/WorksiteView.cs) retains member nodes by stable
structure/member identity and updates unequal geometry records. Terrain geometry changes only when
the projected occupancy changes. Higher members appear as a transparent cutaway; physical member
dimensions come from exact authored millimetres and committed displacement, not from a generic guard
sprite. Terrain departures show known raised surfaces or lower floors; missing cells remain unknown.
Target cues remain above the physical blockout. Actor animation uses observed positions and cannot
cause movement, mining or a fall.

This renderer expresses the finite authored worksite. Nearest terrain contours and member cutaways
are not a general cavern, stacked-room, fog-of-war or final-art renderer. Broader visual layers, accepted
assets, animation, sound, interaction clarity and device support retain their separate design contracts.
