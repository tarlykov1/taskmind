#!/usr/bin/env python3
"""Simple offline analyzer for GSP Task Mining Agent JSONL/CSV logs."""
from __future__ import annotations

import argparse
import json
from collections import Counter
from pathlib import Path

import pandas as pd

COLUMNS = [
    "timestamp", "user", "machine", "eventType", "processName", "windowTitle",
    "domain", "durationSeconds", "isIdle", "screenshotPath", "message"
]


def read_jsonl(path: Path) -> list[dict]:
    rows: list[dict] = []
    for line_number, line in enumerate(path.read_text(encoding="utf-8-sig", errors="replace").splitlines(), start=1):
        if not line.strip():
            continue
        try:
            rows.append(json.loads(line))
        except json.JSONDecodeError as exc:
            rows.append({"eventType": "error", "message": f"{path}:{line_number}: {exc}"})
    return rows


def load_events(log_dir: Path) -> pd.DataFrame:
    frames: list[pd.DataFrame] = []

    for jsonl in sorted(log_dir.rglob("*.jsonl")):
        rows = read_jsonl(jsonl)
        if rows:
            frame = pd.DataFrame(rows)
            frame["sourceFile"] = str(jsonl)
            frames.append(frame)

    for csv in sorted(log_dir.rglob("*.csv")):
        try:
            frame = pd.read_csv(csv)
            frame["sourceFile"] = str(csv)
            frames.append(frame)
        except Exception as exc:  # noqa: BLE001 - analyzer should preserve input errors in report
            frames.append(pd.DataFrame([{"eventType": "error", "message": f"{csv}: {exc}"}]))

    if not frames:
        return pd.DataFrame(columns=COLUMNS)

    data = pd.concat(frames, ignore_index=True, sort=False)
    for column in COLUMNS:
        if column not in data.columns:
            data[column] = None
    data["durationSeconds"] = pd.to_numeric(data["durationSeconds"], errors="coerce").fillna(0)
    data["timestamp"] = pd.to_datetime(data["timestamp"], errors="coerce")
    data["isIdle"] = data["isIdle"].astype(str).str.lower().isin(["true", "1", "yes"])
    data["processName"] = data["processName"].fillna("unknown")
    data["windowTitle"] = data["windowTitle"].fillna("")
    return data


def build_process_chains(data: pd.DataFrame, chain_length: int = 3) -> pd.DataFrame:
    ordered = data[data["eventType"].eq("window_change")].sort_values("timestamp")
    processes = [p for p in ordered["processName"].astype(str).tolist() if p and p != "unknown"]
    chains = Counter(" -> ".join(processes[i:i + chain_length]) for i in range(max(0, len(processes) - chain_length + 1)))
    return pd.DataFrame([{"chain": chain, "count": count} for chain, count in chains.most_common(50)])


def make_report(log_dir: Path, output: Path) -> None:
    data = load_events(log_dir)
    active = data[data["eventType"].isin(["active_window_tick", "window_change"])]

    applications = (
        active.groupby("processName", dropna=False)["durationSeconds"].sum()
        .reset_index(name="totalDurationSeconds")
        .sort_values("totalDurationSeconds", ascending=False)
    )
    switches = int(data["eventType"].eq("window_change").sum())
    screenshot_rows = data[data["screenshotPath"].fillna("").astype(str).ne("")]
    idle_seconds = float(active.loc[active["isIdle"], "durationSeconds"].sum())
    total_seconds = float(active["durationSeconds"].sum())
    idle_share = idle_seconds / total_seconds if total_seconds else 0

    summary = pd.DataFrame([
        {"metric": "events", "value": len(data)},
        {"metric": "window_switches", "value": switches},
        {"metric": "screenshots", "value": len(screenshot_rows)},
        {"metric": "idle_share", "value": round(idle_share, 4)},
        {"metric": "total_duration_seconds", "value": round(total_seconds, 2)},
    ])

    window_titles = (
        active.groupby(["processName", "windowTitle"], dropna=False)
        .agg(totalDurationSeconds=("durationSeconds", "sum"), events=("eventType", "count"))
        .reset_index()
        .sort_values(["totalDurationSeconds", "events"], ascending=False)
        .head(200)
    )
    errors = data[data["eventType"].eq("error")][["timestamp", "message", "sourceFile"]]
    screenshots = screenshot_rows[["timestamp", "processName", "windowTitle", "screenshotPath", "sourceFile"]]
    chains = build_process_chains(data)

    with pd.ExcelWriter(output, engine="openpyxl") as writer:
        summary.to_excel(writer, sheet_name="summary", index=False)
        applications.to_excel(writer, sheet_name="applications", index=False)
        window_titles.to_excel(writer, sheet_name="window_titles", index=False)
        chains.to_excel(writer, sheet_name="process_chains", index=False)
        screenshots.to_excel(writer, sheet_name="screenshots", index=False)
        errors.to_excel(writer, sheet_name="errors", index=False)


def main() -> None:
    parser = argparse.ArgumentParser(description="Build Excel report from GSP Task Mining Agent logs.")
    parser.add_argument("log_dir", type=Path, help="Folder with .jsonl/.csv logs or exported archive contents")
    parser.add_argument("-o", "--output", type=Path, default=Path("task_mining_report.xlsx"), help="Output .xlsx path")
    args = parser.parse_args()

    make_report(args.log_dir, args.output)
    print(f"Report saved to {args.output}")


if __name__ == "__main__":
    main()
