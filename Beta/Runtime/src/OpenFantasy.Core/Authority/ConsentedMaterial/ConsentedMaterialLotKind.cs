namespace OpenFantasy.Core.Authority.ConsentedMaterial;

/// <summary>
/// The canonical owner that holds the approved lot. A kind is an exact claim about existing stock;
/// it never converts a raw lot into produced stock or a produced lot into raw source rights.
/// </summary>
public enum ConsentedMaterialLotKind : ulong
{
    CountedSource = 1,
    Produced = 2
}
