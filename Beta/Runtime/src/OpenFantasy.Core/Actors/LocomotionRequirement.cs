namespace OpenFantasy.Core.Actors;

/// <summary>A declared minimum of functional supporting parts; it does not validate a route.</summary>
public sealed record LocomotionRequirement
{
    public LocomotionRequirement(LocomotionMode mode, int minimumFunctionalParts)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        if (minimumFunctionalParts is < 1 or > BodyDefinition.MaximumPartCount)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumFunctionalParts));
        }

        Mode = mode;
        MinimumFunctionalParts = minimumFunctionalParts;
    }

    public LocomotionMode Mode { get; }
    public int MinimumFunctionalParts { get; }
    public BodyFunction SupportingFunction => Mode switch
    {
        LocomotionMode.Ground => BodyFunction.GroundContact,
        LocomotionMode.Flight => BodyFunction.FlightSupport,
        LocomotionMode.Swimming => BodyFunction.AquaticPropulsion,
        LocomotionMode.Slithering => BodyFunction.UndulatingSupport,
        LocomotionMode.Manifestation => BodyFunction.SpectralMotion,
        _ => throw new InvalidOperationException("The locomotion mode is unsupported.")
    };
}
