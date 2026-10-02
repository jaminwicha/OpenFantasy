# Review and failure disposition

The native transport worker owned Networking termination/admission/channel and server cancellation
classification plus new tests. Astra owned host control/timing, accepted-work scheduling, integration,
qualification and reference docs. A separate native critic reviewed source and tests without editing
or running checks. No Claude request occurred; these native agents consume Astra capacity.

The initial source built and passed 46 focused cases. Those checks did not cover every race:

1. Lead review identified cancellation-origin loss: internal abort can wake a competing I/O loop
   before the failing loop records its reason. The adapter now records typed origin before canceling;
   the session reads that origin and records response-deadline origin before waking socket cleanup.
   A forced competing-loop test retains the original exception and proves the resulting journal event.
2. The independent critic found a lone returning player's accepted-choice deadlock. The ready gate
   prevented the work required to restore control. The host now permits only retained care, staging,
   the returning actor's committed choice or retreat to reach that boundary. Required new human choices
   still hold. New single-human combat, staging and finite-care tests prove eventual restoration,
   paid costs and subsequent pause without automatic readiness.
3. The critic found that credential revocation/expiry could occur while establishment awaited world
   serialization. Final validation now occurs inside that gate, before establishment, outside authority
   failure handling. Actual held-gate tests invalidate/revoke/expire credentials and prove refusal
   without journal change or host failure.
4. The critic found request abortion merged with server shutdown during admission. They now remain
   distinct during both admission and gameplay. A post-establishment peer abort records loss; a failed
   Welcome or shutdown does not. Deadline success at the boundary still fails under the original limit.

The [WIP checkpoint](CRITIC_CHECKPOINT.json) preserves the implementation before critic corrections.
The critic's bounded follow-up found these three corrections address its counterexamples and no
remaining actionable defect in that scope. Source critique is not executable or human acceptance.

## Verification configuration and observed failures

The first broad server selection ran without a configured Godot executable: 72 passed, four skipped
(the three native cases and Windows ACL). The wrapper correctly rejected the aggregate. Original
report: `qualification/server-20261002T150321198898Z/report.json`. No skip was reclassified as a pass.

Enabling native cases in the same broad selection overlapped the native roster journey, two Godot
process cases and other server fixtures. The four-player managed TLS test missed its original
five-second observation deadline at PlayerRosterHostTests.cs:104. Preserve the report and outputs in
`qualification/server-20261002T150507505479Z/`. This is an observed failure under concurrent workload;
concurrency alone does not establish its cause. A subsequent isolated pass cannot prove that the
concurrent-load or production timing concern is solved. No assertion/deadline is changed.

The configured broad run finished with 74 passed, one failed and one Windows skip. Both native client
cases and the four-native-player journey passed; their individual evidence is retained separately
from the unaccepted aggregate. The exact failed TLS case then passed alone in 7.632 seconds, and the
prior workflow's managed server selection passed 72 cases with only the Windows skip in 29.257 seconds.
This restores the established workload split, not a looser deadline or suppressed assertion. The
concurrent failure remains a production/load qualification concern with unproven cause.
