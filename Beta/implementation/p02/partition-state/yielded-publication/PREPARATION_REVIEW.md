# Before-run source review

Initial builds/discovery passed. Before running tests, strengthen the gate-release test: each slice
must execute on a different managed thread, verified explicitly, rather than assuming Task.Run
implies that distinction. Preserve the original source and preflight, then rebuild/pin this final
fixture. No failed run, production change, deadline extension or automatic retry is involved.
