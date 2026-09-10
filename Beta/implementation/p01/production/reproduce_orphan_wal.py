"""Temporary SQLite substrate experiment; does not open game databases or execute C#."""
import json
from pathlib import Path
import sqlite3
import tempfile


with tempfile.TemporaryDirectory(prefix="production-orphan-wal-review-") as folder:
    root = Path(folder)
    unrelated = root / "unrelated.sqlite"
    candidate = root / "candidate.sqlite"
    published = root / "published.sqlite"
    with sqlite3.connect(unrelated) as connection:
        connection.execute("PRAGMA journal_mode=WAL")
        connection.execute("PRAGMA wal_autocheckpoint=0")
        connection.execute("CREATE TABLE marker(value TEXT)")
        connection.execute("INSERT INTO marker VALUES('unrelated stale WAL')")
        connection.commit()
        stale_wal = Path(str(unrelated) + "-wal").read_bytes()
    connection.close()
    with sqlite3.connect(candidate) as connection:
        connection.execute("PRAGMA journal_mode=WAL")
        connection.execute("CREATE TABLE marker(value TEXT)")
        connection.execute("INSERT INTO marker VALUES('verified candidate')")
        connection.commit()
        connection.execute("PRAGMA wal_checkpoint(TRUNCATE)")
    connection.close()
    assert not published.exists()
    Path(str(published) + "-wal").write_bytes(stale_wal)
    candidate.rename(published)
    with sqlite3.connect(published) as connection:
        observed = connection.execute("SELECT value FROM marker").fetchone()[0]
    connection.close()
    assert observed == "unrelated stale WAL"
    print(json.dumps({"schema": "orphan-wal-publication-reproduction.v1",
                      "sqlite_version": sqlite3.sqlite_version,
                      "scope": "Independent Python SQLite mechanism experiment, not the packaged .NET runtime.",
                      "candidate_value": "verified candidate", "observed_value": observed,
                      "orphan_wal_bytes": len(stale_wal), "defect_reproduced": True}, indent=2))
