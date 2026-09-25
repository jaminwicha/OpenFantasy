# Bounded independent review

2026-09-25. Native Astra critic Godel reviewed the timing source, existing exposure arithmetic,
new exposure binding/codec, focused tests and read-only Host boundary. No Fable invocation or external
model handoff was used. The assignment was read-only; root owns integration and verification.

The critic identified a trust boundary during design review: scope/revision/unit monotonicity and
content hashes cannot authenticate accepted timing ancestry or the original exposure binding.
The implementation explicitly requires caller-authenticated history, pins the expected subject/rate
on restore, and retains a counterfeit-history test illustrating that arithmetic plausibility is not
an effect grant. Live world ownership and the full history/receipt integration remain open.

Completed source review found no substantive timing, carry, canonical-recovery or authority-boundary
defect. The critic confirmed all-human gating before automatic confirmation, exact required scope,
wide arithmetic, initial-exposure reconciliation, normalized carry and read-only Host proposals.
One strict-analyzer issue was noted: a test decode call omitted the test cancellation token. The build
also reported this; root added the token. Final compilation and the complete Core suite pass.

Review is source-level evidence. The independent Python oracle and actual Host process runs provide
separate behavioral/byte evidence. Neither substitutes for live world/crash recovery, co-op, native
combat or human playtest acceptance.
