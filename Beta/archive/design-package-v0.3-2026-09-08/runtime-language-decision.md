# Runtime language and boundary decision

8 September 2026. The owner values deterministic simulation and seeds, is considering Rust/C++ kernels, and wants to avoid premature optimization and too many bridges. **Updated owner decision: use Godot with C# and keep the authoritative core engine-independent.** Retain one pure C# runtime initially; do not introduce a second language or simulation IPC bridge without evidence. Early proofs must be retained parts of the implementation. Exact toolchain versions and export support still need validation; no successor runtime is implemented yet. Engine-independent core boundaries are now an explicit owner decision; the user also places the FF6/Chrono Trigger spiritual homage in this core, without imposing historical limitations.

The inspected OpenFantasy simulation already compiles independently of Godot. Preserve that useful separation and reuse its suitable algorithms/tests. New durable world contracts can live in C# without inheriting the old central `World` object's responsibilities. Logical modules and a few assemblies do not imply processes, RPC, serialization or different languages.

## Initial shape

Use one pure runtime library containing domain modules; one host for ordering, network transport and storage; and a Godot client. A headless host can run the same runtime independently. For solo play, an in-process host can use direct command/observation queues; for multiplayer, the network adapter translates into the same command contract. Do not build separate gameplay logic for those hosting modes.

Keep Godot nodes, per-frame animation and render coordinates outside authoritative simulation. Keep content compilation and art generation as development tools, not services every simulation tick depends on. SQLite is a storage candidate, not a reason to split the simulation into a database service. This keeps the first proof small while preserving an eventual dedicated-server deployment.

## Comparing the options

| Option | Real benefit | Added cost and decision |
|---|---|---|
| Pure C# simulation | Existing tested code, one language/debugging model, straightforward headless tests and Godot integration | Profile allocations, pauses, locality and throughput; recommended starting point |
| Entire runtime in Rust | Explicit ownership can suit a carefully designed standalone simulation and native embedding | Broad rewrite, FFI/export tooling, content/dev-tool transition; consider only for a demonstrated product/platform constraint or measured architectural need |
| Entire runtime in C++ | Direct native libraries and strong Godot extension integration | Manual lifetime/ABI concerns and broad rewrite; no local evidence that Beta needs this now |
| One native kernel called by C# | Can exploit a valuable native library or improve a proven hot computation | Small batched interface, platform binaries and differential verification; plausible later option |
| Separate Rust/C++ simulation process | Crash/isolation boundary and independently deployable host | IPC protocol, copies or shared-memory discipline, scheduling, backpressure, failure recovery and mixed debugging; not justified for solo core simulation by current evidence |

Neither Rust nor C++ automatically provides deterministic behavior. C# does not automatically prevent it. The contract must control RNG, iteration, numeric overflow/rounding, command ordering, clocks, persistence, versioning and parallel result ordering in every language. A seed selects initial/generative variation; the current world also depends on its rules/content versions and accepted action/decision history.

GC pauses can affect responsiveness, but should not change an authoritative result if wall-clock timing is excluded from gameplay decisions. Likewise, a native race or floating-point difference can break determinism despite removing a garbage collector. Measure frame/tick latency separately from replay correctness. Cross-platform golden fixtures remain necessary; existing tests prove only the environments actually tested.

## Evidence that would justify native work

First define a representative workload: active people and creatures, distant settlements, path requests, material transformations, generation chunks, quest subscriptions and checkpoint load. Choose budgets against actual target hardware and the intended tick/frame rates. Record CPU samples, allocation rate, peak/resident memory, pause distribution, generation latency and save latency. The existing 20 Hz simulation implies a 50 ms interval, but that is historical evidence, not an approved Beta tick budget.

Improve boundedness, algorithms, data layout, caching and batch sizes before translating code. If one computation still dominates, compare a native implementation of **that same workload**, including conversion, allocation and call overhead. Use end-to-end measurements. Illustratively, making a kernel that consumes 30% of total time four times faster yields only about 1.29× overall speedup before integration costs; a microbenchmark alone is not a reason to create an architectural boundary.

Good potential candidates are bulk topology/pathfinding, batched world generation, a numerical material solver, or an established native codec. They are candidates, not measured bottlenecks in this game. Poor initial boundaries are one call per NPC property, a language split between quest conditions and quest effects, or duplicated authoritative entity graphs.

## If a native kernel earns its place

Use an **in-process C ABI** with fixed-width fields, versioned buffer layouts, explicit lengths, caller/callee ownership, clear lifetimes and error results. Batch useful work. Keep one owner of authoritative state and prevent callbacks from mutating it unpredictably. Prefer stateless input/output kernels initially. Microsoft documents source-generated `LibraryImport` for supported .NET 7+ cases, matching native signatures, blittable layouts and `SafeHandle` for owned native resources. These are practical facilities, not proof that an interface is cost-free. [Microsoft native interoperability guidance](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/best-practices)

Rust can expose a `cdylib` through a C ABI; its FFI boundary still needs explicit representation, pointer validity, lifetimes and panic/unwinding policy. Safe Rust inside a kernel does not make arbitrary foreign pointers safe. Keep errors inside the agreed result contract and prevent unwinding across an unsupported boundary. [Rust FFI reference](https://doc.rust-lang.org/nomicon/ffi.html)

Choose Rust for a standalone kernel when its libraries, ownership model and toolchain fit the measured task. Choose C++ when a required library or deeper Godot integration makes it the simpler path. Godot's official C++ bindings use GDExtension and normally avoid rebuilding the engine; custom engine modules add a different build/export obligation. Pure simulation kernels do not need to become Godot extensions at all. [Godot C++ integration](https://docs.godotengine.org/en/stable/tutorials/scripting/cpp/about_godot_cpp.html)

Compare native output against the C# reference over normal, boundary and adversarial fixtures. Verify supported architectures and builds, error handling, memory ownership, cancellation and determinism. Ship the extra language only if the measured benefit and deployment/support cost justify it. No language prototype or performance benchmark has been run as part of this decision note.

## Portability and classic lineage contract

The core's public API uses owned game-domain types: stable definition/entity/world/branch IDs, logical coordinates, commands, observations and versioned data. No Godot Node, Resource, Vector, Variant, ENet type, scene path, database connection, wall-clock timer, OS file handle or service credential enters authoritative state. Content authoring may use engine tools, but compiles into engine-neutral records. A Godot adapter maps these records to scenes and art; another engine can provide a different adapter.

A few interfaces should express actual external capabilities: ordered command ingress, checkpoint loading/commit, package loading and observation delivery. Do not abstract every helper or invent a universal cross-engine rendering API. Keep physics/collision rules needed for authoritative play in deterministic logical form; host engine physics can provide presentation effects or explicitly nonauthoritative approximations. Treat coordinate scale, numeric rounding and collision behavior as versioned rules.

FF6/Chrono Trigger inform the core through composable field objects, authored event commands, conditional progression, coordinated actor actions, expressive waits/signals, and continuity between exploration and encounters. Beta adds explicit scheduling, transaction scope, multiplayer ownership and durable history. Use fresh schemas, code, content and visual designs; no proprietary event bytecode, extracted source or copied game assets are needed. The reference study labels modern reconstructions as such. This is a creative and engineering direction, not a legal clearance opinion.

The first portability proof is a headless CLI that loads the same content and runs the same deterministic scenario without Godot. A second lightweight host should be added only when there is a concrete consumer; do not build a second game engine now. Persist package/rules versions and test restart/replay across supported hosts. This offers a practical migration path while avoiding speculative IPC and framework layers.
