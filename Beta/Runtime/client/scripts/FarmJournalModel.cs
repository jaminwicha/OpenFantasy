using OpenFantasy.Core.Quests;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>The player's own character facts, exactly as the permitted progression view states them.</summary>
internal sealed record JournalCharacter(string Name, string Profile, int Level, ulong Experience, int Hp, int MaxHp,
    int Mp, int MaxMp, ulong Currency, string CurrencyUnit);

internal enum JournalQuestState { Accepted = 1, Offered = 2, Completed = 3 }

/// <summary>One quest the view exposes: an addressed offer, the player's own commitment or a completed one.</summary>
internal sealed record JournalQuest(string Title, string Description, JournalQuestState State, ulong Ready, ulong Required,
    ulong RewardExperience, ulong RewardCurrency, string CurrencyUnit, bool CanComplete)
{
    internal string StateText => State switch
    {
        JournalQuestState.Accepted => CanComplete ? "Accepted · ready to deliver" : "Accepted",
        JournalQuestState.Offered => "Offered in this conversation",
        _ => "Completed",
    };

    internal string ProgressText => State == JournalQuestState.Completed ? "Delivered" : $"{Ready} / {Required} ready";
    internal string RewardText => $"{RewardExperience} XP · {RewardCurrency} {CurrencyUnit}";
}

/// <summary>
/// A read-only journal page projected from the player's permitted view. It invents nothing: an offer appears only
/// while the view addresses it, commitments persist as the world keeps them, and absent facts stay absent.
/// Equipment and known skills have no projection yet and are reported as not shown.
/// </summary>
internal sealed record JournalPage(JournalCharacter? Character, IReadOnlyList<JournalQuest> Quests)
{
    internal const string EquipmentAndSkills = "Equipment and known skills are not shown yet.";

    internal static JournalPage From(LocalWorldView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        JournalCharacter? character = view.Progression is { } progression
            ? new JournalCharacter(view.Actors.Single(actor => actor.ActorId == view.PlayerActorId).DisplayName,
                progression.ProfileName, progression.Level, progression.Experience, progression.Hp, progression.MaxHp,
                progression.Mp, progression.MaxMp, progression.CurrencyBalance, progression.CurrencyUnit)
            : null;
        List<JournalQuest> quests = view.Quests.Select(quest => new JournalQuest(quest.Title, quest.Description,
                quest.Status switch
                {
                    ActorQuestStatus.Accepted => JournalQuestState.Accepted,
                    ActorQuestStatus.Completed => JournalQuestState.Completed,
                    _ => JournalQuestState.Offered,
                },
                Math.Min(quest.EligibleQuantity, quest.RequiredQuantity), quest.RequiredQuantity, quest.RewardExperience,
                quest.RewardCurrency, quest.CurrencyUnit, quest.CanComplete))
            .OrderBy(quest => quest.State).ThenBy(quest => quest.Title, StringComparer.Ordinal).ToList();
        return new JournalPage(character, quests.AsReadOnly());
    }
}
