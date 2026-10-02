# Explicit controller allowance recovery

Storage schema 10 preserves a durably measured reconnect allowance across host restart. The original
Core profiles 36–38, canonical world bytes, receipts and gameplay laws remain unchanged. Fresh creation
and copy activation are explicit; ordinary opening or inspection never upgrades a save or starts time.

The live host resumes the saved subtotal on a fresh monotonic clock, checkpoints exact episode-bound
rows and records expiry at 30,000 ms. Stop time is captured before cleanup. Disconnect/reconnect/expiry
and operational row changes commit atomically through the existing SQLite authority. Stale generations,
partial or inconsistent rows and unsupported expiry evidence are refused; storage uncertainty requires
recovery. Idle hosts with unchanged progress avoid operational writes.

This is a **measured lower-bound policy**: offline time and an uncommitted crash interval are uncharged.
Scheduling stalls can make the lost interval longer than one cutoff, and repeated crashes can extend
effective grace. Explicit activation of an older save records unknown prior elapsed duration rather
than inventing clock evidence. The [architecture reference](../../../Runtime/docs/architecture/controller-recovery.md)
and operator instructions describe the policy and its limits.

## Evidence

[Final checks](FINAL_CHECKS.json) pin current source, binaries, tools and actual results:

- Warning-free build; **933 distinct selected passing cases**, including **44 new** cases.
- 31 storage cases cover all three controller profiles, explicit upgrade, original activation
  provenance, malformed/corrupt rows, fencing, transaction faults and legacy replay semantics. An
  authenticated lived profile-38 save retains its first care archive, earns a second actual cut and
  reopens both archives against its original genesis without restoring health or losing closed care.
- Five host journeys prove partial and repeated restart, shutdown between cutoffs, arbitrary fresh
  monotonic origin and UTC changes, threshold boundaries, retained paid combat work/receipts, safe
  return, separate later episodes and saved Human/Autonomous behavior. The committed-prefix backup
  test distinguishes unpublished time from graceful-stop time; it is not called a process-kill proof.
- Eight actual Host processes are killed before/after disconnect, expiry, reconnect and measured
  checkpoint commit. Reopening authenticates complete old/new state and rows against original genesis.
- All 457 selected older storage, 81 controller, 272 networking, 26 shared-session, 76 managed-server
  and 24 Core controller/retreat cases pass. Counts overlap; the distinct total above removes duplicates.
- The real operator copies the retained genuine autonomous-retreat save into schema 10 with all
  seven original canonical tables unchanged. One rendered Godot process continues it over TLS,
  observes Pending/Restored/Available control, explicitly resumes and reopens exact original history.
  Three captures were directly inspected; control text/buttons and the player remain visible.

Windows credential permissions are explicitly unrun on macOS. The background-rendered process
records absent native focus and a guarded startup; it is not OS/device or human acceptance. Native
hostile approach, initial transport loss and allowance expiry remain managed preparation in this route.

## Preservation, review and remaining scope

The [review record](REVIEW.md) retains the independent replay-parity finding and repair, initial build
failure and three first-run fixture failures. No old assertion, deadline or rule was relaxed. All 726
older tests/fixtures, 23 frozen specs and exact unrelated predecessor staging remain unchanged;
1,316 architecture file links resolve. The pre-change ref is
`codex/checkpoint-before-allowance-recovery-20261002T220920Z` at `2950c00c`.
The verified milestone is `codex/milestone-allowance-recovery-20261002` on the existing working branch.

Next is consented co-player policy. General party retreat/pursuit/rescue, production profile selection,
broader lived-save migration, maximum composite/disposition capacity, production history latency,
other platforms and full device/art/human/campaign gates remain open. Copying operational storage
does not qualify general canonical-world migration. No external provider request was made after its
terminal spending limit; native workers use Astra capacity, with no claimed Claude credit savings.
