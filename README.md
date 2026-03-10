# Installer & Updater — Windows-сервисы управления и обновления агента

[![CI](https://github.com/rzwnz/installerupdater/actions/workflows/ci.yml/badge.svg)](https://github.com/rzwnz/installerupdater/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/rzwnz/installerupdater/branch/main/graph/badge.svg?flag=installerupdater)](https://codecov.io/gh/rzwnz/installerupdater)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![InnoSetup](https://img.shields.io/badge/Inno%20Setup-6.x-1B365D)
![Windows Service](https://img.shields.io/badge/Windows-Service-0078D6)

---

## Оглавление

1. [Назначение](#1-назначение)
2. [Архитектура](#2-архитектура)
3. [Структура проекта](#3-структура-проекта)
4. [Требования](#4-требования)
5. [Сборка](#5-сборка)
6. [Конфигурация](#6-конфигурация)
7. [Компоненты](#7-компоненты)
8. [Миграции базы данных](#8-миграции-базы-данных)
9. [Ключи реестра Windows](#9-ключи-реестра-windows)
10. [Процесс обновления](#10-процесс-обновления)
11. [Инсталлятор](#11-инсталлятор)
12. [Тестирование](#12-тестирование)
13. [Деплой](#13-деплой)
14. [Частые проблемы](#14-частые-проблемы)

---

## 1. Назначение

Проект содержит три компонента для управления локальным агентом на Windows-машинах в инфраструктуре с сервером Astra Linux:

| Компонент | Описание |
|-----------|----------|
| **InstallerService** | Windows-сервис: heartbeat с Tomcat, авторизация через AD, локальная БД (SQLite / PostgreSQL), хранение состояния в реестре |
| **InstallerUpdater** | Windows-сервис: опрашивает сервер обновлений Astra Linux, скачивает бинарники с проверкой SHA-256, останавливает/обновляет/перезапускает основной сервис с автоматическим откатом при сбое |
| **InstallerUpdaterSetup** | Inno Setup 6 инсталлятор: регистрирует оба сервиса, задаёт ключи реестра, создаёт директории, запускает миграции БД |

---

## 2. Архитектура

```
 ┌──────────────────────────────────────────────────┐
 │                Сервер Astra Linux                │
 │  ┌────────────┐  ┌──────────────────────────┐    │
 │  │  Tomcat/AD │  │  Сервер обновлений       │    │
 │  │  :8080     │  │  /api/updates/latest     │    │
 │  └─────┬──────┘  └──────────┬───────────────┘    │
 └────────┼────────────────────┼────────────────────┘
          │  HTTP              │  HTTP (polling 60 мин)
 ┌────────┼────────────────────┼───────────────────┐
 │        ▼                    ▼    Windows-клиент │
 │  ┌──────────────┐   ┌──────────────┐            │
 │  │InstallerSer- │   │InstallerUp-  │            │
 │  │vice (Worker) │   │dater (Poll)  │            │
 │  └──────┬───────┘   └──────┬───────┘            │
 │         │                  │                    │
 │    ┌────▼────┐         ┌───▼───────┐            │
 │    │ SQLite  │         │ Скачать   │            │
 │    │ / Post- │         │ → SHA-256 │            │
 │    │ greSQL  │         │ → Бэкап   │            │
 │    └─────────┘         │ → /SILENT │            │
 │                        │ → Откат?  │            │
 │    ┌─────────────────┐ └───────────┘            │
 │    │ Реестр Windows  │                          │
 │    │ HKLM\SOFTWARE\  │                          │
 │    │ rzwnz\Installer │                          │
 │    │ Service         │                          │
 │    └─────────────────┘                          │
 └─────────────────────────────────────────────────┘
```

---

## 3. Структура проекта

```
installerupdater/
├── InstallerUpdater.sln             # Solution-файл (.NET 8)
├── build.bat / build.sh             # Скрипты сборки (Windows / кросс-компиляция)
├── deploy.bat                       # Деплой на сервер обновлений
├── HOW-IT-WORKS.md                  # Подробное техническое описание (672 строки)
├── installer/
│   └── InstallerUpdaterSetup.iss    # Скрипт Inno Setup 6 инсталлятора
└── src/
    ├── InstallerService/            # Основной Windows-сервис
    │   ├── Program.cs               # DI, Serilog, Windows Service хостинг
    │   ├── appsettings.json         # Конфигурация
    │   ├── Database/                # IDatabaseMigrator, SqliteMigrator, PostgresMigrator
    │   ├── Migrations/              # V001__Initial_schema.sql, V002__Update_history.sql
    │   ├── Models/                  # HeartbeatResult, InstallerServiceOptions, MigrationRecord, RegistryEntry
    │   ├── Registry/                # IRegistryManager, WindowsRegistryManager, InMemoryRegistryManager
    │   └── Services/                # InstallerWorker, TomcatClient, FileSystemService (+ интерфейсы)
    ├── InstallerUpdater/            # Сервис опроса обновлений
    │   ├── Program.cs               # DI, Serilog, Windows Service хостинг
    │   ├── appsettings.json
    │   ├── Models/                  # UpdateManifest, UpdateResult, UpdaterOptions
    │   └── Services/                # UpdateWorker, UpdateServerClient, UpdateApplier,
    │                                # VersionComparer, RegistryInstalledVersionProvider (+ интерфейсы)
    └── InstallerService.Tests/      # xUnit-тесты (78 тестов, 7 классов)
        ├── Database/                # SqliteMigratorTests (12 тестов)
        ├── Registry/                # InMemoryRegistryManagerTests (16 тестов)
        └── Services/                # TomcatClientTests (10), FileSystemServiceTests (12),
                                     # VersionComparisonTests (16), ModelTests (7), UpdaterModelTests (5)
```

---

## 4. Требования

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Inno Setup 6](https://jrsoftware.org/isdown.php) (для сборки инсталлятора)
- Windows 10+ (для установки сервисов и работы с реестром)

---

## 5. Сборка

### Windows

```cmd
build.bat
```

### Linux (кросс-компиляция)

```bash
chmod +x build.sh
./build.sh
```

Оба скрипта выполняют одинаковый 5-шаговый pipeline:

1. Очистка `publish/`
2. `dotnet restore` — восстановление NuGet-пакетов
3. `dotnet build -c Release` — сборка решения
4. `dotnet test` — запуск 78 unit-тестов
5. `dotnet publish` — публикация **self-contained single-file** win-x64 бинарников (`PublishSingleFile=true`)

Результат: `publish/InstallerService/` и `publish/InstallerUpdater/`

### Сборка инсталлятора

```cmd
iscc installer\InstallerUpdaterSetup.iss
```

Результат: `installer/output/InstallerUpdaterSetup-1.0.0.exe` (LZMA2/ultra64 сжатие)

---

## 6. Конфигурация

### InstallerService (`appsettings.json`)

| Ключ | По умолчанию | Описание |
|------|-------------|----------|
| `TomcatBaseUrl` | `http://localhost:8080` | URL сервера Tomcat |
| `HeartbeatIntervalSeconds` | `30` | Интервал heartbeat |
| `AdDomain` | `AD_DOMAIN` | Домен Active Directory |
| `DatabaseProvider` | `sqlite` | Провайдер миграций (`sqlite` / `postgres`) |
| `LocalDbPath` | `C:\ProgramData\InstallerUpdater\installer.db` | Путь к БД SQLite |
| `PostgresConnectionString` | `""` | Строка подключения к PostgreSQL |
| `WorkingDirectory` | `C:\ProgramData\InstallerUpdater` | Рабочая директория |
| `RegistryBasePath` | `SOFTWARE\rzwnz\InstallerService` | Путь в реестре |

### InstallerUpdater (`appsettings.json`)

| Ключ | По умолчанию | Описание |
|------|-------------|----------|
| `UpdateServerUrl` | `http://update-server.local` | Сервер обновлений (Astra Linux) |
| `PollIntervalMinutes` | `60` | Интервал опроса (мин.) |
| `DownloadDirectory` | `C:\ProgramData\InstallerUpdater\updates` | Директория загрузки |
| `TargetServiceName` | `InstallerService` | Имя целевого Windows-сервиса |
| `MaxRetries` | `3` | Число попыток скачивания |
| `HttpTimeoutSeconds` | `120` | Таймаут HTTP-запросов |
| `SilentInstall` | `true` | Тихая установка (без UI) |
| `InstallerPath` | `...updates\InstallerUpdaterSetup.exe` | Путь к файлу инсталлятора |

---

## 7. Компоненты

### InstallerService

Основной Windows-сервис, работающий как `BackgroundService` (.NET Generic Host).

**DI-регистрации:**

| Регистрация | Интерфейс → Реализация | Lifetime |
|-------------|------------------------|----------|
| Конфигурация | `IOptions<InstallerServiceOptions>` | Options |
| Реестр (Windows) | `IRegistryManager` → `WindowsRegistryManager` | Singleton |
| Реестр (не Windows) | `IRegistryManager` → `InMemoryRegistryManager` | Singleton |
| Файловая система | `IFileSystemService` → `FileSystemService` | Singleton |
| Мигратор БД | `IDatabaseMigrator` → `SqliteMigrator` / `PostgresMigrator` (factory) | Singleton |
| HTTP-клиент | `ITomcatClient` → `TomcatClient` | Typed HttpClient |
| Worker | `InstallerWorker` | HostedService |

**Логирование:** Serilog — Console + rolling file (`%ProgramData%\InstallerUpdater\logs\installer-service-.log`, retention 30 дней).

**CLI-режим:** `--migrate` — запуск только миграций без запуска worker loop.

### InstallerUpdater

Облегчённый Windows-сервис опроса обновлений.

**DI-регистрации:**

| Регистрация | Интерфейс → Реализация | Lifetime |
|-------------|------------------------|----------|
| Конфигурация | `IOptions<UpdaterOptions>` | Options |
| HTTP-клиент | `IUpdateServerClient` → `UpdateServerClient` | Typed HttpClient |
| Версия | `IInstalledVersionProvider` → `RegistryInstalledVersionProvider` | Singleton |
| Сравнение версий | `IVersionComparer` → `VersionComparer` | Singleton |
| Применение обновлений | `IUpdateApplier` → `UpdateApplier` | Singleton |
| Worker | `UpdateWorker` | HostedService |

**Логирование:** Serilog — Console + rolling file (`updater-.log`, retention 14 дней).

---

## 8. Миграции базы данных

Миграции именуются по шаблону `V{NNN}__{Описание}.sql` и применяются автоматически при запуске сервиса (аналогично Flyway).

| Миграция | Описание |
|----------|----------|
| `V001__Initial_schema.sql` | Начальная схема |
| `V002__Update_history.sql` | Таблица истории обновлений |

Мигратор:
- Отслеживает применённые миграции в таблице `__migrations`
- Выполняет миграции **в транзакциях** с автоматическим откатом при ошибке
- Поддерживает **SQLite** (локально) и **PostgreSQL** (удалённо)

---

## 9. Ключи реестра Windows

Сервис хранит рабочее состояние в `HKLM\SOFTWARE\rzwnz\InstallerService`:

| Значение | Тип | Описание |
|----------|-----|----------|
| `Version` | String | Версия установленного сервиса |
| `InstallPath` | String | Директория установки |
| `DataPath` | String | Путь к данным |
| `Status` | String | Running / Stopped |
| `LastHeartbeat` | String | ISO 8601 timestamp |
| `TomcatStatus` | String | OK / Unreachable |
| `DbVersion` | String | Текущая версия схемы БД |
| `AutoUpdate` | DWORD | 1 если updater установлен |

---

## 10. Процесс обновления

1. **InstallerUpdater** опрашивает `GET /api/updates/latest` на сервере Astra Linux (каждые 60 мин.)
2. Если доступна новая версия — скачивает EXE-инсталлятор
3. **Проверяет SHA-256** контрольную сумму
4. Останавливает основной сервис (`sc stop`)
5. **Создаёт бэкап** текущей установки
6. Запускает инсталлятор с флагом `/VERYSILENT`
7. Перезапускает основной сервис
8. **При сбое — автоматический откат из бэкапа**

---

## 11. Инсталлятор

Inno Setup 6, 234 строки. Основные характеристики:

- **Минимум:** Windows 10 (x64)
- **Языки:** Английский, Русский
- **Типы установки:** Full (оба сервиса), Service Only, Custom
- **Регистрация сервисов:** через `sc.exe create` с автозапуском и recovery (restart 5/10/30 сек.)
- **Пост-установка:** запуск `--migrate` для миграций БД, затем старт обоих сервисов
- **Деинсталляция:** остановка сервисов → удаление → опциональная очистка данных
- **Директории:** logs, updates, migrations (sqlite + postgres), backup — ACL `admins-full system-full`
- **Сжатие:** LZMA2/ultra64

---

## 12. Тестирование

```bash
dotnet test src/InstallerService.Tests/ --verbosity normal
```

**78 тестов, 7 классов, 0 failures.**

| Класс | Тестов | Что проверяет |
|-------|--------|---------------|
| `InMemoryRegistryManagerTests` | 16 | Чтение/запись/удаление ключей, нечувствительность к регистру |
| `VersionComparisonTests` | 16 | Семантическое сравнение версий для определения обновлений |
| `SqliteMigratorTests` | 12 | Один/несколько миграций, идемпотентность, ошибки, отмена (реальная SQLite) |
| `FileSystemServiceTests` | 12 | Операции с директориями/файлами, очистка, подсчёт размера (реальная FS) |
| `TomcatClientTests` | 10 | Heartbeat, авторизация, flash-сообщения, устойчивость к ошибкам (mock HTTP) |
| `ModelTests` | 7 | Значения по умолчанию, назначение свойств InstallerService DTO |
| `UpdaterModelTests` | 5 | UpdateManifest, UpdateResult, UpdaterOptions |

---

## 13. Деплой

```cmd
deploy.bat
```

Выполняет:
1. Вычисляет **SHA-256** хеш инсталлятора (`certutil -hashfile`)
2. Генерирует `manifest.json` (версия, URL скачивания, хеш, release notes, mandatory flag)
3. Загружает инсталлятор + `latest.json` на сервер обновлений Astra Linux через **SCP**

Целевой сервер: `update-server.local` → `/var/www/updates/installerupdater`

---

## 14. Частые проблемы

| Проблема | Решение |
|----------|---------|
| Сервис не запускается | Проверить Event Viewer → Applications; убедиться что .NET 8 Runtime установлен |
| SQLite locked | Убедиться что только один экземпляр сервиса запущен |
| Tomcat недоступен | Проверить `TomcatBaseUrl` в `appsettings.json`, firewall на порту 8080 |
| Обновление не применяется | Проверить `UpdateServerUrl`, SHA-256 манифеста; просмотреть `updater-.log` |
| Откат после обновления | Проверить наличие бэкапа, логи updater; при необходимости переустановить через инсталлятор |
| Миграция PostgreSQL не работает | Проверить `PostgresConnectionString`, доступность сервера; убедиться что `DatabaseProvider=postgres` |

---

## NuGet-зависимости

### InstallerService

| Пакет | Версия | Назначение |
|-------|--------|------------|
| Microsoft.Extensions.Hosting | 8.0.1 | Generic Host |
| Microsoft.Extensions.Hosting.WindowsServices | 8.0.1 | Windows Service хостинг |
| Microsoft.Extensions.Http | 8.0.1 | IHttpClientFactory |
| Microsoft.Data.Sqlite | 8.0.11 | SQLite-провайдер |
| Npgsql | 8.0.6 | PostgreSQL-провайдер |
| Serilog.Extensions.Hosting | 8.0.0 | Интеграция Serilog |
| Serilog.Sinks.File | 6.0.0 | Rolling file логи |
| Serilog.Sinks.Console | 6.0.0 | Console логи |
| System.ServiceProcess.ServiceController | 8.0.1 | Управление Windows-сервисами |

### InstallerUpdater

| Пакет | Версия | Назначение |
|-------|--------|------------|
| Microsoft.Extensions.Hosting | 8.0.1 | Generic Host |
| Microsoft.Extensions.Hosting.WindowsServices | 8.0.1 | Windows Service хостинг |
| Microsoft.Extensions.Http | 8.0.1 | IHttpClientFactory |
| Serilog.Extensions.Hosting | 8.0.0 | Интеграция Serilog |
| Serilog.Sinks.File | 6.0.0 | Rolling file логи |
| Serilog.Sinks.Console | 6.0.0 | Console логи |
| System.ServiceProcess.ServiceController | 8.0.1 | Управление Windows-сервисами |
