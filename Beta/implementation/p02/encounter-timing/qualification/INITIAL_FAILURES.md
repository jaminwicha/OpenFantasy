# Initial development failures

- The first solution build failed the xUnit1051 analyzer because the new exposure recovery test called
  `EncounterTimingCodecV1.Decode` without `TestContext.Current.CancellationToken`. The original output
  remains in `build.log`. Adding that token fixed the test call; no analyzer rule was disabled.
- The first Python fixture-generation invocation returned `SyntaxError: closing parenthesis '}' does
  not match opening parenthesis '['` in the diagnostic description's decision list. Root supplied the
  missing list delimiter. The corrected generator produced the independent exposure vectors, and the
  same script later passed all 35 real Host calls. No runtime rule, assertion or deadline was relaxed.

These failures precede qualification and are not accepted as passing runs. Final logs and exact fixtures
are retained alongside this note.
