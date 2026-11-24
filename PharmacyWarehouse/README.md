# Информационная система учёта товара на складе аптеки (C# / WinForms / SQLite)

Курсовой проект по дисциплине «Разработка кода информационных систем».

## Структура
```
PharmacyWarehouse.sln
├── src/PharmacyWarehouse.Core     — бизнес-логика и доступ к данным (net8.0, без зависимости от WinForms)
│   ├── Models/ · Data/Database.cs · Services/ (Auth, Product, Stock, Report, PasswordHasher, Session)
├── src/PharmacyWarehouse.UI       — Windows Forms (net8.0-windows)
├── tests/PharmacyWarehouse.Tests  — xUnit, 44 теста
└── docs/                          — пояснительная записка, руководство пользователя
```

## Запуск (Windows, .NET 8 SDK)
```
dotnet test
dotnet run --project src/PharmacyWarehouse.UI
```
База данных создаётся автоматически: `%LOCALAPPDATA%\PharmacyWarehouse\pharmacy.db`.
Учётная запись по умолчанию: **admin / Admin123!** (смените пароль после первого входа).

## Что проверено, а что нет
- `dotnet test` на Linux (.NET 8): **44 теста проходят**.
- Исходники UI компилируются (проверено сборкой против справочных сборок WinForms), но **само окно приложения не запускалось** — в среде разработки нет Windows. Скриншотов форм поэтому нет.
- Отклонение от ТЗ: вместо `System.Data.SQLite` использован `Microsoft.Data.Sqlite` (кроссплатформенный, поддерживается Microsoft). Остальное — по ТЗ.
