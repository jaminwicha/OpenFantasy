# Shared History interface implementation

`FarmGame.History` binds the engine-free history model to one scene-owned `FarmHistoryPanel`.
The model captures the actual optional application facade; the UI never creates a local selection,
substitutes a full controlled view or borrows the home actor's inventory/trade permission.

History is reachable from the HUD's icon button, the quick wheel's separate History navigation
button, H and controller left-stick click. It has its own input layer, independent of Pack and Trade.
Unsupported and denied states remain readable through the model rather than manufacturing an empty
successful directory. Browsing remains possible while locally paused and while a world action is
pending, provided another modal does not own input. A history visit neutralizes player movement
without changing the existing host clock/readiness policy. Closing returns focus to the prior visible
control and retains the router's neutral-release requirement before walking resumes.

The panel retains one directory page and one selected historical detail. Rows contain the kind,
terminal status, permitted counterparty name and a short subject suffix that is unique on the page;
the tooltip includes the complete subject reference. A row click selects only. Inspect explicitly
reads the corresponding typed saved quote or material proposal. Details show the recorded parties,
material and quantity, plus quote price/fees/settlement when applicable; no acceptance, withdrawal,
purchase or other world-action control exists in this screen. Names come only from the model's
permitted public descriptions, with an unidentified-material fallback.

Next uses the exact returned cursor. One Latest button starts a new first-page request and explains
that it returns to the newest records. There is no redundant Start operation or page cache. No frame
update starts retrieval: the game updates the model only to observe current scope and completed tasks.
Navigation buttons capture the displayed opening/page and selected identity on button-down; rows
capture their opening/page at creation. A release or queued callback cannot reinterpret an older
choice against a replacement page.

The `.tscn` owns anchors, margins, typography, columns, scrolling and navigation button layout.
Existing market-offer/gift icons are reused; no production art claim is made. Keyboard/controller
focus follows an explicit chain through rows, read-only details, available navigation and Close.
The detail scroll accepts arrow/D-pad up/down while focused; Tab or left/right moves to another
control. Close uses Escape/controller B. Icon controls retain descriptive hover/focus tooltips.

Close, focus loss, controller disconnect, character selection, control return and failure clear the
visible private rows and terms. The model continues to own admitted work after the screen closes;
ordinary completion observation drains it without repopulating another visit. Session shutdown
joins `DrainAsync` alongside the existing market work and application disposal.

## Source ownership and pending verification

New UI sources are `FarmGame.History.cs`, `FarmHistoryPanel.cs` and `FarmHistoryPanel.tscn`.
Existing edits are confined to the game lifecycle/selection/control-return integration, semantic
input/default bindings, HUD scene/presenter and quick-wheel scene/presenter. Four new `HistoryInputTests`
cover independent paused/pending access, held-movement release, native accept-release ownership and
wheel navigation without inventory/trade actions. Existing tests are unchanged.

The boundary worker owns the model/facades; the native worker owns the genuine UI probe/journey.
The passive hooks are `FarmGame.IsHistoryOpen`, `FarmGame.History`, scene unique names and row metadata
`history_kind`/`history_id`. They observe the same UI/model used by ordinary interaction and provide
no separate mutation path.

No build, test or native render was run by this UI worker. `git diff --check` passed at handoff; the
integration lead owns serial source-pinned qualification and visual inspection. Bounds, visual
readability, physical input, human comfort, production art and broader history/phase gates are not
closed by these source changes or the managed input tests alone. No staging or commit was performed.

## Native detail overflow correction

The retained `native-history-20261003T140851165567Z` journey reached the actual history-only
controller detail and rejected its clipped content. Inspection of
`native/shared-history/5795dc4890da7573cfc5b3948d0296df-first/0068-history-history-only-delegated-detail.png`
showed that the gift's status, parties and quantity fit, while the repeated read-only explanation
extended below the detail viewport. The panel now removes that redundant tail from gift and quote
details; every factual term remains, with the existing read-only notice and tooltips retained.
No font size, layout, test assertion or deadline changed.

A detail tooltip now includes the complete disclosed material definition ID and content version.
It is cleared with the private detail on close. History-only access still reports `Unidentified material`
when the authorized observation has no material description; it does not borrow the home catalog or
broaden a grant. A friendly material catalog for that scope remains outside this increment.
This is a source-only correction; the integration lead owns the unchanged native gate rerun.
