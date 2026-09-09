# Review evidence — 8 September 2026

The review examined the working tree including staged milestone 8 changes. Existing game source was not edited. Baseline toolchain: Godot 4.4.1 stable .NET, .NET 8 project; runtime visual capture on Apple M2 using OpenGL Compatibility.

## Results

- `dotnet build --no-restore`: passed, zero warnings/errors.
- `/Applications/Godot_mono.app/Contents/MacOS/Godot --headless --path . -- --selftest`: 40 passed, zero failed.
- Plain net8 harness including unmodified `src/Sim/**/*.cs`: compiled and ran with no Godot reference.
- Diagnostic results: chest ownership, respawn schedule and dungeon tile mutations are invisible to the current StateHash; a dungeon tile edit is lost after character-save restart with the same seed; an accepted generated frontier bounty is lost when restored into a fresh world.
- Isolated dedicated server plus two automated clients: both reported two players and four chats. Damage events were zero, so this run does not establish combat correctness.
- Isolated automatic solo launch: completed and captured [current-openfantasy.png](/Users/alucinor/Workspace/Projects/OpenFantasy/Beta/review-evidence/current-openfantasy.png), which was visually inspected.
- RogueFiction: `CARGO_TARGET_DIR=/private/tmp/ofbeta-review-20260908/rogue-target cargo test --offline --locked`: 30 unit + 17 playthrough + 5 topology + 5 conversational-flow tests passed; zero failed.
- Beta v0.2 requirement IDs and referenced test IDs were checked for uniqueness and presence in the Markdown baseline.

## Reproducing the diagnostic harness

The sources are deliberately stored as `.txt` so the root C# project's default source glob cannot compile them into the game:

- [Probe project](/Users/alucinor/Workspace/Projects/OpenFantasy/Beta/review-evidence/probe.csproj.txt)
- [Probe program](/Users/alucinor/Workspace/Projects/OpenFantasy/Beta/review-evidence/persistence-probes.cs.txt)

Copy these into an empty temporary directory as `Probe.csproj` and `Program.cs`. Update the absolute source/data paths if the repository has moved. Run `dotnet run --project <temporary-directory>/Probe.csproj`. The five diagnostic lines print `True` for the shortcomings observed in this review; they are characterization probes, not acceptance tests that should keep passing after a fix. The program also checks that setup actually enters a generated site and accepts a generated bounty.

The fresh-world probes reconstruct the unmodified simulation through its public API. They are not a host-process crash test. The result demonstrates missing world-save coverage independently of transport or rendering; a future durable host still needs separate kill/restart testing.

The rendered/server smoke checks used a temporary copy at `/private/tmp/ofbeta-review-20260908/game`, with saves redirected into that temporary review directory. No real player save data was loaded. Those temporary binaries are not a persistent deliverable.

## Limits

Passing tests establish the covered behaviors only. No full manual playthrough, cross-platform replay proof, sustained performance benchmark, network impairment trial, security certification, or art acceptance was performed. See the decision review for what follows from each result.
