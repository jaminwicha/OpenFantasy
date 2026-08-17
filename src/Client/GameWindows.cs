using Godot;
using OpenFantasy.Net;
using OpenFantasy.Sim;

namespace OpenFantasy.Client;

/// <summary>
/// Toggleable game windows in the classic style: inventory (I), quest log (J),
/// and the NPC dialog/shop window (E near an NPC). One window open at a time.
/// </summary>
public partial class GameWindows : CanvasLayer
{
    private ClientNode _client = null!;
    private GameData _data = null!;

    private NinePatchRect? _openWindow;
    private enum WindowKind { None, Inventory, Quests, Npc, Craft, Character }
    private WindowKind _open = WindowKind.None;

    public bool AnyOpen => _open != WindowKind.None;

    public void Setup(ClientNode client, GameData data)
    {
        _client = client;
        _data = data;
        _client.PlayerStateChanged += RefreshIfOpen;
        _client.NpcInfoReceived += () => Open(WindowKind.Npc);
    }

    public void ToggleInventory() => Toggle(WindowKind.Inventory);
    public void ToggleQuests() => Toggle(WindowKind.Quests);
    public void ToggleCraft() => Toggle(WindowKind.Craft);
    public void ToggleCharacter() => Toggle(WindowKind.Character);
    public void CloseAll() => Close();

    private void Toggle(WindowKind kind)
    {
        if (_open == kind) Close();
        else Open(kind);
    }

    private void Close()
    {
        _openWindow?.QueueFree();
        _openWindow = null;
        _open = WindowKind.None;
    }

    private void RefreshIfOpen()
    {
        if (_open != WindowKind.None) Open(_open);
    }

    private void Open(WindowKind kind)
    {
        Close();
        _open = kind;
        _openWindow = kind switch
        {
            WindowKind.Inventory => BuildInventory(),
            WindowKind.Quests => BuildQuests(),
            WindowKind.Npc => BuildNpc(),
            WindowKind.Craft => BuildCraft(),
            WindowKind.Character => BuildCharacter(),
            _ => null,
        };
        if (_openWindow == null)
        {
            _open = WindowKind.None;
            return;
        }
        AddChild(_openWindow);
    }

    // ------------------------------------------------------------------ shared

    private NinePatchRect NewWindow(string title, Vector2 size)
    {
        var panel = UiTheme.MakePanel();
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        panel.Position = -size / 2;
        panel.Size = size;

        var titleLabel = UiTheme.MakeLabel(title, 15);
        titleLabel.Position = new Vector2(14, 8);
        panel.AddChild(titleLabel);

        var closeHint = UiTheme.MakeLabel("[Esc]", 10);
        closeHint.Position = new Vector2(size.X - 44, 10);
        closeHint.Modulate = new Color(0.6f, 0.65f, 0.8f);
        panel.AddChild(closeHint);
        return panel;
    }

    private ScrollContainer NewScroll(NinePatchRect panel, float top)
    {
        var scroll = new ScrollContainer
        {
            Position = new Vector2(12, top),
            Size = new Vector2(panel.Size.X - 24, panel.Size.Y - top - 12),
        };
        panel.AddChild(scroll);
        return scroll;
    }

    private static Button SmallButton(string text)
        => new() { Text = text, CustomMinimumSize = new Vector2(0, 24) };

    // ------------------------------------------------------------------ character

    private NinePatchRect BuildCharacter()
    {
        var state = _client.PlayerState;
        var panel = NewWindow("Character", new Vector2(340, 380));
        _client.Entities.TryGetValue(_client.LocalEntityId, out var me);

        var rows = new VBoxContainer { Position = new Vector2(18, 36), CustomMinimumSize = new Vector2(300, 0) };
        rows.AddThemeConstantOverride("separation", 6);
        panel.AddChild(rows);

        void Line(string label, string value, Color? tint = null)
        {
            var h = new HBoxContainer();
            var l = UiTheme.MakeLabel(label, 12);
            l.CustomMinimumSize = new Vector2(150, 0);
            l.Modulate = new Color(0.72f, 0.78f, 0.94f);
            var v = UiTheme.MakeLabel(value, 12);
            if (tint is { } t) v.Modulate = t;
            h.AddChild(l);
            h.AddChild(v);
            rows.AddChild(h);
        }

        if (me != null)
        {
            string race = me.HasAppearance && me.Appearance.Race < World.RaceMods.Length
                ? World.RaceMods[me.Appearance.Race].Name : "—";
            string cls = _data.Classes.TryGetValue(me.ArchetypeId, out var c) ? c.Name : me.ArchetypeId;
            Line("Name", me.Name, new Color("f8e050"));
            Line("Class · Race", $"{cls} · {race}");
            Line("Level", $"{me.Level}");
            Line("Experience", $"{me.Xp} / {state.XpToNext}");
            Line("HP", $"{me.Hp} / {me.MaxHp}", new Color("70e878"));
            Line("MP", $"{me.Mp} / {me.MaxMp}", new Color("78a8f0"));
            Line("Gil", $"{me.Gil}", new Color("f0d060"));
            rows.AddChild(new HSeparator());
        }
        Line("Attack", $"{state.Attack}");
        Line("Magic", $"{state.Magic}");
        Line("Defense", $"{state.Defense}");
        Line("Speed", $"{state.Speed}");
        rows.AddChild(new HSeparator());
        Line("Weapon", state.WeaponId.Length > 0 ? _data.Items[state.WeaponId].Name : "—");
        Line("Armor", state.ArmorId.Length > 0 ? _data.Items[state.ArmorId].Name : "—");
        return panel;
    }

    // ------------------------------------------------------------------ inventory

    private NinePatchRect BuildInventory()
    {
        var state = _client.PlayerState;
        var panel = NewWindow("Inventory", new Vector2(380, 360));

        string equipLine = "Equipped: " +
            (state.WeaponId.Length > 0 ? _data.Items[state.WeaponId].Name : "no weapon") + " · " +
            (state.ArmorId.Length > 0 ? _data.Items[state.ArmorId].Name : "no armor");
        var equip = UiTheme.MakeLabel(equipLine, 11);
        equip.Position = new Vector2(14, 30);
        equip.Modulate = new Color(0.75f, 0.8f, 0.95f);
        panel.AddChild(equip);

        var scroll = NewScroll(panel, 52);
        var list = new VBoxContainer { CustomMinimumSize = new Vector2(340, 0) };
        list.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(list);

        if (state.Inventory.Count == 0)
            list.AddChild(UiTheme.MakeLabel("Your pack is empty.", 12));

        foreach (var (itemId, count) in state.Inventory)
        {
            var item = _data.Items[itemId];
            var row = new HBoxContainer();
            var name = UiTheme.MakeLabel($"{item.Name} ×{count}", 12);
            name.CustomMinimumSize = new Vector2(190, 0);
            row.AddChild(name);

            if (item.Kind == ItemKind.Consumable)
            {
                var use = SmallButton("Use");
                string id = itemId;
                use.Pressed += () => _client.SendItemOp(ItemOpKind.Use, id);
                row.AddChild(use);
            }
            else if (item.Kind is ItemKind.Weapon or ItemKind.Armor)
            {
                var equipBtn = SmallButton("Equip");
                string id = itemId;
                equipBtn.Pressed += () => _client.SendItemOp(ItemOpKind.Equip, id);
                row.AddChild(equipBtn);
            }
            list.AddChild(row);
        }

        foreach (string equipped in new[] { state.WeaponId, state.ArmorId })
        {
            if (equipped.Length == 0) continue;
            var row = new HBoxContainer();
            var name = UiTheme.MakeLabel($"{_data.Items[equipped].Name} (equipped)", 12);
            name.Modulate = new Color("a0e0ff");
            name.CustomMinimumSize = new Vector2(190, 0);
            row.AddChild(name);
            var unequip = SmallButton("Unequip");
            string id = equipped;
            unequip.Pressed += () => _client.SendItemOp(ItemOpKind.Unequip, id);
            row.AddChild(unequip);
            list.AddChild(row);
        }
        return panel;
    }

    // ------------------------------------------------------------------ quests

    private NinePatchRect BuildQuests()
    {
        var state = _client.PlayerState;
        var panel = NewWindow("Quest Log", new Vector2(400, 320));
        var scroll = NewScroll(panel, 34);
        var list = new VBoxContainer { CustomMinimumSize = new Vector2(360, 0) };
        list.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(list);

        if (state.Quests.Count == 0)
            list.AddChild(UiTheme.MakeLabel("No quests yet. Talk to the folk of the four towns.", 12));

        foreach (bool mainSection in new[] { true, false })
        {
            var entries = state.Quests
                .Where(q => _data.Quests[q.questId].Main == mainSection)
                .ToList();
            if (entries.Count == 0) continue;

            var header = UiTheme.MakeLabel(mainSection ? "— Main —" : "— Side —", 12);
            header.Modulate = UiTheme.AtbColor;
            list.AddChild(header);

            foreach (var (questId, questState, kills) in entries)
            {
                var quest = _data.Quests[questId];
                bool done = (QuestState)questState == QuestState.Completed;
                string turnInName = _data.Npcs[quest.Kind == QuestKind.Talk ? quest.TalkNpc : quest.Giver].Name;
                string progress = done ? "Complete" : quest.Kind switch
                {
                    QuestKind.Kill =>
                        $"{Math.Min(kills, quest.TargetCount)}/{quest.TargetCount} {_data.Monsters[quest.TargetMonster].Name}s" +
                        (kills >= quest.TargetCount ? $" — return to {turnInName}" : ""),
                    QuestKind.Explore => kills > 0
                        ? $"Seen — report to {turnInName}"
                        : $"Reach: {quest.TargetRegion.Replace('_', ' ')}",
                    QuestKind.Talk => $"Deliver to {turnInName}",
                    QuestKind.Fetch => $"Bring {quest.TargetCount}× {_data.Items[quest.TargetItem].Name} to {turnInName}",
                    _ => "…",
                };
                var title = UiTheme.MakeLabel((quest.Main ? "★ " : "") + quest.Name, 13);
                title.Modulate = done ? new Color(0.6f, 0.65f, 0.7f) : UiTheme.AtbReadyColor;
                list.AddChild(title);
                var detail = UiTheme.MakeLabel(progress, 11);
                detail.Modulate = new Color(0.75f, 0.8f, 0.95f);
                list.AddChild(detail);
            }
        }
        return panel;
    }

    // ------------------------------------------------------------------ crafting

    private NinePatchRect BuildCraft()
    {
        var state = _client.PlayerState;
        var panel = NewWindow("Crafting", new Vector2(440, 400));

        var hint = UiTheme.MakeLabel("Forge recipes need you standing at the town forge.", 10);
        hint.Position = new Vector2(14, 28);
        hint.Modulate = new Color(0.7f, 0.75f, 0.9f);
        panel.AddChild(hint);

        var scroll = NewScroll(panel, 48);
        var list = new VBoxContainer { CustomMinimumSize = new Vector2(400, 0) };
        list.AddThemeConstantOverride("separation", 5);
        scroll.AddChild(list);

        int CountOf(string itemId) => state.Inventory.Where(s => s.itemId == itemId).Sum(s => s.count);

        foreach (var recipe in _data.Recipes.Values.OrderBy(r => r.Station).ThenBy(r => r.Id, StringComparer.Ordinal))
        {
            var output = _data.Items[recipe.Output];
            bool affordable = recipe.Inputs.All(i => CountOf(i.Item) >= i.Count);

            var row = new HBoxContainer();
            string title = recipe.OutputCount > 1 ? $"{output.Name} ×{recipe.OutputCount}" : output.Name;
            if (recipe.Station == CraftStation.Forge) title += "  ⚒";
            var label = UiTheme.MakeLabel(title, 12);
            label.CustomMinimumSize = new Vector2(200, 0);
            label.Modulate = affordable ? UiTheme.TextColor : new Color(0.55f, 0.55f, 0.62f);
            row.AddChild(label);

            var craft = SmallButton("Craft");
            craft.Disabled = !affordable;
            string id = recipe.Id;
            craft.Pressed += () => _client.SendCraft(id);
            row.AddChild(craft);
            list.AddChild(row);

            string inputs = string.Join("  ", recipe.Inputs.Select(i =>
                $"{_data.Items[i.Item].Name} {CountOf(i.Item)}/{i.Count}"));
            var detail = UiTheme.MakeLabel(inputs, 10);
            detail.Modulate = new Color(0.68f, 0.72f, 0.88f);
            list.AddChild(detail);
        }
        return panel;
    }

    // ------------------------------------------------------------------ npc dialog / shop

    private NinePatchRect? BuildNpc()
    {
        var info = _client.NpcInfo;
        if (info == null) return null;
        var state = _client.PlayerState;

        var panel = NewWindow(info.Name, new Vector2(460, 420));

        var dialog = UiTheme.MakeLabel(info.Dialog, 12);
        dialog.Position = new Vector2(14, 30);
        dialog.Size = new Vector2(430, 40);
        dialog.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        dialog.Modulate = new Color(0.85f, 0.88f, 1f);
        panel.AddChild(dialog);

        var scroll = NewScroll(panel, 76);
        var list = new VBoxContainer { CustomMinimumSize = new Vector2(420, 0) };
        list.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(list);

        foreach (var (questId, name, text, status) in info.Quests)
        {
            var row = new HBoxContainer();
            var label = UiTheme.MakeLabel($"◆ {name}", 12);
            label.CustomMinimumSize = new Vector2(150, 0);
            label.Modulate = status switch
            {
                NpcQuestStatus.Offerable => UiTheme.AtbReadyColor,
                NpcQuestStatus.ReadyToTurnIn => new Color("70e878"),
                NpcQuestStatus.Done => new Color(0.55f, 0.6f, 0.65f),
                NpcQuestStatus.Locked => new Color(0.55f, 0.5f, 0.5f),
                _ => UiTheme.TextColor,
            };
            row.AddChild(label);

            if (status == NpcQuestStatus.Offerable)
            {
                var accept = SmallButton("Accept");
                string id = questId;
                accept.Pressed += () => _client.SendQuestOp(QuestOpKind.Accept, id, info.NpcEntityId);
                row.AddChild(accept);
            }
            else if (status == NpcQuestStatus.ReadyToTurnIn)
            {
                var complete = SmallButton("Complete");
                string id = questId;
                complete.Pressed += () => _client.SendQuestOp(QuestOpKind.Complete, id, info.NpcEntityId);
                row.AddChild(complete);
            }
            list.AddChild(row);

            var detail = UiTheme.MakeLabel(text, 10);
            detail.Modulate = new Color(0.7f, 0.75f, 0.9f);
            detail.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            detail.CustomMinimumSize = new Vector2(410, 0);
            list.AddChild(detail);
        }

        if (info.ShopItems.Count > 0)
        {
            var shopTitle = UiTheme.MakeLabel("— Wares —", 12);
            shopTitle.Modulate = UiTheme.AtbColor;
            list.AddChild(shopTitle);
            foreach (string itemId in info.ShopItems)
            {
                var item = _data.Items[itemId];
                var row = new HBoxContainer();
                var label = UiTheme.MakeLabel($"{item.Name}  ({item.Price} gil)", 12);
                label.CustomMinimumSize = new Vector2(230, 0);
                row.AddChild(label);
                var buy = SmallButton("Buy");
                string id = itemId;
                buy.Pressed += () => _client.SendItemOp(ItemOpKind.Buy, id, info.NpcEntityId);
                row.AddChild(buy);
                list.AddChild(row);
            }

            var sellable = state.Inventory;
            if (sellable.Count > 0)
            {
                var sellTitle = UiTheme.MakeLabel("— Your pack (sell for half) —", 12);
                sellTitle.Modulate = UiTheme.AtbColor;
                list.AddChild(sellTitle);
                foreach (var (itemId, count) in sellable)
                {
                    var item = _data.Items[itemId];
                    var row = new HBoxContainer();
                    var label = UiTheme.MakeLabel($"{item.Name} ×{count}  ({Math.Max(1, item.Price / 2)} gil)", 12);
                    label.CustomMinimumSize = new Vector2(230, 0);
                    row.AddChild(label);
                    var sell = SmallButton("Sell");
                    string id = itemId;
                    sell.Pressed += () => _client.SendItemOp(ItemOpKind.Sell, id, info.NpcEntityId);
                    row.AddChild(sell);
                    list.AddChild(row);
                }
            }
        }
        return panel;
    }
}
