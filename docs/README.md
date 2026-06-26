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
