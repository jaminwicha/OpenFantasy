# Controller allowance recovery

## Policy and owners

Disconnect allowance is host-observed time, distinct from simulation exposure. Core's
[controller law](controller-lifecycle.md) consumes a recorded external duration; it never consults UTC,
a socket or a process timestamp. Storage schema 10 adds explicit operational recovery for controller
profiles 36–38 without changing their canonical world encodings or gameplay rules.
Schema 11 retains this operational policy for fresh [co-player permission](control-permissions.md)
worlds under profile39. Its permission decisions change no disconnect episode or measured duration.

[SqliteWorldAuthority.ControllerRecovery](../../src/OpenFantasy.Hosting/Worlds/SqliteWorldAuthority.ControllerRecovery.cs)
owns recovery records under the existing authority gate and exclusive database lease. There is no
second writer or session database. [ControllerAllowanceProgress](../../src/OpenFantasy.Hosting/Worlds/ControllerAllowanceProgress.cs)
binds an actor, control revision, disconnect episode and cumulative measured lower bound. The complete
row set equals the current ReconnectAllowance assignments, with no rows for Human or Autonomous.
Missing, extra, stale, unordered or inconsistent rows are errors, not invitations to regenerate them.

Only durably observed milliseconds survive a crash. The remaining uncommitted interval and all
downtime are uncharged. This can extend effective grace, particularly after repeated crashes. A
nominal host cutoff interval is not a bound on the lost interval when scheduling or storage stalls.
This policy does not promise exact reconstruction of every elapsed millisecond.

## Admission and history

`CreateRecoverable` explicitly creates a fresh schema-10 controller world. `CreateNew` retains its
ordinary schema selection for earlier profiles and selects schema11 for a fresh profile39 world.
`UpgradeControllerRecovery` copies a schema-nine source into a create-only
destination, preserving its exact genesis, journal, receipts, identities and canonical head. The source
remains a recovery copy. Ordinary `OpenExisting` never performs an upgrade or restarts a timer.

Immutable activation provenance records its policy, source revision/hash, fresh-versus-upgrade origin
and original outstanding episodes. An upgrade without prior elapsed evidence records those episodes
as UnknownPriorElapsed with a zero lower bound. Choosing that migration explicitly admits the policy;
later openings carry forward actual durable measurements instead of repeating that initialization.
[ControllerRecoveryReplay](../../src/OpenFantasy.Hosting/Worlds/ControllerRecoveryReplay.cs) compares
the activation against its original authenticated world during complete replay and validates the
current row set against the resulting head. Checksums protect encoding integrity; elapsed observations
remain trusted host evidence, not cryptographic proof that a physical clock ran.
Replay also enforces the capped 30,000 ms expiry interpretation for events after activation. A copied
schema-nine prefix retains its original event semantics, including its uncapped observed durations.

## Live measurement

[LocalWorldSession.ControllerRecovery](../../src/OpenFantasy.Hosting/Simulation/LocalWorldSession.ControllerRecovery.cs)
starts a fenced segment before the host scheduler starts. Its generation prevents an older attachment
from overwriting a newer segment. The segment takes each persisted subtotal as a fixed base and adds
monotonic elapsed time from one start timestamp, capped at the 30-second threshold. It does not sum
rounded cutoff increments. UTC changes, opening/inspection and offline duration do not enter this sum.

At a cutoff, the host captures one timestamp for both checkpointing and expiry selection. Readiness
does not stop external clock observation; zero ready players still grant no simulation time or new
autonomous action. The host checkpoints the full current allowance set before submitting an expiry.
Restoration removes the old episode before another timer can apply to it. Safe boundaries, accepted
work and deliberate player Resume retain their ordinary contracts.

Shutdown captures its cutoff timestamp before cancellation, joins the scheduler, checkpoints under
world serialization and then joins inspections and disposes storage. Query cleanup earns no additional
allowance time. A failure stops publication and requires recovery; it cannot continue with only
transient elapsed progress. A saved Human assignment does not prove a lost socket and creates no
disconnect merely by opening or closing a host. Autonomous state remains autonomous without repeated
expiry or implicit readiness.

## Transactions and operator boundary

[WorldDatabase.ControllerRecovery](../../src/OpenFantasy.Hosting/Worlds/WorldDatabase.ControllerRecovery.cs)
stores immutable activation plus the fenced current progress under
[schema ten](../../src/OpenFantasy.Hosting/Worlds/WorldDatabaseSchemaTen.cs). Disconnected inserts its
zero row in the same transaction as the world event. Reconnected and AllowanceExpired retire their
exact row with the event. Expiry cannot exceed its persisted measured evidence. All privileged world
commit paths pass this check; diagnostics do not confer an alternative permission.

A progress checkpoint validates the exact complete episode set and nondecreasing elapsed values before
writing. It changes no world revision, time, action, receipt or ownership. Unchanged progress needs no
storage transaction, while fencing and validation still apply. Commit uncertainty poisons the live
authority until reopen, including an exception after SQLite has committed. Before-commit rollback and
after-commit recovery retain either complete state, never half an episode transition.

The [operator](../../src/OpenFantasy.Host/ControllerRecoveryOperator.cs) exposes explicit fresh creation,
copy-and-activate upgrade and pure inspection. Its crash probe measures a real monotonic interval;
it cannot accept caller-supplied earned time. These privileged offline tools require exclusive local
save access and do not authenticate network players or start a production listener.
