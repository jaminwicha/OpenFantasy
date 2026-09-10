# Independent spatial review

Reviewed the seven spatial implementation files, result types, four test files and spatial contract at the hashes in EVIDENCE.json. No blocking correctness issue was found within the admitted local geometry profile.

The review traced the original 180×140×464 body through actual clearance, half-open cell coverage, all-footprint support, cardinal swept occupancy and immutable actor revision preparation. Bodies are not reduced to one tile to pass headroom tests. Adjacent regions jointly supply dependencies; missing volume or support remains Unavailable. Ground-function loss stops movement without deleting the body or its occupancy.

Signed subunit division uses floor semantics. Cell coordinates derived from Int64 subunits remain far inside Int64 boundaries, so support-cell subtraction and bounded loop increments are safe. Body endpoint construction and actor revision increments are checked. Odd widths retain half-subunit centers through Int128 sums. Sightline range rejects large XY differences before squaring; equal feet elevation and bounded body heights also bound Z differences. Slab comparisons use local differences, so world coordinate magnitude does not enlarge the rational products.

The range is an inclusive XY circle around actual body centers, not feet or a bounding square. Occlusion uses closed cell cubes, deliberately including face, edge and corner grazing. Missing cells actually touched by the segment win over occlusion; unrelated cells in its bounding box do not. Geometry does not claim hearing, language, consent, authority or dynamic actor occlusion.

Known local world, branch, realm, actor, form, physical-body, place and body-definition identities are separated by kind; same definition identity requires one exact version. This is a local registry check, not a world-wide source or export proof. Input arrays are copied, instance collections are bounded and sorted, and candidates retain existing metadata. World ownership must still compose these values in its transaction.

The coordinator's earlier integrated run reported 310 passes and one spatial test expectation failure. The spatial owner corrected that test without changing runtime behavior: feet Z=255 require support at cell Z=-1, outside the original fixture. The corrected test expects Unavailable, then supplies the real compatible lower region and asserts BlockedTerrain. This review inspected that final correction. No .NET tests were run by this critic; the coordinator owns final compiled verification.

`review_arithmetic.py` adds independent mathematical evidence: 5,125 signed floor cases, 610 half-open interval cases, 3,660 cardinal sweeps and 1,451 rational segment/cell cases. Segment checks cover reversal and translation near extreme magnitudes. These Python checks establish arithmetic properties used in the review; they do not execute or qualify the C# implementation.

This does not clear future climbing, falling, slopes, swimming, noncorporeal movement, streaming residency, persistence or full world locomotion. The reviewed contract makes those boundaries explicit.
