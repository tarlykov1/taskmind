# GSPTaskMiningAgent

Portable Windows task-mining agent rebuilt from scratch.

## Build on Windows

```powershell
pwsh -File tools/build-portable.ps1
```

The script runs unit tests, publishes a self-contained `win-x64` single-file `GSPTaskMiningAgent.exe`, runs `--self-test`, creates `GSPTaskMiningAgentPortable.zip`, and fills `dist`.

## Portable package

The ZIP contains:

- `GSPTaskMiningAgent.exe`
- `START_AGENT.cmd`
- `ENABLE_AUTOSTART.cmd`
- `DISABLE_AUTOSTART.cmd`
- PowerShell equivalents
- `config.example.json`
- `README.txt`

## Runtime output

The executable creates `config.json` and these local data paths on first run:

```text
data/agent-status.txt
data/logs/events-YYYYMMDD.jsonl
data/logs/events.csv
data/screenshots
data/archives
data/errors
```

See `docs/` for pilot, security, and analyzer instructions.


## Window title privacy

The primary setting is `privacy.windowTitleMode` with values `plain`, `masked`, or `off`. The pilot default is `plain`, which stores the real active window title. This can include document names, email subjects, folder names, and web page titles. Use `masked` to store an irreversible `masked:<hash>` value, or `off` to leave `windowTitle` empty. Legacy `captureWindowTitle` and `maskWindowTitle` settings are still mapped for backwards compatibility when `windowTitleMode` is absent. Excluded processes and excluded title fragments suppress title capture in every mode.
