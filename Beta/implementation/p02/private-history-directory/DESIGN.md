# Authenticated private-history discovery

## Scope

Add a bounded directory of terminal paid quotes and material proposals for the exact selected
participant, including records from earlier sessions and archived segments. This is the backend and
selected transport/application contract needed by the shared history browser. It does not claim that
the browser, a general life journal, quest history, or production history latency is complete.

The previous APIs inspect one known identifier; observed client identifiers cannot discover history.
No new authority, storage index, cache, world mutation, schema migration or simulation rule is needed.
The existing complete journal replay verifies archives, checkpoints, receipts and the pinned live head.
A page collector observes actual terminal publications during that replay and retains at most one page
plus one lookahead record. Nothing is disclosed until the entire replay and head comparison succeed.

## Contract

- `LocalPrivateHistoryKind`: `PaidQuote = 1`, `MaterialProposal = 2`.
- `LocalPrivateHistoryEntry`: kind, subject identity, counterparty identity, terminal sequence and the
  corresponding terminal status. No live stock, balance, sender-presence pin, credentials, private
  hash, world root or actionable offer. Exact terms remain behind existing participant inspections.
- Canonical subject identities retain the full 128-bit `EntityId` domain, including zero. This
  preserves the existing identity law; malformed encodings remain invalid.
- A page holds at most 16 entries, ordered by descending terminal sequence, then ascending kind and
  canonical subject identity. Ties are never skipped by a sequence-only cursor.
- `LocalPrivateHistoryCursor` carries the first page's inclusive `ThroughRevision` and the last
  emitted `(ClosedAtSequence, Kind, SubjectId)` tuple. The next page is strictly after that tuple in
  the same ordering. Appends after the cap do not move records between pages. A cursor is a filter,
  not an authority token; client-supplied values never choose the actor or bypass current permission.
- `LocalPrivateHistoryPageRequest` holds optional `Before`; no caller-controlled unbounded page size.
  Future or malformed cursors are expected refusals, not durable corruption or host faults.
- `LocalPrivateHistoryPage` echoes the request, carries the snapshot cap, copies the entries, and
  returns an exact next cursor only when another eligible record exists. Empty history is an ordinary
  result. Page validation checks ordering, identities, statuses, cap, cursor and request correlation.
- `ReadSelectedPrivateHistoryAsync(selection, request)` returns a selected result using new operation
  `ReadPrivateHistory` (append without renumbering existing operations). Home and delegated reads use
  the same explicit selection contract; delegated reads require only `PrivateHistory`.
- Check exact port/actor/generation/witness at admission, serialization and publication; retain current
  authority under the storage gate before retrieval. Foreign, open and due-but-unclosed records are
  excluded. Switching away/back or revoke/owner return invalidates late application of a result.
- A dedicated per-port directory slot shares identical outstanding requests and refuses different
  requests while busy. Complete request and selection are part of the key. Session and port shutdown
  join admitted directory work. Network uses its existing single query slot, queue and deadlines.
- Extend selected wire shapes explicitly, preserving every existing request/result version and byte.
  Directory response correlation includes its page request. Do not relabel a legacy result or fake a
  quote/proposal subject. Expose an optional directory interface on actual local/remote application
  sessions, with explicit availability and real observation capture on that same optional interface;
  existing selected-only test adapters need not pretend to support directory retrieval.

## Verification and exclusions

Pin existing tests/specs and predecessor staging before edits. Add contrasting tests for participant
filtering, terminal lifecycle, equal-sequence ordering, multi-page traversal, stable append, invalid
cursors, archive/reopen, corruption after a requested record, permission independence, stale selection,
queued revocation, identical task sharing, pressure and shutdown. Preserve original deadlines and laws.
Version/canonical malformed-frame tests and a real authenticated TLS discovery route qualify transport.
An independent critic reviews authority, completeness, cursor and lifetime boundaries. Run affected
managed suites and source-pinned builds under the shared lock; do not repeat unrelated native rendering
or Core suites solely to accumulate counts. Any inherited evidence needs exact dependency comparison.
No native history UI or physical/human acceptance is claimed by this increment. Retained unexplained
history timing and gift-opening failures, full crash/scale and campaign gates remain open.

## Ownership and preservation

Before-change checkpoint and all original inputs are pinned in `PRESERVATION.json`. The integration
lead owns docs, qualification, staging and commits. Workers receive disjoint implementation ownership;
all preserve unrelated staged predecessor work. External Claude's terminal spending-limit failure is
not retried; native workers use Astra capacity and provide no external-credit savings claim.
