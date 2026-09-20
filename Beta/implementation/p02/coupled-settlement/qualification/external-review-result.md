# Retained external critic result

Provider result; claims require lead disposition and are not qualification evidence by themselves.

I found no substantiated unsafe acceptance in the inspected scope. Every counterexample I built ended in a refusal, but some of those refusals depend on final admission code I did not read.

**Files inspected**
- `Beta/AGENTS.md`
- `coupled-settlement.md`
- `ConstructionSettlementScene.cs`
- `ConstructionSettlementResolution.cs`
- `VerticalDescentQuery.cs`
- `FootingQuery.cs`
- `StructuralContactWeights.Resolve`, lines 11–50 only
- `ConstructionPhysicalSnapshot.ValidateStableGeometry`, lines 98–127 only

I did not read the handoff routing, `ConstructionBearingModel`, `CoupledSettlementPolicy`, `LocalSpatialSnapshot`, or either Hosting test file, because the budget ran out first.

## Cases traced to refusal

- **Dependent straddling a mover and a stationary support:** any overlap with a moving surface counts as moving support (`Scene.cs:153-160`), so it is not an external base. Its member sweep meets the stationary top at drop 0, which is not the group drop, so it refuses (`Scene.cs:165`; `VerticalDescentQuery.cs:164` includes `top == MinimumZ`).
- **Mover resting partly on stationary structure S:** the external base query returns drop 0 and refuses (`Resolution.cs:142`).
- **Stationary actor or structure below a member or rider:** `FirstContact` selects its top before the group drop, so the drop mismatches and it refuses. A member landing exactly on an actor's head returns `ActorContact` and refuses.
- **Rider with partial coverage:** `Resolve` throws on a leading gap, a trailing gap or an empty strip (`StructuralContactWeights.cs:37,50`), so the whole candidate refuses.
- **Actor on stepped footing with a mover surface below its feet plus a stationary sliver at feet height:** it is not a rider and stays as an obstacle. `QueryActor` later finds the sliver at drop 0 and refuses (`Resolution.cs:223`).
- **Work bound:** one collision box per definition member (`ConstructionPhysicalSnapshot.cs:113-123`) gives at most 32 boxes and at most 32 bases per assembly. The assembly cap is enforced before the phases begin (`Scene.cs:130`), and riders and actors are each at most 64. I found no way to exceed the bound, provided the policy actually computes `2*A*32+128`, which I did not verify.

## Unverified concerns

These are not findings; each would need one more file to confirm or dismiss.

1. **Upward expansion depends entirely on the bearing graph (`Scene.cs:114-129`).**
   - Seeding (`:104-112`) evaluates footing with all structures in their original positions, so a structure resting wholly on a mover is never seeded geometrically. It is collected only through a bearing from the mover to it.
   - If `ConstructionBearingModel` omits any physical base/top contact, such as a zero-weight patch, a filtered joint or a fringe-only contact, that dependent stays stationary while its support descends.
   - Safety then rests entirely on final admission refusing an unsupported structure. I confirmed `ValidateStableGeometry` iterates installed projects by component (`:111-127`), but not its support predicate.
   - Suggested check: a test where the bearing graph lacks a contact that `FootingQuery` accepts.

2. **`HasMovingSupport` uses rounded collision envelopes against exact surface bounds (`Scene.cs:157-159`).**
   - The doc says rounded edges cannot create contact. Here they can exclude a base from the external bases.
   - Example: a seeded assembly B has exact base edge x=10.4 (envelope 11). Mover A has an exact top starting at x=10.6 on B's base plane.
   - B's base then never seeks its own floor, and its member sweep accepts `NoContactWithinLimit` (`:165`).
   - An earlier obstruction still refuses, so the only exposure is B ending unsupported, which again relies on final admission.
   - An exact-footprint intersection would remove that dependence.

3. **Seeding inspects only boxes on `GroundPlaneZ` (`Scene.cs:106`).**
   - The doc says each connected component keeps its own supported lowest members.
   - A higher component that loses its terrain ledge is not seeded. The candidate goes to the actors phase and is presumably refused at final validation.
   - That is a conservative refusal, but it diverges from "losing full base support seeds a moving assembly."

4. **Seeding treats any non-clear footing result as lost support (`Scene.cs:108`).**
   - This includes `Unavailable` from missing cells or more than `MaximumRectangles` patches, so a standing structure can be seeded as a mover.
   - It most likely ends in refusal through `Clearance`. I did not check whether the refusal reason is misleading.

5. **The work-bound breach throws outside the refusal handler (`Resolution.cs:78-81`).**
   - It propagates as `InvalidOperationException` rather than a refusal, so the whole bound argument rests on preparation.
   - It appears unreachable if the policy formula holds (unverified, as above).

## Coverage limits

- I did not trace profile 15/16 byte preservation, the codecs, world-step integration, final load-policy admission, conservation, issuance or the clock.
- I did not run any tests or read the Hosting tests, so I cannot say which cases are actually proved.
- Concerns 1–3 each depend on unread final-admission code. If that admission does not check exact per-component footing for every structure, concern 1 becomes an unsafe-acceptance defect.
