# Claude delegation compatibility

Verified 2026-09-27. Owner-approved routes remain Fable with 1M context, or Opus 5.5 with 1M context,
both at explicitly requested Max effort. Astra integrates and reviews bounded assignments.

The installed native client reports version2.1.268. Its help supports explicit `--model` and
`--effort max`, and completed retained runs report `claude-fable-5-1` with a1,000,000-token window.
The current world-resource worker uses that established route; it must finish before changing the
shared client or its installed guidance. No paid probe or fallback has been run for Opus.

The [official configuration reference](https://code.claude.com/docs/en/model-config) identifies
`claude-opus-5-5`, requires Claude Code2.1.280 or later, and documents older `opus` alias resolution.
It also documents Max effort support and per-model effort defaults. The [model specification](https://platform.claude.com/docs/en/models/opus-5-5/overview)
reports native1M context. A future Opus dispatch should pin the exact model (with explicit1M selection
where appropriate), pass `--effort max`, and inspect returned model/context. Published support does not
prove this account's access or actual subscription deductions. Upgrade with a recoverable local-client
checkpoint when that route is used; do not silently downgrade or substitute if availability fails.

Ecosystem commit `2e42704` adds portable client/version/effort guidance and refreshes the maintained
standard context manifest. Source/installed byte comparisons found18 stale/drifting entries across
reviewed changes: most installed sources already matched, while the three installed adapter test files
and two global entrypoints had not received the accepted updates. All differences were inspected;
no unrelated edits are to be overwritten. The guarded plan touches eight files, including the three
updated reference copies. Its application is deferred until the current worker terminates.
The task-specific skill/context manifest for that active worker is unchanged.
