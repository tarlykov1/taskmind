# Security and privacy

The agent stores data beside the executable and does not require administrator rights.
Window titles are masked and user names are SHA-256 hashed by default. Screenshots are enabled by default for pilot completeness and can be disabled in `config.json`.
Do not publish real `data` directories, real screenshots, credentials, tokens, network paths, or customer data.


## Window title privacy

The primary setting is `privacy.windowTitleMode` with values `plain`, `masked`, or `off`. The pilot default is `plain`, which stores the real active window title. This can include document names, email subjects, folder names, and web page titles. Use `masked` to store an irreversible `masked:<hash>` value, or `off` to leave `windowTitle` empty. Legacy `captureWindowTitle` and `maskWindowTitle` settings are still mapped for backwards compatibility when `windowTitleMode` is absent. Excluded processes and excluded title fragments suppress title capture in every mode.
