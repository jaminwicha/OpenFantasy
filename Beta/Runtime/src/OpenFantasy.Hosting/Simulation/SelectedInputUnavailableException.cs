namespace OpenFantasy.Hosting.Simulation;

/// <summary>A captured selection or its Resume proof is no longer available; the connection can remain healthy.</summary>
public sealed class SelectedInputUnavailableException(string message) : InvalidOperationException(message);
