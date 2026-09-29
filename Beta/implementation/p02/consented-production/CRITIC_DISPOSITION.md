# Lead disposition: revision required

The [original independent review](CRITIC_REVIEW.md) is unchanged. It returned verified Fable 5.1
with 1M context and requested Max effort, in 415.959 seconds within 600 seconds/$7, reporting a
$4.4230395 client estimate and no denials. It was a read-only source review with the omissions
stated in its report; it did not execute or inspect the qualification results.

**The assignment is not accepted as complete.** Preserve the worker's implementation as WIP and
do not freeze its new approval or V10 formats. Reusable movement/approval code is useful, but does
not discharge the world integration requirement. [The design revision](DESIGN_REVISION.md)
defines the correction and required proofs; the original assignment is not narrowed after review.

| Finding | Disposition |
| --- | --- |
| B1: disconnected V10 history | Accepted. The complete-world owner cannot consume this capability, and its missing raw chronology prevents directly replaying the proposed production-only history. Implement world-owned evidence, transformed custody validation and a real coordinated-world path. Retain or replace the experimental adapter on its merits. |
| B2: global revision pins in consent | Accepted. Bind consent to the specific goods/parties and lot revision; keep complete current-state freshness in execution/application. Prove an unrelated craft or bag change does not invalidate consent by revision alone while actual capacity, ownership, control and lot changes still matter. |
| B3: missing bounds, real construction and balanced mutations | Accepted as missing required evidence. Add exact operation/lot limits, actual installed construction and balanced custody/interval/operation mutations to the corrected complete-world unit. |
| Weak exception assertions | Tighten tests to the real intended failure stage/class. Some production decoding includes semantic construction checks; do not pretend that every invalid frame must survive decode. Rename misleading tests and establish valid controls. |
| In-memory cut contexts | The current tests are component evidence only. Reconstruct continuation from decoded closing segments in the complete-world proof. The external identity registry remains an explicit dependency, not evidence of a durable Host binding. |
| Possible raw-request exception | Unverified by the critic. Review the actual command constructor and guarded inputs during correction; add a concrete negative case if a reachable failure exists. Do not blanket-catch programming errors to make tests pass. |
| History-full check before authentication | Move authentication ahead of availability reporting in the corrected flow and test the failure ordering. |
| Stored delivery bytes | Clarify that decoded deliveries are historical claims/evidence, not authenticated ingress. The world/Host boundary must supply current verified control. |
| Missing World-side negative | Required in the corrected unit, in addition to production codec refusal. |

## Executed regression and limits

- Worker solution build: zero warnings/errors; 317 affected tests passed, including 13 new cases.
- Lead full Core regression: **1,642 passed**, no failures/skips, sources unchanged.
- Lead Hosting selection: **195 passed, five opt-in native cases skipped**, sources unchanged.
  The test process returned zero, but the retained qualification wrapper returned failure because
  it required every selected case to execute. This is not a clean pass for the complete selection.
  [The original result](qualification/integration-hosting.json) and TRX are retained. No native
  acceptance is claimed and skipped tests are not included in the managed pass count.

The Hosting filter accidentally included the explicit opt-in native fixture/result classes without
their external setup. Do not rerun unchanged managed cases merely to remove that notation, or launch
native gameplay solely to satisfy this overbroad filter. Subsequent qualification must select its
intended managed set explicitly and retain the separate native gates. The WIP remains unaccepted
independently of that selection error because B1/B2/B3 are unresolved.

No paid retry of the old implementation or review is authorized by their success exit codes. A
subsequent assignment must be concrete new integration work with its own ownership, synchronized
context and finite limits. The complete-world files were read-only in the old worker assignment;
the next unit must explicitly own the additional world/profile/archive changes it needs.
