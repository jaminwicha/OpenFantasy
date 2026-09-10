# Quest client and local projection review

10 September 2026 UTC. Independent static inspection by the worker that authored the Hosting migration. The coordinator owns the reviewed client/session/projection changes; this reviewer changed only the newly assigned client regression test and review/validation documents. No runtime source was edited and no builds, native probes or rendering checks were run for this review. [CLIENT_REVIEW.json](CLIENT_REVIEW.json) binds the exact inspected inputs.

## Finding QC-01: supplied gathering progress could be skipped

The original `FarmQuestSaveOpener.Open` accepted a null intermediate production destination together with an existing gathering source. Its upgrade branch then had no destination for the intermediate production copy and fell through to fresh quest genesis. The normal default path supplied both destinations, but the engine-free helper admitted an argument combination that could silently skip supplied progress.

The coordinator added an explicit rejection when the quest destination is absent, the intermediate destination is null and the gathering source exists. Reinspection confirms that this occurs before save creation. An initial placement of the guard also blocked an already existing quest destination; the coordinator restricted it to the absent-destination case, preserving selection precedence.

**Disposition:** fixed in inspected source; regression execution pending. This finding is not marked finally closed until the linked client tests execute. `FarmQuestClientTests.cs` includes both the rejected fresh-destination combination and successful existing-destination precedence with that same source argument combination.

## Save selection and lifetime

An existing quest destination is opened and validated against the expected world, branch, profile and admission pin. Failure does not select an older save. With no quest destination, the client checks the referenced sources for incomplete SQLite file sets, preserves a newly produced intermediate production copy, and prepares the exact quest request from the retained production head. The Hosting operation reacquires ownership and rechecks that head before publication. Source data are not supplied as a mutable replacement inside the request.

All content loading, save selection and initial identity/control lookup occur before `LocalWorldSession` takes ownership. Failure before that transfer disposes the authority. `FarmClientSession` performs no fallible work after creating the live session beyond assigning its immutable adapter fields. The existing `FarmGame` close-during-opening path remains the sole consumer of the opening result while shutdown is pending; the main callbacks stop publishing/accepting input during that close. This review found no new lifetime leak in the quest startup changes. Native shutdown qualification remains separate evidence.

When no quest destination exists, an incomplete referenced gathering source currently rejects even if a complete production source also exists. This is the inspected conservative recovery policy, not a claim that every referenced backup is necessary to replay the production save. Existing quest destinations take precedence and do not read those unused source files.

## Quest requests, gifts and display truth

The UI shows an explicit Accept/Deliver label only during an addressed offer. `FarmGame.Give` routes that interaction to `FarmQuestActions`; outside such an offer, the existing ordinary Give request remains separate. An ordinary gift does not create acceptance or completion. Closing the dialogue removes the current addressed quest action while retaining the actor's known commitment.

The linked request builder first emits `AcceptDeliveryQuestIntent`. After acceptance it selects from the current player's sorted inventory, requiring raw provenance and the projected unprotected/unreserved availability flag, and stops at the exact admitted quantity. The authority still checks the exact item/version, owned storage and lot revisions, conversation, capacity and reward. The view omits the item version on each lot, but world admission pins one exact version per item identity; the request also binds the exact quest definition hash. A stale view can therefore cause a rejection without changing the material bill or reward.

Completion text says that supplies are set aside. It does not display a finished windbreak or claim construction occurred. The message about a reward is shown only for an applied receipt. Other failures use rejection feedback rather than locally updating money or quest status.

## Projection and pacing

Quest offers are visible only while addressed, or as this actor's retained participation. New progression fields contain only this actor's level, XP, HP/MP and currency. Reward terms and acceptance availability are intentionally public offer information; the reserve's account identity, balance, obligation count, project identity/reservation and other actors' progression/currency are not serialized in this view. Source intervals, production batches and durable root hashes remain outside it.

The current locality is explicitly sight-complete and exposes actor positions/presence revisions. The carried-forward crafting view also exposes the shared production revision as a concurrency token. This review therefore does **not** establish a general cross-actor noninterference policy or global knowledge model. The new quest fields avoid adding a global quest revision as a player dependency.

Quest input uses the same bounded pending slot, current control and durable receipt lookup as the other actor commands. UI event frequency does not advance the clock. A held interaction adds no advancing steps; accepting or completing an offer does not accrue gathering effort, calendar time or offline simulation. Readiness, focus loss and closing remain owned by the existing session/lifecycle paths.

## UI and qualification boundaries

The inspected scene's unique names match `FarmHud` lookups. Header, action row and dialogue are authored in `FarmHud.tscn`; content visibility and disabled states are driven by the projected state. Static structure does not prove legibility, focus behavior or rendered layout at 960×540 or other sizes.

The client intentionally selects a single offer per giver and the admitted farm has two residents and one quest. Broadening content to several simultaneous offers requires a selection UI before retaining these `Single`/`SingleOrDefault` assumptions. `CanComplete` is an owned-item/level/addressed affordance, not a promise that every later authority condition will pass.

Six new engine-free client cases are authored in [FarmQuestClientTests.cs](../../../Runtime/tests/OpenFantasy.Hosting.Tests/Simulation/FarmQuestClientTests.cs). The test project links the exact three client helper source files; it does not replace them with test reimplementations. The cases cover QC-01, three orphan sidecars, actual production progress/quest destination precedence, and explicit acceptance followed by exact six-unit raw selection excluding protected wood. Execution is pending at this review checkpoint.

The earlier coordinator Debug qualification is [715 passing Core/Hosting tests](hosting-validation/DEBUG_VALIDATION.json). That run predates QC-01 and these six client cases. It remains valid evidence for its recorded source, and is not reused as a pass for the later fix or native presentation. No other concrete blocking client defect was identified within this bounded inspection.
