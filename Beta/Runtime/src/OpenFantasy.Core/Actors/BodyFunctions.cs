namespace OpenFantasy.Core.Actors;

public enum BodyNature
{
    Corporeal = 1,
    Incorporeal = 2
}

/// <summary>Explicit anatomical functions, not action grants or inferred species abilities.</summary>
public enum BodyFunction
{
    Sight = 1,
    Hearing = 2,
    Manipulation = 3,
    GroundContact = 4,
    FlightSupport = 5,
    AquaticPropulsion = 6,
    UndulatingSupport = 7,
    SpectralPerception = 8,
    SpectralMotion = 9,
    Vocalization = 10,
    NaturalWeapon = 11
}

public enum LocomotionMode
{
    Ground = 1,
    Flight = 2,
    Swimming = 3,
    Slithering = 4,
    Manifestation = 5
}
