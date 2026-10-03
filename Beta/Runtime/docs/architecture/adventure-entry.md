# Adventure admission and entry

[AdventureWorldAdmission](../../src/OpenFantasy.Hosting/Content/AdventureWorldAdmission.cs) composes
an unplayed outcome-capable cargo world with the existing controller, assisted recovery, care
disposition, control-permission and effective-control interpretations. Its output uses profile40;
it adds no snapshot codec or independent authority. [Fresh roster admission](player-rosters.md)
supplies one to four ordered human characters. The source NPCs retain their controller kinds,
identities, inventories, positions, progression, quests and construction responsibilities.

Wren remains an autonomous builder. The original project starts unworked, its materials unfunded,
and the player's purse retains its original balance. No qualification-specific wounds, converted
NPCs, completed jobs or granted control assignments enter the adventure. The stable recovery
definition reuses the admitted remedy and forms; its availability grants neither medicine nor a
new treatment permission. Defining a policy, obtaining consent, granting control and performing
an action remain separate operations.

## Local ownership and save selection

[Bootstrap](../../client/scripts/Bootstrap.cs) selects the adventure for ordinary launch or
`--adventure [SAVE]`. The default path is `user://worlds/openfantasy-adventure.sqlite`, using the
admission's declared development seed and default player name. An explicit path changes only the
storage location. It cannot change a saved world's seed, name, roster or meaning.

[FarmClientSession.OpenAdventureAsync](../../client/scripts/FarmClientSession.Adventure.cs) creates
that exact fresh solo world when absent. For an existing database it authenticates the original
canonical genesis and the saved selection. A different admission, seed or cooperative roster is
refused. Failure never falls back to creating a replacement world. Old saves retain their explicit
entry points; `--farm` selects the original construction farm and its preserved upgrade chain.

Local play owns one in-process `LocalWorldSession` through the same client session used by the
regional cargo presentation. Trading configuration, actor-bound terrain observation, retreat,
outcomes and history use the existing owners. No network listener or IPC bridge is needed for local
play. The client renders participant views and submits captured intents; it never writes world
state or derives authority from a screen.

## Shared ownership and entry

The offline Host command `--create-adventure <new-world.sqlite> <seed> <name> [name2] [name3] [name4]`
creates a declared one-to-four-player admission. Invalid rosters and existing destinations are
refused. Creating this world does not launch a listener, distribute invitations or open players'
connections. It does not convert a played solo world to multiplayer.

[CargoPlayerHost](../../src/OpenFantasy.Hosting/Networking/CargoPlayerHost.cs) opens the saved world
under its exclusive authority. The ordinary [TLS host](player-http-host.md) issues protected
invitations for its admitted human characters. `tools/play.py --join INVITATION` selects the native
remote entry; normal certificate validation and authenticated admission remain in force. The
player-session boundary is shared with local presentation. Connection, readiness, controller
return and deliberate Resume preserve their existing distinct meanings.

The [human-construction scenario](../../src/OpenFantasy.Hosting/Content/HumanConstructionWorldAdmission.cs)
has a different admission: it intentionally makes Wren human-controlled. It is never chosen as the
ordinary adventure or silently substituted for its population. Its saved identity remains intact.

## Presentation and validation boundaries

Regional terrain, merchant appearances, field actions, recipes, inventory, care, history and encounter
outcomes use the existing presentation components. Default entry does not imply complete onboarding,
procedural frontier generation, unrestricted content, a polished visual kit or a full campaign.

The disposable adventure onboarding probe uses the ordinary adventure opener at an explicit temporary
save. It injects native input for gathering, crafting, quest funding and excavation, observes Wren's
autonomous construction, and continues to the first merchant. A separate invocation opens the same
save paused and verifies recorded history. Background rendering is confined to the explicit probe;
normal play retains window-focus handling. Runtime qualification results belong in the implementation
records, not in the admission contract.
