# Specification snapshot verification

These standard-library Python scripts reproduce the mechanical checks for the 8 September 2026, unimplemented Beta specification 1.1 and research 0.9. They are dated snapshot checks, not runtime tests or a general validator for every future revision. Run Python normally, without optimization flags that disable assertions.

From the OpenFantasy repository root:

```sh
python3 Beta/tools/verify_research_v0_9.py
python3 Beta/tools/verify_spec_v1_1.py
```

The research check is read-only. The specification check verifies the source registers, requirement and phase ancestry maps, research bindings, local links, selected table structure, archived payloads, Alpha source ZIP and originals, local reuse source fingerprints, synchronized ecosystem targets, and coordinator-captured critic input hashes. On success it refreshes [SPEC_VALIDATION.json](../spec-v1_1/SPEC_VALIDATION.json). It does not fetch external URLs or execute any game, export, animation or playtest.

The full check intentionally requires the inspected local projects and GameDevAIEcosystem manifest, including the original absolute source/installed-skill targets recorded there. It fails on missing or changed evidence rather than silently omitting checks. On another machine, a dated report remains evidence of the original run; current external checks need the same inputs or an explicitly reviewed environment mapping.

Once implementation begins, the assumptions that all Beta evidence paths are empty and inspected source files are unchanged can legitimately cease to hold. Preserve this script and report with the reviewed edition; revise a subsequent edition's checks and evidence semantics deliberately. A changed file hash is a request to inspect the change, not a reason to restore old code automatically. Mechanical success establishes document/source integrity only. Independent review, actual runtime qualification and human acceptance remain separate evidence.
