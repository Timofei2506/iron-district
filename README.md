# IRON DISTRICT

**Процедурный FPS с открытым миром на Unity.** Весь мир — город, промзона, деревня, лес и горы — генерируется кодом при запуске. Все текстуры, меши и звуки создаются процедурно: ни одного скачанного ассета, никаких проблем с авторскими правами.

![status](https://img.shields.io/badge/status-playable-orange) ![unity](https://img.shields.io/badge/Unity-2022.3%20LTS-black) ![license](https://img.shields.io/badge/license-MIT-green)

---

## Что уже есть в игре

| Система | Реализация |
|---|---|
| Мир 1×1 км | даунтаун с небоскрёбами и окнами, промзона (ангары, цистерны, контейнеры, труба с дымом), деревня (дома, колодец, сено), лес, горы, дороги, заборы |
| Графика | PBR-материалы (albedo + normal + metallic/gloss), линейное цветовое пространство, MSAA, мягкие тени, туман, процедурное небо «золотой час», эмиссивные окна и фонари |
| Игрок | CharacterController, бег, прыжок, инерция, покачивание камеры, регенерация HP |
| Оружие | AR-7: автоматический огонь, ADS, разброс+bloom, отдача, перезарядка с анимацией магазина, трассеры, вспышка, декали от пуль, искры |
| ИИ | NavMesh-патрули, погоня, стрейф, стрельба очередями, зоны попадания (голова ×2.1), 3 уровня сложности по волнам |
| Геймплей | бесконечные волны, очки, хедшоты, аптечки/патроны, рекорд в PlayerPrefs |
| UI/UX | полностью код: меню, настройки чувствительности/звука, HUD с прицелом и хитмаркерами, пауза, экран смерти |
| Сборка | GitHub Actions → готовый `.exe` / Linux-бинарник без установки Unity на твой ПК |

---

## 1. Сборка .exe без установки Unity — GitHub Actions

> ⚠️ Для работы этого пути нужна **активация Unity на раннере**. Unity в 2025+ убрал ручную оффлайн-активацию Personal-лицензий, поэтому самый стабильный вариант — **локальная сборка** (раздел 2). CI-путь оставлен, если у тебя Unity Plus/Pro (тогда всё работает через `UNITY_SERIAL`) или если раннеру удаётся активировать Personal по email+паролю.

1. Создай **приватный** репозиторий на GitHub и загрузи туда всё содержимое этой папки (с `.github/`, `Assets/`, `Packages/`, `ProjectSettings/`, `Tools/`).
2. **Settings → Secrets and variables → Actions** и добавь:

   | Secret | Когда нужен |
   |---|---|
   | `UNITY_EMAIL` | всегда (почта Unity-аккаунта) |
   | `UNITY_PASSWORD` | всегда (пароль Unity-аккаунта) |
   | `UNITY_SERIAL` | только Unity Plus/Pro |
   | `UNITY_LICENSE` | опционально: содержимое `.ulf` (если он у тебя есть) |

3. Вкладка **Actions → Build Iron District → Run workflow**.
4. Через 15–40 минут скачай артефакт **`IronDistrict-StandaloneWindows64`** → распакуй → запусти **`IronDistrict.exe`**.

Если job падает на шаге активации лицензии — не трать время, собери локально (раздел 2): это 20 минут и 100% результат.

## 2. Локальная сборка (самый надёжный путь)

1. Установи **Unity Hub** и **Unity 2022.3.22f1 LTS** (или любую 2022.3.x / 2021.3.x).
2. Unity Hub → **Add → Add project from disk** → выбери папку `IronDistrict`.
3. Первый импорт займёт 1–3 минуты (импорт текстур, компиляция скриптов).
4. Откроется сцена `Assets/Scenes/IronDistrict.unity` (создаётся автоматически). Нажми **Play**.

### Сборка .exe из редактора
`File → Build Settings → Windows, Mac, Linux → Windows x86_64 → Build` → выбери папку → готово.

### Сборка из командной строки
```bat
Tools\build_windows.bat
```
```bash
UNITY_PATH=/path/to/Unity ./Tools/build_linux.sh
```

---

## 3. Управление

| Клавиша | Действие |
|---|---|
| `WASD` | движение |
| `Shift` | бег |
| `Space` | прыжок |
| `ЛКМ` | огонь |
| `ПКМ` | прицеливание (ADS) |
| `R` | перезарядка |
| `Esc` | пауза / выход в меню |
| `Enter` | начать / рестарт |
| `Q` (в паузе) | выход в меню |

---

## 4. Структура проекта

```
Assets/
  IronDistrict/
    Scripts/
      Core/       GameRoot (состояния), GameBootstrap, AssetLibrary, Sfx, InputService, Util (MeshBuilder/Geo)
      Player/     PlayerController + CameraController, PlayerHealth
      Weapons/    WeaponManager (viewmodel, стрельба, перезарядка)
      AI/         Enemy (NavMeshAgent + бой), EnemySpawner (волны)
      World/      WorldGenerator (город/промзона/деревня/лес + NavMesh), SkyController
      Fx/         TracerFx, ImpactFx, ExplosionFx
      Gameplay/   Pickup, Damage (IDamageable/HitZone)
      UI/         HUD, MenuController
    Editor/       ProjectAutoSetup, CiBuild, TextureImportRules
  Resources/
    Textures/     сгенерированные PBR-текстуры (albedo/_n/_r)
    Audio/        сгенерированные wav (выстрелы, шаги, ветер...)
  Scenes/         IronDistrict.unity (создаётся редактором)
Tools/
  gen_assets.py   генератор текстур/звуков (Python + numpy + pillow)
  build_windows.bat, build_linux.sh
.github/workflows/build.yml
```

Ключевая идея: **сцена пустая**, всё создаётся в рантайме через `GameBootstrap → GameRoot`. Поэтому проект невозможно «сломать» потерянными ссылками в сцене.

---

## 5. Тонкая настройка

| Что | Где |
|---|---|
| Скорость/урон/магазин оружия | `Weapons/WeaponManager.cs` (поля `Damage`, `FireRate`, `MagSize`, `Spread`) |
| Сложность волн, макс. врагов | `AI/EnemySpawner.cs` (`BasePerWave`, `MaxAlive`) |
| HP, регенерация | `Player/PlayerHealth.cs` |
| Сила ИИ | `AI/Enemy.cs` (`MaxHealth`, `agent.speed`, `hitChance`) |
| Новый мир | меню → «НОВЫЙ МИР (СБРОС СИДА)» или `Iron District → Сбросить сохранённый мир` |
| Свет/туман/небо | `World/SkyController.cs` |
| Плотность застройки/леса | `World/WorldGenerator.cs` (блоки города, `trees = 250`) |
| Качество | `GameRoot.ApplyPerformanceSettings` + `FpsProbe` (авто-понижение при <24 FPS) |

Сид мира хранится в `PlayerPrefs["IrD_Seed"]`: один и тот же сид = один и тот же город.

---

## 6. Что нужно знать честно

- Это **играбельный прототип**, а не AAA-тайтл. «Топовая графика» здесь = аккуратный свет, PBR-текстуры и плотная детализация, но модели персонажей — процедурные манекены без скелетной анимации.
- Коммерческий релиз возможен: все ассеты сгенерированы скриптом `Tools/gen_assets.py` (лицензия MIT), Unity Personal разрешает продажу до $200k дохода в год (проверь актуальные условия Unity). Шутер с насилием → возрастной рейтинг (PEGI 16/18) и правила площадок (Steam и т.д.).
- Заработок «на ПК» с шутера — это маркетинг + вишлисты + полировка, а не строчка кода. Проект даёт готовую базу, чтобы не начинать с нуля.
- Multiplayer, сюжет, скины, Steamworks — следующие шаги (раздел 7).

---

## 7. Ближайший роадмап

1. Скелетная анимация врагов (Mixamo-совместимый риг) + звуки боли.
2. Второе оружие (дробовик), гранаты, миникарта.
3. Ranged-баланс + боссы волн.
4. Steam-обёртка: Steamworks.NET, вишлист-страница, трейлер.
5. Оптимизация: статик-батчинг зданий, LOD, occlusion culling.

---

## 8. Регенерация ассетов

Если захочешь другие текстуры/звуки:
```bash
python3 -m pip install numpy pillow
python3 Tools/gen_assets.py
```
Скрипт перезапишет `Assets/Resources/Textures` и `Assets/Resources/Audio` (правила импорта задаёт `Editor/TextureImportRules.cs`).

---

## Лицензия

MIT — делай что хочешь, только не предъявляй претензий. Код и сгенерированные ассеты созданы в этом проекте.
