# Construction continuation review

Status: independent source review and fix review complete; Core qualification passes. Final Hosting and preservation results are recorded in the qualification packet.

The independent source audits in [AUDIT.md](AUDIT.md) identified the reserved-craft mismatch, required
complete source ingredients, physical backing dependencies and repeated-frame work risk before edits.

Integration review raised two further issues:

- A completion proof records the preceding production hash as well as inventory. Raw inventory alone
  cannot distinguish all transformed-material states. Replay must validate the exact production prefix,
  rather than merely storing an unused hash or assuming inventory uniquely identifies every owner.
- A new cut must preserve each actual construction completion record independently of a caller's
  supplied recipe/project catalog. Omitting that catalog entry must not erase a just-completed producer.
  Active and imported construction backing still requires exactly one installed project.

Both integration findings are corrected. Replay verifies the complete production prefix, and cuts
preserve every qualified construction producer. The same-inventory/different-derived-prefix test
passes after successful structural decoding and requires the exact subsequent authority rejection.

## Independent frozen-source findings

The native critic found three additional blockers in the first source checkpoint:

1. New source-world time could be combined with an older supplied spatial owner. Stance/reach/body
   checks would then use the stale actor state. Join current actors and terrain to the actual source
   frame while allowing only the explicitly qualified construction projection to differ.
2. First V5 selection accepted an arbitrary construction catalog. Omitting completed project A and
   its distinct definition made its original producer appear ordinary. Bind selection to an opaque
   state captured from the admitted closing world and that actual material boundary.
3. An ordinary V5 craft could use a construction recipe with unreserved stock. While active, component
   admission refused its uninstalled producer; a generic cut then hid it among imported generic
   witnesses. Freeze the construction recipe catalog across cuts, refuse ordinary use of those recipes,
   and require every matching active/imported producer to retain exactly one earned installation and
   its full historical batch record.

These are actual source-level invariant findings. Runtime corrections and targeted regressions are
in progress. Final re-review and execution must verify the fixes; no later pass may erase this record.
The critic also confirmed the exact preceding-production binding, reserved replay separation, created
identity checks, atomic refusal, bounded original batches and old-world guard in the reviewed checkpoint.

## Final source disposition

The critic re-reviewed all three fixes and reported no remaining actionable finding. Current actor,
body and terrain bytes match the source frame; initial selection requires the opaque admitted state
and exact original material hashes; construction recipe classification persists through every cut.
Ordinary craft and craft-labelled decoded operations cannot acquire installation authority, and every
classified imported producer needs exact original backing and one completed project.

The first focused run subsequently found the structural imported-identity rejection gap retained in
[FAILURES.md](FAILURES.md). The runtime added a V5-only imported batch/command check; the critic
reviewed that isolated delta and confirmed ordinary use of imported output remains legal and V1–V4
behavior is unchanged. The final full Core regression passes all 1,491 cases, including all 23 new
cases and the original formerly failing assertion. Final affected Hosting results and file identity
checks are recorded in [verification](qualification/verification.json).

This is one bounded independent review with necessary fix follow-ups. It proves no durable archive
authenticity, complete-world V5 publication, native presentation, production throughput or human
acceptance. No external Claude retry or credit saving is claimed.
