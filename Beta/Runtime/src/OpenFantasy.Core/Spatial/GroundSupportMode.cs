namespace OpenFantasy.Core.Spatial;

/// <summary>Explicit interpretation of grounded actor contact in an admitted local world.</summary>
public enum GroundSupportMode
{
    TerrainOnly = 1,
    TerrainAndConstruction = 2,
    SteppedTerrainAndConstruction = 3
}
