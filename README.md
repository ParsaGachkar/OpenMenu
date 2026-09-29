# OpenMenu

A self-hosted, single-restaurant digital menu built with .NET 10, Blazor, EF Core, PostgreSQL, Tailwind CSS and DaisyUI.

## Stack

- .NET 10 — Blazor Web App (server-side SSR, no WebAssembly)
- EF Core 10 + PostgreSQL (Npgsql)
- Tailwind CSS v4 + DaisyUI 5
- Cookie authentication — deliberately **no** ASP.NET Core Identity

## Project structure

```
OpenMenu/
├── src/
│   ├── OpenMenu.Web/              Blazor UI, auth, localization, admin pages
│   ├── OpenMenu.Domain/           Entities + UserRole enum (no dependencies)
│   ├── OpenMenu.Application/      Reserved for future use-case logic (intentionally empty)
│   └── OpenMenu.Infrastructure/   EF Core DbContext, migrations, password hasher, seeder
├── tests/
│   ├── OpenMenu.Tests/            Unit tests (password hasher)
│   └── OpenMenu.E2ETests/         Playwright end-to-end tests (real browser)
├── .github/workflows/ci.yml       Build + unit tests + E2E (Docker Compose)
├── docker-compose.yml
├── Dockerfile
└── README.md
```

## Features

- **Public menu** (`/`) — restaurant name, description, categories, items, prices, availability. Mobile-first, responsive, themeable.
- **Admin area** (`/admin`) — dashboard, full CRUD for categories and menu items, restaurant settings (name, description, logo, currency, DaisyUI theme), user management (Admin only).
- **Authentication** — cookie-based login with roles (Admin, Editor), no registration, no Identity framework.
- **Localization** — English, Farsi (فارسی), Turkish, Arabic via the standard ASP.NET Core localization stack (resx + `IStringLocalizer` + `RequestLocalization`). Right-to-left layouts are applied automatically for Farsi and Arabic. Users pick a language from the header; the choice is stored in the standard culture cookie.

## Authentication

Small, explicit, and no Identity framework:

- Passwords hashed with **PBKDF2-SHA512** (210,000 iterations, 16-byte random salt, per-call salted) via `OpenMenu.Infrastructure/Auth/PasswordHasher.cs`. Stored format: `iterations.salt.hash` (Base64). Plaintext is never stored or logged; hashes are never rendered.
- Login is a static-SSR Blazor form (`/login`) validated by `AuthService`. Unknown username, wrong password, and disabled account all return the same generic error — no user enumeration.
- Cookie auth (`OpenMenu.Auth`, HttpOnly, SameSite=Lax, 7 days). Data Protection keys persist to a mounted volume in Docker so sessions survive restarts.
- Roles come from the `UserRole` enum (Admin, Editor) and are enforced with `[Authorize(Roles = ...)]` (see table below).
- No registration. The initial admin is seeded from configuration on startup.

| Route | Access |
|---|---|
| `/` | Public |
| `/login` | Public |
| `/admin` (dashboard) | Admin, Editor |
| `/admin/menu`, `/admin/categories` | Admin, Editor |
| `/admin/settings`, `/admin/users` | Admin only |

## Running with Docker Compose

```bash
cp .env.example .env      # then edit POSTGRES_PASSWORD and OPENMENU_ADMIN_PASSWORD
docker compose up -d --build
```

App: http://localhost:8088 (override with `APP_PORT` in `.env`).

On startup the app applies EF Core migrations and seeds the initial admin (from `OPENMENU_ADMIN_USERNAME` / `OPENMENU_ADMIN_PASSWORD`) plus demo restaurant data (`OPENMENU_SEED_DEMO_DATA=true`).

## Running locally

You need .NET 10 SDK, Node.js (for CSS), and a PostgreSQL instance:

```bash
# 1. CSS (Tailwind + DaisyUI)
cd src/OpenMenu.Web
npm install
npm run build:css

# 2. Run (dev seeds admin/admin123 and demo data via appsettings.Development.json)
dotnet run --project src/OpenMenu.Web
```

Connection string and seed credentials come from configuration; environment variables use the standard double-underscore form, e.g.:

```
ConnectionStrings__Default="Host=localhost;Port=5432;Database=openmenu;Username=postgres;Password=postgres"
Seed__AdminUsername=admin
Seed__AdminPassword=change-me
Seed__DemoData=true
```

## Localization

Implemented with the standard ASP.NET Core stack, no custom machinery:

- `AddLocalization()` + `UseRequestLocalization` with supported cultures `en`, `fa`, `tr`, `ar` (`Program.cs`).
- UI strings live in `src/OpenMenu.Web/Components/SharedResource.*.resx`.
- The header language selector is an interactive-server island that redirects through `/culture/set`, which sets the standard `.AspNetCore.Culture` cookie and bounces back to the current page (the documented redirect-based pattern).
- Validation messages are localized through `ErrorMessageResourceType`/`ErrorMessageResourceName` data annotations.
- `App.razor` sets `lang` and `dir` (RTL for `fa`/`ar`) on `<html>`; the admin-selected DaisyUI theme is applied as `data-theme`.

## Tests

```bash
# Unit tests
dotnet test tests/OpenMenu.Tests

# End-to-end tests (requires the app running at localhost:8088; override with OPENMENU_BASEURL,
# and a reachable PostgreSQL at localhost:5433 — both provided by `docker compose up`)
dotnet test tests/OpenMenu.E2ETests
```

The E2E suite drives a real browser (Playwright, system Chrome) through the public menu, login/logout, role gates, category and menu-item CRUD, settings + theme, culture switching, RTL, and localized validation messages.

## CI

`.github/workflows/ci.yml` runs on every push/PR: builds the solution, runs unit tests, boots the full stack with Docker Compose, waits for the app to become healthy, runs the Playwright E2E suite against it, and uploads browser test artifacts on failure.

## Notes

- `RestaurantSettings` is enforced single-row via a database check constraint (`Id = 1`).
- The EF migration (`InitialCreate`) is committed; migrations are applied automatically on startup.
- Category deletion cascades to its menu items (enforced in the database).
- No Redis, MinIO, RabbitMQ, multi-tenancy, or SaaS features — by design.
