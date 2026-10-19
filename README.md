# PaymentProcessingAPI

ASP.NET Core Web API на .NET 10 с PostgreSQL и Entity Framework Core.

База данных называется `PaymentProcessing`. При запуске API EF Core применяет миграции и создаёт базу, если PostgreSQL доступен и указанный пользователь имеет право создавать базы.

## Требования

- .NET 10 SDK.
- Установленный и запущенный PostgreSQL.
- PostgreSQL и пользователь с правом `CREATEDB` и правами на схему.

## Настройка PostgreSQL

В `PaymentProcessingAPI/appsettings.json` находится безопасная локальная строка подключения по умолчанию:

```text
Host=localhost;Port=5432;Database=PaymentProcessing;Username=postgres;Password=postgres
```

Измените пользователя и пароль под свою установку PostgreSQL. Чтобы не хранить собственный пароль в файле проекта, задайте строку через User Secrets:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=PaymentProcessing;Username=postgres;Password=<ваш-пароль>" --project PaymentProcessingAPI
```

При запуске API применяет EF Core миграции. Если базы `PaymentProcessing` ещё нет, PostgreSQL создаст её автоматически при наличии у пользователя права `CREATEDB`; затем будут созданы таблицы. Подключение задаётся через конфигурацию приложения, а пароль рекомендуется хранить в User Secrets.

## Запуск

```powershell
dotnet restore
dotnet run --project PaymentProcessingAPI
```

Swagger UI в профиле Development: [http://localhost:5006/swagger](http://localhost:5006/swagger).

## API и обработка платежа

- `POST /api/payments` — создает платеж и резервирует сумму. Ответ `202 Accepted` означает, что платеж принят в обработку; итоговый статус можно получить отдельным запросом.
- `GET /api/payments/{transactionId}` — возвращает текущий статус платежа.
- `GET /api/accounts/{accountId}/balance` — возвращает доступный остаток с учетом зарезервированных сумм.

Фоновый обработчик отправляет платеж демо-банку. При подтверждении он в одной транзакции БД списывает сумму с отправителя, зачисляет получателю и записывает проводки. При отказе резерв снимается. Если ответ потерян, платеж получает статус `Unknown`, а деньги остаются зарезервированными. Обработчик запрашивает статус банка по тому же `transactionId` и применяет результат, не отправляя повторное списание вслепую.

Для демонстрации установите `BankService:DemoMode` в `PaymentProcessingAPI/appsettings.json`:

- `Success` — демо-банк подтверждает платеж.
- `Reject` — демо-банк отклоняет платеж.
- `TimeoutAfterExecution` — демо-банк выполняет платеж, но имитирует потерю ответа; следующая сверка получает сохраненный результат.

Демо-клиент хранит результаты в памяти процесса. Для реального банковского сервиса его нужно заменить реализацией `IBankPaymentClient`, передавая `transactionId` как ключ идемпотентности и запрашивая итог операции у банка после Timeout. Хранилище демо-клиента не предназначено для эксплуатации и теряет состояние при перезапуске приложения.

## Миграции EF Core

Инструмент `dotnet-ef` должен быть версии 10. Установите его при необходимости:

```powershell
dotnet tool install --global dotnet-ef --version 10.0.5
```

Применить миграции к локальной базе:

```powershell
dotnet ef database update --project PaymentProcessingAPI --startup-project PaymentProcessingAPI
```

Создать следующую миграцию после добавления сущностей и конфигурации в `ApplicationDbContext`:

```powershell
dotnet ef migrations add <MigrationName> --project PaymentProcessingAPI --startup-project PaymentProcessingAPI
dotnet ef database update --project PaymentProcessingAPI --startup-project PaymentProcessingAPI
```

Миграции создают таблицы `Accounts`, `Payments`, `PaymentAttempts`, `PaymentStatusHistory` и `LedgerEntries` в БД `PaymentProcessing`. Уникальный `TransactionId` защищает создание от повторов; `PaymentAttempts` хранит результаты вызовов банка, а `PaymentStatusHistory` — историю смены статусов. При регистрации платежа сумма резервируется на счёте отправителя до подтверждения результата банка. `LedgerEntries` хранит историю дебета и кредита после подтверждённого перевода.

## Типичные проблемы

- **Не удаётся подключиться к PostgreSQL:** проверьте, что служба PostgreSQL запущена, а порт, имя пользователя и пароль в строке подключения верны. Пользователю нужно право создавать базу `PaymentProcessing` и таблицы в ней.
- **Порт 5432 занят другим проектом:** укажите фактический порт вашего PostgreSQL в строке подключения.
- **Swagger не открывается:** запустите профиль `http` или `https` с `ASPNETCORE_ENVIRONMENT=Development`.
