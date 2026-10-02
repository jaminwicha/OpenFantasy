namespace OpenFantasy.Hosting.Networking;

/// <summary>Records the origin of an adapter's abort without changing its public I/O exceptions.</summary>
internal enum PlayerTransportTermination { None, LocalDisposal, ProtocolFailure, TransportLost }
