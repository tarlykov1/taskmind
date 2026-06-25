# GSPTaskMiningAgent

Fresh implementation of a portable Windows task-mining agent.

## Build on Windows

```powershell
pwsh -File tools/build-portable.ps1
```

The build script runs unit tests, publishes a self-contained `win-x64` single-file executable, runs `--self-test`, creates `GSPTaskMiningAgentPortable.zip`, and fills `dist`.

## Runtime

The executable creates `config.json` and these local data folders on first run:

```text
data/logs
data/screenshots
data/archives
data/errors
```

No administrator permissions are required. Data is written next to the executable.
