# Independent integration review

These are native Astra workers; external Claude's retained spending-limit failure remains terminal.
No provider request or external-credit saving is claimed. Reviews cover independently authored code,
not an author reviewing its own implementation. Test execution is recorded separately in qualification.

## Protocol and host authority

The codec worker reviewed the integration lead's protocol/administration and the host worker's consent
boundary. Two actionable findings were accepted:

1. A queued selected observation was installed as the admissible Resume basis before its bounded send
   completed. The server now installs the exact captured frame only after successful transport send
   and elapsed-deadline verification, then emits `SelectionDelivered`. The client waits for the matching
   proof at startup and explicit selection completion. Pending proofs are bounded; an old lifetime's
   delayed proof cannot confirm a newer selection. New transport barrier tests exercise this boundary.
2. Client observation ordering checked selection generation but allowed a different actor/witness at
   the same generation, or a regressing home-control generation. Equal selection generation now requires
   complete selection equality; control-port identity is stable and control generation cannot decrease.

The host worker independently reviewed the repaired delivery ordering, bounds and lifetime checks.
It found no authority bypass in the inspected repair. It identified an intentional transient API refusal:
spontaneous restored/selected observations publish immediately, but Resume requires the separate delivery
proof. This promptly exposes revocation and home obligations without accepting premature readiness.
The native manual toggle handles `InvalidOperationException` as feedback. Root review also found that
polled movement could encounter a newer selection between render and input, treating the expected
refusal as terminal failure. `SelectedInputUnavailableException` now distinguishes this transient
condition, including proof-pending Resume, from actual connection loss; native movement/readiness and
wheel capture discard it with feedback. No stale action is redirected. Startup and explicit selection
tasks await confirmation. This source review does not establish rendered or physical input acceptance.

## Scoped serialization

The native application worker independently reviewed the codec worker's binary/JSON codecs and narrow
rehydration paths. No concrete production defect was found. The review confirmed unchanged legacy
view codecs, the existing 16 KiB inner command bound, aggregate 900 KiB wire / 16 MiB expanded selected
observation limits, and current-only filtering of expired offers. It identified missing lawful pending-care
roundtrip evidence. Four new cases cover actual patient/helper pending care under Recovery-only and
CareDisposition-only permissions, precise treatment/cancel/decline pins and withheld private dose fields.
Those managed cases do not substitute for live custody or native care presentation.

## Shutdown race

The unchanged selected-query shutdown test exposed an admitted queued history read acquiring the
serialization semaphore concurrently with stop cancellation. Its post-acquisition availability check
reported `ObjectDisposedException` instead of cancellation. Independent source investigation identified
the exact lock/stop interleaving. The host now checks stop under the input lock before that availability
check and cancels the admitted read, outside the backend-fault handler. New calls after closure retain
their original disposal/authority refusals; executing selected reads retain current-authority validation.
An additive barrier-based regression is separate from the preserved original test.

## Scope of conclusion

Source critique and added tests are useful evidence, not acceptance by themselves. Qualification retains
every build/test failure. Four-player TLS, compatibility, native rendering, process-kill recovery,
platform coverage and production timing retain their separate gates. Full domain-screen coverage remains
open as described in CLIENT_NOTES.md; no reduced UI replaces the complete specification.
