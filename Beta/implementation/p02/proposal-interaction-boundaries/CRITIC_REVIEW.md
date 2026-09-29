# Bounded source critique

Native Astra critic `/root/scheduled_proposal_critic`, read-only source review. Initial six-minute
scope: session inspection/stop/failure/serialization, participant disclosure, captured exact creation
terms and profile30 versus deferred excavation. No external model, edits, builds, tests or GUI.

The first review found no confirmed source defect, but a meaningful test weakness: an unlocked
inspection could finish before Stop, leaving the shutdown wait unproved. Integration changed query
scheduling so a free session slot is acquired before returning the pending task, while the durable
query always runs on a worker even on that synchronous acquisition path. The actual authority-lock
harness now forces an in-flight read, starts Stop, checks it remains pending, releases the lock and
checks both completion and exact reopened state. No sleep establishes the selected boundary.

A two-minute source recheck found no concurrency regression. Occupied semaphore acquisition stays
asynchronous and lock ordering remains consistent. The critic confirmed the forced shutdown test and
separation of live writer exclusion, tampered-file reopen refusal and backend failure/no-cache cases.

The critic also confirmed that deferred structural consequences are excluded by complete reservoir-
seal admission in the coordinated profile, not merely absent by fixture convention. No combined
profile or private pending root was fabricated. Lead-owned executable results are recorded separately.
