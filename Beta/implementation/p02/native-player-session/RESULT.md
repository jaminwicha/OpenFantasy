# Native participant session binding

The ordinary Godot game can join the dedicated player host through a private invitation and platform
TLS trust. It receives actual admitted presentation identities, actor views and participant status,
then uses the same immutable offers, controls and action receipts as the local game. The client owns
its connection; the host owns the world and saved history. Closing one does not close the other.

`IPlayerSession` separates participant operations from privileged local history verification and route
advice. The local adapter preserves existing host ownership. The remote adapter bounds startup and
status refresh, prioritizes one waiting foreground query, propagates merchant inquiry outcomes and joins
all work at shutdown. Queries 16/17 provide immutable presentation and coherent participant status;
old query bytes remain unchanged. Status and view are independent publications, not a remote barrier.
Shared pause labels describe this participant's input rather than promising stopped world time.

## Evidence

[Final checks](FINAL_CHECKS.json) pin the current build, source, binaries, scripts and reports. The
aggregate has **446 distinct passing cases**, including all prior 56 server, 188 networking and 167
client-regression cases. Twelve new managed session/query cases and two new native process cases
cover this binding. Additional shared-session and presentation checks retain their original assertions.
Windows credential ACL execution is explicitly unrun on this Mac.

Two actual Godot processes, headless and rendered, join a real TLS listener using production cargo
genesis. Each walks, commits Talk and Close, pauses and disconnects. Reopening the host's database
authenticates both exact command receipts, final feet and replayed state. No local client authority,
regenerated genesis, teleport, grant or automatic action retry is used. The disposable fixture pins one
loopback test certificate with hostname checking; production `--join` retains platform trust.

The rendered route starts unfocused before window creation, with normal focus binding and no local
pause. It proves startup withholds readiness before any injected focus event; the OS window never
acquires focus. Root inspected the captured frame: player and Wren are visible, with the shared-input
pause message. The asset kit is provisional. Injected keyboard and background rendering do not prove
physical devices, human usability or production art.

The automatic Waiting→Offered test uses the real scheduled native-market composition, SQLite authority,
merchant policy, admission and protocol. It authenticates the observed quote against the authority,
without an explicit inquiry read. This is separate from the production cargo/TLS test.

## Failures and review

The [independent review](CRITIC_REVIEW.md) found the initial focus assumption and missing automatic
inquiry coverage; both are corrected and exercised. Native probe guards now wait for displayed offers
to match their public actor/target/dialogue state. The first stale Talk receipt and subsequent Close
timeouts remain preserved. No command revision repair, retry or deadline extension was added.

The initial broad presentation selection had 155 passes, two absent-manifest failures and four
absent-manifest skips. Its report remains incomplete. Supplied retained artifacts subsequently pass
five authenticators; the old trade-boundary artifact fails its initial public-view hash comparison.
Instead of rewriting that artifact or its assertion, fresh current-build fixtures ran all three
expired/unfunded/moved-merchant Godot journeys and passed the unchanged journal authenticator. These
three processes plus the two network journeys give **five fresh native processes**. The retained five
journal authentications are current-binary checks of earlier recordings, not fresh execution of those
UI routes. Every selected case is covered by an actual pass in the aggregate; original failures remain.

The wrapper serializes resource-heavy checks and pins source and binaries. All **649 prior test/fixture
files**, **23 frozen specs** and exact unrelated predecessor staging remain unchanged. Architecture
pages and launch instructions describe the actual binding; all 1,228 architecture file links resolve.

## Checkpoint and remaining scope

Pre-change ref: `codex/checkpoint-before-native-player-session-20261002T105857Z`, preserving
`ed0e590bf200420aab630a44bb414b51edb4c000`. Accepted scope is preserved by
`codex/milestone-native-player-session-20261002`; work stays on `codex/runtime-foundation`.
Native workers/critic used Astra capacity. No Claude request, fallback or retry occurred after its
terminal account limit, and no Claude credit savings are claimed for this increment.

Next integration: actual concurrent native players and pause/reading/combat interactions, local
envelope parity, recorded disconnect/reconnect/controller handoff and adverse transport qualification.
The full 1–4-player production claim, Windows/Linux/mobile, listening-host exports, production resources,
all broader simulation/content/asset plans and human acceptance remain open. This checkpoint closes
the first native network binding only; it does not narrow the full game or close a phase.
