# Coupled structural contact assessment

## Candidate geometry and retained ownership

[ConstructionContactScene](../../src/OpenFantasy.Core/Structures/ConstructionContactScene.cs) describes a
complete proposed scene against one retained world. It is a read-only domain input, not an admitted world
snapshot. The region partition, generation baseline, existing excavations, installed construction,
material backing, member condition, actor identities, body conditions and home bindings remain tied to
the basis. Proposed additional excavation and uniform downward intact assembly placement are explicit
inputs. Actor placement may change; clearance remains mandatory for every body and material collider.
Unknown terrain cannot become clear space. Footing and load assessment are separate responsibilities.

The candidate retains every assembly and corporeal actor. Removing an obstacle from the manifest cannot
make contact safe. Its dependency pin includes the exact basis root, complete candidate terrain and physical
state, actor records and support interpretation. Input ordering is canonical. A candidate pose does not
prove a feasible trajectory, paid labor, movement permission, impact response or material-source entitlement.

## One contact partition and one mass owner

[ConstructionLoadPolicy.AssessContacts](../../src/OpenFantasy.Core/Structures/ConstructionLoadPolicy.cs)
requires a basis with a complete structural policy owner. It validates the entire construction catalog,
including unbuilt templates, and uses the same
[ConstructionBearingModel](../../src/OpenFantasy.Core/Structures/ConstructionBearingModel.cs) material,
member, actor and joint binding as ordinary stable-world assessment. A separate interpretation pin
(`construction.contact-assessment.v1`) includes the full policy and candidate scene. An unused calibration
change therefore changes the dependency even when it leaves the resulting graph unchanged.

The receiver policy is explicitly selected for this read-only calculation and may differ from the
basis world's retained calibration. This supports comparing authored capacities or mass calibrations
without replacing world rules. `PolicyPin` identifies that selection; it is not a claim of authority.
The basis must have a structural owner to provide complete located custody. Actual stable-world
admission obtains its policy from `world.StructuralPolicy`. A future consequence publisher must likewise
use and verify its retained policy; it cannot promote an arbitrary counterfactual assessment to authority.

Every lowest member's complete exact bottom footprint is partitioned once among rated terrain and
foreign assembly members whose exact tops coincide with its base plane. All member surfaces are collected
before resolving bases, so enumeration cannot hide a supporting assembly. Members of the same assembly
are excluded from this external partition; authored internal joints retain their declared ownership.
Rounded collider fringes, partial ledges and merely nearby planes provide no missing contact area.
Gaps refuse assessment. The resulting graph retains its global acyclic and per-element bearing bounds.

Each member keeps its original element, project, structure and installed-lot identities. Installed mass
is split by exact member volume once; there is no additional assembly mass. Actor loads include their
retained body, inventory and explicitly builder-carried stock/byproducts. Installed backing is excluded
from carried stock. Contact changes redistribute those same inputs through
[StructuralLoadResolution](../../src/OpenFantasy.Core/Structures/StructuralLoadResolution.cs), including
overload and cascading failure. Complete geometry and adequate strength remain separate checks.

A proposed hole removes that terrain contact from this calculation without asserting completed excavation.
The retained inventory is the mass basis: loot that a future transaction might issue is absent from this
assessment. A publication owner must assess its complete proposed custody and all final consequences before
admitting a root. The ordinary `Bind` method and stable save profiles retain their terrain-only base law;
the candidate assessment does not relax those gates or rewrite existing continuation bounds.

## Conservative load neighborhood

[ConstructionLoadNeighborhood](../../src/OpenFantasy.Core/Structures/ConstructionLoadNeighborhood.cs)
derives canonical element, structure and actor sets from nonempty unique known changed elements. It follows
bearings in both directions and includes every member of each touched rigid assembly. Reverse dependents
alone miss surviving alternate supports and their other occupants when load redistributes. Complete
connected closure is deliberately conservative and may include the whole graph.

The neighborhood retains the source model pin and copied seed set. It has no independent saved authority.
When contacts change, a consequence owner must consider both old and candidate graphs, including deleted
edges and newly contacted assemblies. A removed element is seeded in the graph in which it exists.
Whole-graph load resolution remains the correctness baseline; this catalog does not authorize dropping
unlisted input from an assessment. Finite graph bounds do not establish a frame-time budget.

## Boundary to physical consequences

A load neighborhood is not a swept-collision neighborhood and does not mean every included object falls.
Foreign walls, bodies and overhangs can intersect a moving assembly despite having no initial bearing edge.
Coupled motion must retain those obstacles, determine synchronized trajectories and contact response, then
reinsert conserved material before assessing subsequent support. Cross-assembly live settlement therefore
needs an explicit versioned world interpretation, complete collision ownership and a correspondingly proved
continuation/journal reservation bound. The existing
[durable consequence owner](consequence-continuation.md) provides command, time and publication ownership;
this read-only assessment supplies contact and load dependencies without substituting for those obligations.
