# Material basis review

One independent native critic reviewed the five new production-continuation files, the two new
test files and the affected architecture descriptions. The integration lead separately reviewed
the source and owns execution evidence. This was not a Claude run or a human acceptance session.

## Findings and disposition

1. **Test equality correction.** Capture copies `TransformedLot` class instances, which have no value
   equality. The original collection comparison would compare object identities. Before execution,
   it was replaced with tuples containing every retained field; raw lots already use record equality.
2. **Coverage correction.** A live suffix alone did not exercise interior output holes or coalescing
   across custody boundaries. A conserved structural diagnostic now includes a spent middle unit,
   adjacent live intervals in different custody and separate lot revisions/protections/reservation.
   Coverage across the hole is refused while individual lot restrictions remain exact. This input
   does not claim an executed transfer or earned world history.

The integration lead additionally requested the explicit structural-capture/complete-world issuance
contrast and an exact 4,096-live-lot encoding check. The critic reviewed those frozen assertions too.
Both original findings are resolved; no blocking source defect remains.

After the first passing affected run, the integration lead strengthened the world-admission contrast
to require the exact gathering-cursor error, then rebuilt and reran the affected selection. This
prevents an unrelated rejection from satisfying the negative case; it changes no runtime behavior.

## Reviewed boundaries

Capture validates the supplied complete V1/V2 production owner against inventory. It preserves live
lot restrictions, excludes spent produced rights, counts direct raw retirement once, normalizes only
compatible exact adjacency and retains no owner or ancestor chain. Collection and encoding bounds
are explicit. A producer hash is not authenticated provenance. The documentation distinguishes
these executable domain contracts from complete-world continuation, issuance, consumer witnesses,
care/resource joins and durable Host publication, which remain unimplemented by this component.

This source review establishes neither execution success nor the full continuation protocol.
See the packet's qualification reports for actual runs; all wider acceptance obligations remain open.
