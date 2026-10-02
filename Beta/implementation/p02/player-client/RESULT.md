# Player protocol client checkpoint

The reusable C# client owns the retained authenticated protocol over local and actual TLS transports.
It validates Welcome and the first complete regional view while unready, bounds ordered input,
correlates genuine receipts and queries, coalesces identical pending actions and recovers missing
delta bases. Shutdown releases callers and joins I/O without claiming world ownership. The production
connector uses platform TLS trust. The timeless architecture reference describes these contracts.

Build and244 distinct affected cases pass: server56 (23 new client cases plus all33 previous), and
networking188. The Windows DACL case remains explicitly unrun on Mac. `FINAL_CHECKS.json` binds exact
reports/source/binaries, prior-case inclusion and preservation. Tests cover actual TLS, genuine receipts
and retry, initial refusal, substituted Welcome/query/receipt fields, full recovery, pending history
responsiveness, queue saturation, startup cancellation/expiry, input suppression and send deadlines.

The independent [critic](CRITIC_REVIEW.md) found no concrete source defect. Its two evidence gaps now
have checks: identical pending submissions share one task/envelope; blocked and late-successful sends
terminate at the absolute deadline. The first build failed because new tests used a private fixture
constructor and internal core encoding helper. They now use the existing fixture factory and direct
wire-field mutation, with canonical decode/encode validation. A subsequent analyzer failure was fixed
using the assertion's predicate overload. Raw failures remain unchanged. No old test or gate changed.

All644 existing tests/fixtures,23 frozen specs and the exact unrelated19-file predecessor index remain
intact. Pre-change `codex/checkpoint-before-player-client-20261002T103703Z` preserves
`f2ec8674a1e8133650f1c89722839e0055c03e55`. The verified milestone is
`codex/milestone-player-client-20261002`; work stays on `codex/runtime-foundation`.

## Routing and remaining scope

One native worker owned the protocol implementation; Astra wrote connector/tests and reviewed/integrated
the complete unit. One native critic reviewed it independently. No Claude request followed its recorded
monthly spending limit. Native agents consume Astra capacity and establish no Claude credit savings.

This checkpoint does not implement Godot network play or solo envelope parity. [Design](DESIGN.md)
records the native-binding prerequisites: authoritative run status, content-bound presentation bootstrap
and bounded Waiting-inquiry refresh. Do not infer global readiness or reconstruct server genesis for
display metadata. Native1–4-player proof, journaled disconnect/reconnect/controller handoff, other
platforms, production resources, full composite-world limits, physical devices, art and human acceptance
remain open. The complete campaign and all unclosed phase/production gates remain preserved.
