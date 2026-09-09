# Anatomy, condition and form-reference contracts

This P00 increment retains concrete body definition, condition derivation and form-reference contracts for [technical design §8](../../../spec-v1_1/TECHNICAL_DESIGN.md) and [language/lifecycle/afterlife requirements](../../../spec-v1_1/LANGUAGE_AND_AFTERLIFE.md). These definitions apply equally to player and NPC actors. They do not implement an actor controller, AI, health/need simulation, lifecycle transition, equipment transaction, animation or world execution.

## Stable anatomy and geometry

`BodyDefinition` identifies one versioned anatomy definition. Its immutable parts form one connected parent tree with exactly one root, unique stable local `BodySlotKey` values, existing parents and no cycles. Slots are lowercase ASCII keys scoped by that definition; the definition ID/version distinguishes identically named slots in different anatomies. Changing a part, dimension, function, parent or locomotion requirement changes the canonical version. Input order cannot change it.

`BodyDimensions` uses three positive signed-64 integer extents in 1/256 cell subunits. These are simulation units, independent of native sprite dimensions. A definition has an explicit whole-body clearance envelope; each part separately has its own extents. A torso's dimensions must not be used as the whole person's height. These data do not implement body placement, pose-specific collision, rotations, support contacts or swept movement. Spatial shape and animation acceptance must still validate actual placements, anchors and movement envelopes. An incorporeal envelope is not permission to traverse a solid cell or another realm.

Parts explicitly declare supported functions: sight, hearing, manipulation, ground contact, flight support, aquatic propulsion, undulating support, spectral perception or spectral motion. No species name, body outline, equipment name or sprite implies a function. The current corporeal profile supports the first seven functions; the incorporeal profile supports the two explicitly spectral functions. A materialized spirit using physical senses or physical objects needs a supported corporeal manifestation and its actual authority, rather than silently acquiring physical manipulation through an incorporeal definition.

These are anatomy-derived functions, not the complete set of actor capabilities. Learned magical sight, projection, phasing or other supported powers can compose with an ordinary corporeal body through the future typed effect/action system and its costs, permissions and exposure rules. The nature/function checks do not prohibit a corporeal person from learning those powers; they prevent an anatomy definition from accidentally claiming an unrelated physical or spectral function. No separate ability framework is implemented by these contracts.

`LocomotionRequirement` names a supported mode and the minimum count of functional supporting parts. Its mapping is explicit: ground/contact, flight/flight-support, swimming/aquatic-propulsion, slithering/undulating-support and manifestation/spectral-motion. Definitions must satisfy every declared requirement while intact; missing supports or duplicate modes reject. An undeclared mode remains unavailable. This checks anatomy eligibility only, not energy, load, terrain, medium, navigation, weather, speed, skill, permission or animation coverage.

The initial bounds are 64 parts per definition and a 64-character slot key. Function/mode enumerations are concrete implemented eligibility categories, not placeholders granting arbitrary verbs. The bounds qualify these local operations and reject excessive inputs; they do not limit the shipping species catalog. An anatomy exceeding this profile requires measured profile expansion or an explicitly qualified representation, never silent part deletion.

## Condition and applicable capabilities

`BodyConditionSnapshot` binds a form-instance identity to a pinned body definition and up to 256 current causal `BodyInjury` records. Each record retains injury identity, cause identity, target slot, kind and an optional lost function. The kinds have distinct implemented effects:

- A scar remains a cosmetic record and disables no function.
- A missing part suppresses every function on that part and its descendants. Removing an arm suppresses its hand's manipulation; removing the root suppresses all anatomical functions without itself performing death.
- Function loss disables exactly the named function on its named slot. That slot must actually provide the function. Loss of one eye reduces available sight slots; loss of both removes anatomical sight eligibility.

Physical scars and missing-part records require corporeal anatomy. Incorporeal condition records can explicitly suppress a supported spectral function. This does not simulate occult damage or define an unlimited effect interpreter.

Duplicate injury IDs, unknown slots, inapplicable functions and malformed kind/function combinations reject. Different injuries may overlap: their disabling effects form a deterministic union while all distinct causal records remain retained. A missing arm plus a separate disabled hand does not double-subtract an integer capability, and removing one record cannot erase the other cause. A scar on a missing part can remain a historical/cosmetic record; it does not restore the part. Input order does not affect bytes or derived capabilities.

`FunctionalSlots` returns stable ordered read-only slot keys. `SupportsFunction` checks a declared minimum count, permitting later callers to distinguish one-handed and two-handed work or one-eye and two-eye requirements without treating every anatomy as humanoid. `CanLocomote` evaluates the definition's explicit supporting-part threshold. There is no inferred damage penalty, speed change or blindness dialogue here; downstream action, perception, animation and NPC behavior systems must consume the actual applicable state.

Snapshots are immutable candidate state. Constructing a replacement with fewer injuries is not an authorized healing operation. Supported restoration, prostheses, treatment, wound progression, rarity tuning, lawful equipment drop/reassignment, death and causal historical retention remain required authority work. The local 256-record bound is not permission to discard an actor's injury history. Production must retain historical causes and qualify larger current-condition workloads or explicit record compaction semantics.

## One person across supported forms

`ActorFormBinding` keeps the accountable actor identity separate from the current form-instance ID, body definition ID/version and optional physical body or tether-anchor ID. Factories provide three actual reference shapes:

| Reference kind | Required nature | Physical body reference | Tether reference |
| --- | --- | --- | --- |
| Corporeal | Corporeal | Required | Absent |
| Independent spirit | Incorporeal | Absent | Absent |
| Tethered projection | Incorporeal | Absent | Required |

Actor, form and any supplied body/tether instance identities cannot alias. `ValidateCondition` requires the same form-instance and exact definition pin; a changed anatomy version cannot silently reuse condition interpretation.

These references contain no controller grant, independent inventory or copied actor state. They do not create, transition, kill, resurrect, possess, duplicate or authorize a person. In particular, constructing several candidate bindings for one actor does not establish that several forms may act simultaneously. The durable authority must validate current identity/control, tether target/type and permissions, exposure clocks, exclusive or explicitly bounded manifestation, reserves and custody before any form acts.

The same actor can be used in the three qualification vectors to demonstrate representation continuity, not an executed death→spirit→return sequence. Real transitions must revoke/replace prior action grants, reconcile remains/estate and current property, preserve world/branch history, and never recreate inherited or consumed inventory. An independent spirit may only continue when the selected cosmology and explicit transition permit it. The retained form references alone cannot prove any of these facts.

## Authored qualification examples

`actors-v1.json` includes five explicitly authored examples, independent of protected game statistics or legacy source conversion:

| Example | Implemented proof |
| --- | --- |
| Humanoid | Two sight slots, two hands beneath distinct arms and two ground-contact feet; eye/arm loss changes applicable slots. |
| Quadruped mount anatomy | Four contacts and a declared three-contact ground threshold, with no humanoid grip. This threshold is candidate tuning; it does not grant rideability or qualify biomechanics. |
| Avian | Two contact feet and two independent flight-support wings; loss of a wing function removes flight eligibility while retaining ground eligibility. |
| Reptilian serpent | An undulating/aquatic body and sensory head; slithering and swimming require no legs or manipulation. |
| Incorporeal presence | Explicit spectral perception/motion, independently pinned spirit/projection references and spectral-function suppression. |

These do not approve final species dimensions, movement thresholds, animation, art or the full required menagerie. The body families are composable starting qualification data; original actor/species statistics remain in their preserved source records until a reviewed mapping exists. The code makes no arbitrary source-stat conversion.

## Canonical identity and validation

`BodyContractEncodingV1` uses the existing `OFC\0` version-1 framing: big-endian version/count/field lengths, strict text and exact integer/identity bytes. Contract version is unsigned 64-bit `1`. Dimensions are signed 64-bit; enum/count values are unsigned 64-bit. Local keys are ASCII text; an absent parent, body, tether or lost function is an empty field, distinct from a numeric zero. Nested frames separate parts, functions, locomotion and injuries.

Definition frames bind definition ID, nature, whole-body clearance, sorted parts and sorted locomotion requirements. Part frames bind slot/parent, three extents and sorted functions. Condition frames bind form instance, definition ID/version and injuries ordered by injury ID, including every cause. Form-binding frames bind actor, form, definition ID/version, kind and optional body/tether references. SHA-256 over the complete canonical frame is the content version/hash; encoding returns fresh arrays.

This increment supplies encoding of already validated values, not an untrusted binary/JSON loader. A future package/body compiler must bound input, reject unknown schema/functions and validate the complete definition before constructing authority state. The test fixture reader is test support, not a runtime import path.

The independent Python standard-library oracle prints definitions, six condition vectors and three form-binding vectors with canonical bytes, hashes and expected available functions/locomotion. It imports no application code. Retained C# tests cover those exact vectors, ordering, body-tree failures, integer/key bounds, defensive copies, function/nature compatibility, missing-ancestor and overlapping injury effects, slot counts, legless locomotion, identity separation and stale definition pins. The coordinator records actual executed test results and reviewer evidence; this contract alone does not mark unrun qualification as passed.
