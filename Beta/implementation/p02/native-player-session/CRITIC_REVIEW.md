# Independent review and integration

One native critic reviewed the host/query and application binding, then a focused follow-up reviewed
the corrections. This uses Astra capacity; it is not Claude offloading. No provider request occurred.

- **P2: initial window focus was assumed.** FarmGame subscribed to future focus events while its router
  started focused. It now reads `Window.HasFocus()` before publishing startup readiness. The real
  rendered native test starts with `no_focus=true` in a disposable project configuration, normal focus
  binding and no local pause. It proves the router is unfocused, host status is Paused and calendar
  remains unchanged before injecting fixture focus. The actual window stays unfocused throughout.
- **Missing proof: automatic Waiting→Offered refresh.** The added scheduled-market test uses retained
  native-market genesis, real SQLite authority, merchant policy, admission and command protocol. It
  observes the cached offer without an explicit inquiry read and authenticates its exact quote against
  the authority. This in-memory transport test complements the production cargo/TLS test; it does not
  claim a native walk to the cargo merchant.
- **Probe ordering: dialogue view before displayed Close.** The follow-up identified that a newly
  received dialogue could precede the next presentation frame. Both intermediate native runs reached
  Talk, then timed out after the Close input; only the Talk command was submitted. This is consistent
  with the ordering risk, although those reports did not record the Close offer at the press. The probe
  now waits for an executable Close offer with matching dialogue identity and actor revision.

The first native failure retained a Speak command with actor revision 0 after movement. The probe now
waits for host-reported pause and agreement between the displayed Talk offer, actor/target revisions
and drawn feet before resuming. View and status remain independent publications: these checks do not
establish an atomic remote pause barrier. Success is established by the real applied receipts and saved
history, not inferred from those caches. No revision recapture, automatic action retry, rule relaxation
or deadline extension was introduced. The original failures remain in qualification.

The headless and rendered journeys pass on the current build. Root inspected the retained rendered
frame: the player and Wren are visible, and the pause message explains that other players may continue.
This is provisional existing art and injected keyboard evidence, not production art, physical-device,
full multiplayer or human acceptance. No other concrete production defect was reported by the critic.
