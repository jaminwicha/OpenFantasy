# Explicit target selection review

This is a local code and rendered review by the implementation lead. No independent critic or
external model was invoked, and no human usability or style acceptance is claimed.

| Concern | Resolution and evidence |
|---|---|
| Duplicate display names or changing distance retarget an action. | Selection orders public candidates by kind then entity ID and retains that ID. Two identically named trees and actual movement are covered; a previously captured offer executes against its original tree even after cycling. |
| Choosing a plant silently talks to or gifts Wren. | Incompatible person actions carry the selected name/ID and no intent. Native E on a plant submits nothing; choosing Wren blocks Gather. |
| Depletion or disappearance chooses a substitute or exposes hidden position. | Retain only the known name/ID while absent; remove the position/marker and executable gather intent. Actual depletion, absence and regrowth are covered without inventory issuance or nearest-tree substitution. |
| Multiple input events in a frame separate the label from the executable offer. | A successful selection refreshes selection, field marker, HUD and captured offers together from the same view before returning. Selection itself never submits a command. |
| Target input escapes pause/dialogue/focus or duplicates a held press. | Both semantic actions use the existing input router. Pure context tests and injected native keyboard/shoulder events cover the bounded farm contexts; a pending action refuses selection changes. |
| Client state leaks into another controlled identity. | Scope includes world, branch and controlled actor; refresh clears on scope change. Actor-change regression and new-session construction are retained. Persistent preferences and full branch-switch UI remain future qualification. |
| The new two-tree test reused a one-tree fixture helper. | The initial focused run had five passes and one fixture `Single()` error. Replace that test's swing helper with an explicit command naming the intended node and revisions. All six facts and the full 838-test run pass; no production assertion was weakened. |
| A screenshot read the preceding render despite updated C# state. | Wait for the rendered frame after selecting Wren, then capture. Add a nearby plant image. The final retained images show the actual selected label/marker and retain all action quantity assertions. |

At 960×540 the upper-right target bar, action costs and focused out-of-range reason fit. The
footer uses substantial world space and can cover a selected object's footprint; off-screen
targets also lack directional guidance. The status line retains the last action message even
after a current offer becomes usable; the broader feedback pass should distinguish past results
from present blockers. Keep these limits in the broader UI/layout acceptance work, which remains
open. The new semantic marker is a client overlay, not approved production
art or a collision change. Native injected events do not establish physical-device behavior.

The full verifier retains strict host revision rejection and all existing source/custody/save
checks. No automatic retry, substitute ingredient, new authority, IPC or schema migration was
introduced. Its hashes bind all original source inputs and identify the two generated UIDs.
The naming scan passes. The frozen specification and prior qualification folders remain intact.
