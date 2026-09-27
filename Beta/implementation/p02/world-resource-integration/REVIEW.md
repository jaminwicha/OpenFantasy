# Integration review and disposition

The bounded native critic `resource_integration_critic` read the complete in-memory causal binding,
writers, codec, rollback and journey tests without edits, builds or tests. It replaced the timed-out
external review; no second paid request was issued. The lead reviewed and integrated its findings.

| Finding | Correction and executable evidence |
| --- | --- |
| P2: current/released episode could select scenario B at ordinal zero while history retained scenario A at the same ordinal | Join the retained episode to the historical ordinal-to-identity map. A regression uses a valid alternate admitted scenario identity, not an arbitrary invalid ID. |
| P2: an unknown completed-quest participant could leak `KeyNotFoundException` before world membership admission | Guard historical actor lookups for attacks, rewards and treatment. A component-valid altered quest archive fails complete-world decode with `FormatException` wrapping an admission error. |

The critic found no additional concrete defect in writer/rollback parity, complete MP charging,
funded reward replay or bounded parsing. That is a scoped source review, not exhaustive proof.
Its suggested reward-before-attack coverage was added: actual earned level/stats feed a later real
encounter and survive canonical restoration. Archive-exhaustion transaction coverage belongs to
the still-open authenticated continuation work; no durable lifetime acceptance is claimed here.

Lead refinements also retain the current episode's authority-derived identity, prevent two historic
scenarios from sharing an episode ordinal, and verify a treatment helper's historical speed.
No medical permission, gameplay rejection rule or acceptance threshold was relaxed.

External evidence remains separate: [Fable write denial](failed-dispatch/README.md) and
[Opus timeout](opus-review/README.md). Neither provided accepted implementation or review findings.
