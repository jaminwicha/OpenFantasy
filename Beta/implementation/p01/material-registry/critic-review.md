# Bounded native review

Reviewer: existing native `geography_admission_critic`. Central Astra owns implementation, validation,
documentation and commits. Review is read-only; the critic did not build, test or edit files.

The design review identified two concrete integration requirements: all registry allocation/physical
IDs must reach the underlying cause/spawn collision guard, and macro data must not be copied once per
slab. It also requested shared-source transfer/neighbour tests and a global active-cell budget that
counts empty bindings. The implementation includes those guards, shares immutable network data and
qualifies 64 allocations over the maximum 65,536-column network.

Source review found no concrete conservation, registry-role or lazy-cache defect. The reviewer requested
more direct normalized parser adversaries rather than relying on broad regression or only trailing-byte
tests. The central task added count 65/UInt32 max, binding length 8,193, duplicate/decreasing IDs and
cumulative overflow across two rows, plus equivalent real-host cases. The final focused suite passes.

Final response: “Review closed: the new test and host malformed cases directly cover the outstanding
parser bounds and ordering checks, including cumulative overflow before candidate construction. No
remaining consequential findings within this scope.” This is bounded source critique, not full campaign
feasibility certification or human/native acceptance.
