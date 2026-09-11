# First farm feedback session

This is a small playable OpenFantasy loop on the qualified macOS development host. It uses the real portable C# authority and retained saves. The full game, P00/P01/P02 exits and all 181 requirements remain separate work.

## Start and continue

Double-click [Play OpenFantasy.command](../../../Runtime/Play%20OpenFantasy.command), or run `python3 tools/play.py` from the runtime directory. The launcher checks the pinned .NET/Godot tools, builds and imports the client, then opens the farm. This is a development launch route, not an exported Windows/macOS/Linux release. See [runtime setup](../../../Runtime/README.md) for explicit tool paths on another machine.

The client selects `user://worlds/wrens-farm-construction.sqlite`. Closing and reopening retains this world. When first continuing an older quest, production or gathering save, it creates verified copies through the preserved upgrade chain; each older file remains intact. Incomplete or incompatible saves stop opening with an error rather than starting over. A genuinely new game begins with eight wood and the admitted gathering tools.

## Try the loop

1. Move toward Wren and press **E** to talk. Read his orchard offer. **G accepts**. Close with **E** if you want to explore before delivery, or press **G** again to deliver immediately. The Continue/E control closes the conversation; it does not accept the offer. Pressing G outside a conversation asks you to speak first and keeps the wood in your pack.
2. Gathering and ration crafting are optional. **Close any conversation with E first.** Approach the berry plant and use **F** twice, then **C** packs two berries and one wood into two rations plus an offcut. You can do this before delivery and still have six wood for Wren, or use the spare wood afterward. Gathering can briefly wait for effort to recover; there is no need to repeatedly mash the key.
3. If you have not delivered yet, return to Wren, talk with **E** and deliver with **G**. Delivery awards **420 XP and 200 gil** once. The admitted Human Squire reaches level three with 38 residual XP. Close the conversation with **E** so Wren can walk to the plot and work. Keep his route and the small crop plot clear.
4. Watch the fitted percentage become **Seedling guard built**, then walk around the small installed board. Talk to Wren during work to hold the world; close the conversation to continue. **Esc** closes dialogue first and pauses in ordinary play; the visible Pause button can preserve an open conversation. No offline time finishes the project.
5. Close the window while work is unfinished or after it completes, then launch again. The same work, inventory, reward and installed guard should remain. Completed delivery and work cannot repeat their reward or material transformation.

| Action | Keyboard | Controller |
|---|---|---|
| Move | WASD / arrows | Left stick |
| Talk / close conversation | E | A |
| Accept / deliver addressed task | G | X |
| Gather nearby resource | F | Y |
| Pack rations in ordinary play | C | B |
| Pause / resume in ordinary play | Esc | Start |
| Close dialogue | Esc | Start / B |

Current onscreen hints are keyboard-oriented. Losing window focus also stops new simulation steps. Release held movement controls before resuming movement after a blocking context. Controller B also resumes an existing pause. The visible Pause button can pause with dialogue open. Use the ordinary close button so the local session finishes shutdown.

## Feedback focus and limits

Useful feedback is whether the next action is discoverable, whether the reward and Wren’s work feel connected to the delivery, whether pause/talk/reopen behave as expected, and whether the opening view feels clear. Scripted virtual playtesting cannot establish unaided human discovery or enjoyment.

The art is deliberately the retained original-source baseline. The tiny 13×8 board swatch represents a small 270 g sapling guard; it does not protect a whole orchard. Dedicated joinery animation, sound, richer personalities, final art, food consumption, combat, recruitment, broader construction, frontier streaming and multiplayer remain unfinished. The current grass repetition and minimal work choreography are known presentation limits. This feedback session evaluates the connected loop without treating those limits as the final target.
