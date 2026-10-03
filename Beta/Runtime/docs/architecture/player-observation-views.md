# Player observation values

## Boundary

[PlayerViewCodec](../../src/OpenFantasy.Hosting/Networking/ObservationViews/PlayerViewCodec.cs)
encodes and decodes the immutable `LocalWorldView` used by the Godot client. The host selects and
projects that view for an authenticated player before encoding it. Decoding restores presentation
values only: it never constructs a `WorldSnapshot`, reads a save, creates a controller or evaluates
an action against authority state. Authenticated command admission remains separate.

The codec is separate from the existing default diagnostic JSON. The projection's public properties,
property order and JSON attributes retain their diagnostic meaning. In particular, `Regional` and
`IsRegional` remain ignored there, and nullable retreat/outcome/threat fields retain their omission
rules. A diagnostic digest therefore is not a network observation encoding.

## Typed reconstruction

The codec's explicit converter registry maps each projection-only class to a concrete immutable
transport record. Its value constructor copies already-projected fields and freezes collections;
ordinary public leaf records retain their concrete types. Derived facts are either omitted from
the transport record and recomputed by the existing type, or checked through canonical re-encoding.
There is no object-property bag, private field restoration or polymorphic type-name activation.

Three details require an explicit representation beyond diagnostic JSON:

- Material proposal preparation retains its captured world, branch, sender and source storage,
  eligible recipient-to-destination storage mappings, own exact lots and fixed expiry. These are the
  terms already consumed by `Prepare` and `CanStillPropose`, not another actor's inventory or policy.
- A market stall retains the player's own receiving account and the captured advancing step used
  by `PrepareShowing`. The enclosing market observation must carry that same advancing step.
- `CommandContentHash` uses its canonical lowercase hexadecimal form. Its codec-local converter
  does not alter its default diagnostic encoding. Content hashes are exact concurrency pins, not
  authentication credentials.

`RestorativeAvailability.FromObservation` reconstructs read-only advice from a known reason,
rejection and optional exact intent. It validates their shape; it performs no admission and grants
no use of medicine. Available advice requires an intent and no rejection; blocked advice requires
no intent and a nonempty rejection. The authority reevaluates every submitted intent normally.

## Outcome interpretation

An outcome view with `AcknowledgmentMode.ControllerAcceptance` uses observation-body version 2,
unless it also declares the separately admitted recovery interpretation below.
The required nondefault marker means the compatibility reader fields contain character acknowledgments,
not proof of human reading. Its presence depends on the world's controller interpretation and survives
human rejoin. Views without either nondefault interpretation retain body version 1, and the default HumanReading marker is omitted from
both transport and diagnostic JSON. Thus legacy profile-35 frames keep their exact spelling.

The decoder checks the version against the reconstructed interpretation before canonical re-encoding.
Version 2 without the controller marker, version 1 with it, unknown modes and explicit default spellings
are refused. A client supporting only body version 1 cannot consume a controller-acceptance outcome;
there is no downgrade that silently presents autonomous acceptance as a person's reading. This is a
presentation format distinction, not a new world codec, permission or historical submitter record.
The surrounding authenticated session must use clients supporting the host's emitted interpretation.

`AllowsAssistedRecovery` selects body version 3 and requires the controller-acceptance mode. It means
the world's law allows separately permitted aid after completion, not that this patient has a willing
helper, supplies or a granted recovery permission. Completed zero HP is therefore not presented as
terminal. The property is omitted when false, preserving older transport and diagnostic bytes. Version
3 without the marker and older versions carrying it are refused; delta reconstruction retains the
same interpretation checks. Current HP remains visible independently of any retained outcome record.

`SupportsCareDispositions` selects body version 4 even with no pending request. Scoped
[care decisions](care-dispositions.md) distinguish patient cancellation, helper treatment/decline and
terminal results. The false capability and null additions are omitted in legacy views. A mismatched
body version, contradictory participant/request/episode or private helper dose exposed to the patient
is refused. The capability describes the saved law; it does not authorize a command.

## Geometry and privacy

### Public material descriptions

[`LocalMaterialDescriptions`](../../src/OpenFantasy.Hosting/Simulation/LocalMaterialDescriptions.cs)
contains bounded immutable definition identity, exact item version, stable key and display name. The
content catalog validates authored payloads and their source pins before associating names. Unknown or
conflicting pins cannot acquire a known label by matching a tool kind or recipe role. The projection
selects names only for disclosed inventory lots, open material terms and Production recipe/tool facts;
it carries no quantities, effects, knowledge, eligibility or private stock. Naming a medicine does not
expose Recovery. These metadata are ignored by default diagnostic JSON.

`PlayerViewCodec.EncodeWithMaterialDescriptions` opts into body version 5. Default `Encode` preserves
the legacy interpretation-specific representation, including when a local view contains metadata.
The decoder accepts both representations and checks canonical bytes using the received version;
descriptors cannot be smuggled into an older body. Re-encoding a described view with the explicit
entrypoint retains its described representation. The construction-aware live representation below also includes these descriptors.
Clients must support the host's emitted presentation version; old saved frames remain independently
readable. This is not a world profile or save-schema change.

### Construction work and installed geometry

`EncodeWithConstruction` selects home body version 6, including purpose-specific construction work
and independently public installed structures. Work choices retain explicit project/storage/tool
revisions and the builder's actual readiness, funding, stance and reach facts. They are previews,
not authority or reservations. Installed members carry their exact bounded identity and geometry
without private backing stock. Missing legacy fields and present-empty current fields remain distinct.
The version-5 material-description entrypoint retains its own representation; default legacy encodings
remain unchanged. Canonical decoding follows the received version; the cumulative care representation
also retains construction validation and fields.

### Known care permission terms

`EncodeWithCarePermissionDescriptions` selects home body version 7 when ordinary permission terms are
present. It includes material descriptions and construction alongside each grant's known medicine,
restoration amount, duration and delivery mode. The authority projection resolves the authenticated
permission → use definition → remedy relationship. The wire validator checks bounded shape, supported
assistance modes, the ordinary duration and matching withdrawal identity; the view alone cannot
authenticate that catalog relationship. No helper inventory or executable assistance request is exposed.

The permission model's `Description` is ignored by default diagnostic JSON. Its explicit
[wire record](../../src/OpenFantasy.Hosting/Networking/ObservationViews/CarePermissionDescriptionData.cs)
emits terms only under the declared new representation. Existing encoding APIs retain their exact
older field sets. Canonical decoding rejects a descriptor under an older version, missing terms under
version 7 and inconsistent withdrawal identities. Repeated self-use definition identities or permission
identities are rejected in home and selected arrays before they can become ambiguous menu keys. Views without descriptions retain the construction
or material-only representation according to their existing structure facet; omitted older facts are
not invented during republishing. Live publication uses this conditional cumulative encoding.

Regional data contains the observer identity and exact feet, each observed region's full bounds
and revision, row-major ground kinds and opening depths, and each wet column's exact volume and
surface height. Standing floor height derives from the feet. Ground and water are received with
the same view's actors and offers. `IsRegional = true` with a null regional observation remains a
supported message describing an unsupported standing floor; it preserves the client's recoverable
last-frame behavior.

The observer must match the view's player and feet, and a nonnull regional observation includes
that player's column. Occupancy arrays retain their exact bounds and indexing contracts. Unknown
space is not reconstructed as empty terrain. Geometry contains no geological material identity,
source lineage, excavation history or reservoir identity.

The codec preserves the existing sight-complete projection contract. The regional terrain window
is not general fog-of-war: other projected collections retain their existing admitted-locality scope,
and presentation still filters regional positions. The codec does not silently discard those fields.
Participant quote/proposal checks, own preparation scope and own retreat identity prevent a
contradictory payload from being published as the current player's view. Host projection remains
the owner of privacy; the decoder cannot independently authenticate facts using the view alone.

## Bounded format

Small observations retain the existing UTF-8 JSON body with a version and a typed view. Version 1
JSON has one canonical spelling: property order, explicit nulls, number and string representation,
and complete field presence are fixed by the codec. Expanded JSON must exactly match re-encoding of
its reconstructed values. Missing, duplicate, unknown, contradictory derived fields and alternate
JSON spellings are refused. Even otherwise harmless whitespace is refused.

Terrain occupancy uses a bounded `{Count, Bits}` value: row-major boolean entries are packed low bit
first into base64 bytes. The byte length is exactly the ceiling of count divided by eight, and every
unused trailing bit is zero. Region bounds still require the complete cell count; packing omits no
geometry. The existing diagnostic encoding remains a boolean array.

The encoded wire body is limited to **900 KiB**, including any compression header. JSON at or below
that limit is sent unchanged. Larger canonical JSON uses a 12-byte `OFVB` header followed by one
Brotli stream. Header bytes 4–5 are big-endian wrapper version 1, bytes 6–7 are zero reserved bits,
and bytes 8–11 are the big-endian unsigned expanded JSON length. The encoder uses fixed Brotli
quality 5 and window 22. A compressed wrapper for JSON at or below 900 KiB is refused.

The separate expanded JSON cap is **16 MiB**. This explicit resource budget equals the current
complete-world codec byte budget as a sizing choice; it does not prove that every composite maximum
admitted world fits an observation. Before decompression, the decoder validates the header and
clamps the declared length to that cap. It allocates exactly that declared output length and permits
no decoder growth or retry. Decompression must report completion, consume every compressed byte and
write exactly the declared length. Truncated, trailing, concatenated, malformed and overexpanding
streams are refused. The expanded JSON then receives all ordinary shape and canonical-value checks.

Different Brotli implementations can produce different valid compressed bytes. The decoder compares
expanded JSON with typed canonical JSON, never with locally recompressed bytes. Observation stream
identity and delta comparison use the originating server's transmitted bytes; a consumer must not
replace them with its own recompression.

Encoding also has a hard bound while serializing. `Utf8JsonWriter` writes into a bounded
`IBufferWriter<byte>` that refuses output beyond 16 MiB. Its buffer has a fixed additional 128 KiB
reservation margin for writer span requests, which cannot become accepted output. No unbounded
intermediate serialized array is created. Compression receives at most the capped JSON and an output
buffer of exactly the wire limit. An incompressible body that exceeds the wire limit is refused as a
whole; fields are never truncated.

The expanded budget adds real memory cost. Decoding can simultaneously hold a 16 MiB expanded array,
a canonical comparison buffer of up to 16 MiB plus its fixed reservation margin, the decoded view,
and Brotli working memory. Encoding buffer growth can temporarily retain both old and new arrays.
The 900 KiB wire limit is not a total memory limit or proof of runtime performance at maximum capacity.

JSON nesting remains limited to 48 levels, text values/property names to 16,384 encoded bytes and
collections to 16,384 elements. A streaming JSON preflight bounds containers and depth before typed
deserialization. Typed construction and public-value validation then check nullability, known enums,
collection dimensions, identity uniqueness and participant/observer consistency. Public-value
traversal inspects only public properties for validation; private preparation values have explicit
typed accessors and checks. Malformed bodies raise `FormatException` and yield no view. Arrays retained
by decoded views are read-only copies. These transport/resource bounds do not change world admission
rules. Maximum inventory, proposal/quote and composite-geometry size coverage remains a separate
qualification obligation.

Worksite and liquid occupancy use the sealed internal
[LocalTerrainOccupancy](../../src/OpenFantasy.Hosting/Simulation/LocalTerrainOccupancy.cs), which
defensively copies its boolean cells and exposes the same declared `ReadOnlyCollection<bool>`
property. Packed wire JSON and default diagnostic arrays therefore retain their existing spelling.
Host projections share that owned occupancy across worksite, liquid and player views only when
they refer to the exact same immutable `TerrainRegionSnapshot` object. A conditional weak table
holds the association without retaining discarded regions or world roots. Bounds and revisions
alone never identify a cached value: distinct branches, excavations or decoded snapshots may share
those numbers. Each terrain successor receives its own projection, and previously published views
remain unchanged. Wire rehydration always copies its input and does not enter this cache; private
inventories, offers, liquids and participant fields continue to project from the selected root.

The value validator counts these owned primitive cells in bulk: the collection owner and every
cell still consume the shared 921,600-value traversal budget, and nonempty cells still require
their child depth. Collection limits and the first applicable failure are unchanged. Boolean
values need no null or enum inspection, so this path avoids per-cell boxing. Arbitrary collections,
including read-only wrappers over custom lists, retain ordinary element traversal; a read-only
interface by itself is not proof of immutable ownership. Canonical re-encoding, geometry dimensions
and all cross-field checks still run for the complete view.

## Extension contract

A new presented or action-relevant field must be added to the affected typed transport record and
its capture/reconstruction mapping, with parity and malformed-input coverage. A new private
preparation capture must be explained as participant-visible request terms before it is transported.
Default diagnostic JSON changes require their own compatibility decision; this codec grants none.
Network publication, observation ordering/recovery and native client session ownership are separate
bindings around this value codec.

## Delegated local observations

The [effective-control selection boundary](effective-control.md#player-selection-boundary) publishes
an immutable home view alongside a separate capability-filtered selected actor view. `ReadView()`
retains its permanent home meaning. `ReadSelectedObservation()` captures selection, both views,
control status and readiness under the input lock. An unpublished transition refuses observation
and input rather than pairing a new selection token with an old actor view. These internal values
have no implicit representation in the home-player wire codec described above; adding transport
requires explicit mapping and generation checks at both ends.
