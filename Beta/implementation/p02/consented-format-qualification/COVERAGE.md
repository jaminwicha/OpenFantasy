# Consented format coverage and provenance

Mapping of vectors and the critic's negative matrix to executed tests. "Existing" names tests that
were already in the tree at `1b922ba` and were reused, not duplicated. Nothing here is Host, native,
retained-save or production acceptance. Formats are not declared frozen by this document.

Test classes: **CWF** `Core.Tests/Worlds/ConsentedWorldFormatTests` · **CMF**
`Core.Tests/Production/ConsentedMaterialFormatTests` · **CQ**
`Hosting.Tests/Care/ConsentedWorldCodecQualificationTests.cs` (partial `ConsentedWorldPublicationTests`).

## 1. Vector provenance

Generator: `format_vectors.py` (standard library only; imports/invokes no runtime code, reads no
runtime output). Fixture: `Core.Tests/Fixtures/consented-world-formats-v1.json`, SHA-256
`51bf661f1c576c13e15647370de6e41d7141ce81a8406a561eb39c95cfc372f2`; `--check` matched after the final build.
Each entry pins hex, length and SHA-256. Identities reuse the fixed semantic values of the read-only
`consented-material-v1` generator (`0x0102030405060708 / 0x1122334455660000+n`); none of its output is read.

| Vector | Independent? | How the runtime is compared | Test |
| --- | --- | --- | --- |
| `termsRaw`, `termsProduced` | Yes, every field | C# builds terms from facts; `EncodeTerms` bytes equal | CWF offer/acceptance |
| `actionOffer`, `actionAcceptance` (V15) | Yes | C# builds commands from facts; `WorldActionCodecV15.Encode` and dispatcher bytes, `ContentHash`, decoded fields, offer hash | CWF offer/acceptance |
| `stepBatch` (V28) | Yes, incl. two signed movement samples | C# builds the batch out of order; encode bytes, decoded fields, re-encode | CWF step |
| `evidenceRaw`, `evidenceProduced`, `history`, `historyEmpty` | Yes, incl. SHA-256 split-child derivation and both content hashes | **Decoder entry**: evidence has no public constructor. Fixture bytes decode, every field equals the stated fact, `Encode()` reproduces the bytes, evidence/history `ContentHash` equal pinned facts. Empty history is encoded from a constructed object | CWF evidence/history |
| `operationV10Raw`, `operationsV10` | Yes (Core journey facts, identities `0/n`) | Extracted from the actual `ProductionCodecV10.Encode` frame of a real journey gift and compared byte-for-byte | CMF vector |

Limit: the evidence vectors prove decode, field meaning and re-encode. They are not a
construct-from-facts encoder proof, and evidence produced by the real planner carries runtime owner
hashes that no independent generator can state; those stay covered field-wise by existing tests.

### Compositional vectors (NEW framing around opaque imports)

Literal segments (magic, version, field count, tag, profile/version number, fixed-width prefixes,
field order) come from the generator. Slots are imported by the test at run time from the named
origin and are **not** independently encoded. Each composition pins a synthetic example; the C#
assembler must first reproduce that example before it is applied to actual bytes.

| Composition | Independent part | Opaque imports (origin) | Test |
| --- | --- | --- | --- |
| `snapshotV28` | 24-field envelope, tag, profile 28, order | 20 owner fields + resource history: actual encoded root of the existing `OrdinaryCareWorldFixture` graph, each owner previously qualified under its own codec; coordination via `coordinationV28` | CQ composition (pre-cut and post-cut root) |
| `coordinationV28` | 5-field frame, tag | care, sources (fields 1–2 of the actual coordination frame) | CQ composition; CQ capability (re-frame) |
| `archiveReference` | whole layout | public reference facts; `boundaryHash` = SHA-256 of the opaque closing root (checked) | CQ composition |
| `archiveAnchor` | whole layout; SHA-256 equals `Anchor.ContentHash` | seven runtime owner hashes; lifecycle hash recomputed from closing root field 21 | CQ composition |
| `archiveBinding` | whole layout, ≤ 512 B | reference composition + anchor hash | CQ composition (equals actual root bytes) |
| `productionV11` | 11-field frame, version 11, fixed widths | context hash (interpretation 9) and three collections from the actual continued production | CQ composition |
| `productionV10` | everything except one slot: revision 1, counters, empty collections, operation | context hash (interpretation 8) | CMF vector |

Not claimed: independent encoding of any legacy owner payload, of the production context frame, of
care/liquid/resource continuation, or of a root that contains lived transfers after a cut.

## 2. V10 operation frame (prior review exclusion)

Read: `MaterialOperationCodec.cs`, `MaterialOperationHistory.cs`, `MaterialOperation.cs`,
`ProductionMaterialCodec.cs`, `ProductionCodecV3/V9/V10/V11.cs`, `ProductionContinuationContext.cs`.
Not read: `ConsentedMaterialApproval`, `MaterialContinuationReplay`, `CanonicalProductionReader`,
`ProductionSnapshot`. The codec is `internal`; all tests use the public `ProductionCodecV10` boundary.

| Case | Refusal asserted | Test |
| --- | --- | --- |
| Sequence gap, zero sequence, swapped order, duplicated operation | exact `InvalidValue` + "…bounded exact order and distinct permanent commands." | CMF hostile |
| Outer command id ≠ inner command; outer id names the acceptance | exact "…must bind exact canonical command content and order." | CMF hostile |
| Second acceptance re-issued under first command id / first acceptance id | exact "…distinct permanent approval identities." | CMF hostile |
| Unknown kind 11 | exact "Unknown material operation kind." | CMF hostile |
| Kind relabelled 4/5, payload +1/−1 field, operation +1 field | `ProductionFormatException` (type only; reader message not pinned) | CMF hostile |
| Other journey's context; context after a cut; continued bytes under the closed context | exact "…closing context does not match…" | CMF cross-context |
| V10 bytes+context → V9, V11, V3 decoders; V10 state → V9/V11 encoders; version relabel 9/11/4 | exact argument message / `ProductionFormatException` | CMF cross-context |
| V11 and V9 bytes+contexts → V10 decoder and encoder | exact `InvalidValue` message | CQ capability |
| Wrong moved lot, non-binding acceptance, swapped recorded delivery | refused by structure **or** authority replay; the layer is not pinned | CMF never-admitted |
| Balanced custody edits; movement without approval; 512-operation limit; identities closed by a cut | — | Existing `ConsentedMaterialConsentTests` |

## 3. Critic's negative matrix

| # | Critic item | Existing proof | Added here | State |
| --- | --- | --- | --- | --- |
| 1 | Archive by bytes: first/second reopen | `Fresh_interpretation_and_two_decoded_cuts…`, `A_raw_pair…`, `Full_transfer_window…` | — | covered |
| 1 | wrong ordinal / sequence / swapped scope | — | CQ archive (both cuts), exact reference refusal | covered |
| 1 | wrong or null previous anchor; anchor on first root | snapshot-level V27/V28 anchor mix only | CQ archive: null, own, and other-lineage anchors, exact joint refusal | covered |
| 1 | flipped / truncated bytes | — | CQ archive: three positions + truncation, exact hash refusal | covered |
| 1 | mutated root with recomputed reference | — | CQ recomputed: re-hashed evidence **admitted** as structure; reversed positions and out-of-segment sequence refused | covered, boundary documented |
| 2 | M1 positions, actor order | `Evidence_cannot_describe_reversed_actor_order…` | CWF roles: equal positions, position 64, four positions for two actors | covered |
| 2 | M2 intervals / issuance | `Raw_evidence_cannot_claim_unissued_rights…` | CWF roles: interval ≠ quantity | covered |
| 2 | sequence ≤ boundary, > revision | — | CQ sequence (post-cut): boundary, revision+1, max; CWF: zero | covered |
| 2 | cross-role identity reuse | 512-op test (V10 only) | CWF roles: command↔acceptance both ways, repeats, moved lot = command/acceptance, child derived from wrong cause/ordinal | covered |
| 2 | evidence without movement / movement without evidence; definition, producer, prefix | `AssertInvalidEvidenceRejectedByWorld`, `Moved_stock_without_its_recorded_approval…` | — | covered (existing) |
| 3 | V28 with V9 production; V27 with V11 production | encoder/decoder pair refusals in `Fresh_interpretation…` | CQ capability: actual root field replaced, exact version refusal; version relabels | covered |
| 3 | V27 bytes with consented anchor; V28 with non-consented anchor | `Fresh_interpretation…` lines 33–36 | CQ archive (other-lineage anchor) | covered |
| 3 | pre-cut 28 with non-empty binding; cut root without binding | — | CQ capability | covered |
| 3 | 27↔28 relabel | V27 decoder refuses V28 bytes | CQ capability; CWF step relabel 28→27 with exact profile refusal | covered |
| 3 | L1 re-frame | — | CQ capability: **admitted**, keeps profile-27 admission hash | recorded as deliberate structure |
| 4 | 256 vs 257 transfers | real 256 rollover (`Full_transfer_window…`) | CWF bounds: 256 decode/re-encode, 257 exact field-count refusal | covered |
| 4 | 4,097-byte evidence | — | CWF bounds: 4,096 passes the bound, 4,097 exact field refusal | covered |
| 4 | 65th action | — | CWF actions: 64 admitted, 65 exact refusal (decoder and constructor), unordered actions | covered |
| 4 | V15 command > 2,048 B | — | CWF actions: 2,048 vs 2,049, also inside a step | covered |
| 4 | 65 movement samples; maximum-size roots | — | — | **open** |
| 5 | Protected stock across a cut, then retirement | reservation refusals before/after cut | — | **open** |
| 6 | Identity lookup rebuilt from archive and journal | in-memory set only | — | **open, Host scope** |
| 7 | V10 cross-offers | — | CMF cross-context, CQ capability | partial: V10 bytes under a complete-world *context* not executed (fixture graphs live in different assemblies) |

Reused without duplication: 4,096 combined lots, construction, encounter lifecycle phases, two
simultaneous pairs, balanced custody tampering and actor-order/issuance mutations (existing journey
and Core cases, all inside the executed selections).

## 4. Structure versus authentication

Two executed cases are deliberate acceptances, not defects and not migrations:

- A pre-cut profile-27 root re-framed as profile 28 with an empty history decodes. No decoder can
  recompute the fresh admission hash. Host genesis and interpretation pinning must refuse it.
- A closing root whose evidence carries different recorded owner hashes, with a recomputed
  reference, decodes to a *different* boundary hash and anchor identity. Only an authenticated
  journal and a pinned anchor chain can tell it from the real boundary.
