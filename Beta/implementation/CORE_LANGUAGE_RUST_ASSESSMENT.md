# OpenFantasy core language decision specification: C# and Rust

**Status:** decision recorded on 17 October 2026: **stay fully in C#** for the portable core, hosting and the Godot C# presentation. No Rust core, Rust calculation kernel, engine replacement, custom engine or migration into the sibling Lattice platform is authorized or underway. The Rust shapes below are retained research with named reassessment triggers, not implementation tasks. This reaffirms the [20 September 2026 decision](CORE_LANGUAGE_CPP_ASSESSMENT.md) and the [17 October review disposition](roadmap-review-20261017/LEAD_DISPOSITION.md) finding R7.
**Assessment date:** 17 October 2026, at commit `8c7f6f23` plus the uncommitted spatial-route working-tree files. **Scope:** three candidate shapes: a pure Rust application on an existing or custom Rust engine; a Rust authority behind a C ABI with the Godot/C# tier as presentation; and the retained C# application. It also assesses integration with the owner's Lattice platform. Deterministic worlds, preserved history, one authoritative simulation, Windows/macOS/Linux delivery and later mobile remain requirements in every shape.

## Decision

**Retain C# everywhere. Run the already scheduled partition/state measurement before any language work. Record the two follow-ups below as candidates for separate assignment, not as authorized work.**

The reasons are measured, not stylistic:

1. The port target has more than doubled in four weeks and is still growing. A port chases a moving target unless the C# authority is frozen, and freezing it stalls the roadmap's actual critical path: the inhabited P02 loop, NPC cognition, story, ecology, art, audio and human acceptance. None of those improve with a language change.
2. There is still no measured evidence that managed execution is the limiting factor. The [LANG-01 to LANG-04 gates](p00/BENCHMARK_FIXTURES.md) remain unrun. The two retained symptoms, a 15.99-second historical read against a 30-second deadline and a 150 ms lease miss, have no identified cause. The source-supported suspects in the earlier assessment are algorithmic: full-root content hashing, whole-world admission validation and genesis replay without checkpoint pruning. Those costs port unchanged.
3. The expensive part of any port is byte-exact compatibility, not gameplay logic. The portable Core now carries 233 codec, encoding and frame-reader files across 39 retained snapshot interpretations. That work cannot be rewritten better; it can only be reproduced exactly.
4. Hosting and the client construct Core domain objects directly. A "Rust kernel with a C# skin" is therefore not a core swap: a C ABI forces a projection layer, and the honest form of that option is Rust Core plus Rust Hosting with C# reduced to presentation.
5. The Godot client's engine use is small on rendering and large on UI, the inverse of where Rust engines are strong. A pure Rust client rebuilds the wheel, Pack, Recipes, Trade, Care and History surfaces, input remapping, exports and the native post-draw evidence harness.

The earlier Rust research in [research-v0_8](../research-v0_8/csharp-rust-core-reassessment.md) and the [C++ assessment](CORE_LANGUAGE_CPP_ASSESSMENT.md) remain the shared basis for execution model, in-process ABI design, IPC rejection, determinism obligations and platform constraints. This record does not repeat that material; it adds the current measurements, the Rust-specific and Lattice-specific findings and the decision triggers.

## What is actually being migrated

The [evidence file](CORE_LANGUAGE_RUST_EVIDENCE.json) pins the measured tree. Counts are physical lines excluding `bin`, `obj` and `.godot` output, including the uncommitted spatial working-tree files present at measurement. Lines are not a complexity score.

| Production area | Files | Lines today | Lines at 19 September | Growth |
|---|---:|---:|---:|---:|
| Portable Core, zero Godot references | 802 | 63,731 | 30,525 | 2.1× |
| Hosting: SQLite journal, scheduler, WebSocket/TLS | 312 | 26,717 | 5,904 | 4.5× |
| Host operator executable | 35 | 1,849 | 1,103 | 1.7× |
| Player WebSocket server | 6 | 382 | not separate | |
| Godot C# client | 143 | 27,966 | 6,698 | 4.2× |
| Total production application | 1,298 | 120,645 | 44,230 | 2.7× |

| Compatibility and coupling surface | Today | At 19 September |
|---|---:|---:|
| Core codec, encoding and frame-reader files | 233 files, 17,742 lines | 109 files, 8,856 lines |
| Retained snapshot codec interpretations | 39, through profile 41 | 18 |
| Core files using Int128, UInt128 or BigInteger | 83 | 58 |
| Core files using SHA-256 | 10 | |
| Core `float`/`double` tokens | 2 tokens in 2 files; no transcendental math | |
| Test methods | 3,006 across 812 files and 136,580 lines | |
| Non-Core production files importing Core types | 407 of 496 | 108 of 139 |
| Client files importing Core directly, bypassing Hosting | 114 of 143 | |
| Commits since 19 September | 195 | |

The core is integer-only deterministic arithmetic. That favors Rust in principle: `i128`/`u128` are primitive types and overflow checking is a build policy. It does not reduce the compatibility surface. Every retained interpretation must round-trip bit-identically with its original refusals, receipts, identities and pending cursors, and the Hosting tests also compile selected client files, so the test oracle is entangled with the Godot tier.

The Godot client's engine dependency is narrow. It uses Control widgets, `Node2D`, `Sprite2D`, one `Camera2D`, `CanvasLayer`, keyboard, mouse and joypad input events, `DisplayServer`/`Viewport` queries and `RenderingServer.FramePostDraw` for native probes. It uses no physics bodies, tilemaps, navigation, shaders, tweens, resource loading or audio yet, and renders with the GL Compatibility method at a 960×540 canvas. See [project.godot](../Runtime/client/project.godot) and the [client project](../Runtime/client/OpenFantasy.Client.csproj), which enables dynamic loading and targets Godot.NET.Sdk 4.6.0 on net8.0.

## Three candidate shapes

| Shape | Rebuilt | Expected gains | Costs this assessment adds to the earlier research | Assessment |
|---|---|---|---|---|
| Retain C# and Godot | Nothing | Gameplay velocity continues on the critical path; one language across authority, host, tests and presentation | Managed allocation/latency still unmeasured; LANG gates still unrun | **Selected** |
| Rust authority, Godot/C# presentation | Core and Hosting, about 90,000 lines, plus a projection ABI and rewiring most client files off Core types | Core lifetime control without a tracing collector; embeddable by non-.NET hosts; shareable with Lattice | Godot C# export limits remain, so mobile and web gain nothing; two rulesets during transition; the September ABI contract applies unchanged | Conditional on measured evidence |
| Pure Rust on an existing or custom engine | Everything, about 120,000 lines, plus engine integration | No .NET anywhere; mobile and web freed from Godot C# limits; native Lattice sibling | UI rebuild where Rust tooling is weakest; loss of the Control tree and the post-draw evidence harness; engine choice problems below | Not justified by any current requirement |

### Engine options for a pure Rust client

| Engine route | Current state | Fit with retained contracts |
|---|---|---|
| Bevy | 0.20 released 8 October 2026; releases every few months with migration guides | ECS-first architecture conflicts with the [world composition contract](../Runtime/docs/architecture/world-composition.md), which rejects a universal ECS/property bag, and with the owner's object-oriented style rule. Periodic breaking migrations add upkeep to a client that would be the smallest part of the port. |
| godot-rust (`gdext`) | 0.5 supports Godot 4.6 and every version from 4.2; Android, iOS and Wasm documented as experimental in earlier releases | Keeps Godot and its Control tree while replacing C# scripts with Rust. This is a Godot-plus-Rust variant, not pure Rust, and it removes the .NET runtime only if no C# remains. It is the realistic route if the Godot C# export limits ever become the blocking constraint. |
| Custom engine on `wgpu`, `winit`, `egui` or similar | Mature crates, no editor, no scene format | The rendering need is a sprite batcher. The real need is a UI toolkit with layout, focus, theming and accessibility for many panels; immediate-mode UI changes the visual and interaction contract and the native probe method. Every engine bug becomes project work. |
| Fyrox, macroquad and smaller engines | Smaller communities; Fyrox ships an editor and a scene graph | Not evaluated beyond landscape; none removes the UI rebuild. |

None of these routes is required by a current requirement. The pure Rust shape is a product decision to rebuild presentation, not a Core swap.

### The Rust authority boundary, if evidence later selects it

The [C++ assessment](CORE_LANGUAGE_CPP_ASSESSMENT.md#native-boundary-specification-if-evidence-justifies-it) already specifies the required ABI responsibilities: negotiation, open/restore, prepare, batched identity checks, read result, commit notification after the host's single durable transaction, abort, batched queries and explicit buffer ownership. Those apply unchanged to Rust. Rust-specific obligations:

- Export a C ABI with `extern "C"` functions and opaque handles; never pass `Vec`, `String`, trait objects or references into managed code as public data. [Rust FFI](https://doc.rust-lang.org/nomicon/ffi.html).
- Never unwind a panic across the boundary. Convert to status data and poison the handle until recovery from the last durable commit; aborting panics cannot be caught. [Unwinding across FFI](https://doc.rust-lang.org/nomicon/ffi.html#ffi-and-unwinding).
- Set `overflow-checks = true` in every profile that produces authoritative binaries, or use explicit checked operations throughout; release profiles disable overflow checks by default. [Cargo profiles](https://doc.rust-lang.org/cargo/reference/profiles.html#overflow-checks).
- Never iterate `HashMap`/`HashSet` to produce authoritative order or bytes; the default hasher is randomly seeded. Use `BTreeMap` or sorted vectors for canonical ordering. [HashMap](https://doc.rust-lang.org/std/collections/struct.HashMap.html).
- Keep the C# side on source-generated `LibraryImport` with `SafeHandle` ownership, as the earlier research states. [Native interop best practices](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/best-practices).
- Because 407 of 496 non-Core production files import Core types, plan the projection layer before the kernel. A thin wrapper around `Commit` leaves Hosting and the client unable to compile.

## The Lattice platform

[Lattice](/Users/alucinor/Workspace/Projects/Lattice/README.md) is the owner's game-neutral Rust platform beneath the discrete-world games RogueFiction, RustAdventure and LifeFormzFable, which depend on it by the relative path `../Lattice`. Measured at this assessment it is one dependency-free crate, `lattice-engine`, of 1,759 lines across eleven modules: PCG32 `Rng` with `sub_seed`, a `Grid` trait with lines, line of sight, field of view, reachability and distance fields over a Z² lattice, value noise, rooms/caverns map generation, an FNV hasher for replay digests, a text run log and save reader, a host file port, energy scheduling and a byte store. Its [doctrine](/Users/alucinor/Workspace/Projects/Lattice/DOCTRINE.md) requires that nothing names a game, that behavior is added rather than changed, that every dependent is green before a commit, that the engine crate links no dependencies and that the platform never shapes itself to one game.

| Primitive | Lattice | OpenFantasy canonical contract |
|---|---|---|
| Random streams | PCG32, single mutable stream, modulo range selection | SHA-256 counter stream per owner with rejection sampling, exhaustion and rollback; see [CONTRACTS.md](../Runtime/src/OpenFantasy.Core/CONTRACTS.md) |
| Identity and digests | FNV-based replay digest | 16-byte SHA-256 generated/spawned identities; SHA-256 content hashes |
| Space | Z² grid, Chebyshev/Manhattan metrics | Three-dimensional stacked terrain in 32×32×16 regions with excavation history, structural loads and liquid flow |
| Time | Energy scheduling per action | `WorldInstant` signed days plus milliseconds with Int128 differences; 20 Hz host scheduler; tactical readiness |
| Persistence | Text save with replayed input log | Canonical big-endian binary frames across 39 interpretations; SQLite journal with original-genesis replay |
| Multiplayer | None | One to four authenticated players over TLS/WebSocket with a shared input cutoff |

The overlap is small and the primitives are incompatible. Lattice's RNG, hash, scheduling and save cannot replace OpenFantasy's without a versioned rules change that breaks replay of every retained profile, and OpenFantasy's three-dimensional world does not fit a Z² grid trait. Adopting Lattice primitives in the authority is rejected regardless of language.

The value flows upward. OpenFantasy's retained primitives are already game-neutral and have an independent oracle: [reference-v1.json](../Runtime/tests/OpenFantasy.Core.Tests/Fixtures/reference-v1.json), produced by [generate_reference_vectors.py](../Runtime/tests/OpenFantasy.Core.Tests/Fixtures/generate_reference_vectors.py) in the Python standard library. A Rust crate holding [canonical encoding](../Runtime/src/OpenFantasy.Core/CanonicalEncodingV1.cs), the [counter RNG](../Runtime/src/OpenFantasy.Core/CounterRandomV1.cs), [entity identity](../Runtime/src/OpenFantasy.Core/EntityId.cs), [world instants](../Runtime/src/OpenFantasy.Core/WorldInstant.cs) and [rule math](../Runtime/src/OpenFantasy.Core/RuleMath.cs), validated against those vectors, is a bounded deliverable that Lattice's add-rather-than-change doctrine admits as a new crate. Its SHA-256 dependency would live in a separate crate under the no-dependency rule. Two governance cautions apply: OpenFantasy would dwarf the platform it joins, against the doctrine that the platform never shapes itself to one game, and OpenFantasy's test suite is far heavier than the dependents' gates, so the ecosystem check would need tiering before OpenFantasy could be a dependent.

This crate is **not authorized by this decision**. It is recorded as the first candidate if the owner later opens Rust work, because it costs days, creates the cross-language oracle any later kernel needs and gives the sibling games something they can adopt additively.

## Determinism and preservation requirements

Every obligation in the [C++ assessment](CORE_LANGUAGE_CPP_ASSESSMENT.md#determinism-and-preservation-requirements) applies: exact integer widths and refusals, the counter RNG and SHA-256 derivations, canonical encoding and ordering, no floating-point in authoritative arithmetic, every retained profile's interpretation, cross-architecture and cross-configuration replay, and a single production authority after transition. Rust adds the profile, hasher and unwinding rules above. Rust's safety guarantees address memory and data-race defects; they do not decide domain invariants such as which of two valid purchases commits first, and they do not establish replay equivalence. The reference vectors and the 3,006 retained tests remain the proof, whichever language runs.

## Platforms and deployment

Godot 4.6 documents C# support for Windows, macOS and Linux, experimental Android and iOS, and no web export. [Godot 4.6 C# platform support](https://docs.godotengine.org/en/4.6/tutorials/scripting/c_sharp/index.html). A Rust authority with a retained C# presentation inherits those limits unchanged. Only a presentation tier without C#, through `gdext` or a Rust engine, removes them, and that route must still qualify actual device exports, suspend/resume and save recovery under P14. Rust's own target tiers, linking, signing and packaging obligations are as the earlier research describes. [Rust platform support](https://doc.rust-lang.org/rustc/platform-support.html). No native library, export or device test was executed for this assessment.

## Effort and opportunity cost

These are planning extrapolations from the September envelope, scaled by the measured growth. They are not measured port throughput and do not estimate the remaining game.

| Option | Provisional envelope | Basis |
|---|---|---|
| Representative measurement, PLAN-STATE and the R7 probe | Already scheduled; 3 to 7 engineer-days for existing fixtures | Unchanged from September |
| Primitives crate validated against the reference vectors | 2 to 5 engineer-days | Five source files with an existing independent oracle |
| Projection boundary between Hosting/client and Core types | Incremental per subsystem; no credible total | 407 of 496 files; best done alongside PLAN-STATE where those boundaries are already under review |
| Rust Core only, retained C# Hosting | Roughly 27 to 54 engineer-weeks | September's 13 to 26 weeks scaled by 2.1× Core growth; unstable shape because Hosting cannot compile against a C ABI without the projection layer |
| Rust Core plus Hosting, C# presentation | Materially above the Core-only envelope; Hosting grew 4.5× and now owns SQLite, Kestrel and channel scheduling | No credible bounded estimate without a representative port |
| Pure Rust application | Greater still; replaces presentation, probes, exports and tooling | Same conclusion as the September "rewrite everything" row |

The opportunity cost is the same as in September but larger: during a port, most effort restores behavior that already exists while the P02 closure table, NPC cognition, story, ecology, art, audio and human acceptance wait. The case for migrating early rather than late is real and was weighed; it would only outweigh the above if the scheduled measurement shows a pervasive managed cost. Doing it now is a bet without that evidence.

## Reassessment triggers

Reopen this decision, with a new explicit owner decision and a fresh record, when any of the following holds:

1. **Measured managed cost:** LANG-01 to LANG-03, or the bounded R7 probe, show allocation, collection or latency costs that C# layout, allocation and scheduling work cannot bring within the declared budgets. A microbenchmark win is insufficient. The first response remains a single bounded kernel behind the specified ABI with the C# implementation as differential oracle.
2. **A concrete non-.NET host:** a Lattice sibling, terminal host or other product requires the OpenFantasy authority as a library. That selects the Rust-authority shape, after the projection boundary exists.
3. **Export qualification failure:** the Godot C# client fails mobile or web export qualification at its required phase. That is the only trigger for the pure Rust or `gdext` presentation shape, because it is the only constraint the kernel shape does not remove.

Absent a trigger, the standing rule in [AGENTS.md](../AGENTS.md) holds: optimize within C#, keep the core decoupled, and do not add a native core, kernel or IPC worker.

## Documentation review

- **Plan/spec:** reaffirms the binding "Pure C#, decoupled core" decision in [CURRENT_ROADMAP](CURRENT_ROADMAP.md) and the owner decision paragraph in [AGENTS.md](../AGENTS.md); both now link this record and its triggers. No requirement, phase exit, gate or catalog target changes. The LANG gates remain unrun. The handoff's decision list links this record.
- **Reference:** no impact. [Runtime and authority](../Runtime/docs/architecture/runtime-and-authority.md) continues to state that a portable C# domain core owns game rules; this decision leaves ownership, interfaces, data flow, canonical bytes, timing and persistence unchanged. The Lattice comparison and the Rust boundary obligations are dated research and belong here, not in the timeless reference.
- **Accuracy/completeness:** the lead measured the working tree at `8c7f6f23` with the uncommitted spatial-route files, inspected the Core, Hosting, Host, Server and client project files, the Godot project settings, the coupling counts, the codec inventory, the reference-vector fixtures, the Lattice README, doctrine, manifest and crate sources, and the September assessment and research. External engine facts come from the Bevy 0.20 release note and the godot-rust 0.5 announcement; no Rust toolchain, benchmark, native library, Godot export or device test was run. Omissions retained explicitly: no comparative Rust benchmark, no Native AOT qualification, no representative port, no estimate of the remaining game. Line counts include working-tree edits not yet committed, so a later recount at the commit boundary may differ slightly.
- **Checks:** `python3 Beta/tools/check_documentation.py --work-record Beta/implementation/CORE_LANGUAGE_RUST_ASSESSMENT.md --files Beta/AGENTS.md Beta/implementation/CURRENT_ROADMAP.md Beta/implementation/AGENT_HANDOFF.md Beta/implementation/CORE_LANGUAGE_RUST_ASSESSMENT.md` from the repository root, and `git diff --check` on the authored pages. Results are recorded in [CORE_LANGUAGE_RUST_EVIDENCE.json](CORE_LANGUAGE_RUST_EVIDENCE.json). This is a document-only decision; no runtime suite, benchmark or Godot launch is claimed. Independent review is not claimed.
