# Independent counted-authority review

The reviewed authority has no remaining blocking finding within its declared finite counted-transfer scope. One P2 boundary finding was reported and corrected before this final input set was captured. This is a read-only critic review of the core, tests, oracle and contract; the reviewer did not edit those files, execute .NET/Godot, or test the durable store.

## Finding and correction

**AUTH-REVIEW-01 — split allocation at the lot-count limit: closed by static review.** A valid state can already hold all 4,096 permitted lots. A partial transfer previously let the custody planner attempt to construct 4,097 lots, causing the snapshot constructor's `ArgumentException` to escape the authority's narrow failure handling. This ordinary profile-capacity refusal needed a normal unchanged rejection.

`CountedTransferAuthority` now checks the existing lot count and whether the requested quantity requires a split, after access and exact-target admission and inside the private-resource failure boundary. It returns `ResourceUnavailable` before constructing an excessive candidate. It does not broadly catch unrelated programming errors. The retained `Full_lot_profile_rejects_a_split_but_allows_whole_lot_transfer` regression constructs a valid 4,096-lot state, requires an unchanged rejection for a one-unit partial transfer, and requires a two-unit whole-lot transfer to succeed while keeping 4,096 lots. The contract states this rule. This reviewer inspected those actual changes; execution of the regression belongs to the coordinator.

## What was checked

- **Finite admission and conservation:** positive source capacities; UInt128 aggregate accumulation bounded by the supplied UInt64 budget; exact material ID/version consistency; every lot constrained to its admitted source interval; custody overlap, membership and per-storage capacity validation; reservation admission. The transfer processor reuses the existing split/full planner, preserves source interval partitions and all source/control/policy admission, and cannot expand source budgets. Unallocated admitted intervals are legitimate; construction of the admission snapshot itself is a trusted host operation, not authorization to invent source material.
- **Access before private-state inspection:** independently supplied delivery principal/session, submitted immutable origin, current actor control, withdrawal owner, and receiving policy all precede lot/revision lookup. Public receiving does not grant withdrawal. Wrong-branch and missing/inaccessible-control/storage paths share `NotAuthorized`. Wrong-world dispatch is explicitly a host precondition failure and must not publish exception details.
- **Expected revisions and privacy:** exactly four unique required targets are admitted: selected branch, lot, source storage and destination storage. `ExpectedRevisionSet` forbids duplicate kind/identity pairs, and `CommandEnvelope` binds branch revision identity. Extra known and unknown targets reject before planner probing. Private planner failures share `ResourceUnavailable`; the contract correctly does not promise constant-time behavior.
- **Known identity separation:** world, branch, actor, principal, session, custodian, lot, source, definition and reservation kinds reject aliases. The authority adds uncontrolled actors, other controllers, unused admitted sources/definitions and reservation IDs to the current identity set. Command cause and split-child collisions with that set reject. The same versioned definition or controller reference may repeat legitimately. Historical/global collision checks remain the store's responsibility.
- **State/decision immutability:** collections are copied, sorted and exposed read-only; domain values are immutable. Applied decisions bind the full original authority hash, including permissions/admission, and return a candidate plus receipt. Rejections retain the exact original object and no moved lot. Revision overflow returns the common resource refusal. No write, retry lookup or publish occurs in `Decide`.
- **Strict canonical loading:** all frame/field lengths and collection counts are bounded before expansion; fixed record fields must be consumed completely; exact typed widths, tags, versions, enum values, order and uniqueness are checked. Nested custody errors become the authority format error boundary. Decoded state re-runs semantic source/access/identity invariants. The reader cannot silently omit malformed rows or add defaults.

The reviewer independently calculated encoded upper bounds from the actual field layout: control rows 93 bytes, policy rows 84, source rows 120; maximum actor/control/policy/source/reservation collections 20,512 / 99,362 / 22,562 / 127,009 / 81,958 bytes. Combining these with the existing maximum custody payload yields **1,294,243 bytes**, below the authority decoder's 1,500,000-byte cap. Each nested actual maximum also fits its declared reader bound. This is field-size arithmetic, not a claim that a maximum combined profile was executed through .NET by this reviewer.

## Actual evidence and limits

The Python standard-library oracle was independently executed and its stdout exactly matched all **174,601 bytes** of the frozen fixture, SHA-256 `db99a9ce599ddd768f0e9b94a8feed3ce433f83a98854a63ef653b01db6bfa74`. It contains original/candidate/owner-only/reserved state bytes, command and receipt bytes, plus 47 malformed/semantically invalid states. This confirms independent fixture reproducibility; it is not itself execution of the C# decoder.

The 26 authored .NET Facts were read, including the corrected maximum-lot regression, source/admission failures, ownership/current control/reconnection, actor parity, exact expected-target privacy, identity collisions, full/split conservation, unchanged rejections, bounded count round trips, defensive copies, 47 malformed vectors and every incomplete original prefix. No .NET, Godot, crash/restart, SQLite, networking or playability result is claimed by this reviewer. Coordinator execution evidence remains separate.

The durable integration must still authenticate the delivery adapter; privately admit and reload complete scope/source/control state; authorize receipt lookup; deduplicate exact command content before fresh adjudication; allocate a unique monotonic world commit; compare the decision's full base hash with current committed authority; atomically persist state, receipt, command, identities and other coupled changes; and publish permitted observations only after commit. The full authority candidate contains private state and must not be an ordinary client response. The source constructor and strict decoder do not establish historical rights, content acceptance, global identity novelty, extraction, cross-branch export, physical packing, spatial eligibility or complete actor simulation.

## Exact reviewed inputs

[INPUTS.json](INPUTS.json) records the machine-readable evidence and file hashes. The aggregate below is SHA-256 of UTF-8 concatenated sorted repository-relative path, NUL, lowercase file SHA-256, and newline. Every input was unchanged across this report generation.

**32 input files; aggregate `86a7afe1460d51a06789f19df2ae2f21b1a711c5a407a387569904b496b74920`.**

| Repository-relative file | SHA-256 |
|---|---|
| `Beta/Runtime/src/OpenFantasy.Core/Authority/ActorControlBinding.cs` | `f2c1b4b4c705625ea167c3266e0ed6ad70852ca62ad0fad580c1f9b2c7bd3c3e` |
| `Beta/Runtime/src/OpenFantasy.Core/Authority/AdmittedCountedSource.cs` | `07995b37d8a7d4b8c391f66584271a5eabc8acda98ab35f45da99810f70eddd1` |
| `Beta/Runtime/src/OpenFantasy.Core/Authority/AuthorityDecision.cs` | `e86bb45a289bbf9c7bcfbd22b44171f465315e66319abbefd0d6263c9c5530ab` |
| `Beta/Runtime/src/OpenFantasy.Core/Authority/AuthorityEncodingV1.cs` | `e7a0a5660ff72d1da3ac96adb0f8d874cc9bd201dda945eda13b5786e0c34d3e` |
| `Beta/Runtime/src/OpenFantasy.Core/Authority/AuthorityFormatException.cs` | `369f4ec62606c1f001da0b2c91afe4793483d1d015d23a67fbf960fed6d2401f` |
| `Beta/Runtime/src/OpenFantasy.Core/Authority/AuthorityFrameReader.cs` | `50af1572e33b91be18a2c5c87afb808415b73f6e895cd7f0dad0c09f5050cf84` |
| `Beta/Runtime/src/OpenFantasy.Core/Authority/AuthorityIdentityRegistry.cs` | `3cc95dff2193ea701906f27b3b8c8d6ef8ed18796035f3832aaab5231014c86e` |
| `Beta/Runtime/src/OpenFantasy.Core/Authority/CountedAuthorityState.cs` | `78eff7c6c8239b4c8688224422e46301e2710448e4162427e790791f866b4408` |
| `Beta/Runtime/src/OpenFantasy.Core/Authority/CountedTransferAuthority.cs` | `541c867b738d3d7ae40309767f9c7211c9a3e55a1750aadd9bcf8f595b916f6c` |
| `Beta/Runtime/src/OpenFantasy.Core/Authority/CustodianAccessPolicy.cs` | `d91703f58d7923396e6461532c08c903b772624d1dbd462335de4ba666d39faa` |
| `Beta/Runtime/src/OpenFantasy.Core/Authority/TrustedCommandDelivery.cs` | `a1a6c904555056e075c69e4c949369fd873a21236a2dbef435acbbf470390f61` |
| `Beta/Runtime/src/OpenFantasy.Core/CanonicalEncodingV1.cs` | `1e8871ebbf3aece0316fabdcdbf1432c02240e053fa5b50f7bdf70223f46f4c4` |
| `Beta/Runtime/src/OpenFantasy.Core/Commands/CommandCommitIdentity.cs` | `911ced9e1e857c08cd29034949ec8799d92e3ad3bdeb874929d42ebc96bb45c0` |
| `Beta/Runtime/src/OpenFantasy.Core/Commands/CommandEnvelope.cs` | `4101e369c38d70b51b0f19045eca1e719d966ef2d0b87e6b9519a7d32993e533` |
| `Beta/Runtime/src/OpenFantasy.Core/Commands/CommandReceipt.cs` | `3541752c3c9879c69b439f4ceb273d3cafb763e7dbea59c3dc2631abbf66f1ee` |
| `Beta/Runtime/src/OpenFantasy.Core/Commands/ExpectedRevisionSet.cs` | `83c67e5b8b54a06e13343e7ce18004cad30e46161552ee2a80e71d0cf019c0e8` |
| `Beta/Runtime/src/OpenFantasy.Core/Commands/TransferLotIntent.cs` | `da3c56bc3f0385a3cca4bc44428283c97bfa14f2b7082f6733643a628458f6ac` |
| `Beta/Runtime/src/OpenFantasy.Core/Content/ContentHash.cs` | `279bcb92c2a19dffa74bcb9f1a7f13e709b2a2a7943ccc87e454f6e726d3bc45` |
| `Beta/Runtime/src/OpenFantasy.Core/Custody/CountedItemDefinition.cs` | `45fdd14ec87f906ba5cb5905652c12ea2f2e43d85a668da79232fbd073e357f6` |
| `Beta/Runtime/src/OpenFantasy.Core/Custody/CountedSourceLot.cs` | `e04acde01d3420c1460728f67a827a4c1b5e17d892dc9d5279c98804af0046a8` |
| `Beta/Runtime/src/OpenFantasy.Core/Custody/CustodyEncodingV1.cs` | `69ca395f7de8be8043f4288732be488947a63ee0be4979a624db3458287649fb` |
| `Beta/Runtime/src/OpenFantasy.Core/Custody/CustodySnapshot.cs` | `54cb6923f7da74edeb364591b2e839c0eea497f8d2a4b8932d7102e8fbd9747e` |
| `Beta/Runtime/src/OpenFantasy.Core/Custody/CustodyTransferPlanner.cs` | `f7b9654be2adb772471c6001b4d5037b8f8cca5907f269efdfce8fff65fa6b61` |
| `Beta/Runtime/src/OpenFantasy.Core/Custody/SourceInterval.cs` | `c4a5c84865743f70bf5f43a1d9c2268a93a6718700dc8d255bf5e099b8b8bd10` |
| `Beta/Runtime/src/OpenFantasy.Core/EntityId.cs` | `85211ece11a8715cd530ad5abf7c922935e7a4b0505ab3bffbaa369759c7190c` |
| `Beta/Runtime/tests/OpenFantasy.Core.Tests/Authority/AuthorityEncodingTests.cs` | `0e3c0b772c21a1b4f5543d17567eb6816ec72722cf53fe78c4376cc88b1f3068` |
| `Beta/Runtime/tests/OpenFantasy.Core.Tests/Authority/AuthorityFixture.cs` | `58897cfb8960053298cb66a04a9124f1ea14d41122e9591be6a1b6bc8c4469df` |
| `Beta/Runtime/tests/OpenFantasy.Core.Tests/Authority/CountedAuthorityStateTests.cs` | `c62e8d66aa94da98e0aba427aaff9d169e3c586bfaaa0fe9f12704890bada13d` |
| `Beta/Runtime/tests/OpenFantasy.Core.Tests/Authority/CountedTransferAuthorityTests.cs` | `5a4669d284cf893831aa17f8cf25225cf6437ab7ba22c9596e0cc3688b35831d` |
| `Beta/Runtime/tests/OpenFantasy.Core.Tests/Fixtures/authority-v1.json` | `db99a9ce599ddd768f0e9b94a8feed3ce433f83a98854a63ef653b01db6bfa74` |
| `Beta/Runtime/tests/OpenFantasy.Core.Tests/Fixtures/generate_authority_vectors.py` | `6bd9661116b0f34acebb96e759013a535fd068130a66df577484e9e6c99226e3` |
| `Beta/implementation/p01/authority-contracts/CONTRACT.md` | `4de9916a5d2124efa7adc00896ee758f53afacad654be7d492263dd897798d9f` |
