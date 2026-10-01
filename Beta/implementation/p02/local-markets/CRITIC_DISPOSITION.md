# Independent critic follow-through

The [independent report](INDEPENDENT_REVIEW.md) is preserved as delivered. Its two actionable
findings are repaired rather than waived. The critic read source and existing evidence; Astra
implemented the focused fixes, reviewed the resulting paths and executed the regressions.

| Finding | Repair and contrasting evidence |
|---|---|
| F1: stale showing leaks remaining quantity through refusal reasons | Disclosed issuer-buy lot failures return one `Unavailable` result. Both market and ordinary bilateral routes supply the disclosure boundary; profile 32 keeps its prior interpretation. Four Host cases first issue a lawful quote, then make a real partial gift or full move and probe allowances one and two. Every refusal preserves inventory, production, progression, quotes and market state while ordinary time advances. |
| F2: fee payee as counterparty crashes sell preparation or prepares an unusable buy | Preparation checks the actual quoted counterparty account before constructing either quote. Two Host cases include actual gifting/showing, an unavailable payee counterparty and successful issuance to a lawful other counterparty. Preparation itself leaves the complete head unchanged. |

The added cases were run before the fixes: `new-host-20261001T033339Z-7c324` produced four failures
and 42 passes. Both partial-gift routes returned `RevisionConflict`; sale to the fee payee threw,
and the fee-payee buy unexpectedly prepared a command. The full-move contrasts already passed.
After the fixes, build `build-20261001T033447Z-62f50` and `new-host-20261001T033522Z-66a88` pass:
46 exchange Host cases, including all six additions. Logs, source/build pins and the original failed
TRX remain in `qualification/`. [Final checks](FINAL_CHECKS.json) records the broader integration result.

The architecture now distinguishes directly exposed stock fields from inference through prices,
and structural acceptance of a missing carried showing from journal-authenticated withdrawal.
Bilateral prices remain unconstrained by the market policy and require separate seller acceptance;
no extra restriction was added. Observation/showing causes remain covered by durable command
reservation; registering additional root identity roles is not required by this increment.

The pre-fix tested state and delivered critique are recoverable through
[CRITIC_FIX_CHECKPOINT.json](CRITIC_FIX_CHECKPOINT.json). No existing test was removed or weakened.
No second paid critic was needed for these localized repairs; the independent review, failing
regressions, direct review and final checks form the evidence chain.

The reusable privacy and economy lessons are committed upstream in GameDevAIEcosystem `19e8200`.
The maintained technical-design skill and its two installed copies match; all 71 manifest entries
verify. Sync backups and restore receipt are under
`~/.local/share/openfantasy/delegation-backups/market-lessons-20261001/`. The skill frontmatter is
unchanged; the optional validator remains unavailable under the selected Python because PyYAML is
missing. The prose, diff and source/target hashes were checked without installing a dependency.
