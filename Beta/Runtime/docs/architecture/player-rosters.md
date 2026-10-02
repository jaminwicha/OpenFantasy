# Fresh player rosters

## Composition and ownership

[PlayerRosterAdmission](../../src/OpenFantasy.Hosting/Content/PlayerRoster/PlayerRosterAdmission.cs)
creates an unplayed outcome-completion cargo world with one to four ordered human names.
[PlayerRosterDefinition](../../src/OpenFantasy.Hosting/Content/PlayerRoster/PlayerRosterDefinition.cs)
copies and validates those names and pins their order, count and provisioning interpretation. The
one-name route uses the original solo admission without changing its bytes. Roster selection creates
a new world; it does not change a lived save, recruit an NPC or implement controller handoff.

[PlayerRosterGenesisExtension](../../src/OpenFantasy.Hosting/Content/PlayerRoster/PlayerRosterGenesisExtension.cs)
extends the fresh ordinary-care cargo/chief composition before paid exchange, markets, retreat and
outcome admission. Each additional human has a deterministic actor, physical body, form, grounded
presence, resident record, language comprehension, inventory custodian, access policy, principal and
session, gathering effort, progression, currency account and readiness debt. The initial class, body
and comprehension come from the admitted original player. New actors start at full class HP/MP,
zero experience and zero wallet balance. They are ordinary world actors under the same rules.

Spawn selection searches square rings one through six around the first player's actual feet in a
fixed order. It rejects blocked or unsupported routes and reserved construction/gathering locations,
checks the complete occupancy after each insertion, and requires a grounded exit. Any actor with
a previously reachable cardinal exit must keep at least one of those exits after insertion. Complete composition
revalidates every existing construction access requirement from its explicit start/end anchors and adds access obligations for the new
actors. Failure refuses the fresh composition; it never moves an existing actor or clears terrain.

The admission hash binds the original basis, roster definition, inventory, progression, gathering,
construction, encounter and care hashes, plus the new identities, bodies and exact spawn coordinates.
Deterministic IDs are checked against existing identities and every new role. Water ownership is
rebound to the exact extended inventory and physical scope before complete-world admission.

## Finite provisions and shared world content

Each added player receives exactly one axe and one sickle. Each unit has its own deterministic admitted
source, source interval and lot in that player's storage. Total admitted capacity increases by two
units per additional player. These are declared genesis provisions, not copies of another actor's
source rights. The tools use the retained source definitions and the same gathering rules.

Every existing source, lot, owner and currency balance remains intact. The roster does not multiply
wood, food, merchant stock, funded quest rewards or medicine. Wren, merchants, wolves and the chief
retain their NPC control and existing bodies. Added care agreements and ordinary-care permissions
authorize the same helper/remedies; treatment still consumes the helper's original finite supply.
Existing quest definitions and funded reserves remain shared world content, with their normal
eligibility and reservation rules.

## Cooperative encounter membership

[Catalog composition](../../src/OpenFantasy.Hosting/Content/PlayerRoster/PlayerRosterGenesisExtension.Catalogs.cs)
retains every original opportunity and adds an equivalent initiating/target endpoint for each new
human. All roster members occupy the original player's positive side, with that player's applicable
ability grants. Every scenario retains the exact complete resident/debt actor set. Four humans yield
twelve scenarios: two wolf entries and one chief target per human. Each human owes its own required
decision; connection absence or another player's choice does not waive Wait. Entry retains the normal
requirement that every positive-side participant be in the initiating actor's actual reach and sightline.
Players must physically gather before entering; roster membership grants no teleport or range waiver.

Multi-human admission selects [lifecycle catalog version two](encounter-lifecycle.md), with a sixteen
scenario count bound and the unchanged 512 KiB archive bound. The combined encoded scenario rows
must also fit 320 KiB, reserving the complete action/debt envelope. This is a fixed authored cooperative
starting roster. General recruitment, proximity-based participation and independent encounter pockets
have separate rules; scenario membership does not implement them implicitly.

## Host and presentation binding

`--create-cargo-roster <database> <seed> <name> [name2] [name3] [name4]` creates a separate saved world
through the exclusive create-only authority. [The dedicated host](../player-host.md) then discovers
the actual saved humans and issues one scoped invitation per actor. Ports cannot create or reassign
them. Closing a connection retains its actor and possessions; ordinary world and encounter rules
continue to govern that actor.

[NativeCargoWorld](../../src/OpenFantasy.Hosting/Content/NativeCargo/NativeCargoWorld.cs) resolves the
original authored scenario initiator as the corridor anchor instead of assuming there
is only one human. Its explicit-player corridor overload validates the requested saved human. Saved
NPC itineraries and host scheduling remain single-owned. Native presentation uses each authenticated
player's own inventory, progression, offers and observation scope.
