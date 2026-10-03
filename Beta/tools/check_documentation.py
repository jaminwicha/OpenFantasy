#!/usr/bin/env python3
"""Check live documentation structure and local links; semantic review remains explicit."""
import argparse
import json
from pathlib import Path
import re
import sys
from urllib.parse import unquote, urlsplit

BETA = Path(__file__).resolve().parents[1]


def prose(text):
    # Template comments and fenced examples cannot constitute the review. Markdown
    # permits up to three leading spaces and a closing fence at least as long.
    output = []
    fence = None
    comment = False
    for line in text.splitlines(keepends=True):
        if fence:
            character, length = fence
            if re.fullmatch(r" {0,3}" + re.escape(character) + "{" + str(length) + r",}[ \t]*\n?", line):
                fence = None
            output.append("\n")
            continue
        remaining, visible = line, ""
        while remaining:
            marker = "-->" if comment else "<!--"
            position = remaining.find(marker)
            if position < 0:
                if not comment:
                    visible += remaining
                break
            if not comment:
                visible += remaining[:position]
            remaining = remaining[position + len(marker):]
            comment = not comment
        line = visible if visible.endswith("\n") else visible + "\n"
        opening = re.match(r"^ {0,3}(`{3,}|~{3,})([^\n]*)", line)
        if opening and not (opening[1][0] == "`" and "`" in opening[2]):
            fence = (opening[1][0], len(opening[1]))
            output.append("\n")
        else:
            output.append(line)
    return "".join(output)


def check_file(path):
    errors = []
    if not path.is_file():
        return [f"Missing document: {path}"]
    text = prose(path.read_text())
    # A Markdown link written as a literal code span is not a clickable reference.
    text = re.sub(r"(?<!`)(`+)(?!`)(.*?)(?<!`)\1(?!`)", "", text, flags=re.S)
    links = re.findall(r"!?\[[^\]\n]*\]\((<[^>]+>|[^\s)]+)(?:\s+\"[^\"]*\")?\)", text)
    for link in links:
        target = link.strip("<>")
        parsed = urlsplit(target)
        if parsed.scheme or not parsed.path or target.startswith("//"):
            continue
        name = unquote(parsed.path)
        # Editor links can include a one-based source line suffix.
        name = re.sub(r":\d+$", "", name)
        resolved = Path(name) if Path(name).is_absolute() else path.parent / name
        if not resolved.exists():
            errors.append(f"{path}: missing local target {target}")
    return errors


def check_record(path):
    if not path.is_file():
        return [f"Missing work record: {path}"]
    text = prose(path.read_text())
    match = re.search(r"(?m)^## Documentation review\s*$", text)
    if not match:
        return [f"{path}: missing '## Documentation review' section"]
    section = re.split(r"(?m)^## ", text[match.end():], maxsplit=1)[0]
    errors = []
    for label in ("Plan/spec", "Reference", "Accuracy/completeness", "Checks"):
        field = re.search(rf"(?m)^- \*\*{re.escape(label)}:\*\*[ \t]*(\S[^\n]*)$", section)
        if not field or not field.group(1).strip() or field.group(1).strip().lower() in ("todo", "tbd", "pending", "n/a"):
            errors.append(f"{path}: documentation review needs a substantive {label} disposition")
    return errors


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--work-record", required=True, type=Path,
                        help="Existing result/review containing the four documentation-impact fields")
    parser.add_argument("--files", nargs="+", type=Path,
                        help="Affected live Markdown files; default checks Runtime/docs and current navigation/plan")
    parser.add_argument("--output", type=Path, help="Optional concise JSON evidence path")
    args = parser.parse_args()
    files = args.files or [*sorted((BETA / "Runtime/docs").rglob("*.md")),
                           BETA / "Runtime/README.md",
                           *[BETA / "implementation" / name for name in (
                               "CURRENT_ROADMAP.md", "AGENT_HANDOFF.md", "DOCUMENTATION_MAINTENANCE.md",
                               "PLAYER_INTERFACE_AND_NAVIGATION.md", "EQUIPMENT_AND_VEHICLE_UPKEEP.md")]]
    files = sorted({args.work_record.resolve(), *(path.resolve() for path in files)})
    errors = check_record(args.work_record.resolve())
    for path in files:
        errors.extend(check_file(path))
    result = {"accepted": not errors, "scope": "Work-record fields and local file links only; no semantic, anchor, runtime or human acceptance",
              "filesChecked": len(files), "files": [str(path.relative_to(BETA)) if path.is_relative_to(BETA) else str(path) for path in files],
              "workRecord": str(args.work_record.resolve()), "errors": errors}
    if args.output:
        args.output.write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps({key: value for key, value in result.items() if key != "files"}, indent=2))
    return 0 if result["accepted"] else 1


if __name__ == "__main__":
    sys.exit(main())
