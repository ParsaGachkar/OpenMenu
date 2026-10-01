# AGENTS.md — working agreements for this repo

Lessons from real mistakes made in this codebase. Read this before changing anything.

## Verification workflow (TDD)

1. **Reproduce with a failing test first.** The Playwright E2E suite
   (`tests/OpenMenu.E2ETests`) exists precisely so you never hand-verify UI
   behavior. If a bug is reported and no failing test covers it, write the
   test, run it, watch it fail, then fix and watch it pass.
2. **Never debug HTTP flows with curl.** ASP.NET Core antiforgery tokens,
   `_handler` routing, and cookie auth cannot be hand-assembled reliably;
   that path wasted hours. Use the E2E suite — it logs in through the real
   form and drives a real browser.
3. **Never read validation output from long-lived background logs blindly.**
   Prefer a single `dotnet test` / `dotnet build` invocation and read its
   output.

## Timeouts

- Terminal commands must stay under **20 seconds**. Builds and test runs are
  long: start them in the background (`nohup ... > log 2>&1 &`), then poll the
  log with short sleeps.

## Blazor / EF Core pitfalls (all hit for real)

- **DbContext is not thread-safe.** During SSR of an `InteractiveAuto` page,
  the layout and the page initialize concurrently on the same circuit; if both
  issue async queries on the *same scoped* `AppDbContext`, you get
  "A second operation was started on this context instance". Keep async data
  fetching out of layouts that render on server circuits (or use
  `AddDbContextFactory` and one context per call).
- **Ordering at startup:** `MigrateAsync()` must run *before* any code reads
  newly added columns. The first deploy of the translations feature crashed
  because the default-culture read preceded migrations.
- **WASM and server must both compile.** `IAdminApi` has two implementations
  (`AdminApiServer`, `AdminApiClient`); adding an interface member breaks both
  until implemented.
- **Enhanced navigation breaks culture switching.** Anchors to `/culture/set`
  must carry `data-enhance-nav="false"`: otherwise Blazor patches the DOM and
  the running WebAssembly runtime keeps its old `CultureInfo` — the page stays
  in the previous language until the user manually refreshes.
- **Kestrel caps request bodies at 30 MB by default.** The 50 MB video upload
  died with a connection reset mid-flight until
  `ConfigureKestrel(o => o.Limits.MaxRequestBodySize = ...)` was raised.
- **Serve media with the right content type.** The video endpoint must derive
  the type from the stored extension (webm/ogv/mov/mp4) — hard-coding
  `video/mp4` breaks playback in some browsers.
- **204 NoContent kills `GetFromJsonAsync`.** Endpoints whose body may be null
  (e.g. GET video of an item without one) must return `Results.Ok(null)` and
  the client must check `Content-Length` before parsing.
- **`ImageFile.Id` is `ValueGeneratedNever`** — always set `Id =
  Guid.NewGuid()` explicitly, or the second upload hits a duplicate-PK error.

## Playwright patterns (all hit for real)

- **One `Page.Dialog` handler per test**, with a flag + `TaskCompletionSource`
  for the message: subscribe once, `await` the dialog *before* flipping the
  accept flag. Two concurrent handlers race and wrongly dismiss dialogs; and
  flipping the flag without awaiting can accept the wrong dialog first.
- **Wait for WASM after every admin navigation** (`WaitForBlazorReadyAsync`):
  wait for `Blazor.runtime`/`Blazor._internal` (not just `typeof Blazor !==
  'undefined'`) plus a settle window, or SSR-phase clicks get swallowed.
- **Number inputs reformat on re-render.** A price filled as `99000` comes
  back as `99000.00` (step=0.01); assert against already-2-decimal values or
  use N0-style currencies.
- **Strict mode:** `GetByRole(Name = "Save")` also matches "Save translation";
  use `Exact = true` when a page has similar buttons.
- **CI is slower than dev machines.** The fixture timeout is env-tunable
  (`OPENMENU_TIMEOUT`, seconds) so CI can raise it instead of failing every
  admin test.
- **Never wait for `NetworkIdle` as a login/redirect gate.** A WebAssembly
  boot keeps circuits/assets in flight and `NetworkIdle` intermittently never
  settles → 20s hangs that come and go. Wait for a URL/element instead.
- **Write base-URL-agnostic tests.** Resolve target URLs from `Page`/fixture
  base URL (`**` globs), not a hard-coded `http://localhost:8088` — tests run
  against `127.0.0.1` locally and `localhost` in CI.
- **Nesting gotchas in role queries:** a row's `GetByRole(Link, "Edit")`
  resolves to the row's edit link, but on `article` cards `img`/`video` counts
  are the card-level media assertions (home shows ONE media: image XOR video).
- **Detail-page gallery hooks:** `[data-gallery-main]` = active slide box,
  `[data-gallery-thumbs] [data-thumb]` = switcher (`.thumb-active` marks the
  current), `[data-main-image]` = active image. The video is a regular slide
  (`.thumb-active` moves onto it too). The island is `InteractiveServer`, so
  `WaitForBlazorReadyAsync` is required before clicking thumbs.
- **bUnit for admin editors:** register `Services.AddLocalization()` (the
  components inject `IStringLocalizer<SharedResource>`) and a fake `IAdminApi`
  — never copy component markup into a test double. `InputText` renders no
  `type` attribute; drive it via `FindComponents<InputText>()` +
  `ValueChanged.InvokeAsync`, not CSS input selectors.

## Test layers

- `OpenMenu.Tests` — pure unit (hasher, PriceFormatter).
- `OpenMenu.ComponentTests` — bUnit against the **real** components from
  `OpenMenu.Web.Client`. Do not write test doubles that copy a component's
  markup: they test the copy, not the component (antipattern).
- `OpenMenu.IntegrationTests` — `WebApplicationFactory<Program>` with the
  external dependency mocked: PostgreSQL is swapped for in-memory SQLite
  (held-open connection) and `Database:EnsureCreated=true` skips Npgsql
  migrations. Auth is a fake scheme with an `X-Test-Role` header. Anything
  needing the real database belongs in E2E, not here.
- `OpenMenu.E2ETests` — Playwright; each test creates its own rows with a
  known prefix ("E2E ", "Confirm ", "Detail ", "Gal ", "Card ", ...) that the
  fixture cleanup deletes afterwards; run against `docker compose up`.

## Localization

- UI strings live in `src/OpenMenu.Web.Client/Resources/SharedResource*.resx`.
  Add new keys to **all four** files (en, fa, tr, ar); the neutral file doubles
  as the fallback. Keep keys duplicate-free (resx breaks silently on dupes).
- Menu content translations are data (per-culture rows), not resx.
- **Per-culture prices carry their own currency** (`MenuItemTranslation.Price`
  + `.Currency`, null = restaurant default from settings). Public pages render
  `Format(translation.Price ?? item.Price, translation.Currency ?? settings.Currency)`.
  A blank price always clears the stored currency (server rule) — no half-cleared
  rows. The currency list lives in `CurrencyOptions.All` (shared by Settings and
  the translation editor; TOMAN intentionally offered alongside ISO 4217 codes).
- **Never hardcode English in admin pages.** Every visible string — page
  titles, empty states, buttons, role/option labels, error toasts — goes
  through `L["..."]`. Hardcoded strings are the #1 cause of "i18n is broken"
  reports; the fa E2E flow tests (`AdminLocalizationTests`) catch them via
  `DoesNotContain` assertions on the rendered body text.

## Prerender-safe components

- Static components (no `@rendermode`) cannot fetch async data without
  blocking; prefer parameters injected by the host or synchronous context
  reads, as `MainLayout` does for enabled cultures.

## Branding assets & CI/CD

- Branding assets live in **`docs/images/`** (logo.png, logo.svg,
  screenshot-mobile.png) and are referenced from the README only — never
  re-add loose images at the repo root.
- Runtime assets ship from **`src/OpenMenu.Web/wwwroot/`** (`favicon.ico`,
  `icons/icon-192.png`, `icons/apple-touch-icon.png`, generated from the
  logo). `App.razor` links them; do not revert to the inline SVG data-URI
  favicon. If the logo changes, regenerate the icons (ffmpeg scale works
  fine) and keep filenames stable.
- Two workflows: `.github/workflows/ci.yml` (build + all test layers) and
  `.github/workflows/docker.yml` (GHCR publish). The docker workflow publishes
  on pushes to `main` and `v*` tags (`latest` + semver + sha tags, built-in
  `GITHUB_TOKEN`, no PAT needed) and is build-only on PRs. After publishing
  it smoke-tests the fresh image against a disposable Postgres.
- Version tags must be `v`-prefixed (`v1.2.3`) for the semver tag rules to
  fire.
- `github.repository` keeps the case used on GitHub (`ParsaGachkar/OpenMenu`);
  GHCR refs must be lowercase — lowercase before any `docker run`/`docker tag`
  (bash `${VAR,,}`), even though `docker/build-push-action` tolerates it.
