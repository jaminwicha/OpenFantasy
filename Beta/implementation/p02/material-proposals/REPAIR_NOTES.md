# Saved-proposal repair: working notes

Worker record for [REPAIR_ASSIGNMENT.md](REPAIR_ASSIGNMENT.md) against
[INDEPENDENT_CRITIC.md](INDEPENDENT_CRITIC.md). The original worker report and notes stay exact.
Astra owns integration, process tests and Git. No Git mutation, package or toolchain change.

## Plan

1. **Anchor carry (P1-1).** The coordinated anchor captures proposal capability and the exact open
   roster from the actual closing owner. Old anchor bytes stay exact when the capability is absent.
   The successor check requires equal capability and exact carried grants, open or terminal.
2. **Participant outcome after archival (P1-2).** An exact-identity historical lookup authorizes the
   current delivery first, then filters one complete verified replay under the authority lock.
3. **Expired before its expiry (item 3, P2-4).** Admission refuses the outcome; due-but-open stays.
4. **Opportunity and control contrasts (item 4).** New tests on the admitted fixture only.
5. **ExpectedActorRevision (P2-3).** One check for all four commands after authorization.
6. **Structural checks (P2-4).** Created count, earliest expiry sequence, settlement participants.
7. **Tactical advance past expiry (item 7).** Tested if the admitted fixture makes it practical.

Checks are sequential through the four permitted runner shapes only.

## Outcome

Items 1, 2, 3, 5 and 6 are implemented and pass on build `proposal-build-20260929T114625Z`.
Items 4 and 7 are partly open. [REPAIR_RESULT.md](REPAIR_RESULT.md) lists code, evidence and the
exact incomplete cases.

## Decisions

| Question | Choice | Reason |
|---|---|---|
| What the anchor hashes | The roster of full proposal encodings, appended only with the capability | Exact content, and unchanged bytes for profiles 27 and 28 |
| Where the carry rule runs | Continuation constructor and anchor successor check | Construction and decoding both refuse; one rule owner |
| Source of the archived outcome | The replayed closing root, after its bytes equal the retrieved archive | No retained root on the segment and no second replay |
| Missing identity | Always one replay when an archive exists | No identity-table shortcut that would answer differently by occupancy |
| Absent acting actor | `Unavailable` before the revision check | The resolver never throws for a command |

## Failures and corrections

No check failed in this unit. One test defect was corrected before the first build: the extended
pressure test first took the archive's first outcome as its withdrawn example, which the added
accepted offer now precedes. It selects a withdrawn outcome explicitly.

No permission was denied. No tool outside the four runner shapes and native file tools was used.
