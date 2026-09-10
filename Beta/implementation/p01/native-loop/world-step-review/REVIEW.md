# Independent world-step review

The initial critique was a read-only static review of the exact inputs hashed in EVIDENCE.json. A subsequently assigned session implementation closed its host-integration finding; SIMULATION_EVIDENCE.json records that implementation and the coordinated 82-test Hosting Debug run, including all 12 new session/NPC tests. The critic did not change the reviewed Core processor or codecs.

## Confirmed behavior

- Actions resolve in canonical actor order against the starting spatial snapshot. Intermediate inventory and dialogue candidates are passed to subsequent actions, so their effects compose deterministically within the batch.
- An advancing batch contributes one calendar increment and one experienced-time increment per resident, including when an action opens a reading hold. Remaining movement is suppressed whenever a dialogue remains open. Held-interaction batches contain only actions and never increment either clock, including the batch that closes the last dialogue.
- Movement checks the current admitted principal/session/actor control and the same anatomy and geometry for all actors. Dialogue checks actual reach, speaker vocalization, listener hearing, and separately pinned knowledge of the spoken channel on both sides.
- Item handover checks current actor control, target presence/revision/reach, source ownership and destination deposit policy before reading recipient storage. Lot aliases to either storage are rejected before constructing the expected-revision set. The lot-limit split guard and narrow custody/overflow catches prevent those client payloads from escaping as constructor errors.
- Candidates are immutable. New command, result, spawn and commit-cause identities are reserved against current state, earlier reservations in the batch and the supplied history lookup. A collision cannot silently overwrite an entity or commit partially. The durable caller still owns authentication, exact retry lookup, transaction ordering and publication.

## Actionable host integration concern

A wire-valid command can select an identity already occupied by another local actor, lot or historical cause. The command constructor excludes only its own envelope identities; `Reserve(action.CommandId)` then throws `InvalidOperationException`, aborting the complete selected batch. Current identity collisions should be rejected before host batch selection, with a recoverable response to that request.

There is also a collision that cannot be excluded by checking current state alone: a later command in a batch can use the predictable `ResultId` of an earlier command's outcome. The processor reserves all command identities first, then `Reserve(receipt.ResultId)` throws on the earlier action. The host needs an explicit recoverable path for such selection conflicts so unrelated actors' work is not lost. The reviewed code is atomic on failure; this is a batch admission/recovery concern, not partial-state corruption.

The follow-up `LocalWorldSession` now checks current/historical command occupation before enqueue and privately previews pending selections. A dispatch collision faults only that request and rebuilds an ordinary idle/NPC step with a fresh cause. A held interaction does not create an empty fallback commit. Recovery-budget failures now have a distinct `WorldRecoveryLimitException`, so they propagate to session completion rather than being hidden by fallback. Controlled tests for occupied-command and predictable-result collision cases passed in the coordinated Hosting run. The current session admits one player action, while the core reservation logic remains safe for future multi-actor batches.

## Restoration findings closed during review

The first snapshot-codec read rebuilt and re-encoded state without the full cross-domain admission checks. The coordinator added admission to the `WorldSnapshot` constructor; restoration now reaches it before returning the decoded root.

The first active-dialogue admission read checked listener language but only speaker anatomy. The coordinator and admission owner added the speaker's own explicit channel query. Both changes were inspected after they were applied.

The snapshot codec's final exact re-encode rejects unsorted nested lists and redundant body definitions, in addition to bounded framing and typed body/language decoders. Counts and nested byte bounds cover their admitted maxima. Resident UTF-8 text, enum modes, nonnegative normalized experienced time, exact profile actor binding and normalized signed calendar instants are preserved. These checks do not establish source-content provenance beyond the admitted pins or authenticate stored claims.
