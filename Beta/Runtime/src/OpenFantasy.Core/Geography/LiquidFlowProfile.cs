using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Geography;

/// <summary>Integer material-unit allowances per face and selected exploration step under the fixed cell rule.</summary>
public sealed class LiquidFlowProfile
{
    private readonly Lazy<ContentHash> _version;

    public LiquidFlowProfile(ulong downwardUnitsPerFace, ulong lateralUnitsPerFace)
    {
        if (downwardUnitsPerFace == 0 || lateralUnitsPerFace == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(downwardUnitsPerFace), "Flow allowances must be positive.");
        }
        DownwardUnitsPerFace = downwardUnitsPerFace;
        LateralUnitsPerFace = lateralUnitsPerFace;
        _version = new(() => ContentHash.Compute(LiquidCellStateCodecV1.EncodeProfile(this)));
    }

    public ulong DownwardUnitsPerFace { get; }
    public ulong LateralUnitsPerFace { get; }
    public ContentHash Version => _version.Value;
}
