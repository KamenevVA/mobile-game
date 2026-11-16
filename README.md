# mobile-game — BoltRush

Мобильная головоломка «откручивай болты» (жанр screw/plate puzzle): снимай винты сверху вниз, освобождай пластины, заполняй коробки по цветам. Unity-проект, вся игра на C#, без сторонних ассетов (арт и звуки генерируются процедурно в коде).

- **Движок:** Unity 6.3 LTS `6000.3.28f1` (версия должна совпадать точно)
- **Платформа:** Android (ARM64, IL2CPP, minSdk 25), package `com.boltrush.puzzle`
- **Языки:** C# (runtime + Editor-инструменты), JSON для уровней
- **Команда:** два автора, код пишут ИИ-ассистенты (Codex / Claude / ZCode); правила работы ИИ — в [AGENTS.md](AGENTS.md)

## Структура репозитория

```
AGENTS.md                              — правила работы ИИ в проекте (читать первым)
PROJECT_STATE.md                       — журнал этапов, решения, текущий следующий шаг
ONBOARDING.md                          — подключение нового разработчика
MARKET_RESEARCH_AND_GAME_CONCEPT.md    — анализ рынка и концепция игры
MONETIZATION_AND_PUBLISHING.md         — площадки, выплаты, Яндекс Реклама, релиз
TOOLS_AI_AND_HONOR_X8B.md              — инструменты, ИИ-скиллы, целевое устройство
skills-lock.json                       — фиксация установленных ИИ-скиллов

Game/                                  — Unity-проект
  Assets/
    Scripts/                           — игровая логика (runtime)
      BoltRushCore.cs                  — правила, генератор уровней, решатель (Solve/MinBuffer)
      BoltRushGame.cs                  — поведение: ввод, анимации, коробки, буфер, конец уровня
      BoltRushShell.cs                 — оболочка/навигация экранов
      BoltRushShop.cs                  — магазин/экономика
      BoltRushCampaign.cs              — кампания/прогрессия
      BoltRushGlassArt.cs              — процедурный арт (стекло)
      BoltRushNavigationArt.cs         — процедурный арт (навигация)
      BoltRushToolsUi.cs               — UI-инструменты
      BoltRushRegression.cs            — регрессионные проверки правил/генератора
      ThreadlightPrototype.cs          — архивный прототип (автозапуск отключён: active = false)
    Editor/                            — инструменты редактора
      BoltRushTools.cs                 — генерация 150 уровней → Assets/Resources/levels.json,
                                         сборка APK (меню BoltRush / executeMethod для CI)
      BoltRushCampaign.cs              — editor-хелперы кампании
      BoltRushToolsUi.cs               — editor-UI
      ThreadlightBuildTools.cs         — сборка архивного прототипа
    Resources/levels.json              — сгенерированные уровни (коммитится)
    Scenes/Main.unity                  — единственная сцена
    Workshop/                          — процедурные PNG-иконки
  Docs/                                — спецификация прототипа, QA, арт-заметки
  Packages/manifest.json               — зависимости Unity (внешних пакетов почти нет)
  ProjectSettings/ProjectVersion.txt   — точная версия движка
  Builds/Android/                      — APK (в git не попадает)

.agents/skills/token-saver-codex/      — скилл экономии лимитов ИИ (MIT)
```

## Ключевые факты для ИИ-ассистентов

1. Прежде чем работать: прочитай `AGENTS.md` (правила) и последние записи `PROJECT_STATE.md` (текущее состояние и следующий шаг).
2. В конце этапа допиши запись в `PROJECT_STATE.md`: результат, изменённые файлы, проверки, следующий шаг. Секреты не записывать.
3. Правила игры и решатель живут в одном классе (`BoltRushCore.cs: Sim/Solver`), поэтому генератор и игра не могут разойтись. Меняя правила — правь оба места и запускай регрессию (`BoltRushRegression`).
4. Уровни генерируются кодом и проверяются решателем на решаемость; `levels.json` коммитится. Уровни 64/71/82/102 отсутствуют — игра подставляет предыдущий уровень (`FindLevel`).
5. Сборка APK: меню `BoltRush → Generate And Build Android` или batch-метод `BoltRush.BoltRushTools.BuildAndroidLogged` (ошибка сборки пишется в `Game/Builds/Android/build_error.txt`). Ошибка 1392 «файл повреждён» при сборке — транзиентная, лечится повторным прогоном.
6. Проверка устройства: HONOR X8b (Android 14, API 34) через adb из Unity SDK; `adb` лежит в `Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/`.
7. Git: работа в ветке `feature/*` от `main`, PR обязательны, в `main` напрямую не коммитить. `Game/Library`, `Game/Builds`, логи не коммитить.

## Сборка из терминала

```bash
"C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -batchmode -quit ^
  -projectPath <путь к репо>\Game ^
  -executeMethod BoltRush.BoltRushTools.BuildAndroidLogged -logFile build.log
```

Успех = строка `BoltRush APK built` в конце `build.log`; APK в `Game/Builds/Android/BoltRush.apk`.
