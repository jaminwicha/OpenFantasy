namespace OpenFantasy.Hosting.Networking;

/// <summary>A single player's response completed after its transport send deadline; it is not a world-host timeout.</summary>
public sealed class PlayerResponseTimeoutException() : TimeoutException("The player response exceeded its send deadline.");
