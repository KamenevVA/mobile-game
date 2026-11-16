# Подключение к проекту (инструкция для второго разработчика)

Мобильная игра BoltRush (Unity, C#): головоломка «откручивай болты». Проект ведут два автора, оба пишут код через ИИ-ассистентов (Codex / Claude / ZCode). Эта инструкция — как развернуть всё у себя и начать работать.

## 0. Доступ

1. Создай аккаунт на github.com (если нет) и скажи владельцу свой логин — он пришлёт приглашение в приватный репозиторий.
2. Прими приглашение (придёт на почту / в уведомления GitHub).

## 1. Инструменты (один раз)

| Инструмент | Зачем | Как |
| --- | --- | --- |
| Git | версия кода | https://git-scm.com/download/win (по умолчанию) |
| Unity Hub + Unity **6000.3.28f1** | движок, версия должна совпадать точно | Unity Hub → Installs → Install Editor → 6.3.28f1, с модулями: Android Build Support (+ OpenJDK, SDK & NDK Tools) |
| ИИ-ассистент | написание кода | любой, который умеет читать папку проекта и запускать команды |

Проверка: `git --version` в терминале; Unity Hub показывает 6000.3.28f1 установленной.

## 2. Клонирование

```bash
git clone https://github.com/KamenevVA/mobile-game.git
cd mobile-game
```

Открой папку `Game` в Unity (Hub → Add → выбрать `Game`). Первый импорт займёт несколько минут.

## 3. Как устроен проект

- `AGENTS.md` — правила работы ИИ в проекте. Покажи/прочитай своему ИИ в первую очередь.
- `PROJECT_STATE.md` — история и текущее состояние: что сделано, что дальше. Читай перед началом работы, обновляй в конце этапа.
- `Game/Assets/Scripts/BoltRush*.cs` — вся игра: ядро правил (`Core`), поведение (`Game`), shell/магазин/кампания/арт.
- `Game/Assets/Editor/` — инструменты редактора: генерация уровней и сборка APK.
- `Game/Docs/PROTOTYPE_SPEC.md` — спецификация прототипа.
- `MARKET_RESEARCH_AND_GAME_CONCEPT.md`, `MONETIZATION_AND_PUBLISHING.md`, `TOOLS_AI_AND_HONOR_X8B.md` — исследования.
- `.agents/skills/token-saver-codex/` — скилл экономии лимитов ИИ, уже в репозитории.

## 4. Рабочий цикл (важно)

1. Обновись: `git pull origin main`.
2. Создай ветку: `git checkout -b feature/краткое-название`.
3. Делай изменения небольшими шагами, коммить часто: `git add -A && git commit -m "что сделал"`.
4. Перед завершением убедись, что проект компилируется (см. проверку ниже).
5. Обнови `PROJECT_STATE.md` (результат, изменённые файлы, проверки, следующий шаг).
6. `git push -u origin feature/...` и открой Pull Request в `main` на GitHub. PR мержит второй автор после просмотра.

Никогда не работай напрямую в `main`. Не коммить `Game/Library`, `Game/Builds`, логи — они уже в `.gitignore`.

## 5. Проверка сборки (без телефона)

В Unity: меню `BoltRush → Generate And Build Android` (сгенерирует уровни и соберёт `Game/Builds/Android/BoltRush.apk`).

Или из терминала:

```bash
"C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -batchmode -quit ^
  -projectPath <путь к репо>\Game ^
  -executeMethod BoltRush.BoltRushTools.BuildAndroidLogged -logFile build.log
```

Успех = в конце `build.log` строка `BoltRush APK built`. Ошибка сборки пишется в `Game/Builds/Android/build_error.txt`.

## 6. Задачи

Работу распределяем через Issues в репозитории: каждая задача = отдельная ветка + PR. Обсуждение — в PR/Issues, не в личке, чтобы ничего не терялось.

## 7. Если что-то сломалось

- Unity ругается на версию проекта → проверь, что стоит ровно 6000.3.28f1.
- Сборка падает → смотри `build_error.txt` и конец `build.log`; почти всегда это временная ошибка файлов — пересборка лечит.
- Конфликты при мерже → не разрешай вслепую, позови второго автора.
