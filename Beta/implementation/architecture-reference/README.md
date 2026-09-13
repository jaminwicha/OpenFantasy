# Architecture reference backfill

Reviewed 2026-09-12 against runtime source at `54f59ca` (latest runtime implementation `bf96ed9`).
The owner requested a timeless encyclopedia, backfilled against actual implementation and maintained
as architecture changes. [The reference](../../Runtime/docs/architecture/README.md) covers runtime
authority, identity/determinism, persistence/history, time/encounters, world/geography, actors/society,
materials/activities, presentation/interaction and content/composition.

## Reconciliation and scope

Source bindings are separated from standalone domain contracts and full design obligations. In
particular, exact recorded-commit inspection is not arbitrary time travel; local dialogue holds are
not independent co-op Wait; local projections are not global perception; one retained watershed is
not composed terrain or fluids. Composed RNG-counter persistence is a required integration contract,
not a field in the world snapshot. The full design remains represented without claiming it runs.

A bounded independent source review identified three attribution errors: resident versus spatial
body ownership, pure reward planning versus once-only authority, and RNG persistence versus its
integration requirement. All three were corrected. Identity-check descriptions also specify their
finite admitted scope. The reviewer found no further material discrepancies in the examined history,
visibility, profile, recovery and watershed claims. The corrections received local source review;
this is documentation review, not an independent gameplay acceptance pass.

The [project hook](../../AGENTS.md#architecture-reference-and-maintenance-hook) requires affected
reference/dependency updates with architectural changes, bounded source reconciliation at phase
handoff, and no forced edits for changes with no architectural impact. Progress and evidence stay
in implementation records. The shared ecosystem carries the same rule in its project doctrine,
technical-design skill and verification skill.

## Verification and preservation

[validation.json](validation.json) records link checks, reference hashes, frozen-spec preservation,
the untouched predecessor index and synchronized skill hashes. The two maintained skills passed
the skill frontmatter validator and all five mapped installed entrypoints match their source.
The system Python lacked PyYAML on the first validation attempt; the validator then ran successfully
in an isolated `uv` environment with `PyYAML==6.0.2`. No global package installation or model call was
needed. The skill sync used guarded target hashes and retained backups/rollback receipts.

No gameplay source, schema, runtime toolchain or acceptance threshold changes in this increment.
Runtime tests were not rerun for documentation-only edits. Existing 883-test evidence retains its
own source revision and scope; no new game qualification is claimed. Frozen specifications and
unrelated staged predecessor work remain untouched.

The next implementation dependency remains finite compatible macro-basin composition and downstream
contracts before admitted local mapping. A network design discussion supplies no implemented API,
new save profile, fluid custody or phase exit. All full production gates remain open.
