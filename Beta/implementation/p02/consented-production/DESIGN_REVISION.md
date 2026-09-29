# Integration revision after independent critique

The [independent review](CRITIC_REVIEW.md) is accepted on B1 and B2. The worker's implementation is
recoverable WIP, not the completed integration assignment, and its new V10/profile-8 and material
approval formats are not frozen. Existing published formats, including raw ConsentedCustody V1,
remain immutable. The next implementation must prove the full material outcome in the coordinated
world instead of accepting a disconnected component as a substitute.

## Consent terms and execution freshness

Consent describes the intended material transaction: world/branch, named participants, source and
destination, exact lot identity/kind/definition, quantity and the approved lot revision. A change
to those terms requires new consent. The approved lot revision prevents offering one interval and
silently spending a different surviving suffix after partial consumption or another transfer.

Global production/custody revisions and unrelated storage changes must not be part of long-lived
consent. They are execution freshness checks. The executor reads the actual current owners, checks
current control, ownership, stock, protection, reservation and shared capacity, then pins the whole
candidate to those exact owners for atomic application. There is no weakening of concurrent commit
protection: a candidate decided against one state cannot publish against a changed state.

Consequences to prove:

- Another actor crafting an unrelated item does not invalidate otherwise unchanged consent.
- An unrelated item entering/leaving either bag does not invalidate consent by revision alone;
  current capacity and ownership still decide whether the transaction is possible.
- Moving, consuming, reserving or changing the approved lot, losing required control/ownership,
  or changing the actual terms prevents application without partial effects.
- Raw movement still uses the real custody planner with execution revisions captured from current
  state. It must not reinterpret the old raw V1 contract or manufacture a recipient approval.

Offer lifetime, expiry, withdrawal and reconnection remain the explicit Host/offer-owner contract
in [the binding review](WORLD_BINDING_REVIEW.md). This revision does not claim an asynchronous offer
implementation. It ensures the material contract does not force every unrelated world action to
invalidate a future offer. Historical delivery bytes are evidence of a recorded submission, not
current authenticated ingress.

## Complete-world ownership

The coordinated world keeps raw movement, gathering and work under their existing owners. Do not
rebuild its raw state by replaying an isolated production-only ledger that does not contain those
operations. Use world-owned ordered consent evidence and the shared material decision step at the
actual world transaction boundary. Production's validation must account for legitimate transformed
custody partitions and their retirement/recraft ancestry using that evidence, while continuing to
reject arbitrary balanced mutations. There must remain one authoritative inventory per lot kind.

Specify exact evidence binding before publishing bytes: participant roles, approved terms, command
and acceptance identities, actual moved interval/result and the relevant original/candidate material
owners. Compare with the existing qualified evidence owners. Hashes cannot certify themselves or
introduce a cycle in the state whose hash they describe. Complete publication hashes belong to the
existing journal boundary; any in-state witness must have a well-defined acyclic interpretation.

An explicit complete-world capability/profile, codec, admission and coordinated archive continuation
must support the transaction. Older worlds continue to reject the new interpretation. Keep the
reusable approval/decision logic where it earns its place; revise or replace the experimental V10
adapter if necessary. Preserving WIP does not require shipping an incompatible architectural branch.

## Required correction proofs

1. In a fresh coordinated world, craft actual stock, perform a partial bilateral gift through its
   world step with both control and physical opportunity/reach checks, consume it through ordinary
   care, cut through the actual coordinated archive owner, reopen, give onward and cut again.
2. Derive the next continuation context from a decoded closing segment rather than reusing the
   original in-memory context. Preserve exact lineage, identity roles and intervals throughout.
3. Complete real construction and refuse transfer of its installed reserved output. A reserved
   ration remains a useful unit test but does not replace the real construction proof.
4. Exercise exact material-operation and combined live-lot limits, including split versus whole
   movement, a legitimate cut and identity reuse from closed history. Preserve all bounds.
5. Mutate otherwise balanced lots: swapped custodians, moved suffix instead of prefix, and removal
   of the matching operation. Separate structural decode checks from authority/admission rejection;
   assert the intended failure rather than accepting any exception from either stage.
6. Prove rejection by old World admission as well as old production codecs. Keep the original golden
   inputs and legacy runtime interpretations exact. Add independent vectors for the final approval
   format once the semantic contract is settled; the current private vector draft is not a freeze.

Existing scoped behavior tests and full Core regression provide useful evidence about the WIP, not
completion of this list. Unrun opt-in native tests remain unrun. Retained-world migration, durable
dual-participant offers/retries, native controls, the original personal-dose acquisition route and
all full-phase/production gates remain required after the complete-world Core proof.
