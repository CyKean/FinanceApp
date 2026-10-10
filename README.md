# FinanceApp

FinanceApp is an offline-first personal finance manager built with .NET MAUI for Android, iOS, macOS Catalyst, and Windows. It keeps a full picture of your money — accounts, transactions, budgets, goals, and recurring payments — on a local SQLite database first, then keeps everything in sync with the cloud through Supabase so your data follows you across devices.

Everything works without a connection: log spending on the go, review budgets and forecasts offline, and let the app reconcile automatically the next time you're online. An optional AI assistant is built in for answering money questions and drafting budget ideas from your own financial data.

## Features

### Money management
- **Dashboard** — at-a-glance balance, income vs. expense, and budget summaries with gradient hero cards.
- **Transactions** — quick add/edit of income and expense entries with notes, categories, accounts, filters, and search.
- **Accounts** — multiple accounts (cash, bank, e-wallet) with running balances and account history.
- **Categories** — customizable income and expense categories with icons and colors.
- **Budgets** — per-category spending limits with live spent/remaining tracking, progress indicators, and over-budget alerts.
- **Goals** — savings targets with progress tracking, target dates, and on-track projections.
- **Recurring transactions** — daily/weekly/monthly/yearly schedules that auto-generate due entries.
- **Calendar view** — transactions laid out on a monthly calendar for spotting patterns.

### Insights
- **Analytics** — spending breakdowns and trend charts across categories and time periods.
- **Predictions** — forward-looking cash-flow estimates based on your history and recurring items.

### AI assistant
- **Chat** — ask questions about your finances and get answers grounded in your own data.
- **Budget suggestions** — AI-drafted budget proposals you can review and apply.
- **Bring your own key** — works with any OpenAI-compatible API (base URL, model, and key are configurable in the app).

### Offline-first sync
- **Works offline** — all features read and write to a local SQLite database.
- **Automatic outbox sync** — every change is queued locally and pushed to Supabase when connectivity returns; pull-merge resolves conflicts with last-write-wins semantics.
- **Sync history** — a visible log of queued, synced, and failed operations with retry.
- **Periodic background sync** — optional auto-sync with a manual "Sync Now" fallback.

### Accounts & security
- **Supabase Auth** — real email/password sign-up and sign-in with persistent sessions and identity migration from local accounts.
- **Per-user data isolation** — every record is scoped to the signed-in user.

### Experience
- Light and dark themes, toasts, confirmation modals, success overlays, page transitions, and pull-to-refresh throughout.

## Tech stack

| Layer | Technology |
| --- | --- |
| UI | .NET MAUI (XAML), CommunityToolkit.Mvvm |
| Runtime | .NET 10 (`net10.0-android`, `-ios`, `-maccatalyst`, `-windows`) |
| Local storage | EF Core + SQLite |
| Cloud | Supabase (PostgreSQL + Auth) |
| AI | OpenAI-compatible chat completions API |
| Tests | xUnit + Moq |

## Architecture

Clean Architecture solution with a strict dependency direction:

```
FinanceApp.slnx
├── src/
│   ├── FinanceApp.Domain          # Entities, value objects, enums, interfaces
│   ├── FinanceApp.Application     # Services, DTOs, validators, sync engine
│   ├── FinanceApp.Infrastructure  # EF Core, repositories, Supabase, AI client
│   └── FinanceApp.Mobile          # .NET MAUI pages, view models, controls
└── tests/
    ├── FinanceApp.UnitTests
    ├── FinanceApp.IntegrationTests
    └── FinanceApp.SynchronizationTests
```

## Getting started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download) with the MAUI workload:
  ```
  dotnet workload install maui
  ```
- For Android: Android SDK + an emulator or device (Visual Studio 2026 with the .NET MAUI workload also works).
- A Supabase project (free tier is fine) for auth and sync. Schema can be applied from `supabase-schema.sql`.

### Setup

1. Clone the repository:
   ```
   git clone https://github.com/CyKean/FinanceApp.git
   cd FinanceApp
   ```

2. Create your local configuration (the example file is committed; `appsettings.json` is git-ignored):
   ```
   copy src\FinanceApp.Mobile\appsettings.example.json src\FinanceApp.Mobile\appsettings.json
   ```
   Then fill in your Supabase URL and anon key, and optionally your AI provider settings:

   | Key | Description |
   | --- | --- |
   | `Database:SupabaseUrl` | Your Supabase project URL |
   | `Database:SupabaseAnonKey` | Supabase anon (public) API key |
   | `Database:SqliteConnectionString` | Local SQLite database file |
   | `Database:SeedDemoData` | Seed demo data on first run |
   | `Ai:BaseUrl` | OpenAI-compatible API base URL |
   | `Ai:Model` | Chat model name |
   | `Ai:TimeoutSeconds` | AI request timeout |

3. Build and run:
   ```
   dotnet build FinanceApp.slnx
   dotnet build src/FinanceApp.Mobile/FinanceApp.Mobile.csproj -f net10.0-android -t:Run
   ```

### Tests

```
dotnet test tests/FinanceApp.UnitTests/FinanceApp.UnitTests.csproj
```

### Android release signing

Release builds are signed with the production key, and that key must never change:
if it does, no later APK can update an already-installed Finora. The keystore and
its passwords are deliberately **not** in this repository. Supply them as
environment variables before building:

```
set FinoraKeyStorePath=C:\path\to\finora-release.keystore
set FinoraKeyAlias=finora
set FinoraKeyStorePassword=<store password>
set FinoraKeyPassword=<key password>
```

Then build the signed APK:

```
dotnet publish src/FinanceApp.Mobile/FinanceApp.Mobile.csproj -f net10.0-android -c Release
```

If those variables are missing, the Release build fails on purpose rather than
falling back to the shared debug key.

**Where the key lives.** It is kept outside the repository, under
`%USERPROFILE%\.finora\android-signing\`:

```
finora-release.keystore
credentials.txt      <- alias and both passwords, in plaintext
```

If you ever cannot find it, that folder is the first place to look. If the whole
folder is genuinely gone, the app cannot be updated in place: you would have to
change `ApplicationId` and ask every existing user to uninstall and reinstall,
losing their local data. **Back the folder up off this machine** - cloud storage
or a password manager, anywhere but this disk. `credentials.txt` is plaintext by
necessity (the build has to read it), so it is worth putting a copy somewhere you
would not mind losing the laptop.

**Confirm the key before you publish.** A wrong or substituted key produces an
APK that installs cleanly in a fresh sandbox and then refuses to update any real
install - the failure only shows up in the field. Two certificates must match
exactly:

```
keytool -list -v -keystore %FinoraKeyStorePath% -alias %FinoraKeyAlias% -storepass %FinoraKeyStorePassword%
```

Compare the SHA-256 it prints against the signer of the APK already published,
which is the certificate embedded in the release's `META-INF/*.RSA`. For v1.0.0
and v1.0.1 that value is:

```
AE:6F:A9:BD:55:8C:8C:53:92:06:D2:D3:F9:21:0C:E9:A7:7F:D3:BD:17:FC:2D:19:ED:04:91:3E:D3:AA:96:0D
```

Release APKs are published through GitHub Releases. To cut a release:

1. Bump `ApplicationDisplayVersion` and `ApplicationVersion` in
   `src/FinanceApp.Mobile/FinanceApp.Mobile.csproj`. `ApplicationVersion` must
   increase on every release or Android refuses the install.
2. Commit, then tag the matching commit `vMAJOR.MINOR.PATCH`.
3. **Attach the APK to the release.** This is not optional: the app's own update
   prompt reads the release's `.apk` asset for the download link, so a published
   release with no APK leaves every installed copy showing "a newer version is
   available" with a Download button that goes nowhere. Prepare a release as a
   draft until the APK is uploaded - drafts are excluded from the
   `/releases/latest` endpoint the app polls, so a draft is never surfaced to
   users.

### In-app updates

Finora is not on the Play Store, so Google's in-app update service is not
available to it. Instead the app checks the public GitHub releases endpoint for
its own repository and, when a newer version has been published, shows a prompt
when the dashboard appears. Like Play Store's own in-app updates, nothing is
pushed to the device: the app finds out the next time it is opened.

Tapping **Download** opens the release APK in the browser, from where Android's
installer takes over. Installing over an existing Finora requires the same
signing key and a higher `ApplicationVersion`, which the release process above
guarantees.

Configure it under the `AppUpdate` section of `appsettings.json`:

| Key | Description |
| --- | --- |
| `AppUpdate:Enabled` | Set to `false` to switch the prompt off |
| `AppUpdate:Owner` | Repository owner |
| `AppUpdate:Repository` | Repository name |
| `AppUpdate:CacheHours` | How long a check result is trusted |
| `AppUpdate:TimeoutSeconds` | Network timeout for the check |

The cache matters: GitHub allows only 60 unauthenticated API requests per hour
per IP address, and every Finora install on a network shares that budget. A
result is therefore reused for `CacheHours` (default 12), and a user who taps
**Later** is not asked again about the same version. `Settings` also has a
manual **Check for updates** button that bypasses the cache.

The repository must stay public for this to work; a private repository's release
endpoint requires authentication, and embedding a token in the APK would leak it
to anyone who unzips the app.
