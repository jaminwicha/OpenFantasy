# Host timing diagnostics

[HostTimingEvents](../../src/OpenFantasy.Hosting/Diagnostics/HostTimingEvents.cs) is an opt-in
`EventSource` named `OpenFantasy-Host-Timing`. It observes host and client work without owning
simulation time, input, authority, scheduling or persistence. Without an enabled listener, its
helpers return before timestamp and payload construction. Per-instance diagnostic identifiers
are separate from world identities and are never stored in canonical roots or commands.

Stages distinguish client startup and input queueing, server input receipt, serialization-gate
waits, host cutoffs, movement sampling, commit, view projection, observation delivery, replica
application and session teardown. Bind events relate diagnostic owners to ports, host instances
and actor identities. Payloads contain identifiers, stage names, numeric counters and monotonic
`Stopwatch` timestamps; they contain no credentials, message bodies, world snapshots or dialogue.

Elapsed stage intervals describe observed wall time, including suspension and contention. They
are not CPU attribution. A missing terminal stage may indicate an exception or an unfinished
operation; it does not prove a deadlock. Movement-age values use `TimeSpan` ticks from the host's
own time provider, so synthetic-clock tests must not compare them directly with wall timestamps.
An accepted input sample is not proof that the simulation selected it while its lease was fresh.
Likewise, first-fragment timeout and completed durable disconnect are different boundaries.
The `commit` spans surround selected simulation batches. Direct controller-event commits are
included in their enclosing disconnect or cutoff spans, without separate commit spans. An absent
commit marker therefore does not mean that controller cleanup performed no authority work.

Diagnostic consumers must bound retained output and disclose dropped events. Enabled listeners
can perturb timing; traces support diagnosis but do not replace normal-operation acceptance.
No event authorizes extending an input lease, retrying a command, suppressing validation or
changing a test deadline. A successful isolated run cannot explain an earlier failed aggregate.

The Server test assembly has a separate opt-in capture adapter. It activates only with an
absolute existing `OPENFANTASY_HOST_TIMING` output directory, holds a bounded capture in memory,
and writes its report at process exit. Its process/thread-pool/GC observations provide context,
not proof of the cause of a delayed operation. Ordinary tests leave capture disabled.
