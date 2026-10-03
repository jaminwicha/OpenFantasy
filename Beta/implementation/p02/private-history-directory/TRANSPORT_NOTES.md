# Selected private-history transport

The new optional `IPrivateHistoryPlayerSession` contract exposes
`ReadSelectedPrivateHistoryAsync(LocalActorSelection, LocalPrivateHistoryPageRequest)` on actual
owned local and remote application sessions. Existing `ISelectedPlayerSession` and its adapters are
unchanged. The immutable page carries request correlation; the existing selected envelope retains
world, branch, complete selection and operation correlation.

Selected input version 2 belongs only to `ReadPrivateHistory`. It retains the versioned binary selection
header and appends the operation byte plus a one-byte cursor-present flag. A present cursor appends
big-endian `ThroughRevision` and `ClosedAtSequence` (eight bytes each), kind (one byte) and canonical
subject identity (16 bytes). First-page requests have no subject identity. All prior operations remain
version 1 with their existing payloads and canonical bytes; version relabeling is refused.

Selected response version 2 belongs only to the new operation. Its result adds `Directory`, containing
`Request`, `ThroughRevision`, `Entries` and `Next`. The directory property is omitted when null, retaining
the eight-field legacy result shape and version 1 bytes. New responses exclude inquiry/quote/proposal
payloads and subjects, require the exact PrivateHistory witness capability, and validate typed page
ordering, closure statuses, cursor/cap and complete original request. Canonical parsing rejects unknown,
missing, duplicate and malformed fields. All EntityId bit patterns remain legal subject identities;
malformed wire text/length is distinct from a valid zero identity.

The server dispatches through the selected local directory reader. Existing network single-query
ownership, bounded output queue and deadlines remain in force. The protocol client authenticates
directory request correlation through its existing selected-result route before checking the current
selection generation. The remote facade joins the actual foreground task and shares its existing query
slot with status refresh. It neither polls history nor retries a refusal.

Added checks, pending lead execution at this worker's handoff:

- `PrivateHistoryDirectoryCodecTests`: legacy explicit request bytes and result fields; version
  separation; first/continuation request shape; complete response correlation; malformed statuses,
  identities, ordering, duplicates, cap, cursor, counterparty and missing/unknown fields.
- `PrivateHistoryDirectoryTlsTests`: authenticated real WebSocket/TLS application flow creates and
  withdraws an actual proposal, reconnects through a fresh port, discovers its prior subject without
  supplying an ID, and inspects that discovered result. A real PrivateHistory-only delegate reads it
  after the original allowance, sees no Inventory/Trade/Recovery facets, and cannot use inventory
  inspection. Away/back invalidates the captured result. An outbound directory request deliberately
  held while the real owner returns and revokes is refused without terminating the connection.

The TLS case uses the existing disposable certificate pin and original fixture deadlines, not production
trust overrides. It includes actual allowance waiting and is not a native rendered/UI proof. The held
outbound test proves queued stale-request refusal; it does not claim delayed inbound result delivery
or native browser lifetime evidence. Archive completeness and local work sharing/shutdown belong to
the boundary worker's separate tests. No build or heavy test was run by this worker; serial qualification
is owned by the integration lead. A lightweight `git diff --check` passed before handoff. No commit or
staging action was performed, and existing tests/specifications/predecessor staging were left intact.
