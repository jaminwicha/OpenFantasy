using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

internal sealed class PendingLocalAction(WorldActionCommand command, byte[] bytes)
{
    internal WorldActionCommand Command { get; } = command;
    internal byte[] Bytes { get; } = bytes;
    internal TaskCompletionSource<WorldActionReceipt> Completion { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
