# Independent critique and integration disposition

The [original critic report](CRITIC_REVIEW.md) is retained unchanged. One read-only Fable 5.1
review used `fable[1m]`, one-million context and requested Max effort. It completed in 420.08
seconds within 480 seconds/$5, with a $4.1379245 client estimate, no permission denials and
verified context postflight. These are routing/usage observations, not measured net credit savings.
The reviewer inspected the first lead receipt fix, but did not run tests or inspect the 138-case
result. The lead implemented and verified the subsequent corrections; there was no second paid review.

## Findings

| Finding | Disposition and evidence |
| --- | --- |
| P1: receipt matches an unrelated moved lot | Reproduced before correction. Applied receipts now require either the original lot or the deterministic split child. A characterization explicitly shows that this does not determine which of those two actually moved. |
| P2: applied receipt matches a nonbinding approval pair | Reproduced before correction. Applied matches require `acceptance.Binds(command)`; rejected submissions need not bind. |
| P3: outcome/commit are not established by matching | A limitation of a data matcher, not a durable authenticity mechanism. The reference and a characterization now demonstrate that changed commit sequence, plausible whole/split identity and rejected outcome claims cannot be adjudicated by `Records`. Actual decisions and authenticated history own that proof. |
| P4: test name overclaims policy coverage | Renamed the existing control-only test. Added a real storage-owner change: an old candidate becomes stale and the old approvals cannot authorize a fresh decision. |
| P5: never-emitted rejection reasons decode | Reproduced for `RevisionConflict` and `InvalidAction`. Constructor and decoder now admit only this contract's None/NotAuthorized/ResourceUnavailable values and enforce applied/rejected invariants. No existing receipt codec changed. |

The four failing counterexamples for P1/P2/P5 are retained in
[critic-receipt-red.json](qualification/critic-receipt-red.json). They supplement the four earlier
[metadata substitution failures](qualification/receipt-binding-red.json). The final solution build
and affected regression are retained as [build evidence](qualification/critic-final-build.json) and
[test evidence](qualification/critic-final-core.json), with exact source pins. No failure or gate
was rewritten into a pass.

## Challenged boundaries

- **U1, protection flags:** retain the existing explicit manual-transfer semantics. Equipped stock
  and actual reservations block movement; UserReserved/Heirloom/Favorite persist on moved stock.
  Three new cases pin this behavior. It is not permission for an automatic NPC to disregard its
  preferences; willingness belongs before proposal/approval creation. No old rule was weakened.
- **U2, history concurrency:** `ApplyTo` pins counted state, not external history. The reference
  requires the durable caller to serialize occupancy checking, identity reservation and commit,
  or revalidate complete occupancy at commit. The standalone component cannot establish that join.
- **U3, rejected identities and expiry:** terminal/retry admission and offer expiry remain explicit
  Host obligations. A future implementation must avoid both reapplying a terminal rejection and
  allowing forged counterparty claims to occupy another actor's identities. No durable rejection
  policy or asynchronous lifecycle is claimed here.
- **U4, claimed counterparties:** rejected receipts can contain unverified participant claims.
  Visibility, retry lookup and identity reservation must use verified delivery provenance;
  matching or decoding a receipt grants none of them.

The architecture also retains explicit reconnection/current-control rules, the shared spawn
namespace, NPC control parity and a new contract version for produced-goods kind/definition pins.
The [integration plan](INTEGRATION_PLAN.md) remains mandatory. The critic found no blocker in the
standalone decision path; it did not qualify a world, persistence or gameplay binding.

## Reusable lessons

Ecosystem commit `8201a6d970c0223a1ceb682f5e0b68f5d1dc068a`, preserved by
`codex/milestone-handoff-receipt-discipline`, records independent receipt-field mutation checks
and lossless handoff compaction between pinned agent runs. Six installed skill/reference targets
across Codex, shared agents and Claude match their maintained sources. The formal skill validator
could not import PyYAML; unchanged frontmatter, local links, exact installed bytes and diff checks
were verified separately. No runtime or skill-validation pass is inferred from that unavailable tool.
