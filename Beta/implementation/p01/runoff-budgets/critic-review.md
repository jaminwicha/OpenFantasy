# Bounded native review

A native Astra critic reviewed the next-dependency design, then the implemented source/funding,
receiver aggregation, immutable preparation and canonical archive. No external Claude/Fable calls,
worker edits, builds or commits were delegated.

The design pass established three boundaries: funded sources lead the contract; terminal column
capacity is topology rather than volume; and capacity headroom is only a pinned witness until a
receiving-capacity reservation owner exists. The implementation adds explicit physical receiver
capacity and checks all competing terminals together with actual raw and optional crafted occupancy.

The follow-up also required disjoint source intervals within a candidate, exact half-open time,
column quantities described as potential throughput rather than resident water, and no globally
unique scheduling claim from several inspection candidates. These boundaries are documented.

The code review returned no concrete findings in scope. Funding coverage, within-candidate reservation
completeness, calibrated receiver aggregation, source conservation, complete request hashes and
archive rederivation were consistent with the agreed contract. This was read-only source review;
the integration lead owns the retained tests and host/oracle executions. It does not replace full
independent feasibility reviews or human acceptance.
