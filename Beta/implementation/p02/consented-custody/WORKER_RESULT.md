# Consented custody worker result

Status: COMPLETE for the assigned standalone Core authority. Nothing was staged, committed,
branched, cleaned or reset. No file outside the assigned ownership was written.

## Qualification

| Check | Result |
| --- | --- |
| `run_worker_checks.py build` | Exit 0, 0 warnings, 0 errors, `sourceUnchanged: True` |
| `run_worker_checks.py test` | Exit 0, 134 passed, 0 failed, 0 skipped, `sourceUnchanged: True` (`test-20260929T043418Z`) |

One build attempt and one test attempt were run; both passed first time and are retained by the
runner. The 134 results are the runner's `Authority|Custody` filter, which includes the existing
ordinary authority, encoding and custody regressions. 29 facts are new. Not run: the whole suite,
Hosting tests and any native route.

## Delivered

Standalone authority in `OpenFantasy.Core.Authority.ConsentedCustody`:

- `ConsentedTransferTerms`, `ConsentedTransferCommand`, `ConsentedTransferAcceptance`: the exact
  transaction, the sender command and the recipient approval bound by identity, hash and terms.
- `DeliveredTransferCommand`, `DeliveredTransferAcceptance`: each message with its own verified delivery.
- `ConsentedTransferAuthority`, `ConsentedTransferDecision`, `ConsentedTransferReceipt`.
- `ConsentedTransferCodecV1`: fixed-layout bounded frames (262/390/449/296-312 bytes).

Movement reuses `CustodyTransferPlanner` and `CountedAuthorityState.WithCustody`. No existing
shared API changed, so there is no API change request.

Tests cover: partial transfer between two OwnerOnly bags with exact source coverage, child and
remainder identity, revisions and unchanged policies; applied evidence; whole lot; forged or
borrowed delivery on either side; wrong owner, recipient, branch and world; substituted command,
amount, destination and revisions; replay and aliasing including retired identities and the
spawned child; equipped, reserved, insufficient, capacity and overflow; stale approval after an
intervening ordinary transfer; stale plan; player/NPC parity; codec round trip, truncation,
bounds and malformed frames.

## Decisions for integration review

1. One controller that currently controls both actors may supply both approvals. Both exact
   approvals are still required. Reject this at the Host if same-controller gifts are unwanted.
2. A wrong world on the sender command or commit throws, as in the ordinary authority. A wrong
   world on the acceptance alone is a `NotAuthorized` rejection, because it is client-supplied
   data paired by the Host.
3. Public reasons are `NotAuthorized` and `ResourceUnavailable` only, matching the ordinary
   authority. Revision conflicts are not distinguished.
4. Receipt world and branch are those the sender command names.

## Unresolved and limits

- Expected bytes come from an independent in-test frame writer, one literal 84-byte command
  prefix and the existing `authority-v1.json` child identity. No external oracle fixture was
  generated and SHA-256 values are computed over the independent bytes, not pinned as literals.
  The allowed tools could not produce a separate fixture file.
- The history callback is trusted input. The caller must record the command, acceptance and
  child identities on commit; this authority stores nothing.
- Capacity covers counted raw storage only, not combined raw and produced contents.
- Receipt visibility per participant is undecided.

## Requests outside ownership

- Add `consented-custody.md` to `Beta/Runtime/docs/architecture/README.md`.
- Cross-reference it from the gift paragraph in `ordinary-care.md` and the custody section of
  `materials-and-activities.md`.

## Required later integration, not done here

Persisted asynchronous offers, reservations and expiry; complete-world admission with production
and material history; world profile and action version; Host storage, journal, retry lookup and
recovery; continuation cuts; reach, readiness and time; produced goods; multi-item exchange;
NPC offer policy; native interface. The retained-world personal-dose objective remains open.

## Changed paths

- `Beta/Runtime/src/OpenFantasy.Core/Authority/ConsentedCustody/` (9 new files)
- `Beta/Runtime/tests/OpenFantasy.Core.Tests/Authority/ConsentedCustody/` (3 new files)
- `Beta/Runtime/docs/architecture/consented-custody.md` (new)
- `Beta/implementation/p02/consented-custody/WORKER_RESULT.md`
- Runner-retained logs, records and TRX under `Beta/implementation/p02/consented-custody/qualification/`
