# Independent anatomy and form-contract review

Reviewer: the production-contract worker, reviewing the separate Actors implementation. The reviewer did not implement Actors and does not count this as independent review of Production.

**Disposition: no blocking code defect found; one scope clarification requested with the coordinator and added by the actor owner.**

## Scope clarification closed

The corporeal/incorporeal function checks admit only their currently implemented anatomical categories. Read as all possible actor capabilities, that could wrongly imply a living person can never acquire magical sight, phasing or projection. The owner added an explicit contract paragraph: these are **base anatomy-derived functions**, while future supported effect/action definitions can compose magical capabilities with ordinary bodies under their own costs, permissions and exposure rules. The reviewer verified that text. No speculative ability framework or new runtime behavior was added. Clarification closed.

The existing contract also correctly distinguishes `CanLocomote`'s anatomy eligibility from route/terrain/medium legality, energy, load, equipment, skill, weather and animation. Whole-body clearance is separately declared from part dimensions, and actual placement, posture, collision and swept geometry remain unimplemented. Neither an anatomical support function nor a small clearance number grants an action by itself.

## Reviewed implementation properties

- Body definitions enforce a nonempty, connected parent tree with exactly one root, unique local slot keys, existing parents and no cycles. Parent traversal terminates within the bounded 64-part input. Unknown/duplicate functions or locomotion modes reject, and intact anatomy must actually meet each declared supporting-part threshold.
- Positive signed 64-bit part/clearance extents retain 1/256-cell units without a sprite-pixel conversion. Keys have bounded ASCII grammar. Canonical definition hashes include nature, clearance, parentage, every part's dimensions/functions and locomotion requirements, with explicit stable sorting.
- Condition snapshots preserve unique injury/cause identities and validate target slots and named lost functions. Missing ancestors suppress descendant functions; scars do not. Overlapping distinct losses form a deterministic union without deleting either causal record or subtracting the same capability twice.
- Incorporeal conditions reject physical scar/missing-part records under the current profile while permitting explicit spectral-function loss. This is anatomy condition data, not a damage/healing/death or occult-action interpreter.
- Function/locomotion queries derive from immutable current condition. Legless and nonhumanoid fixtures use their actual body functions; no hand/leg assumptions are inserted by the generic query.
- Accepted source arrays cannot mutate body definitions, requirements, injuries or returned function-slot lists. The 64-part and 256-injury limits stop excess enumeration and reject instead of silently truncating a species or its history. Larger production workloads remain separately qualified work.
- Form bindings separate accountable actor, form instance, physical body/tether and exact body-definition pin. Invalid role aliasing and incompatible nature reject. Condition validation binds form identity and exact version. The bindings grant no controller, inventory, possession, uniqueness or transition execution, so the same actor in several qualification vectors is not a claim that several copies can act.
- The canonical encoder operates only on validated in-memory values and returns fresh bytes. The contract explicitly does **not** present the test JSON reader as an untrusted runtime loader; bounded admission decoding remains future compiler work.

## Executed checks and evidence attribution

The reviewer read all Actors source and tests, the complete authored Python oracle and the current contract. The reviewer ran `generate_actor_vectors.py` and compared its complete stdout with frozen `actors-v1.json`: **exact match, 29,232 bytes**, covering **five definitions, six conditions and three form bindings**. The oracle uses standard-library framing/hash logic and imports no production application code.

The coordinator reported the full current pinned integration passing **194 tests** after Actors and Production compiled. That execution belongs to the coordinator; this reviewer ran no competing .NET build and does not claim an independently executed native or lifecycle test. Current examples are authored qualification dimensions/thresholds, not source-stat conversions or final gameplay balance.

Remaining evidence includes real action/control/custody admission, movement geometry and equipment fit, effect composition, lawful healing and persistent injury history, life/death/form transitions, authored species breadth, native motion/presentation and human acceptance. The reviewed anatomy query does not mark any of those complete.

## Reviewed input snapshot

Hashes bind the inspected application, tests, oracle and final scope clarification. Later semantic edits require follow-up review.

| File | SHA-256 |
|---|---|
| [ActorFormBinding.cs](../../../Runtime/src/OpenFantasy.Core/Actors/ActorFormBinding.cs) | `f7c27d278f63a6bc40f92e1ec58e2bd4882cee1be3e118518a47191eb9544670` |
| [BodyConditionSnapshot.cs](../../../Runtime/src/OpenFantasy.Core/Actors/BodyConditionSnapshot.cs) | `6d8289ca0f6b3957d098dc87b68e1a1600bdd73cede6822fd9d08641fefe77c5` |
| [BodyContractEncodingV1.cs](../../../Runtime/src/OpenFantasy.Core/Actors/BodyContractEncodingV1.cs) | `ab68380d3511228b7cefe073330288b0c58a72f2370525ffbbdb3d800431e15c` |
| [BodyDefinition.cs](../../../Runtime/src/OpenFantasy.Core/Actors/BodyDefinition.cs) | `878dc032f0ef6228a421a3a72290c4590466ee44fcde66fa0eec6199d0188423` |
| [BodyDimensions.cs](../../../Runtime/src/OpenFantasy.Core/Actors/BodyDimensions.cs) | `5e10d7357d870e806860b67f70fdf5b3b2799d414df6f879127ffc5f0ff3912f` |
| [BodyFunctions.cs](../../../Runtime/src/OpenFantasy.Core/Actors/BodyFunctions.cs) | `9764c3475bfd4970aca8e6553113bb5731d8f69338f1a20d5a1bc51bca678e3f` |
| [BodyInjury.cs](../../../Runtime/src/OpenFantasy.Core/Actors/BodyInjury.cs) | `d0b586c749e4af33d26c1015009c2d3e3c8ec0de53c1183c9de52cdc87c17f44` |
| [BodyPartDefinition.cs](../../../Runtime/src/OpenFantasy.Core/Actors/BodyPartDefinition.cs) | `6a5a8d72e56647ae3d2e39e745ddacc7d63e7d3e9e9a79d3663b66da97b0a99b` |
| [BodySlotKey.cs](../../../Runtime/src/OpenFantasy.Core/Actors/BodySlotKey.cs) | `b42549c0cace6c4c36f71a3976cff7835d671a77088e2036d0c932ea28a28536` |
| [LocomotionRequirement.cs](../../../Runtime/src/OpenFantasy.Core/Actors/LocomotionRequirement.cs) | `acbe6648581a4b701ea79fe394b7ff7a1f25cb66fda2791eb5488ad0be25121e` |
| [ActorFixture.cs](../../../Runtime/tests/OpenFantasy.Core.Tests/Actors/ActorFixture.cs) | `a839c2e5b52faa3aa9ad8d12c3bebe6b26a737b6b61fad466a54c6a36f17274d` |
| [ActorFormBindingTests.cs](../../../Runtime/tests/OpenFantasy.Core.Tests/Actors/ActorFormBindingTests.cs) | `3c79fab2698d5dc8e6d114b1486a9312ac4e6ecff3969f881030a44cfff39898` |
| [BodyConditionTests.cs](../../../Runtime/tests/OpenFantasy.Core.Tests/Actors/BodyConditionTests.cs) | `ee7f53d5189a27a3d45aa5924d3d06eb1f4be2b73a6ea08c0815d384b75810ad` |
| [BodyDefinitionTests.cs](../../../Runtime/tests/OpenFantasy.Core.Tests/Actors/BodyDefinitionTests.cs) | `efac5a9b930618e62ee29ad13d81e9d62dd3f1c0fd4021622e12d091d604a2ae` |
| [generate_actor_vectors.py](../../../Runtime/tests/OpenFantasy.Core.Tests/Fixtures/generate_actor_vectors.py) | `d800e5fb096f7b0d67b4bfc3e3637051c63123897df205e4d4f8cf43d9e22824` |
| [actors-v1.json](../../../Runtime/tests/OpenFantasy.Core.Tests/Fixtures/actors-v1.json) | `42594f115d5d31b3814bebe1107510609b6390d6abb30873d2a4ac0655dd4fee` |
| [CONTRACT.md](../actor-contracts/CONTRACT.md) | `a93b44fc5c39835fca4d4750b5cdedd3d4d57c0570279aae89e450a1efc7ae85` |
