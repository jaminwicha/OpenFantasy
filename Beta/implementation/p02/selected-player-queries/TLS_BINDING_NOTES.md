# Next connected consent/selection binding

Read-only dependency mapping by the reused native protocol worker before this increment. These are
implementation obligations, not completed features. The full co-player DESIGN and frozen spec still apply.

- `CargoPlayerHost.OpenExistingAsync` presently asks `CargoOutcomeWorldAdmission.Find` to validate its
  composition; that finder rejects profiles39/40. Add explicit supported composition, never an automatic
  live-save relabel. `PlayerProtocolClient.ConnectAsync` presently waits for initial control only36–38.
- `LocalWorldSession.SubmitAction` deliberately refuses ControlPermissionIntent. Offer/accept/revoke need
  a home-authorized, nonadvancing receipt route plus a bounded board of relevant permission terms.
  Acquisition remains separate, with genuine final admission, owner loss and original allowance expiry.
- The existing v1 control codec is66bytes and permits generation0/1 only. Keep its bytes. Actor-selection
  lifetime needs its own typed versioned inputs and response correlation, not widened old semantics.
- Both WebSocket endpoints negotiate `openfantasy.v1`. Introduce explicit feature negotiation for selected
  observations/inputs. Parse completely before dispatch; preserve monotonic IDs and existing response caps.
- Legacy `ReadView` and its observation replica are pinned to Welcome's home actor. A selected observation
  must contain home and optional capability-scoped controlled view atomically; never fill a fake full actor
  view with empty/zero withheld fields. Force an appropriate full basis at each generation transition.
- Keep the1MiB payload,900KiB observation body and16MiB expanded JSON budgets bounded in aggregate; two
  independently maximal views cannot fit. Keep16KiB command and256byte witness limits. Existing home JSON
  versions1–4 retain exact bytes; a selected body needs its own strict canonical typed decoding.
- Bind selected query requests/results using this increment's captured lifetime, operation and subject.
  Discard delayed results before applying them to a new selection. Existing bootstrap/status remain scoped
  deliberately. Exact accepted-action receipts are not new selection authority.
- Add an explicit optional selection interface at IPlayerSession. RemotePlayerSession and FarmClientSession
  must use it honestly. Farm action offers capture actor/witness/selection and purpose-scoped request terms.
  Camera, wheel, target display and input use the operated actor while the home actor keeps human obligations.
  Switching clears held input and requires deliberate Resume against the applied observation.

The connected qualification uses four real humans over TLS: A offers, B accepts; actual A transport loss
and30-second expiry; B acquires/selects A, applies the scoped frame and deliberately resumes; movement and
one genuine paid action; revoke or owner return; forced paused home generation, stale A-to-home-to-A refusal,
unchanged B identity, exact receipt/archive/reopen. Run the same application path from native `--join` for
rendered evidence. A test-only client cannot replace that dependency. Existing five-second movement and
150ms lease limits remain untouched; the earlier unexplained timing failure remains open.
