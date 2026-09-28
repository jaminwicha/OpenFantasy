# Failure and correction record

No unexpected build, managed-test or external recovery assertion failed in this qualification.
The crash runner deliberately requires failure for fifteen duplicate old-batch submissions and
eleven corrupted saves; their original outputs remain retained as expected refusals.

Independent source review found that the runner originally pinned only its Host assembly. The
[review record](REVIEW.md) preserves the finding and the execution-dependency correction applied
before the first external run. No threshold or game rule changed.

An initial source-manifest capture assumed a `net8.0` Host output and stopped before writing a
manifest because that file did not exist. Reading the Host project confirmed `net10.0`; the retained
manifest pins that actual output. Both solution builds pass. The second includes the final managed
assertion requiring unchanged execution dependencies. No failed run was replaced or relabeled.

The initial preservation inventory used a non-glob Git ref prefix and recorded zero milestones.
The original record remains intact. The corrected explicit-glob check matches all sixty refs against
the preceding packet's preserved inventory and the sixty-first against the starting commit's known
milestone. [Milestone preservation](qualification/milestone-preservation.json) records that independent
baseline comparison; an empty list was not accepted as evidence that historical refs survived.
