# GSP Task Mining Agent

## Скачать готовую версию

Не скачивайте **Source code ZIP** для запуска агента: исходный код не содержит готового EXE. Готовые файлы публикуются во вкладке **GitHub Releases** после автоматической сборки GitHub Actions.

Скачайте из последнего Release один из файлов:

- `GSPTaskMiningAgent.exe` — один автономный Windows x64 EXE;
- `GSPTaskMiningAgentPortable.zip` — portable-комплект с EXE, CMD-скриптами и README;
- `SHA256SUMS.txt` — контрольные суммы для проверки скачанных файлов.

На компьютере пользователя не нужны .NET Runtime, .NET SDK, Visual Studio, Python или права администратора.

## Быстрый запуск

### Вариант 1: один EXE

1. Скачайте `GSPTaskMiningAgent.exe` из последнего Release.
2. Положите EXE в папку, где у пользователя есть права на запись.
3. Запустите `GSPTaskMiningAgent.exe`.
4. При первом запуске рядом с EXE будут созданы `config.json` и папка `data`.

### Вариант 2: portable ZIP

1. Скачайте `GSPTaskMiningAgentPortable.zip` из последнего Release.
2. Распакуйте ZIP в папку, где у пользователя есть права на запись.
3. Запустите `START_AGENT.cmd` или `GSPTaskMiningAgent.exe`.
4. Для автозагрузки текущего пользователя запустите `ENABLE_AUTOSTART.cmd`.
5. Для отключения автозагрузки запустите `DISABLE_AUTOSTART.cmd`.

## Проверка запуска

1. Откройте Диспетчер задач и найдите `GSPTaskMiningAgent.exe`.
2. Проверьте файл `data/agent-status.txt` рядом с EXE.
3. Проверьте папку `data/logs`.
4. Проверьте настройки безопасности в `config.json`, особенно `enableScreenshots`, `maskWindowTitles` и `hashUserName`.

## Где лежат данные

Все данные по умолчанию создаются рядом с EXE:

```text
data/logs
data/screenshots
data/archives
data/errors
data/agent-status.txt
```

## Что не попадает в Release

В Release не должны попадать реальные сетевые пути компании, пароли, токены, персональные данные, реальные логи, реальные скриншоты или содержимое рабочих папок `data`. В Release включается только безопасный `config.example.json`; рабочий `config.json` создается на компьютере пользователя при первом запуске.

## Для разработчиков

Автоматическая сборка и публикация Release настроены в `.github/workflows/build-release.yml`. Workflow запускается при push в `main` и вручную через `workflow_dispatch`, собирает self-contained Windows x64 EXE, формирует portable ZIP, считает SHA-256 и публикует Release `pilot-<run_number>`.

Локальная сборка на Windows с .NET 8 SDK:

```bat
cd GSPTaskMiningAgent
scripts\build_portable_win_x64.bat
```

Подробная документация находится в `GSPTaskMiningAgent/docs`.
