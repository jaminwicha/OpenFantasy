# Ordered resource history: incomplete worker output

The authorized `fable[1m]` / Max-effort implementation run hit its 1,200-second limit.
Its context postflight passed, but it returned no complete history, codec, tests or usage report.
The [sanitized result](failed-dispatch.json) preserves the failure and original limits.

Two partial source files are preserved as `.cs.txt` under `partial/`; these are untested design
candidates, excluded from runtime compilation. The worker checkout remains retained. Neither
these fragments nor the three-pass analysis qualifies resource-history or live care integration.
No automatic paid retry occurred. A smaller follow-up requires the separately requested approval.

The last accepted resource primitives and 1,887-case qualification remain in
[resource composition](../resource-composition/README.md). The next useful deliverable is a complete
ordered-history domain and adversarial tests; canonical codec, causal world admission and durable
writer integration remain distinct required steps. A self-consistent effect chain is not proof of
an authorized cause. Unchanged-resource XP rewards still require revision continuity.
