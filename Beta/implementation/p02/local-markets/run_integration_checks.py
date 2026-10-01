"""Use the existing bounded, source-pinned and mutually exclusive integration checker."""
import importlib.util
import sys
from pathlib import Path
PACKET = Path(__file__).resolve().parent
SPEC = importlib.util.spec_from_file_location("paid_world_checks", PACKET.parent / "paid-world" / "run_integration_checks.py")
CHECKS = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(CHECKS)
CHECKS.PACKET = PACKET
CHECKS.TASK = PACKET
if __name__ == "__main__":
    (PACKET / "qualification").mkdir(parents=True, exist_ok=True)
    sys.exit(CHECKS.main())
