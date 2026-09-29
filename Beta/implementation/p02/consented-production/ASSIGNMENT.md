# Production-aware consented transfer implementation

Implement the next real integration unit after the consented raw-counted authority: gifts of actual crafted/produced goods, their exact provenance and continued history, together with raw stock where the same transaction contract applies. The end objective is ordinary player/NPC gifting and personal medicine acquisition in the existing lived world. Do not replace that objective with a demo or silently change its content/policies/genesis. This assignment supplies the material dependency; actual Host/UI/offer lifetime and admitted world transition remain subsequent bindings.

## Ownership and instructions

You are not alone in this checkout. Preserve others' changes, especially all 19 staged predecessor files. No git/index/commits, subprocess agents, external tools or model switches. The lead owns integration and review. Work only in:
- `Beta/Runtime/src/OpenFantasy.Core/Production/**` (existing owner extensions allowed; preserve old formats/rules).
- new `Beta/Runtime/src/OpenFantasy.Core/Authority/ConsentedMaterial/**` if needed.
- `Beta/Runtime/tests/OpenFantasy.Core.Tests/Production/**` and new `.../Authority/ConsentedMaterial/**`.
- new `Beta/Runtime/docs/architecture/consented-production.md`.
- this packet's `IMPLEMENTATION_NOTES.md` and `WORKER_RESULT.md`.
Do not change existing ConsentedCustody V1, World/Hosting/client sources, frozen specs, handoff, instructions or validation runner. Shared guards outside ownership can be proposed with exact paths if genuinely required, never bypassed. Begin by writing IMPLEMENTATION_NOTES.md with scope/inputs and then record your chosen compatibility/integration design there before extensive code edits. No need to stop for approval within this scope.

Use modular, readable object-oriented pure C#. No new independent inventory, arbitrary property bag, broad framework, IPC or source codename. Read AGENTS.md, Beta/AGENTS.md, current handoff and affected architecture. Skills: cross-agent-delegation and godot-testing are provided; plain .NET owns this code. You may read more task-relevant sources, not the whole archive.

## Required outcome

1. Exact bilateral approvals identify both actors and independently verified current deliveries, world/branch, lot kind, actual definition identity/version, source/destination, amount and all relevant revisions. Reuse current control/custody contracts; recipient participation is not reusable deposit permission or treatment consent. Existing raw V1 remains exact. No lossy V1-to-produced reinterpretation.
2. Transfer actual TransformedLot whole/prefix quantities through ProductionSnapshot, preserving original producer/batch/output index, output intervals, transformed input ancestry, protection/reservation and shared raw+produced storage/lot limits. Whole identity remains; split uses collision-checked deterministic child. No phantom raw-source issuance or altered producer identity. Installed/reserved construction outputs are unavailable. Keep established explicit manual flags versus genuine reservation behavior.
3. Record sufficient exact ordered operation/approval evidence in the actual production owner to validate the transfer, later recipient consumption/recrafting and another transfer. Preserve command AND acceptance identity roles including occupied/retired historical identities and split children. Full candidate application pins both inventory and production and rejects stale control/policy/capacity/content.
4. Prove canonical round-trip, replay and exact source/output conservation across multiple real continuation cuts. Do not relax existing provenance/remainder validation to permit arbitrary custodian mutation. Choose an explicit version/capability for new history interpretation; every old format/profile remains exact and fail-closed. Think carefully about the complete-world V9 production interpretation: it deliberately leaves raw movement/work with World, and currently freezes transformed custody between retirements. Avoid implementing a disconnected V4-only branch that would require rebuilding this work for complete-world use. State how your chosen owner/version will bind to the coordinated world and why the old world cannot silently accept it. This assignment does not qualify durable world admission.
5. Meaningful tests: sender/recipient forgery and stale approvals, wrong kind/definition/version, aliasing command/acceptance/current and historical identities, split collisions, mixed capacity and arithmetic/limits, immutable rejected candidates, protected/reserved construction stock, partial and whole transfers; craft -> partial gift -> recipient consumes/recrafts -> give onward -> cuts -> rejection of reused prior identities. Actual valid codec frames mutated independently where useful; decoder/matcher is not authentication. Match actual output/result as well as claims. Keep external history concurrency, rejected-identity/receipt visibility and reconnect obligations explicit for later Host binding.
6. Timeless architecture reference and compact final report with changed files, exact tests/evidence and incomplete obligations. No claimed full-game, native, Host, asynchronous offer or retained-world pass. Old published bytes and existing tests must remain intact.

Before editing deeper, inspect the actual owners: ProductionSnapshot, ProductionContinuationContext/Validation/MaterialContinuationReplay, ProductionClaimValidation and IdentityRegistry; ConsentedCustody; WorldCoordinatedAdmission and WorldMaterialCareArchiveAnchor (read only). Prefer the smallest cohesive extension that fulfills the above full material outcome. If the complete outcome cannot fit, preserve meaningful partial work with precise remaining tasks; do not introduce fabricated acceptance or weaken guards to finish.

## Budget and validation

Exactly `fable[1m]`, Max effort, 1M context; maximum 1800 seconds and $18 client estimate. No automatic retry or alternate model. Reserve time for tests and final report. Stop promptly on a required denied tool, reporting exact invocation/path; do not bypass permissions.

Allowed shell commands, verbatim:
`python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/consented-production/qualification/run_worker_checks.py build`
`python3 /Users/alucinor/Workspace/Projects/OpenFantasy/Beta/implementation/p02/consented-production/qualification/run_worker_checks.py test`
They retain outputs and source pins. Use Read/Glob/Grep/Edit/Write otherwise; no other shell. They run solution build and production/authority/custody tests with finite limits. The lead will review and run broader affected regression at integration. Do not edit the runner or weaken thresholds. Return compactly; raw logs remain in the packet.
