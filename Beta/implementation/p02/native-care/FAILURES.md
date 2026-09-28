# Retained failures and corrections

- `qualification/build-01.log`: two Hosting content classes attempted to use Core's internal canonical
  framing utility. They now follow the existing Hosting admission pattern: an explicit ASCII domain
  tag followed by fixed-width 32-byte content hashes. Core visibility and prior profiles are unchanged.
- `qualification/build-02.log`: the native probe needed a nullable-flow assertion after its explicit
  lifecycle guard; two synchronous client-admission throw assertions selected xUnit's obsolete
  `Func<Task>` overload. Explicit void lambdas preserve the actual synchronous rejection contract.
- `qualification/import-01.*`: sandboxed Godot import reported denied IDE socket/editor settings
  access and a certificate lookup error. It was not accepted despite process exit zero. Repeating the
  same pinned import with normal host access passed (`import-02.*`).
- `qualification/native-first/`: the sandboxed headless process crashed in Godot's log rotation after
  failing to open its user log. It did not enter the game or create a care world. This is retained as a
  failed launch; later native runs require normal Godot user-directory access and unchanged deadlines.

Native runner failures, if any, remain in their numbered JSON reports and referenced artifacts.
A later pass does not erase a failed attempt or establish production latency or platform acceptance.

`native-rendered-01.json` retained a successful wheel, keyboard and controller-event journey, then
failed before the pointer press: after fixture focus returned, the probe's immediate button assertion
ran before the HUD's next presentation frame enabled the button. `CarePlayProbe.ReadyCareButton`
now waits for actual visible/enabled control, readiness, no pending action and an executable offer
under the existing ten-second deadline. Controller verification also requires actual button focus
before A. No command retry, gameplay adjustment or deadline increase was introduced. The complete
four-control run is repeated on final probe bytes; the earlier failure remains authoritative evidence.
