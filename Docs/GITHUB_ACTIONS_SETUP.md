# Как добавить логин и пароль Unity в секреты GitHub

Репозиторий: **https://github.com/Timofei2506/iron-district**

> ⚠️ Никогда не отправляй пароль в чат/мессенджер. Всё, что введено в GitHub Secrets, зашифровано:
> его нельзя прочитать обратно ни через API, ни через сайт — его видят только раннеры при сборке.

## Шаг 1. Открой страницу секретов

Прямая ссылка:

**https://github.com/Timofei2506/iron-district/settings/secrets/actions**

(либо: репозиторий → **Settings** → в левом меню **Secrets and variables** → **Actions**)

## Шаг 2. Добавь два секрета

Нажми зелёную кнопку **New repository secret** и создай:

| Name | Secret | Что вводить |
|---|---|---|
| `UNITY_EMAIL` | твоя почта | email, на который зарегистрирован Unity ID (именно почта, не никнейм) |
| `UNITY_PASSWORD` | твой пароль | пароль от Unity ID |

Имена должны совпадать **посимвольно** (заглавные буквы и подчёркивания важны).

Если у тебя **Unity Plus или Pro** — добавь третий секрет:

| Name | Secret | Что вводить |
|---|---|---|
| `UNITY_SERIAL` | серийный номер | из https://id.unity.com → Subscriptions → ключ вида `XX-XXXX-XXXX-XXXX-XXXX-XXXX` |

## Шаг 3. Запусти сборку

**https://github.com/Timofei2506/iron-district/actions/workflows/build.yml**
→ кнопка **Run workflow** → **Run workflow**.

Через 20–40 минут в разделе **Artifacts** появятся:

- `IronDistrict-StandaloneWindows64` (внутри `IronDistrict.exe`)
- `IronDistrict-StandaloneLinux64`

## Требования к аккаунту Unity

1. Аккаунт должен входить на https://id.unity.com (проверь пароль на сайте).
2. **Пароль лучше без спецсимволов** (`!@#$%^&*` и т.п.) — они ломают передачу в CI. Для этого можно временно поставить простой пароль.
3. **Двухфакторка (2FA)**: если включена — CI-активация обычно падает. Варианты:
   - временно отключить 2FA;
   - или использовать отдельный аккаунт Unity без 2FA для сборок.
4. Personal-лицензия: активация на CI-раннере может не сработать из-за политики Unity (с 2025 они убрали оффлайн-активацию Personal). В этом случае — локальная сборка (см. README, раздел 2), это 20 минут и работает 100%.

## Если сборка упала — где смотреть

Actions → выбери запуск → job **Build StandaloneWindows64** → шаг **Build with Unity** → разверни лог. Ищи строки:

- `Missing Unity License File` → не добавлены/не подхватились секреты;
- `Invalid license` / `activation failed` → проблема с паролем/2FA;
- `error CS####` → ошибка компиляции в коде (пришли мне лог, поправлю).

## Проверка, что секреты на месте

Открой страницу секретов — там должен быть список имён (значения скрыты):

```
R2_ACCESS_KEY_ID
R2_BUCKET
R2_ENDPOINT
R2_SECRET_ACCESS_KEY
UNITY_EMAIL
UNITY_PASSWORD
```

Если какого-то имени нет — секрет не сохранился, добавь заново.
