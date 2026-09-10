using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Quests;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>Only the current actor's progression and purse, with no reserve or other actor balances.</summary>
public sealed record LocalProgressionView(ulong Revision, string ProfileName, int Level, ulong Experience,
    int Hp, int MaxHp, int Mp, int MaxMp, ulong CurrencyBalance, string CurrencyUnit);

/// <summary>An addressed offer or this actor's retained commitment; funding availability is not a private balance.</summary>
public sealed record LocalQuestView(EntityId DefinitionId, ContentHash Version, string Title, string Description,
    EntityId GiverActorId, EntityId ItemDefinitionId, ulong RequiredQuantity, ulong EligibleQuantity,
    ActorQuestStatus? Status, ulong ParticipationRevision, uint MinimumLevel, ulong RewardExperience,
    ulong RewardCurrency, string CurrencyUnit, bool CanAccept, bool CanComplete);
