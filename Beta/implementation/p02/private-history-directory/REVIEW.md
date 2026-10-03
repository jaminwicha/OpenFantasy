# Independent review

Native Astra critic `/root/selected_trade_native` reviewed the design and frozen implementation as a
read-only assignment, independently of the two implementation workers. No Claude request or claimed
external-credit saving occurred. The critic ran no builds or tests; execution evidence is separately
owned by the integration lead.

The review covered exact selected authority and PrivateHistory independence; participant filtering
before pagination; full journal/archives/head verification after the requested cutoff; mixed-kind and
identity ties; bounded collector retention; per-port work sharing and both shutdown joins; legacy
canonical input/result bytes; request, world, branch and selection correlation; and honest proof limits.

Three review improvements were incorporated before qualification:

1. Preserve the complete canonical `EntityId` domain. Default-valued subject identities are legal;
   the directory must not introduce a narrower identity rule. The entry/cursor validators and
   architecture match Core, and a positive zero-subject cursor/page codec test complements malformed
   identity-text tests. This is a value-domain proof, not an attempt to manufacture a derived zero hash.
2. Add a direct new-directory check for tactical expiry while quote/proposal books remain open.
   Older inspection tests alone do not prove the directory's lifecycle boundary. The new case keeps
   the actual combat/time semantics and checks that reading changes no authoritative state.
3. Cover per-port disposal separately from complete host shutdown for the new inspection slot.
   The theory retains the pending read, allows a lawful completion before closure linearizes, joins
   closing work, and checks the other participant and host remain usable.

The collector also evicts before insertion at capacity so it never transiently holds more than the
page plus lookahead. At final frozen-source review the critic reported no remaining blocking or
nonblocking actionable finding. The integration lead's first build found a missing `ContentHash`
namespace import in the new storage file; the next build found a missing `AuthorityDispatchException`
namespace import in the new TLS test. The retained failures and import corrections are part of
qualification, not a changed design or relaxed gate.

This review is source/test-design evidence. Delayed inbound directory-response delivery, native history
browser interactions, production replay latency/scale and the complete campaign remain separate proof
obligations. The TLS case's deliberate outbound hold proves stale request refusal after owner return
and revoke; it does not substitute for the delayed inbound response case.

The first TLS execution exposed a new-fixture reconnect race: client disposal completed before the
server released that actor's active admission binding. The fixture now observes the exact owner's
`Active=false` under the admission service's own lock, within the existing five-second polling bound,
before issuing one reconnect. This is read-only test reflection, not a production admission change,
reconnect retry or fabricated disconnect. A subsequent authenticated board revision proves durable
progress, not the identity of a particular disconnect event. The first reconnect's acceptance claim is
fresh-session discovery after observed release. The later actual 30-second allowance, delegated read
and owner-return/revocation assertions remain separate and unchanged. The critic reviewed this narrow
correction and found its synchronization sound with that evidence limitation.

Final application integration found an additional facade gap: the owned local wrapper accepted a
selected query but exposed no public way to capture its real port selection. The lead preserved the
192-case qualified intermediate state in `API_CHECKPOINT.json` before completing that contract.
`IPrivateHistoryPlayerSession` now exposes availability and actual observation capture. Owned local
uses its existing home port; remote requires its negotiated selected profile. Availability is not a
PrivateHistory grant. New local-interface and legacy-remote cases cover that distinction; the existing
new TLS case obtains its actual home/delegated captures through this interface. The independent critic
reviewed the correction and found no blocker. Its local test proves a real empty-genesis page through
only application interfaces; terminal discovery remains the separate real TLS/archive proof. Final
qualification reruns affected families because these facade sources and assemblies changed.
