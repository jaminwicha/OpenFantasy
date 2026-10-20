using OpenFantasy.Core;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Client.Presentation.Audio;

/// <summary>The semantic kind of a transient sound; playback assets are chosen later by acoustic intent.</summary>
internal enum SoundCueKind
{
    Footstep = 1,
    GatherContact = 2,
    ConstructionContact = 3,
    ExcavationContact = 4,
}

/// <summary>
/// The contact material a cue implies. <see cref="Ground"/> is generic walking ground: surface-specific footsteps
/// need terrain material in the presented view and are not inferred.
/// </summary>
internal enum SoundMaterial
{
    Ground = 1,
    Oak = 2,
    Berry = 3,
    Joinery = 4,
    Earth = 5,
}

/// <summary>
/// The stable identity of one transient sound: its kind, the visible source and the committed revision and ordinal
/// that produced it. One listener visit delivers each key at most once.
/// </summary>
internal readonly record struct SoundCueKey(SoundCueKind Kind, EntityId SourceId, ulong SourceRevision, ulong Ordinal);

/// <summary>A transient sound derived from a committed, permitted change; it never causes or confirms a world effect.</summary>
internal sealed record SoundCue(SoundCueKey Key, SoundMaterial Material, SubunitPosition Origin)
{
    internal SoundCueKind Kind => Key.Kind;
}
