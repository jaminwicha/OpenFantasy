# Lead integration checks

This is a review plan, not acceptance evidence. Implementation belongs to the bounded Opus assignment; Astra reviews the exact delivery and runs independent integration checks after its terminal result. Preserve every failed check and the original limits.

| Boundary | Independent acceptance question |
| --- | --- |
| Currency-only causality | Does a successful payment change only the ledger and progression revision, leaving every actor revision/stat/HP/MP/XP exact? Can removing, duplicating or reordering payment evidence still produce an admitted candidate? |
| Interleaved causes | Can genuine earned quest payment, a trade, damage/restoration and another trade replay in actual command order, including multiple participants in one world batch? Are unchanged legacy resource batches still nonempty and exact? |
| Historical prefix | Does settlement validate the goods and funds at its own boundary, rather than requiring an old lot to remain current forever or merely checking the final ledger? Can an existing lot be crafted, consumed or sold after a paid transfer without losing lineage? |
| Quote ownership | Is the issuer's control checked at issuance and settlement? Are quote/acceptance payload claims distinct from admitted delivery? Does the buyer/seller own the exact storage and account on both quote sides? |
| Spatial/time/work | Are actual presence revision, manipulation, reciprocal reach, readiness, Wait and both actors' competing work honored? A failed acceptance must not consume the issuer's useful work opportunity. |
| Complete rollback | Do late validation failures restore inventory, production, progression, quote/causal owners and created IDs together? Normal time/step consequences must remain accurately distinguished from a transfer. |
| Ordinary boundaries | Can an open quote carry through a real automatically selected cut with unchanged terms and issuer revision? Can it settle after that cut, followed by another cut/reopen? Expiry and bounded terminal state cannot be lost or held forever. |
| Durable authentication | Does SQLite reopening replay the admitted genesis and complete journal, compare actual archive bytes and return exact receipts on lost-ack retry? Structural decoding alone must not authorize fabricated history. |
| Identity closure | Do quote IDs, acceptance commands and spawned lot IDs remain occupied after closure/cuts? Can a historical acceptance alias any original source/actor/definition or reappear as an unrelated command? |
| Format separation | Do all old frames retain exact rules, including their rejection of paid data? New profile support must be explicitly listed in every host codec, creation, schema, archive and journal path; numeric ranges are insufficient. |
| Growth and capacity | Are quote/evidence capacity and the next complete batch reserved together, with selectable headroom? Refusing overflow forever or dropping open offers is not continuation. |
| Scope honesty | Fresh new-profile persistence does not prove migration of the retained profile27 save, native UI, process-kill safety, production latency or multiplayer authentication. Keep each remaining proof explicit. |

Read the most consequential changed methods and representative adversarial tests rather than replaying the external conversation. Use a single independent critic at the coherent checkpoint, targeting remaining uncertainty. Preserve the source database, all513 prior test/fixture files,23 frozen specs and the predecessor index against `PRESERVATION.json`. Check affected timeless architecture links and source claims in the same increment.
