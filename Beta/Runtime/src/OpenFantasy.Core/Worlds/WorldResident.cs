using OpenFantasy.Core.Content;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Language;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

public enum ResidentControllerKind { Player = 1, Host = 2 }

/// <summary>An authored utterance in one explicit channel; its claims are the speaker's, not world truth.</summary>
public sealed class ResidentUtterance
{
    public ResidentUtterance(EntityId definitionId, LanguageChannelKey channel, ContentHash channelVersion, string text)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(channelVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (CanonicalEncodingV1.Text(text, nameof(text)).Length > 1024)
        {
            throw new ArgumentException("The utterance exceeds its native dialogue profile.", nameof(text));
        }
        DefinitionId = definitionId;
        Channel = channel;
        ChannelVersion = channelVersion;
        Text = text;
    }

    public EntityId DefinitionId { get; }
    public LanguageChannelKey Channel { get; }
    public ContentHash ChannelVersion { get; }
    public string Text { get; }
}

/// <summary>One actor's admitted local behavior, language and experienced time, independent of who controls it.</summary>
public sealed class WorldResident
{
    public WorldResident(EntityId actorId, EntityId inventoryCustodianId, string displayName,
        ResidentControllerKind controllerKind, SubunitPosition homeFeet, int wanderRadiusCells,
        ActorComprehensionProfile comprehension, LocalExposureDuration experiencedTime,
        ResidentUtterance? openingUtterance = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(comprehension);
        if (CanonicalEncodingV1.Text(displayName, nameof(displayName)).Length > 128
            || !Enum.IsDefined(controllerKind) || wanderRadiusCells is < 0 or > 8
            || comprehension.ActorId != actorId)
        {
            throw new ArgumentException("The resident profile is invalid or belongs to another actor.");
        }
        ActorId = actorId;
        InventoryCustodianId = inventoryCustodianId;
        DisplayName = displayName;
        ControllerKind = controllerKind;
        HomeFeet = homeFeet;
        WanderRadiusCells = wanderRadiusCells;
        Comprehension = comprehension;
        ExperiencedTime = experiencedTime;
        OpeningUtterance = openingUtterance;
    }

    public EntityId ActorId { get; }
    public EntityId InventoryCustodianId { get; }
    public string DisplayName { get; }
    public ResidentControllerKind ControllerKind { get; }
    public SubunitPosition HomeFeet { get; }
    public int WanderRadiusCells { get; }
    public ActorComprehensionProfile Comprehension { get; }
    public LocalExposureDuration ExperiencedTime { get; }
    public ResidentUtterance? OpeningUtterance { get; }

    internal WorldResident ExperienceStep() => new(ActorId, InventoryCustodianId, DisplayName, ControllerKind,
        HomeFeet, WanderRadiusCells, Comprehension, ExperiencedTime.Add(ExposurePacingV1.FromExplorationSteps(1)), OpeningUtterance);
}
