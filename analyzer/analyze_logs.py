#!/usr/bin/env python3
"""Summarize GSPTaskMiningAgent JSONL logs into a small CSV report."""
from __future__ import annotations

import argparse
import csv
import json
from collections import Counter
from pathlib import Path


def iter_events(log_dir: Path):
    for path in sorted(log_dir.glob("events-*.jsonl")):
        with path.open("r", encoding="utf-8") as handle:
            for line in handle:
                line = line.strip()
                if not line:
                    continue
                yield json.loads(line)


def main() -> int:
    parser = argparse.ArgumentParser(description="Analyze GSP task-mining logs")
    parser.add_argument("log_dir", type=Path, help="Directory containing events-*.jsonl")
    parser.add_argument("--out", type=Path, default=Path("task-summary.csv"))
    args = parser.parse_args()

    counter: Counter[tuple[str, str]] = Counter()
    total = 0
    idle = 0
    for event in iter_events(args.log_dir):
        total += 1
        if event.get("isIdle"):
            idle += 1
        counter[(event.get("processName") or "unknown", event.get("windowTitle") or "")] += 1

    with args.out.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle)
        writer.writerow(["processName", "windowTitle", "events"])
        for (process, title), count in counter.most_common():
            writer.writerow([process, title, count])

    print(f"events={total}")
    print(f"idle_events={idle}")
    print(f"rows={len(counter)}")
    print(f"out={args.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
