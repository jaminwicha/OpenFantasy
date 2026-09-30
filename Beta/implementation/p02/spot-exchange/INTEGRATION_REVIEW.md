# Atomic exchange integration review

State: standalone component accepted after integration tests and independent native review.
The full P02 two-market cargo/escort loop is not complete.

## Preservation and delegated delivery

Base: `a658a5910f777b31bf4a66d621f89ff62a9446e9` on `codex/runtime-foundation`.
Before-change ref: `codex/checkpoint-before-spot-exchange-20260930T033258Z`.
Opus delivery snapshot: `codex/checkpoint-spot-exchange-delivery-20260930T035049Z`
→ `631c053a58689e8d2898acf498be25b9bae9478f` (WIP, not acceptance).
507 existing tests/fixtures, 23 frozen specs, original retained database and unrelated predecessor
index are pinned in PRESERVATION.json. Root-owned new tests may change; old tests do not.

The implementation request used exact `claude-opus-5-5[1m]`, requested Max effort, finite 2,400s /
$25 client-estimate limits and verified pre/post context/skill synchronization. Returned metadata
confirms Opus 5.5 and 1M context. Provider returned success in 766.361s, estimate $3.5727244;
the adapter returned `permission_denied` because an optional shell read exceeded the wrapper-only
Bash grant. No approval was pending. No retry or fallback occurred. This is not measured billing or
net Astra savings. The code delivery was incomplete despite 373 focused passes: continued history
and several required contrasts were omitted. Preserve OPUS_RESULT.md as the worker's original account.

## Integration findings and corrections

1. Continued produced sales were refused outright; raw paid movement on ordered history lacked an
   operation and could throw during replay. Add explicit context profile 10 / Production V12, genuine
   four-owner settlement evidence, ordered paid operations, and shared goods arithmetic for decision
   and replay. Earlier profiles refuse both kinds. Payment is never represented as gift approval.
2. A fee naming the seller was legal quote data but failed the ledger's distinct-payee requirement.
   Aggregate credits to each account; preserve total supply and advance that account once.
3. Quote issuance omitted current production identity occupancy and pinned only the book at apply.
   Include production and pin inventory, production, progression and book for the decision.
4. Strengthen canonical book shape, recorded delivery equality, finite issuer counters and moved-lot
   identity shape. Add actual continued buy/craft/sell/cut/onward-sale, protected/reserved stock,
   stale terms, all-owner plans, numeric refusal, malformed frames and world-admission contrasts.
5. First integration focused run passed 394/395. The unchanged V10 hostile-format test detected that
   newly recognized operation 11 changed an older decoder's exact refusal. Gate the new decoding
   branch by its explicit capability before reading the payload; keep the original test and failure.

Two root test-build failures used inaccessible internal helpers in new test files; use public owner
reconstruction and an independent frame writer instead. These failures remain in qualification/.
No acceptance threshold, old fixture, world profile or source artifact was weakened.

## Contract and remaining joins

Settlement evidence has no public constructor/decoder. V12 decoding requires genuine opaque proofs;
material authority replays exact goods and approvals. Hashes observe payment/book owners without
owning duplicate balances. This standalone evidence is not durable world authorization.

A host/world binding still must journal all four owners together, authenticate retained settlement
causes, join spatial/time/work rules, publish coordinated archives, prove interrupted commit/retry,
and expose honest UI and dynamic local market knowledge. Pricing, NPC merchants and cargo travel
are retained in INTEGRATION_PLAN.md, not counted as delivered by this component.

Architecture updates cover the new boundary and version. Full solution build, 1,727 Core cases and one hosted admission contrast pass (zero skips).
The independent Opus critic timed out after 900.182s without a report; its unavailable usage is not
zero cost. The separately announced native critic completed its static review without actionable correctness
findings; see INDEPENDENT_REVIEW.md. Standalone component acceptance is recorded; no claim of native, human, performance, multiplayer or production acceptance is made.


Tested WIP: `codex/checkpoint-spot-exchange-tested-20260930T041800Z`
→ `ef0fdf32f23d8ad441f58d411f3cf61486fef14b`. Tests and preservation are qualified;
this checkpoint does not claim independent acceptance. See qualification/INTEGRATION_CHECKS.json.


## Accepted checkpoint

`codex/milestone-atomic-spot-exchange-20260930` preserves the integrated standalone contract.
The accepted source matches the passing solution build, 1,727 Core cases and one actual hosted
admission contrast: 1,728 distinct passes, zero skips, 30 new cases. The unchanged V10 regression
passes after the capability-gated decoder correction. All 507 original tests/fixtures, 23 frozen
specs, original database and unrelated predecessor staging remain exact. All 131 checked local
links on the five affected architecture pages resolve. No further runtime changes followed the
passing checks; final edits add reference coverage and acceptance/routing evidence only.

Opus provided useful partial implementation but needed substantial lead completion. Its separate
critic attempt returned no evidence before timeout. The native critic completed the gate with no
findings. These measured outcomes justify honest, bounded use; they do not establish net savings or
provider-wide quality claims. No additional Claude request is pending or authorized as a retry.
