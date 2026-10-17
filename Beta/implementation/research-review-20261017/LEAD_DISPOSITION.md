# October 17 research review disposition

The [original advisory](advisory-research-20261017.md) is preserved byte-for-byte, SHA256
`3e0277e1e8034b90838b15a95359d080095238fc1e2a30ce59616bede7a70d09`.
Its location in `spec-v1_1` does not ratify its assertions as an implemented or frozen specification.
This lead review checks its four research mappings against their linked authors' posts, the maintained
architecture and accepted source `e576cd27`. No new agent was dispatched while orchestration is paused.

## Source checks and decisions

| Input | What the linked author actually establishes | Decision for this game |
| --- | --- | --- |
| SR-240, [Hydraulic Erosion Planets](https://www.reddit.com/r/proceduralgeneration/comments/1x897z7/hydraulic_erosion_planets/) | The author reports converting their own C++ erosion implementation to CUDA and reducing their planet calculation from ten minutes to ten seconds. The opened post supplies no reproducible hardware, map-size, droplet-count or equivalence benchmark. | Accept erosion, deposition and drainage as a useful research lead within the existing geological design. Do not adopt the advisory's 2048×2048/200,000-droplet/under-five-second assertion, its capacity formula as a selected model, or CUDA/Godot compute as authority. Select and measure an appropriate C# algorithm at GEO-RESEARCH below. |
| SR-245, [BRUTAL announcement](https://www.reddit.com/r/gameenginedevs/comments/1x6d2mp/introducing_brutal_a_free_c_framework_that_helped/) | The framework's author describes a C# framework with a custom native interop layer, unmanaged memory and Vulkan. This is first-person architectural context, not evidence that adopting that stack improves this game's core. | Reaffirm our existing engine-free C# authority. Do not import BRUTAL, change engines or add an interop kernel. Our actual Core/Hosting/client boundaries are already documented; the advisory's named snapshot/batch subscription contract is not our client API. |
| SR-258, [autonomous village battles](https://www.reddit.com/r/hobbygamedev/comments/1x1mhaj/two_of_us_are_making_a_village_builder_where/) | The developer describes assigning village work, preparing army positions and watching a battle. They say the clip omitted preparation, causing viewers to miss controls that already existed. No offscreen algorithm or deterministic statistical combat rule is described. | Adopt the stronger presentation lesson: make preparation, commitments and causal consequences understandable. NPC-01 and its human playtest should demonstrate that connection. Autonomous settlement defense belongs in P07/P08, after persistent actors and offscreen continuation; a separate statistical winner roll is not selected. |
| SR-275, [Red Skies at Dawn cabin](https://www.reddit.com/r/IndieDev/comments/1x4k9qp/im_trying_to_make_the_boat_feel_like_a_home_in_my/) | The developer describes softer sound below deck while the boat continues moving and weather remains outside. The post does not identify a particular reverb/filter implementation. The developer also acknowledges that personal belongings can communicate habitation better than additional surface wear. | Add a concrete exterior→shelter listener contrast to P02 audio acceptance; reuse it for vessel interiors in P06. Preserve actual weather and vessel motion across the presentation transition. Include a few culturally/personal meaningful belongings in art calibration rather than indiscriminate visual clutter or wear. No DSP algorithm is selected. |

These are primary descriptions of the authors' own work, with limited technical detail. They motivate
our design choices; they are not independent measurements or proof of our implementation. Retrieval
was performed on October 17. Research catalog IDs remain provenance identifiers, not acceptance gates.

## Roadmap integration and verification witnesses

| Work / phase | Integration and dependencies | Required witness before acceptance |
| --- | --- | --- |
| GEO-RESEARCH — ECO-01/P04, expanding through P06/P10 | Reuse the existing relief→drainage→sediment→soil→habitat→settlement dependency. Compare bounded grid/flux and droplet approaches in C# when implementing erosion; choose by conservation, historical causality, seams, cost and playable terrain. A heightmap is only a macro representation; it cannot erase caves, overhangs, material ancestry or vertical construction. | Fixed-seed canonical results across repeated generation, region/worker order and supported runtimes; balance source erosion against deposited/exported sediment; finite outlet/receiving capacity; compatible region edges; historical rerouting without rewriting a lived settlement. Record named hardware, workload, cold/warm time, allocations and boundary error. Any SIMD optimization must retain the admitted arithmetic/order semantics. No numerical speed promise is accepted from the post. |
| Authority boundary — continuing P01/PLAN-STATE | Keep Core engine-free, Hosting responsible for durable publication, and Godot consuming permitted views. Relevant boundary changes trigger dependency and headless replay checks; no new framework migration is scheduled. | Core remains free of Godot/native acceleration dependencies; actual local/network observations enforce privacy and authority. A conceptual `WorldSnapshot`/`WorldStepBatch` event diagram cannot substitute for those interfaces. |
| NPC-01/P02, then settlement defense P07/P08 | In the delayed-supplies episode, show the observable evidence, feasible plan, real work and changed-place result without exposing private thought. Carry that causal presentation into staffed defense, supply, injury, damage and rebuilding once those systems exist. Predecessor `SettlementGen.cs`/`World.Settlements.cs` are migration references only. | A player can connect preparation to consequences using permitted cues/dialogue. For distant defense, conserve real identities, equipment, cargo, time, casualties and political consequences; prove declared detailed/offscreen continuation boundaries, save/replay and intervention. Re-entry cannot reroll the outcome, respawn the army or bypass Wait. Any future aggregate resolver needs a separate explicit equivalence/approximation decision. |
| AUDIO-01/P02, vessel reuse P06 | Bind shelter acoustics to the listener's actual enclosure and portal state, source/material intent and real weather. Prioritize quiet, intelligibility and gradual transitions. Combine a small number of lived-in details with the accepted restrained visual direction. | Exterior→threshold→interior and return, open/closed portal, and two listeners on opposite sides retain weather/time while producing distinct permitted mixes. Save/reconnect restores ongoing ambience without replaying transient actions; no hidden source identity leaks through cues. Later vessel boarding preserves motion/damage context. Actual native listening/art acceptance remains required. |

The lead owns these design/implementation decisions until explicitly assigned otherwise. Art/audio
production remains unstaffed. These refinements fit the existing P00–P14 scope and do not expand the
immediate queue: PLAN-STATE's integrated owner transfer, absent-detail work and atomic recovery/history
proof still precedes permanent NPC cognition. ECO-01's existing liquids/ice/ecology introduction and
all P02 closure rows remain required. No research candidate replaces a production gate.

## Architecture and factual corrections

The [Core project](../../Runtime/src/OpenFantasy.Core/OpenFantasy.Core.csproj) has no Godot reference.
[Runtime authority](../../Runtime/docs/architecture/runtime-and-authority.md) distinguishes Core,
Hosting, the headless Host, Server and Godot client. The actual presentation includes
[LocalWorldView](../../Runtime/src/OpenFantasy.Hosting/Simulation/LocalWorldView.cs) and authenticated
[observation frames](../../Runtime/src/OpenFantasy.Hosting/Networking/PlayerObservationFrame.cs),
with scoped selected observations. The advisory's proposed subscription names are not adopted APIs.

The timeless geography reference now makes erosion's macro/detail and optimization boundaries explicit.
The presentation reference states shelter/listener and causal-preparation contracts as **design**
obligations, not implemented sound or cognition. Existing baseline geology, audio and NPC scope remains
intact; no frozen GDD is rewritten. The roadmap also corrects its stale opening-summary claim that the
complete two-player expedition was still unproved; its existing detailed row already links the accepted
expedition. Deeper NPC consequences and human acceptance remain open.

## Preservation and checks

[Checkpoint](CHECKPOINT.json) `codex/checkpoint-before-research-disposition-20261017` preserves
`0ad2f37a7e10012ef3284b1c3bc9c81b02148466`, based on `e576cd27`, including the original advisory
and concurrent language documents as WIP. It is not acceptance of those documents. The original
advisory and other-task language/solution/predecessor edits remain untouched. Accepted review scope
is recorded by `codex/milestone-research-disposition-20261017`.

This is a documentation-only integration. The prepared-placement record retains its 373 within-budget
affected passes, both full-Core timeouts and late TRX; this review does not rerun or relabel them.
No new runtime feature, benchmark, human acceptance or independent critic is claimed. The first
commit preflight reported the original advisory's two-space Markdown break as trailing whitespace.
The original bytes are retained; authored files pass the whitespace check separately.

## Documentation review

- **Plan/spec:** All four research mappings have explicit dispositions, phases, dependencies and witnesses in this record and CURRENT_ROADMAP. Frozen specifications and full scope remain unchanged; unsupported algorithms, speed targets and APIs are not adopted.
- **Reference:** World/geography and presentation/interaction explain macro erosion limits, conserved historical consequences, authoritative optimization, listener-specific shelter and understandable autonomous preparation in timeless design terms. Existing runtime/authority bindings remain accurate without a gratuitous edit.
- **Accuracy/completeness:** Lead checked the four original author posts, Core project dependencies, host/client observation owners, baseline geology/audio contracts and current roadmap evidence. Predecessor references, proposed implementation and established runtime behavior remain distinct. The late aggregate result remains outside its deadline gate.
- **Checks:** See VERIFICATION.json for source/advisory preservation, required-field/local-link checks and whitespace results. Mechanical checks do not establish semantic accuracy, implemented acoustics, generation performance or independent/human acceptance.
