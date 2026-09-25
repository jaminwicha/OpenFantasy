# Bounded integration review

The central task owns Core world integration, Host operators, the independent external oracle,
documentation and qualification. Native Astra workers owned the portable pacing value/codec/tests and
Hosting/schema/session/tests. A separate read-only critic inspected consequential admission, rates,
receipts, replay and legacy compatibility without running overlapping builds or editing code.

Two actionable critic findings were corrected before the final solution build and integration checks:

1. A complete paced root could carry resident exposure inconsistent with its permanent clock.
   `WorldPacingAdmission.Validate` now compares every resident's experienced time with
   `pacing.Progress.Elapsed`. A focused regression rejects both a forged constructor input and an
   otherwise canonical modified archive.
2. Growing mandatory pending choices could consume unreserved space near the16MiB world bound.
   `WorldSnapshotCodecV22.Encode` reserves both maximum liquid and maximum pacing payload sizes,
   preserving outer frame bytes. This is a source-level headroom proof; an empirical maximum-root
   exhaustion test is not claimed.

The independent critic rechecked these two exact fixes and reported no remaining defect in them.
No duplicate broad review was requested. Full resident64 versus component128 bounds remain explicit;
there is no truncation or claim of the full shared-pause fallback. Genesis requires unearned pending
choices to be absent even when component/root bytes are canonical. Durable replay authenticates actual
accepted ancestry; hashes alone do not prove it.

The live fixture independently proves real flow/recovery at the earned interval, not merely increasing
a counter or comparing a managed solver to itself. External process kills test the complete transaction.
Native legacy controls check shared Host/session compatibility; no new combat UI or human acceptance is
claimed. No external Claude/Fable usage occurred for this increment.
