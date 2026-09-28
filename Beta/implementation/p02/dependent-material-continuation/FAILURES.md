# Retained failures and corrections

The initial solution build (`qualification/build-01.log`) failed with three compiler errors:
two null-coalescing expressions tried to infer an empty collection as `ReadOnlyCollection`, and one
batch value required an explicit null guard. The runtime worker corrected enumeration typing and
null flow without changing rules. No tests ran against that failed build. Its exact source set is
retained in `qualification/source-pins-01.json`.

Independent review also identified a test expecting `ContinueMaterial` to reject V5, while the chosen
contract preserves construction capability through subsequent cuts. The test must verify retention
of V5 and historical backing; changing the runtime to discard capability or weaken validation would
be incorrect. This is a source-review correction, not a failed execution result.

Later build and execution results remain separate; they do not rewrite this failed diagnostic.

The second solution build passes with zero warnings/errors (`qualification/build-02.log`). The focused
23-case execution (`construction-02.log`/`.trx`) reports **22 pass, 1 failure**. A replayed historical
completion with refreshed sequence/counter tokens survives structural V5 decoding where the regression
requires refusal. It does not establish valid inventory authority: exact replay remains separately
required. V3 already checks imported producer identities structurally; ordered validation lacked the
corresponding V5 check. The correction explicitly refuses active V5 batch/operation identities that
reuse imported producer IDs/commands. V1–V4 behavior is unchanged; the original assertion is retained.
Second-build source and execution identities are preserved in `source-pins-02.json` and
`execution-pins-02.json`. Final rebuilt regression results are recorded separately.

The final completeness check found one source-inventory omission: the critic-driven
`ConstructionSourceFrame.cs` helper was added after the first source list. Its content was frozen and
compiled, but the copied list omitted its checksum. The retained addendum pins it and
`compiled-source-check.json` matches the actual source to the compiler's SHA256 PDB document checksum,
then matches that PDB's GUID/stamp to the executed Core assembly. The 1,199 original pins remain
unchanged; the corrected application inventory is 1,200 files. No source or binary was changed to
repair this evidence record. The small reproducible metadata probe and build log are retained; its
build emitted a macOS CSSM diagnostic but completed with zero compiler warnings/errors, and its actual
metadata verification succeeded. This is source identity evidence, not another runtime test.
