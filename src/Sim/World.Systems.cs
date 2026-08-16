namespace OpenFantasy.Sim;

/// <summary>
/// M2 systems: NPCs, inventory/equipment, shops, quests, parties, loot.
/// All methods are server-side and deterministic; they mutate world state
/// only through the same rules as the tick loop.
/// </summary>
public sealed partial class World
{
    // ------------------------------------------------------------------ npcs

    private void SpawnNpcs()
    {
        int u = SimConstants.UnitsPerTile;
        foreach (string npcId in Data.NpcOrder)
        {
            var def = Data.Npcs[npcId];
            var e = new Entity
            {
                Id = _nextEntityId++,
                Kind = EntityKind.Npc,
                Name = def.Name,
                ArchetypeId = def.Sprite,
                NpcDefId = def.Id,
                MoveSpeed = 2,
                HasAppearance = def.Appearance != null,
                Appearance = def.Appearance?.ToAppearance().Sanitized(Data.Appearance) ?? Appearance.Default,
                X = def.TileX * u + u / 2,
                Y = def.TileY * u + u / 2,
                Hp = 1,
                MaxHp = 1,
                Level = 1,
                Speed = 1,
            };
            e.HomeX = e.X;
            e.HomeY = e.Y;
            _entities[e.Id] = e;
            _entityOrder.Add(e.Id);
        }
    }

    /// <summary>Villager amble: deterministic wander leashed to home, walkable tiles only.</summary>
    private void TickNpc(Entity e)
    {
        var def = Data.Npcs[e.NpcDefId];
        if (def.Wander <= 0) return;

        if (Tick >= e.NextWanderTick)
        {
            e.NextWanderTick = Tick + _rng.Range(60, 200);
            e.WanderDirX = _rng.Range(-1, 1);
            e.WanderDirY = _rng.Range(-1, 1);
            long leash = def.Wander * SimConstants.UnitsPerTile;
            long dhx = e.X - e.HomeX, dhy = e.Y - e.HomeY;
            if (dhx * dhx + dhy * dhy > leash * leash / 2)
            {
                e.WanderDirX = Math.Sign(e.HomeX - e.X);
                e.WanderDirY = Math.Sign(e.HomeY - e.Y);
            }
        }
        if (Tick % 3 == 0) // unhurried village pace
        {
            long leash = def.Wander * SimConstants.UnitsPerTile;
            int nx = e.X + e.WanderDirX * e.MoveSpeed;
            int ny = e.Y + e.WanderDirY * e.MoveSpeed;
            long dhx = nx - e.HomeX, dhy = ny - e.HomeY;
            if (dhx * dhx + dhy * dhy <= leash * leash)
                MoveEntity(e, e.WanderDirX, e.WanderDirY, allowSafeZone: true);
        }
    }

    /// <summary>The NPC definition a player is close enough to talk to, or null.</summary>
    public NpcDef? NpcInRange(Entity player, int npcEntityId)
    {
        var npc = GetEntity(npcEntityId);
        if (npc is not { IsNpc: true } || npc.MapId != player.MapId) return null;
        long r = SimConstants.InteractRange;
        if (player.DistanceSquaredTo(npc) > r * r) return null;
        return Data.Npcs[npc.NpcDefId];
    }

    // ------------------------------------------------------------------ inventory

    public int CountItem(Entity e, string itemId)
        => e.Inventory.Where(s => s.ItemId == itemId).Sum(s => s.Count);

    /// <summary>Adds items, stacking first. Returns false if no room (nothing added).</summary>
    public bool AddItem(Entity e, string itemId, int count = 1)
    {
        var stack = e.Inventory.FirstOrDefault(s => s.ItemId == itemId && s.Count < SimConstants.MaxStack);
        if (stack != null)
        {
            int room = SimConstants.MaxStack - stack.Count;
            int add = Math.Min(room, count);
            stack.Count += add;
            count -= add;
        }
        while (count > 0)
        {
            if (e.Inventory.Count >= SimConstants.InventorySlots) return false;
            int add = Math.Min(SimConstants.MaxStack, count);
            e.Inventory.Add(new ItemStack { ItemId = itemId, Count = add });
            count -= add;
        }
        return true;
    }

    public bool RemoveItem(Entity e, string itemId, int count = 1)
    {
        if (CountItem(e, itemId) < count) return false;
        for (int i = e.Inventory.Count - 1; i >= 0 && count > 0; i--)
        {
            var stack = e.Inventory[i];
            if (stack.ItemId != itemId) continue;
            int take = Math.Min(stack.Count, count);
            stack.Count -= take;
            count -= take;
            if (stack.Count == 0) e.Inventory.RemoveAt(i);
        }
        return true;
    }

    private void RestoreM2State(Entity e, CharacterSave? save)
    {
        if (save is null) return;
        foreach (var stack in save.Inventory)
        {
            if (Data.Items.ContainsKey(stack.ItemId) && stack.Count > 0)
                e.Inventory.Add(new ItemStack { ItemId = stack.ItemId, Count = Math.Min(stack.Count, SimConstants.MaxStack) });
        }
        if (Data.Items.TryGetValue(save.WeaponId, out var w) && w.Kind == ItemKind.Weapon)
            e.WeaponId = save.WeaponId;
        if (Data.Items.TryGetValue(save.ArmorId, out var a) && a.Kind == ItemKind.Armor)
            e.ArmorId = save.ArmorId;
        foreach (var (questId, progress) in save.Quests)
        {
            if (Data.Quests.ContainsKey(questId))
                e.QuestLog[questId] = new QuestProgress { State = progress.State, Kills = progress.Kills };
        }
        foreach (string chestId in save.OpenedChests)
            e.OpenedChests.Add(chestId);
    }

    // ------------------------------------------------------------------ item ops (return user-facing result text)

    public string BuyItem(Entity player, int npcEntityId, string itemId)
    {
        var npc = NpcInRange(player, npcEntityId);
        if (npc is null) return "There is no one there to trade with.";
        if (!npc.Shop.Contains(itemId)) return "They don't stock that.";
        var item = Data.Items[itemId];
        if (player.Gil < item.Price) return $"Not enough gil ({item.Price} needed).";
        if (!AddItem(player, itemId)) return "Your pack is full.";
        player.Gil -= item.Price;
        return $"Bought {item.Name} for {item.Price} gil.";
    }

    public string SellItem(Entity player, int npcEntityId, string itemId)
    {
        var npc = NpcInRange(player, npcEntityId);
        if (npc is null) return "There is no one there to trade with.";
        if (npc.Shop.Count == 0) return "They aren't buying.";
        if (!Data.Items.TryGetValue(itemId, out var item)) return "No such item.";
        if (itemId == player.WeaponId || itemId == player.ArmorId)
            if (CountItem(player, itemId) == 0) return "Unequip it first.";
        if (!RemoveItem(player, itemId)) return "You don't have that.";
        int price = Math.Max(1, item.Price / 2);
        player.Gil += price;
        return $"Sold {item.Name} for {price} gil.";
    }

    public string UseItem(Entity player, string itemId)
    {
        if (!Data.Items.TryGetValue(itemId, out var item)) return "No such item.";
        if (item.Kind != ItemKind.Consumable) return "You can't drink that.";
        if (!player.Alive) return "The fallen cannot drink.";
        if (CountItem(player, itemId) == 0) return "You don't have that.";
        if (item.HealHp > 0 && player.Hp >= player.MaxHp && item.HealMp == 0) return "Already at full health.";
        RemoveItem(player, itemId);
        int hp = Math.Min(item.HealHp, player.MaxHp - player.Hp);
        int mp = Math.Min(item.HealMp, player.MaxMp - player.Mp);
        player.Hp += hp;
        player.Mp += mp;
        if (hp > 0) Events.Add(new SimEvent(SimEventKind.Heal, player.Id, player.Id, hp, itemId));
        return $"Used {item.Name}.";
    }

    public string EquipItem(Entity player, string itemId)
    {
        if (!Data.Items.TryGetValue(itemId, out var item)) return "No such item.";
        if (item.Kind is not (ItemKind.Weapon or ItemKind.Armor)) return "You can't equip that.";
        if (CountItem(player, itemId) == 0) return "You don't have that.";

        RemoveItem(player, itemId);
        string previous = item.Kind == ItemKind.Weapon ? player.WeaponId : player.ArmorId;
        if (previous.Length > 0 && !AddItem(player, previous))
        {
            AddItem(player, itemId); // roll back: no room to stow the old gear
            return "Your pack is too full to swap gear.";
        }
        if (item.Kind == ItemKind.Weapon) player.WeaponId = itemId;
        else player.ArmorId = itemId;
        RecomputeStats(player);
        return $"Equipped {item.Name}.";
    }

    public string UnequipItem(Entity player, string itemId)
    {
        bool isWeapon = player.WeaponId == itemId;
        bool isArmor = player.ArmorId == itemId;
        if (!isWeapon && !isArmor) return "That isn't equipped.";
        if (!AddItem(player, itemId)) return "Your pack is full.";
        if (isWeapon) player.WeaponId = "";
        else player.ArmorId = "";
        RecomputeStats(player);
        return $"Unequipped {Data.Items[itemId].Name}.";
    }

    // ------------------------------------------------------------------ quests

    /// <summary>Is this quest's objective fulfilled (ready to turn in)?</summary>
    public bool QuestObjectiveMet(Entity player, QuestDef quest, QuestProgress progress) => quest.Kind switch
    {
        QuestKind.Kill => progress.Kills >= quest.TargetCount,
        QuestKind.Explore => progress.Kills >= 1,   // set when the region is entered
        QuestKind.Talk => true,                      // delivering IS the objective
        QuestKind.Fetch => CountItem(player, quest.TargetItem) >= quest.TargetCount,
        _ => false,
    };

    /// <summary>Which NPC accepts the turn-in for this quest.</summary>
    public string QuestTurnInNpc(QuestDef quest)
        => quest.Kind == QuestKind.Talk ? quest.TalkNpc : quest.Giver;

    public string AcceptQuest(Entity player, int npcEntityId, string questId)
    {
        var npc = NpcInRange(player, npcEntityId);
        if (npc is null) return "There is no one there.";
        if (!npc.Quests.Contains(questId)) return "They have no such task.";
        var quest = Data.Quests[questId];
        if (player.QuestLog.ContainsKey(questId)) return "You already took that task.";
        if (quest.Prerequisite.Length > 0 &&
            (!player.QuestLog.TryGetValue(quest.Prerequisite, out var pre) || pre.State != QuestState.Completed))
            return $"First finish: {Data.Quests[quest.Prerequisite].Name}.";
        if (player.Level < quest.MinLevel) return $"You need level {quest.MinLevel} for that.";
        player.QuestLog[questId] = new QuestProgress { State = QuestState.Active, Kills = 0 };
        Events.Add(new SimEvent(SimEventKind.QuestProgress, player.Id, player.Id, 0, questId));
        return $"Quest accepted: {quest.Name}.";
    }

    /// <summary>Marks explore quests done when the player stands in their region.</summary>
    private void CheckExploreQuests(Entity player)
    {
        int tx = player.X / SimConstants.UnitsPerTile;
        int ty = player.Y / SimConstants.UnitsPerTile;
        foreach (var questId in player.QuestLog.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            var progress = player.QuestLog[questId];
            if (progress.State != QuestState.Active || progress.Kills > 0) continue;
            var quest = Data.Quests[questId];
            if (quest.Kind != QuestKind.Explore) continue;
            var region = Data.Map.Region(quest.TargetRegion);
            if (region != null && region.Rect.Contains(tx, ty))
            {
                progress.Kills = 1;
                Events.Add(new SimEvent(SimEventKind.QuestProgress, player.Id, player.Id, quest.TargetCount, questId));
            }
        }
    }

    public string CompleteQuest(Entity player, int npcEntityId, string questId)
    {
        var npc = NpcInRange(player, npcEntityId);
        if (npc is null) return "There is no one there.";
        if (!player.QuestLog.TryGetValue(questId, out var progress)) return "You never took that task.";
        var quest = Data.Quests[questId];
        if (progress.State == QuestState.Completed) return "That deed is already done.";
        if (npc.Id != QuestTurnInNpc(quest))
            return $"Take that to {Data.Npcs[QuestTurnInNpc(quest)].Name}.";
        if (!QuestObjectiveMet(player, quest, progress))
            return quest.Kind switch
            {
                QuestKind.Explore => "You haven't been there yet.",
                QuestKind.Fetch =>
                    $"Not enough: {CountItem(player, quest.TargetItem)}/{quest.TargetCount} {Data.Items[quest.TargetItem].Name}.",
                _ => $"Not done yet: {progress.Kills}/{quest.TargetCount} {Data.Monsters[quest.TargetMonster].Name}s.",
            };

        if (quest.Kind == QuestKind.Fetch)
            RemoveItem(player, quest.TargetItem, quest.TargetCount);

        progress.State = QuestState.Completed;
        player.Gil += quest.RewardGil;
        if (quest.RewardItem.Length > 0 && !AddItem(player, quest.RewardItem))
            player.Gil += Data.Items[quest.RewardItem].Price; // full pack: pay out in gil instead
        GrantQuestXp(player, quest.RewardXp);
        Events.Add(new SimEvent(SimEventKind.QuestProgress, player.Id, player.Id, -1, questId));
        return $"Quest complete: {quest.Name}!";
    }

    private void GrantQuestXp(Entity player, int xp)
    {
        player.Xp += xp;
        while (player.Level < SimConstants.MaxLevel && player.Xp >= XpToNextLevel(player.Level))
        {
            player.Xp -= XpToNextLevel(player.Level);
            player.Level++;
            RecomputeStats(player);
            player.Hp = player.MaxHp;
            player.Mp = player.MaxMp;
            Events.Add(new SimEvent(SimEventKind.LevelUp, player.Id, player.Id, player.Level));
        }
    }

    private void CountQuestKill(Entity player, string monsterId)
    {
        foreach (var questId in player.QuestLog.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            var progress = player.QuestLog[questId];
            var quest = Data.Quests[questId];
            if (progress.State != QuestState.Active || quest.TargetMonster != monsterId) continue;
            if (progress.Kills >= quest.TargetCount) continue;
            progress.Kills++;
            Events.Add(new SimEvent(SimEventKind.QuestProgress, player.Id, player.Id, progress.Kills, questId));
        }
    }

    // ------------------------------------------------------------------ parties

    private readonly Dictionary<int, List<int>> _parties = new();
    private readonly Dictionary<int, int> _pendingInvites = new(); // invitee entity id → inviter entity id
    private int _nextPartyId = 1;

    public IReadOnlyList<int> PartyMembers(int partyId)
        => _parties.TryGetValue(partyId, out var members) ? members : Array.Empty<int>();

    public string PartyInvite(Entity inviter, string inviteeName)
    {
        var invitee = EntitiesInOrder().FirstOrDefault(e =>
            e.IsPlayer && string.Equals(e.Name, inviteeName, StringComparison.OrdinalIgnoreCase));
        if (invitee is null) return $"No one named '{inviteeName}' is here.";
        if (invitee.Id == inviter.Id) return "You are already yourself.";
        if (invitee.PartyId >= 0) return $"{invitee.Name} is already in a party.";
        if (inviter.PartyId >= 0 && _parties[inviter.PartyId].Count >= 4) return "Your party is full (4).";
        _pendingInvites[invitee.Id] = inviter.Id;
        return $"Invited {invitee.Name} to your party.";
    }

    /// <summary>Returns (result text for accepter, inviter entity or null).</summary>
    public (string, Entity?) PartyAccept(Entity accepter)
    {
        if (!_pendingInvites.Remove(accepter.Id, out int inviterId))
            return ("No pending invite.", null);
        var inviter = GetEntity(inviterId);
        if (inviter is not { IsPlayer: true }) return ("The inviter is gone.", null);
        if (accepter.PartyId >= 0) return ("You are already in a party.", null);

        if (inviter.PartyId < 0)
        {
            inviter.PartyId = _nextPartyId++;
            _parties[inviter.PartyId] = new List<int> { inviter.Id };
        }
        var members = _parties[inviter.PartyId];
        if (members.Count >= 4) return ("That party is full.", null);
        members.Add(accepter.Id);
        accepter.PartyId = inviter.PartyId;
        return ($"You joined {inviter.Name}'s party.", inviter);
    }

    public string PartyLeave(Entity player)
    {
        if (player.PartyId < 0) return "You are not in a party.";
        var members = _parties[player.PartyId];
        members.Remove(player.Id);
        if (members.Count <= 1)
        {
            foreach (int lastId in members)
                if (GetEntity(lastId) is { } last) last.PartyId = -1;
            _parties.Remove(player.PartyId);
        }
        player.PartyId = -1;
        return "You left the party.";
    }

    /// <summary>Players credited for a monster kill: taggers plus their party members near the corpse, id-ordered.</summary>
    private IEnumerable<Entity> KillCreditSet(Entity corpse)
    {
        var credited = new SortedSet<int>();
        foreach (int taggerId in corpse.Taggers)
        {
            if (GetEntity(taggerId) is not { IsPlayer: true } tagger) continue;
            credited.Add(tagger.Id);
            if (tagger.PartyId < 0) continue;
            foreach (int memberId in _parties[tagger.PartyId])
            {
                if (GetEntity(memberId) is not { IsPlayer: true, Alive: true } member) continue;
                if (member.MapId != corpse.MapId) continue;
                long r = SimConstants.PartyShareRange;
                if (member.DistanceSquaredTo(corpse) <= r * r)
                    credited.Add(member.Id);
            }
        }
        foreach (int id in credited)
            yield return _entities[id];
    }

    // ------------------------------------------------------------------ loot

    private void RollDrops(Entity player, MonsterDef def)
    {
        foreach (var drop in def.Drops)
        {
            if (_rng.Next(100) >= drop.ChancePercent) continue;
            if (AddItem(player, drop.Item))
                Events.Add(new SimEvent(SimEventKind.Loot, player.Id, player.Id, 1, drop.Item));
        }
    }

    // ------------------------------------------------------------------ world clock (presentation only)

    /// <summary>Fraction of the in-game day elapsed, 0..999 (0 = dawn).</summary>
    public int TimeOfDayPermille => (int)((long)(Tick % SimConstants.TicksPerDay) * 1000 / SimConstants.TicksPerDay);
}
