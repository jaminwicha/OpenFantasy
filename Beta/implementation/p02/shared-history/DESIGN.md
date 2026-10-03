# Shared participant History

Build a usable, independently accessible history screen for the permanent home or actually selected
character. The optional history facade supplies real selection capture, bounded terminal discovery
and typed detail inspection. PrivateHistory remains independent of Inventory and Trade. No core,
schema, wire format, simulation timing or acceptance threshold change is intended.

## Ownership and behavior

- `FarmHistoryModel` owns transient presentation: open generation, captured observation, page request,
  request generation, row selection and detail generation. It retains one page (at most 16 entries),
  one detail and bounded navigation state; explicit Next and Latest avoid unbounded page caches.
- Open/Latest explicitly request the first page; Next uses exactly the returned cursor. Discovery
  never receives remembered subject IDs. Select/Inspect uses an actually displayed entry and the typed
  existing paid-quote or material-proposal history reader. Details are read-only historical terms.
- Validate both selected result and complete page request before applying. Current world/branch/port/
  selection/witness, opening, page request and row lifetime must all agree. Close, lost authority,
  selection changes and focus loss clear private displayed state and invalidate outstanding captures.
  Late tasks are observed and drained, never abandoned; they cannot populate a later opening/row.
- Explicit availability is separate from authorization. Empty, loading, unsupported, denied, busy and
  failed states are readable without exposing withheld inventory, balances or secret metadata.
- A shared icon-led entry point, semantic input, scene-owned layout, focus restoration, scrollable
  rows, tooltips and controller-accessible controls bind the model. It uses existing timing and neutral
  movement boundaries. Browsing does not create a world action, independently pause other players or
  enable actions from old terms. Characters remains permission/control management.
- Keep C# modular and engine-free presentation logic testable through actual Hosting contracts.

## Scope and verification

New managed checks cover paging/capture/row invalidation, permissions, stale failures, shutdown and
local/remote facade detail access. Real TLS delayed inbound delivery complements prior outbound
revocation coverage. A controlled-background Godot fixture must discover genuine terminal records,
inspect exact terms, exercise close/reopen and independently granted history, and retain post-draw
images for visual review. Existing affected client/input, selected history and protocol tests remain.
No full Core rerun is required absent a changed Core dependency; no visual claim comes from managed
checks alone. Use the shared heavy-check lock, exact source/binary pins, finite existing deadlines,
and retain every failure. Frozen specs, original tests and predecessor staging are preserved.

Independent source critique follows a coherent implementation freeze. Physical input, human comfort,
production art, history latency/scale, full profile40 crash/reopen and complete campaign gates remain
open. Directory replay cost per page is unchanged; the UI performs no background polling or retries.

## Assignments

The central lead owns integration, build/test project links, qualification, docs, commits and markers.
Workers receive disjoint ownership and publish the shared model API before dependent integration.
Native workers consume Astra capacity; the external provider spending-limit failure is terminal.
