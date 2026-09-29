# Consented custody — independent critic review

**Verdict: no blockers** for the standalone authority's decision path. Five proved defects (two medium, three low) sit in the receipt matcher, decoder and tests. Four uncertain risks need an owner decision or a binding rule. Everything is from reading pinned source; nothing was executed, and I did not see the 138-case result.

## Astra's `Records` fix

The fix is correct for the four fields and matches the precedent in `CommandReceipt.CompareRetry`. Theory indices 1/2/7/10 map to world, branch, sender principal and recipient principal. The decoder builds the commit from the substituted world, so the receipt stays well-formed and the rejection comes from the new comparisons. Residual gaps are P1–P3.

## Proved defects

| # | Severity | Location | Defect |
|---|---|---|---|
| P1 | Medium | `ConsentedTransferReceipt.cs:76-84` | `Records` ignores `MovedLotId` |
| P2 | Medium | same | `Records` accepts an Applied receipt for a pair that does not bind |
| P3 | Low | same, plus `consented-custody.md:65-68` | Outcome and commit are unbound |
| P4 | Low | `ConsentedTransferAuthorityTests.cs:391-405` | Test name overclaims policy coverage |
| P5 | Low | `ConsentedTransferCodecV1.cs:136-141` | Decoder accepts reasons the authority never emits |

**P1 — moved lot is a fifth substitution.**
- **Input:** the existing theory frame with `fields[12] = Identity(99)`. It decodes as a well-formed Applied receipt and `Records(command, acceptance)` is true.
- **Consequence:** a "matched" receipt can name a lot the transaction never touched.
- **Correction:** when Applied, require `MovedLotId` to be `command.Terms.LotId` or `EntityId.DeriveSpawned(command.CommandId, CustodyTransferPlanner.SplitChildOrdinal)`. Document that whole versus split cannot be decided without state.
- **Missing test:** `[InlineData(12)]`, plus the parent lot id substituted for the child.

**P2 — Applied receipt for a non-binding pair.**
- **Input:** `C = Command(Terms(state))` and `A' = Acceptance(Command(Terms(state, quantity: 5)))`. Build a frame with status 1, reason 0, `H(C)`, `H(A')` and `SplitChild`. `Records(C, A')` is true although `A'.Binds(C)` is false and `Decide` returns `NotAuthorized` for that pair.
- **Consequence:** the matcher certifies an outcome the authority cannot produce. The documentation exempts only rejected pairs.
- **Correction:** when `Status == Applied`, also require `acceptance.Binds(command)`.
- **Missing test:** the input above.

**P3 — outcome is not proved.**
- **Input:** flip the same frame to status 2, reason 3, empty lot, or change sequence 11 to 12. It still matches.
- **Consequence:** `Records` proves pair identity only, never the outcome or the commit.
- **Correction:** a pure matcher cannot fix this. State the limit in the reference and pin it with a characterization test so no binding treats a match as the result.

**P4 — policy staleness is untested.**
- **Input:** `Changed_control_or_policy_makes_a_decided_plan_stale` changes controls only.
- **Correction:** add a case where `RecipientStorage`'s owner becomes `Bystander`, or its deposit rule changes. Expect `ApplyTo` to throw `StalePlan` and `Decide` to return `NotAuthorized`. The state hash does cover policies (`AuthorityEncodingV1.cs:26-28`), so this should pass.

**P5 — unemitted rejection reasons decode.**
- **Input:** `ExpectedReceipt(2, 1, [])` and `ExpectedReceipt(2, 4, [])` decode (`RevisionConflict`, `InvalidAction`).
- **Consequence:** contradicts the stated two public reasons.
- **Correction:** restrict Rejected to reasons 2 and 3, and add both inputs to the fail-closed table.

## Challenged and found sound

- **Same transaction:** `Binds` requires command identity, record-equal terms and the hash of the whole command, which includes the sender's principal and session. Distinct frame tags prevent hash confusion with the ordinary `CommandEnvelope`.
- **Current control:** each side's claims must equal its own delivery, and `CanControl` is checked per actor. Nothing is derived from submitted data.
- **OwnerOnly:** policies pass through `WithCustody` unchanged. The deposit rule is neither consulted nor altered.
- **Intervals and capacity:** the planner's prefix split is re-validated by the `CustodySnapshot` and `CountedAuthorityState` constructors.
- **Identity freshness:** command, acceptance and child are checked against state, history and each other.
- **Exceptions:** I found no untrusted input that escapes `Decide` as an unexpected exception.
- **Codec:** fixed widths, exact field count and `Complete()` give one encoding per value. I found no malformed-byte defect beyond P5.

## Uncertain risks

**U1 (Medium) — only `Equipped` blocks a gift** (`CustodyTransferPlanner.cs:36-39`).
- `Create(protection: LotProtection.UserReserved)`, `Heirloom` or `Favorite` will apply, and the moved lot carries the sender's flags into the recipient's bag (`:70-71`).
- Elsewhere `UserReserved` makes a lot unavailable (`GatherPlanner.cs:82`, `ExcavationPlanner.cs:149`).
- This is inherited from ordinary transfer, but here ownership leaves the sender. It needs an owner decision, then a test pinning either outcome.

**U2 (Medium) — `ApplyTo` pins state, not history** (`ConsentedTransferDecision.cs:31-40`).
- Occupancy consulted at `Decide` is not re-checked. An identity consumed by another owner between decide and commit, without changing counted state, goes undetected.
- The binding must serialize decide, record and commit, or re-run occupancy at commit.

**U3 (Medium) — rejected identities have no rule** (`consented-custody.md:72-75` covers applied only).
- Counterexample: with recipient control at `(31, 39)`, the pair `(C, A)` is rejected `NotAuthorized` at commit 11. Control returns to `(31, 32)` with no custody commit, and the same pair applies at commit 12. Both receipts satisfy `Records(C, A)`.
- An approval also has no expiry or withdrawal other than a revision bump.
- Consuming identities on `NotAuthorized` would instead let forged claims squat a victim's identities. The rule must be explicit per rejection class.

**U4 (Low–Medium) — rejected receipts carry claimed principals and branch** (`ConsentedTransferReceipt.cs:94-98`, `ConsentedTransferAuthority.cs:107-109`).
- An acceptance claiming principal 31 but delivered by the bystander yields a rejected receipt naming 31 as recipient.
- This matches the ordinary receipt precedent, but here it names a second party who never participated.
- Record the verified deliveries, or forbid visibility and retry indexing by these fields on rejections.

## Required later bindings and extension constraints

- **Crafted goods:** terms pin lot identity and revision, not what the goods are. A recipient who cannot see the sender's bag approves identifiers only. A later terms version needs a lot kind and a definition pin (identity plus content hash) verified against the lot; the fixed 262-byte layout forces a new version.
- **Durable offers:** the command hash pins the sender's origin session, and the authority requires the claim to equal the current delivery session (`ConsentedTransferAuthority.cs:96-99`). Any reconnect or restart voids stored offers and acceptances. Either adopt that as the rule, or separate authored-at verification from current principal control. A stored `TrustedCommandDelivery` must never be replayed as current.
- **Shared spawn namespace:** the child uses the same `DeriveSpawned(commandId, 0)` derivation as ordinary transfer. History must span both authorities, and multi-item exchange needs new ordinals.
- **NPC parity:** an NPC needs a controller principal and session admitted in `Controls`. An actor with no binding can neither give nor accept, so the retained world needs an explicit transition.
- **Material continuation:** the receipt and terms omit the moved interval and definition. Derive them from the candidate at commit. Retry lookup needs the three-way comparison per participant that `CompareRetry` gives, not a boolean.
- **Deposit rules:** any future rule stricter than `OwnerOnly` must be added to this authority, which never reads the rule.

## Not verified

I did not read `CanonicalEncodingV1`, `CustodyTransferPlan`, the body of `ExpectedRevision`, the qualification logs or the red-evidence files.
