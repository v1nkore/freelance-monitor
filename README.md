# FreelanceMonitor

Фоновый сервис (.NET 10 Worker Service), который 24/7 опрашивает российские
фриланс-биржи, отбирает **разовые задачи под .NET/backend** (не вакансии и не
аутстафф) и шлёт находки в Telegram с кнопкой «Откликнуться».

## Установка на новой машине

1. Нужен [.NET 10 SDK](https://dotnet.microsoft.com/download).
2. `git clone https://github.com/v1nkore/freelance-monitor && cd freelance-monitor`
3. Скопируй `appsettings.example.json` → `appsettings.json` и вставь свои
   `Telegram.BotToken` и `Telegram.ChatId` (бот — у @BotFather; настоящий
   `appsettings.json` в `.gitignore` и не публикуется).
4. `dotnet run` — сервис начнёт опрашивать биржи; история просмотренного
   копится в `data/seen.json` локально.

## Прицел

Ловим **короткие IT/разработческие заказы «сделал и забыл»** на любом языке:
боты, работа с кодом, парсеры, скрипты, автоматизация, мелкие сервисы и утилиты,
интеграции, API — задачи на 1–4 часа (максимум ~12 ч). НЕ ловим работу в штат,
аутстафф, вакансии, а также дизайн/вёрстку карточек/контент — для этого минус-слова
и потолок бюджета.

Фильтр матчит по **границе слова**, а не по подстроке: иначе «бот» ловил бы
«ра**бот**а/разра**бот**ка», «rest» — «pinte**rest**», «ml» — «ht**ml**».
Латиница — целое слово, кириллица — по началу слова (`парс` → `парсер/парсинг`).

## Что работает

- **Источники:** FL.ru (RSS), Freelancehunt (RSS), Weblancer (HTML-парсер, серверный рендер) — из коробки.
- **Фильтр:** include/exclude ключевые слова + диапазон бюджета `MinBudget…MaxBudget`.
- **Дедупликация:** `data/seen.json` — заказ не приходит дважды, переживает перезапуск.
- **Первый запуск:** текущие заказы помечаются молча, уведомления идут с новых.
- **Telegram:** сообщение с заказом + **готовый отклик, адаптированный под заявку**
  (ссылается на заголовок, специализация подбирается по типу задачи —
  бот/парсер/интеграция/автоматизация/бэкенд). Кнопки: «📋 Скопировать отклик»
  (copy_text кладёт текст в буфер) и «🔗 Открыть заказ». Логика — `ReplyBuilder`.
- **Автозапуск:** launcher в папке «Автозагрузка», стартует при входе в Windows, скрыто.
- **Окно активности (09:00–23:00 МСК):** вне окна сервис продолжает опрашивать и
  молча помечать заказы, но **не шлёт уведомления** — ночью тихо, а утром нет
  шквала из ночных заказов. Настройка — `Monitor.ActiveHours` (часы, offset, вкл/выкл).

## Отключено (это вакансии/аутстафф, не разовые задачи)

- `career.habr.com` RSS (`?q=.NET`) — в конфиге `Enabled=false`.
- Telegram-каналы вакансий (`csharpdevjob`, `DotNetRuJobsFeed`) — `TelegramChannels: []`.

Включай их в `appsettings.json`, если понадобится искать именно работу/найм.

## Требует отдельной фазы (Playwright)

- **Freelance.ru** отдаёт JS-оболочку (проекты грузятся XHR, без публичного API) —
  простым HTTP не парсится, нужен headless-браузер (Chromium ~150 МБ + процесс в фоне).
  Weblancer в итоге удалось взять обычным HTTP (серверный рендер `/freelance/`).

## Архитектура

```
Program.cs         — DI, HttpClient (браузерный UA + gzip), сборка источников из конфига
Worker.cs          — цикл: fetch → dedup → filter → notify раз в N секунд
Sources/           — IProjectSource, RssProjectSource, TelegramChannelSource, TextUtils
Filtering/         — MatchFilter (include/exclude/бюджет)
State/             — SeenStore (JSON виденных id)
Notifications/     — INotifier + TelegramNotifier (кнопка) + ConsoleNotifier
Options/           — типизированный конфиг
```

## Настройка фильтра (`appsettings.json` → `Monitor.Filter`)

- **IncludeKeywords** — заказ проходит, если содержит хотя бы одно (.net, c#, api, бот, парсер, интеграц…).
- **ExcludeKeywords** — отбрасывается, если содержит хотя бы одно (аутстафф, в штат, вакансия, wordpress, дизайн…).
- **MinBudget / MaxBudget** — диапазон в рублях. `MaxBudget: 40000` отсекает крупные/долгие проекты.
  Заказы без распознанного бюджета не отсекаются.

`PollIntervalSeconds` — период опроса (по умолчанию 180 с).

## Управление сервисом

```powershell
# работает ли (виден процесс)
Get-Process FreelanceMonitor

# остановить сейчас
Get-Process FreelanceMonitor | Stop-Process -Force

# запустить сейчас (скрыто)
wscript.exe "$env:USERPROFILE\FreelanceMonitor\run-hidden.vbs"

# отключить автозапуск: удалить launcher из автозагрузки
Remove-Item "$([Environment]::GetFolderPath('Startup'))\FreelanceMonitor.vbs"
```

Рабочая копия лежит в `%USERPROFILE%\FreelanceMonitor` (вне OneDrive, чтобы
`seen.json` не дёргал синхронизацию). Исходники — в `Desktop/фриланс/FreelanceMonitor`.

## Пересобрать после правок

```powershell
cd <папка с исходниками>
dotnet publish -c Release -o "$env:USERPROFILE\FreelanceMonitor"
Get-Process FreelanceMonitor | Stop-Process -Force   # перезапустить
wscript.exe "$env:USERPROFILE\FreelanceMonitor\run-hidden.vbs"
```
