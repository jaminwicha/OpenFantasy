# Native roster journey delivery

This increment supplies an opt-in four-process Godot proof over the existing TLS player protocol.
It creates the exact production `CargoOutcomeWorldAdmission` four-human roster, with the original
NPC roles, counted supplies and ownership. The coordinator creates only the fresh saved world;
all subsequent gameplay passes through each process's `FarmGame` input and authenticated connection.

The finite mailbox supports observation, local pause/resume, one native walking sample, native target
cycling and encounter entry, wheel Wait/Strike/outcome reading, frame capture and joined close.
It does not expose action submission, authority mutation, teleportation or controller reassignment.
The coordinator plans ordinary routes using admitted source terrain/body definitions and observed
positions. This is test tooling, not a claim that a player-facing route planner exists.

The journey starts all four clients paused, assembles the party physically, enters a wolf encounter,
and has three players choose Wait. The fourth withholds its choice, closes, reopens the same saved
actor unready, then deliberately chooses Wait. Public observations demonstrate the held interval;
reopened durable history separately checks that the calendar and elapsed combat time did not advance
between the third and fourth first choices. Automatic NPC queue revisions are allowed during this
hold, and actual NPC resolution is required afterward. This is a reconnect within the current
allowance; it does not implement the later journaled 30-second disconnect/handoff policy.

After the first resolution, the coordinator supplies bounded deliberate native choices: Strike when
the public ability offer permits it, otherwise Wait. It requires a victorious completed wolf encounter,
all four independent outcome readings, return to exploration and authority reopening. Every observed
canonical command and receipt is authenticated against the reopened journal. Recorded per-human Wait
resolutions must show positive incurred debt. Original resident/controller/storage/form/body/control
identities remain equal to genesis. No pending action is automatically retried or reconstructed.

One process renders from a disposable `no_focus=true` project; the others are headless. The rendered
window is unfocusable and native focus acquisition fails the probe. Fixture focus activates only the
game's controlled input path. Captures occur at the missing-choice hold, resumed combat and completed
outcome. Screenshots still require visual inspection; this controlled background run does not prove
physical device, foreground OS focus or human visual acceptance.

## Run and evidence

The integration lead builds the native client and Server.Tests, then runs the `native-roster` mode of
this packet's check wrapper, filtering `NativeRosterJourneyTests`. The wrapper supplies:

- `OPENFANTASY_GODOT`: qualified Godot executable.
- `OPENFANTASY_DOTNET`: qualified .NET executable.
- `OPENFANTASY_NATIVE_PLAYER_EVIDENCE`: a fresh retained output directory.

Evidence goes under its `roster/` child: genesis bytes, actual SQLite save, public certificate,
per-process requests/responses, exact submissions, logs, focus traces and PNGs, plus `journey.json`
or `journey.failed.json`. Bearer invitations and the disposable project symlinks live only under
`ServerFiles` temporary storage. A run requires a fresh directory and never overwrites a prior save.

The combined journey has an absolute 240-second bound, each observation has its own 5-second bound,
opening 30 seconds, mailbox operation 20 seconds, and combat continuation 90 seconds/80 deliberate
choices. Native waits independently check deadlines even on successful completion. Process cleanup
kills and joins only the fixture's owned processes after failure and retains their logs.

## Delivery state

Source is supplied for integration. The worker has run no build, test, Godot process or Git operation.
Compilation, the full native journey, screenshot inspection and preserved gates remain unverified
until the integration lead records their actual results. No four-player phase acceptance is claimed.
The work adds qualification instrumentation and an explicit disposable entry mode; it changes no
canonical gameplay, protocol, party or admission rule.
