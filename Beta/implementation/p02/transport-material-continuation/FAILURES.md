# Retained integration failures

All reports and command/source pins are retained in `qualification/`. No limit, assertion of game
behavior, timing threshold or acceptance gate was relaxed.

- `build-01`: five test-only errors. The canonical frame helper returns a list, not an array; four
  test calls omitted the test cancellation token required by the existing analyzer. Corrected types
  and supplied `TestContext.Current.CancellationToken`; build-02 passed with zero warnings/errors.
- `focused-01`: 16 passes and three test setup failures. The new split-destination plus second-budget
  fixture omitted the dry second basin required by complete runoff admission. The other two failures
  expected FormatException for missing complete-owner dependencies, where the public API deliberately
  rejects the supplied argument with ArgumentException. The fixture now supplies every basin and the
  tests assert the actual pre-decode contract; no runtime behavior changed.
- `build-03`: the fixture correction initially used a nonexistent two-argument dry-input constructor.
  Replaced it with the existing explicit `WatershedRunoffInput.Dry` factory; build-04 passed.
- `focused-02`: 18 passes and one diagnostic assertion failure in the strengthened critic regression.
  Production authority validation reached and rejected the intended preceding-owner mismatch. Nested
  capacity decoding wraps that failure, so the outer Message does not contain its inner diagnostic.
  The test now checks the complete exception chain while retaining the exact underlying cause.
  Build-05 and all 19 focused-03 cases passed. No runtime change or broad exception allowance was made.

The original pre-change baseline build and 16 compatibility tests passed. Focused runs intentionally
exclude the expensive actual 511-operation setup; the full Core run supplies that boundary evidence.
