# Independent native Astra source critique

The bounded critic reviewed the production selection, captured action, fixed-expiry, input,
profile-opening and native panel code without editing or running checks. This is independent native
Astra review, not an external Fable request; Fable's retained monthly-limit failure was not retried.

The critic found one concrete P2 defect: Godot OptionButton does not emit ItemSelected when the user
chooses its already-selected row unless AllowReselect is enabled. After a sender observation changed,
the wheel correctly required reselection, but the same-row operation silently failed to recapture.
The gift-offer dropdown now explicitly allows reselection. The native probe selects the same actual
offer twice and requires exactly one signal for the second selection.

Two additional native concerns were identified without being presented as proven defects. The lead
made displayed popup row identities stable until closure and wired all gift/header directional and
Tab neighbors to the visible gift controls. Pack rebuilding preserves the active gift chain. These
prevent a live refresh from reinterpreting a displayed row or restoring hidden-item focus links.
Native evidence records visible pack focus through a complete Tab circuit.

No other confirmed authority, retargeting, consent or submission defect was found in the reviewed
production paths. The lead separately reviewed the new fixture/result authentication code and owns
build, native rendering, journal authentication and acceptance. Actual results and limitations belong
in INTEGRATION_REVIEW.md; source review alone is not qualification.

## Opus repair and lead integration review

The owner's subsequent explicit Opus5.5 request dispatched a bounded implementation repair, not a
second ceremonial critic. OPUS_DISPATCH.json records verified returned `claude-opus-5-5`, 1M context,
requested Max, no denials and verified pre/post synchronization. The source repair took 868.279s and
reported a $4.4244242 client estimate, not an asserted bill or net credit saving. Its 95,850 output
tokens include 81,065 thinking tokens. It delivered a useful causal diagnosis and an explicit held-press
owner contract, but the time/reasoning overhead is high for a narrow fix. Prefer larger cohesive
assignments when using this authorized route; Max and 1M remain owner doctrine. No external retry occurred.

Astra accepted the owner-per-press approach rather than releasing every event indiscriminately,
which could also activate a focused HUD button after a routed world/dialogue action. The lead found
one additional boundary in the proposed source: a duplicate panel accept could reclaim a router-owned
press established by focus recovery. The integrated guard refuses that reclaim until release. Six
new managed cases contrast panel/world/dialogue/cancel, missing releases and focus return with/without
a duplicate press. They are verification additions, not a claim that Opus ran tests. Native logs
record actual GUI controller press and release delivery; all gates remain unchanged.
