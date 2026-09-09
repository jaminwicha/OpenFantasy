# Independent command and host contract review

The migration-closure worker reviewed the other worker's command implementation and the coordinator's diagnostic host integration. This is separate authorship for the command/host scope. It is not an independent review of the closure packet this reviewer authored, and it does not certify the unimplemented authority or supported platform exports.

**Disposition: no remaining blocking finding in the reviewed command values, codecs and host diagnostic routing.** This conclusion applies to the exact input hashes in [REVIEWED_INPUTS.json](REVIEWED_INPUTS.json); subsequent edits require targeted re-review. The coordinator owns the final integrated suite on the pinned toolchain.

## Findings and corrections

| Finding | Disposition and evidence |
|---|---|
| Host `ReadBoundedFile` read `stream.Length` separately for its byte-budget guard and array allocation. Growth between those observations could exceed the intended memory cap or raise an uncaught cast overflow | Reported by this reviewer. Coordinator changed the method to capture length once, check that value and allocate from that same bounded value. The retained `ReadExactly` and final extra-byte check still detect length change. Final source inspected; no adversarial concurrent-writer timing test is claimed |
| Host diagnostic failure routing needed to handle `InvalidDataException` explicitly | Coordinator added the explicit catch alternative; reviewer verified final source preserves clean diagnostic failure instead of assuming this exception derives from `IOException`. Executed exit-code evidence belongs to the coordinator's integrated run |
| Receipt comparison needed branch coherence in addition to its supplied command hash | Independently identified and fixed by the command author before this final review. Reviewer verified `CompareRetry` compares branch plus content hash and the independent malformed-branch fixture has the original command hash but a different receipt branch. This reviewer does not claim discovery credit |

The reviewed design keeps authentication and gameplay legality outside data decoding. Original submission-session identity stays in canonical command content, while a reconnected delivery session must be authenticated separately by the future host. An immutable receipt value, its hash and a positive commit number cannot prove a real durable transaction. Documentation retains those distinctions and the subsequent admission/transaction/recovery gates.

The codecs limit bytes and fields, preserve big-endian framing, reject unknown tags/versions, copy external data into immutable values, and require canonical sorted unique revision owners. Constructors enforce local structural invariants without inventing inventory state or declaring a request authorized. Class responsibilities remain narrow and readable; no unnecessary inheritance or global mutable service state was added to these contracts. Host methods only inspect files and label their output scope explicitly.

## Executed independent probe

[run_probe.py](run_probe.py) copies [Probe.cs](Probe.cs) and [Probe.csproj](Probe.csproj) into a temporary directory. It compiles the current Commands sources plus the existing identity/framing primitives directly, without building into or changing runtime project output directories. It pins the **reviewer's available SDK 8.0.413** for this isolated observation, targets `net8.0`, and checks source hashes before and after execution. This is distinct from the production candidate **SDK 10.0.401** qualification owned by the coordinator.

From the repository root:

```sh
python3 Beta/implementation/p00/contract-review/run_probe.py
```

[RESULTS.json](RESULTS.json) records **18,312 cases**: every single-bit change in the independent command, applied-receipt and rejected-receipt golden frames, followed by 10,000 bounded random frames from a fixed seed. **3,073** mutated frames were valid and re-encoded byte-identically; **15,239** were rejected with the expected typed format-exception family. **Zero unexpected exceptions** occurred. Valid identity/quantity/hash bit changes are expected to remain representable; the test requires canonical consistency, not rejection of every changed byte.

This probe checks parser failure containment and canonical representation across a bounded sample. It does not establish authenticated messages, stored-receipt permission, actual lot transfer, conservation, persistence, exactly-once execution, gameplay performance, UI or platform compatibility. The reviewed unit tests and independent Python golden-vector generator cover the specific command/receipt invariants; their final integrated execution result is reported by the coordinator rather than inferred from static inspection here.
