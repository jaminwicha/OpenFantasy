# Independent review and dispositions

Native critic `durable_archive_critic` reviewed the combined archive/treatment proof, controller/session
authorization, readiness/reopen, exact public projection and architecture. It found no blocking runtime
issue. A focused follow-up reviewed all seven new session tests, including an actual helper command
rejection caused by a historically occupied consumption identity and the subsequent no-retry behavior.

- Architecture wording in persistence had implied an existing native presentation binding. Corrected
  to require a separate binding; no native care acceptance is claimed.
- Existing `SessionFixture.Until` checked five seconds only while its predicate was false. Added the
  same check after success so delayed scheduling cannot turn a late observation into a passing wait.
  The deadline remains five seconds; this strengthens evidence and changes no gameplay rule.

The critic did not run builds or tests. Root owns execution and integration. This bounded source review
does not replace native, physical or human gates, nor the broader composition and game qualification.
