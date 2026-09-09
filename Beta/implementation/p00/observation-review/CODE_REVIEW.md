# Independent observation contract review

9 September 2026. Reviewer: `p00_core`; implementation owner: coordinator. Runtime was read only throughout this review. The exact inputs and execution evidence are recorded in `REVIEW_VALIDATION.json`.

## Disposition

No unresolved concrete blocking defect was identified in the reviewed, bounded resource-observation contract after the two findings below were addressed. No concrete defect was identified in the actor comprehension profile, exact-channel query and encoding contract inspected during the same review. The independent Debug run passed **24/24 tests: 13 observation tests and 11 language tests**, with no failed or skipped cases. Both independent Python reference generators reproduced their committed fixtures byte for byte.

This is a review of portable projection and receptive-language profile boundaries. It is not acceptance of authenticated networking, an actual committed knowledge store, acquisition/perception, a complete language system, or playable NPC intelligence. The coordinator owns full integration evidence. This reviewer owns the bounded Language static inspection described below; an initially planned duplicate review by another critic was canceled. That ownership correction does not expand the work actually performed or alter the original test execution evidence.

## Findings and disposition

| ID | Finding | Resolution and evidence |
|---|---|---|
| OBS-01 | Rejecting only a record's own private-source/disclosure alias allowed another record's public identity to equal that private source identity. The field whitelist alone did not prevent this local alias leak. | Closed. `ResourceKnowledgeSnapshot` intersects all private source IDs with public disclosure, subject, material definition, reporter, world, branch, actor and epoch IDs across the full bounded input. `ResourceObservationProjector` also checks the public principal and session. `PrivateSourcesCannotAliasPublicIdentitiesAcrossRecordsOrAudience` passed in the independent run. Whole-world identity integrity remains an explicit future insertion requirement. |
| OBS-02 | A source commit could be read as the old event's commit rather than the commit recording the current knowledge version; that interpretation could admit knowledge acquired after a pinned head. | Closed at this contract boundary. The field is now `RecordedCommitSequence`; code states it records this actor's knowledge, and the final contract explicitly requires the commit of **this exact knowledge version**. The constructor rejects zero and the snapshot rejects a sequence beyond its pinned head. `FutureUncommittedForeignAndDuplicateRecordsRejectBeforeProjection` passed. Proving the claimed number against real durable records remains authority work. |

## What was checked

The seven observation implementation files, observation tests, Python wire oracle, final observation contract, and their direct primitive/definition dependencies were inspected. The initial API recommendation is retained separately in `DESIGN_RECOMMENDATION.md`.

- Exact principal/session/world/branch/actor/control binding and missing/revoked binding produce one generic access failure, with no private identity or reason in its message. Journal construction and supplying a current binding are trusted-host operations, not proof of authorization.
- A snapshot accepts one actor's complete current resource view, rejects every future/foreign/duplicate/over-limit candidate atomically, copies its entries, and sorts by unique actor-local disclosure revision. Its 1024-entry limit applies to an actor-specific input and must not be used to scan a hidden global stream.
- Public serialization admits only known audience, disclosure and claim fields. The public DTO does not retain the server snapshot or source metadata. Changes only to the hidden head, recording commits or private source IDs preserve public canonical bytes and JSON. No arbitrary string or event-payload channel exists.
- Direct observation, report, belief, inference and dream remain distinct. Reported quantities and acquisition times are preserved without silently consulting authoritative stock. Zero quantity is distinct from absence; a present zero reporter ID is distinct from omission. Confidence is an assessment scale, not a calibrated probability or truth flag.
- The encoding has deterministic ordering and explicit framing/version/pinned definition bytes. Independent vectors exercise negative dates, minimum signed day, maximum quantity, optional reporter, revision gaps and clearing a full view. The encoder deliberately has no untrusted decoder in this increment.
- The final documentation accurately states that subject/material/reporter identity and precision must already be permitted by acquisition. No hidden identification lookup or automatic translation is implied.

## Bounded Language inspection

This reviewer also inspected all seven Language implementation files, all 11 Language tests, the fixture loader, the independent Python oracle and its fixture, and `language-contracts/CONTRACT.md`. The conclusion applies to the retained actor comprehension profile and query behavior:

- A channel requires an exact language, register and communication mode. Spoken, signed and written channels do not borrow bands from each other; another register, language or actor does not provide an implicit fallback.
- Query subject identity and rules hash must match the profile. A recorded channel must also match the requested definition version before comparing bands, including an explicitly recorded Unfamiliar band or a zero-band requirement. These are consistency checks, not authentication or permission grants.
- All four bands use an explicit inclusive threshold. Missing channels return Unfamiliar with `HasRecordedEntry = false`; meeting an explicitly zero prerequisite does not imply understanding. Invalid mode/band values, null inputs and duplicate exact channels reject.
- Construction defensively copies entries, limits input to 256 unique channels with rejection at the first excess entry, and exposes read-only state. Canonical ordering covers language ID, register ID and mode; profile identity includes the actor, revision, rules and pinned entry definitions/bands. Encoding buffers cannot mutate the accepted profile.
- The contract correctly limits this to receptive knowledge. It does not grant perception, expression, organizational membership, translation, testimony access or truth. Host permission filtering, actual learning and historical causes, dialect progression, dialogue admission, animation and player usability remain integration work. The finite profile bound is not a world language-catalog limit or permission to discard history.

The independent run exercised the 11 retained Language tests, including all 16 band-threshold combinations, pin failures, absence semantics, ordering, immutability and finite enumeration. Its Python oracle matched three authored profiles and eleven query expectations. These checks qualify the small profile/query contract; they do not qualify the full planned language system.

## Integration boundaries retained

These are planned integration obligations, not claims that this P00 module already implements them:

1. The authority must resolve current control bindings from authenticated state and establish the exact committed acquisition version, apparent identity, precision, permitted confidence and reporter. A constructor cannot prove those facts.
2. Actor-local disclosure epochs, IDs and revisions must be assigned transactionally and remain independent of unrelated hidden activity. This module validates supplied values; it does not generate or persist the stream.
3. An already-created immutable projection remains encodable after later revocation. Delivery, cache reuse, session handoff and client application must revalidate current bindings and enforce epoch/revision recovery. Revocation cannot erase knowledge already delivered. The final contract states these limits explicitly.
4. The output replaces only the complete resource-knowledge view. The finite operation limit does not permit discarding causal history, silently truncating knowledge, or treating a replacement as a delta. Paging/qualified expansion and history retention still need implementation.
5. Full perception, testimony, receptive language, learning, motives, relationships and decisions must feed permitted knowledge. Neither a direct-observation label nor a successful language-band query grants administrator truth or omniscient NPC planning.

## Independent execution

Executed against coordinator-built, stable Debug binaries using the private pinned .NET 10.0.401 executable and the installed .NET 8 test runtime. The reviewer did not restore or rebuild. The exact command, start/finish times, test names, per-domain counts and captured assembly hashes are in the manifest and TRX.

| Evidence | Result |
|---|---|
| Observations filtered test domain | 13 passed |
| Language filtered test domain | 11 passed |
| Observation oracle stdout versus committed fixture | Exact match, 5274 bytes, 3 vectors |
| Language oracle stdout versus committed fixture | Exact match, 13226 bytes, 3 profiles / 11 query expectations |

Tested assemblies were hashed immediately after the independent run and before notifying the coordinator that Debug rebuilding could resume. Their hashes identify the actual tested binaries, not a semantic review of every module compiled into those binaries. Subsequent rebuilds should be distinguished from source changes when auditing the manifest.
