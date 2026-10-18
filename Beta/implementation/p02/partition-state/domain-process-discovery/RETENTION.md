# Storage evidence retention

The existing shared StorageFiles fixture deleted its directory on disposal, including assertion failure.
It now honors optional OPENFANTASY_STORAGE_ARTIFACTS. The qualification run supplies a new root and
retains every generated case directory; default temporary cleanup remains unchanged. No production
storage policy changes. Do not reuse the root or modify retained SQLite originals. Copy the complete
case directory including sidecars before recovery inspection. The artifact manifest pins terminal files.
This narrow fixture change requires refreshed Hosting binaries/discovery. Existing admission preflight
uses no StorageFiles; its source and all production behavior remain unchanged, but it will be verified
against final pins before the storage run. No failed behavior run prompted this change.
