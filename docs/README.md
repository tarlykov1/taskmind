# Documentation

Runtime data:

- `data/agent-status.txt` — current state and UTC timestamp.
- `data/logs/events-YYYYMMDD.jsonl` — append-only JSONL event stream.
- `data/logs/events.csv` — analyst-friendly CSV copy of captured events.
- `data/screenshots/*.png` — periodic desktop screenshots when enabled.
- `data/archives/*.zip` — archived old logs and screenshots.
- `data/errors/*.log` — fatal errors.

Run analyzer:

```powershell
python analyzer/analyze_logs.py path\to\data\logs --out task-summary.csv
```


## Window title privacy

The primary setting is `privacy.windowTitleMode` with values `plain`, `masked`, or `off`. The pilot default is `plain`, which stores the real active window title. This can include document names, email subjects, folder names, and web page titles. Use `masked` to store an irreversible `masked:<hash>` value, or `off` to leave `windowTitle` empty. Legacy `captureWindowTitle` and `maskWindowTitle` settings are still mapped for backwards compatibility when `windowTitleMode` is absent. Excluded processes and excluded title fragments suppress title capture in every mode.
