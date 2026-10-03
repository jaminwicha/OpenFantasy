# Timer continuation mechanism review

A reused independent native investigator examined official runtime tag `v10.0.12`, matching the
failed application's reported version. The installed binary has not been authenticated against that
source commit. This was bounded read-only research, with no provider request or runtime change.

`PeriodicTimer` completes a default `ManualResetValueTaskSourceCore<bool>`, permitting an awaiting
continuation to run inline. Therefore the observation loop can resume into synchronous encoding
inside that timer callback. `ConfigureAwait(false)` does not force a new worker. Ordinary `Task.Delay`
timeout completion also permits inline continuation, so the host loop can similarly resume directly
into its tick. Cancellation has a distinct asynchronous-completion path.

On Unix, a dedicated timer thread queues high-priority TimerQueue work. `FireNextTimers` queues
additional expired callbacks before firing one callback inline, outside its lock. A slow callback
can occupy its worker but does not establish global timer dispatch blockage. A succession of
observation/replica/tick intervals on one worker is consistent with callback/continuation chaining;
without enqueue/start events or stacks it does not prove that chain caused the captured failure.

Primary references:

- [PeriodicTimer](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/System.Private.CoreLib/src/System/Threading/PeriodicTimer.cs)
- [ManualResetValueTaskSourceCore](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/System.Private.CoreLib/src/System/Threading/Tasks/Sources/ManualResetValueTaskSourceCore.cs)
- [Task delay completion](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/System.Private.CoreLib/src/System/Threading/Tasks/Task.cs)
- [Portable timer dispatch](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/System.Private.CoreLib/src/System/Threading/TimerQueue.Portable.cs)
- [Timer callback dispatch](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/System.Private.CoreLib/src/System/Threading/Timer.cs)

A controlled timer-provider test could establish inline versus asynchronous callback behavior using
barriers and a thread-local marker. It would prove that API mechanism only; a manual provider firing
callbacks serially does not reproduce production scheduling fairness. The same-process CPU/pause
capture remains the next causal diagnostic. Thread count alone cannot establish spare workers,
and no scheduler rewrite is justified by source inspection alone.
