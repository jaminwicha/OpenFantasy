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

## Public geometry and interactions

[LocalWorksiteView](../../src/OpenFantasy.Hosting/Simulation/LocalWorksiteView.cs) exposes committed
terrain occupancy and installed member geometry within the explicitly sight-complete locality. It
also exposes only the controlled actor's owned excavation targets, request revisions, matching tools,
effort and manipulation capability. Raw source claims, unissued output, another actor's custody and
pending calculation cursors remain private. Unknown terrain outside the supplied bounds is not empty.
During consequence resolution, geometry remains the committed pre-publication state and the view
explicitly reports resolution in progress.

[WorksiteActionOffers](../../client/scripts/WorksiteActionOffers.cs) offers mining and assisted ledge
traversal only for an explicitly selected excavation target. A nearby known terrain contour can suggest
one bounded ledge request; it does not reproduce authoritative body-clearance, sweep or reach rules.
The captured intent includes the actual target/tool/revisions or exact traversal displacement. The
host remains the sole judge of acceptance. Wrong-floor mining, insufficient effort, absent tools and
completed excavation have inspectable blockers. Normal stairs use ordinary walking.

The existing four-sector wheel substitutes Mine and Climb for the selected worksite context. Opening
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
