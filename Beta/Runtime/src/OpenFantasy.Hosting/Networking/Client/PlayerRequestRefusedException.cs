namespace OpenFantasy.Hosting.Networking.Client;

/// <summary>Transient admission feedback, never a committed world action receipt.</summary>
public sealed class PlayerRequestRefusedException : Exception
{
    public PlayerRequestRefusedException(PlayerRequestRefusal refusal)
        : base($"The player request was refused: {refusal}.")
    {
        if (!Enum.IsDefined(refusal)) throw new ArgumentOutOfRangeException(nameof(refusal));
        Refusal = refusal;
    }

    public PlayerRequestRefusal Refusal { get; }
}
