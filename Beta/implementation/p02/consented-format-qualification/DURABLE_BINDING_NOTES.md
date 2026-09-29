# Durable binding reconnaissance

Read-only integration notes against runtime commit `1b922ba`. These are next-unit constraints,
not implemented Host behavior or acceptance. No runtime changes accompany these notes.

## Existing owners to extend

- `Hosting/Worlds/SqliteWorldAuthority.cs` selects schema eight only for world profile 27.
  `WorldJournalRecovery.cs` independently enforces that relationship for genesis and replayed states;
  `WorldDatabase.MaterialCareArchives.cs` also checks profile 27 when tying a closing archive to the
  stored genesis. Profile 28 must be explicitly supported across all these joins, not only accepted
  by the snapshot decoder. Keep exact expected-genesis opening and all old interpretations unchanged.
- Schema eight's existing material/care archive table already stores opaque bounded root bytes,
  scope, ordinal, boundary hash and publication sequence. Evaluate explicit profile-family reuse
  against its actual layout before inventing a duplicate schema/table. Several existing schemas
  serve multiple world profiles. Any reuse must retain genesis-selected interpretation and fail
  unsupported combinations; it is not permission to migrate an old world or broadly allow higher versions.
- `WorldCommitPreparation.cs` already accounts for every selected action's distinct exact receipt
  and commits the resulting identities. `SqliteWorldAuthority.Commit` publishes the root, journal,
  receipts, identities, checkpoint and returned archive atomically. Preserve this sole-writer path;
  a separate inventory service or transfer table cannot become another stock authority.
- `WorldMaterialCareArchiveReplay.cs` checks actual retrieved closing bytes against journal replay.
  `WorldJournalPolicy` retains the existing 1,024/256 MiB archive and other journal/state bounds.
  Do not raise these limits as a side effect of enabling profile 28. Reopening cost and production
  history scale still require their own proof.

## Permission and application boundary

`WorldCommitPreparation.ValidateDispatch` reconstructs delivery claims from an already trusted
selected batch. That consistency check is not network authentication. `LocalWorldSession` receives
an independently supplied delivery, admits only its player actor, and authenticates receipt lookup
before inspecting current state. It has one pending player action, not a two-player or persistent
proposal service. Its per-intent profile guards must also be audited before enabling a new profile.
Simply changing storage's numeric allowlists cannot establish authenticated two-party gameplay.

A durable Core-batch binding can be qualified without pretending it supplies the asynchronous offer
lifecycle. Any subsequent public paired ingress must verify BOTH actual deliveries before availability
or prior-outcome disclosure; copied payload principal/session identifiers cannot provide that proof.
Tests must use two independent controls, wrong delivery on each side, receipt retries after reopen,
identity collisions including refused commands, complete raw/produced rollback and journal corruption.
Do not manufacture a second delivery from the offer's named recipient.

A proposal-creation receipt is a completed creation event. Later acceptance must yield its own
transaction outcome, not rewrite that earlier receipt or reuse its committed command identity.
Persisted proposals require explicit expiry/withdrawal/disconnect and stale stock/reach/control rules;
reusable care permission, friendship or inventory deposit policy cannot stand in for consent.
No asynchronous UI/protocol is implemented by the simultaneous Core command pair alone.

## Required durable journey

Use the existing actual craft/gift/recraft/use/decoded-cuts fixtures through the SQLite owner, with
failure injection at real commit publication boundaries. Verify reopened journal replay, both exact
receipts, every permanent identity, all material/resource/care owners and authentic archive retrieval.
Exercise a successful four-actor/two-pair batch and a refused pair before and after recovery. A stored
canonical snapshot with recomputed hashes is not a substitute for replaying actual accepted commands.
Separate local trusted ingress from eventual network and physical/native input acceptance.

The retained old personal-dose world still needs an explicit admitted transition. Fresh profile-28
admission and a fresh durable test world must never silently upgrade or replace that saved history.
Follow the full consented-custody INTEGRATION_PLAN and original campaign; these notes do not shrink it.
