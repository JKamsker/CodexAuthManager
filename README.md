# Codex Auth Manager 🎛️

[![CI](https://github.com/JKamsker/CodexAuthManager/actions/workflows/ci.yml/badge.svg)](https://github.com/JKamsker/CodexAuthManager/actions/workflows/ci.yml)
[![Release](https://github.com/JKamsker/CodexAuthManager/actions/workflows/release.yml/badge.svg)](https://github.com/JKamsker/CodexAuthManager/actions/workflows/release.yml)
[![NuGet](https://img.shields.io/nuget/v/codex-tokens?color=512bd4&label=NuGet&logo=nuget)](https://www.nuget.org/packages/codex-tokens)
[![License](https://img.shields.io/badge/license-MIT-2ea44f.svg)](#-license)

> A batteries-included .NET global tool for wrangling every Codex auth token you own—safely, versioned, and with zero guesswork.

## ✨ Why you’ll love it

- **All tokens, one brain** – import every `*auth.json`, inspect metadata, and switch identities in seconds.
- **Immutable history** – every refresh becomes a new version, so rolling back feels like using Git for tokens.
- **Automated safety nets** – encrypted backups, environment separation, and guarded destructive actions.
- **Developer-friendly** – human-readable CLI, rich logs, and first-class Windows paths.
- **Shipping confidence** – GitHub Actions builds, tests, publishes NuGet, and cuts GitHub releases on every `master` push.

---

## 🚀 Install (global .NET tool)

```bash
# Fresh install
dotnet tool install --global codex-tokens

# Upgrade to the latest release
dotnet tool update --global codex-tokens
```

Need a local build? Run `dotnet pack src/CodexAuthManager.Cli/CodexAuthManager.Cli.csproj -c Release` and install from the produced `.nupkg`.

---

## ⚡ Quick start (TL;DR)

1. **Scan & import** every Codex token that lives under `%USERPROFILE%\.codex`  
   ```bash
   codex-tokens import
   ```
2. **List** the identities that were discovered (email, plan, last refresh, active flag).  
   ```bash
   codex-tokens list
   ```
3. **Activate** the identity you want to work with—this rewrites your live `auth.json` safely.  
   ```bash
   codex-tokens activate user@example.com
   ```
4. **Rollback** if a token stops working; previous versions are kept forever.  
   ```bash
   codex-tokens rollback user@example.com --version 2
   ```

🎯 _Every dangerous operation automatically snapshots the SQLite database to `%APPDATA%\CodexManager\backups` so you can undo mistakes._

---

## 🧠 Command cheatsheet

| Command | What it does |
| --- | --- |
| `codex-tokens import` | Scans for `*auth.json`, extracts JWT metadata, and versions each identity. |
| `codex-tokens list` | Prints every identity with plan, environment, and last updated timestamp. |
| `codex-tokens show [id|email]` | Displays the active identity by default or a specified one with all historical versions. |
| `codex-tokens activate [id|email]` | Makes the chosen identity live by writing its current token to `%USERPROFILE%\.codex\auth.json`. |
| `codex-tokens remove <targets>` | Deletes identities after taking a backup. |
| `codex-tokens rollback [target] [--version N]` | Creates a brand-new version using an older token payload. |
| `codex-tokens --dev <command>` | Runs every operation against the development storage paths listed below. |

---

## 🗂️ Storage layout & environments

| Scope | Codex folder | Database | Backups |
| --- | --- | --- | --- |
| **Production** | `%USERPROFILE%\.codex` | `%APPDATA%\CodexManager\tokens.db` | `%APPDATA%\CodexManager\backups` |
| **Development (`--dev`)** | `%APPDATA%\CodexManager-Dev\.codex` | `%APPDATA%\CodexManager-Dev\tokens-dev.db` | `%APPDATA%\CodexManager-Dev\backups` |

- Backups retain the 10 most recent snapshots.
- Every identity maintains a strictly increasing `VersionNumber`; nothing is ever mutated in place.
- JWT metadata (email, plan, subscription) is stored alongside tokens so you can search without decrypting anything.

---

## 🧪 Local development

```bash
# Restore dependencies
dotnet restore CodexAuthManager.sln

# Run unit tests
dotnet test CodexAuthManager.sln

# Launch the CLI without global install
dotnet run --project src/CodexAuthManager.Cli -- list
dotnet run --project src/CodexAuthManager.Cli -- --dev import
```

Project map:

```
CodexAuthManager/
├── src/
│   ├── CodexAuthManager.Core/   # database, services, abstractions
│   └── CodexAuthManager.Cli/    # Spectre.Console-powered CLI
├── tests/
│   └── CodexAuthManager.Tests/  # unit tests for core behaviors
└── docs/                        # design notes, plans, etc.
```

---

## 🤖 Automation & releases

- **CI** (`ci.yml`): restores, builds, tests, and packs the CLI on every push/PR.
- **Release** (`release.yml`): on `master`, GitVersion calculates semantic versions, the tool is packed, pushed to NuGet, and a GitHub Release is created with the `.nupkg` artifact attached.
- **Secrets**: the workflow pulls the NuGet API key from Bitwarden via `BW_ACCESS_TOKEN`, so no plaintext tokens live in the repo.

Every successful master push creates a new `codex-tokens` version on NuGet and tags the repo (`vX.Y.Z`).

---

## 🧭 Roadmap ideas

- Portable cross-platform path providers.
- Encrypted-at-rest database option.
- Rich TUI dashboards powered by Spectre Console canvases.
- First-class macOS/Linux support (community contributions welcome!).

---

## 🤝 Contributing

1. Fork the repo & create a feature branch.
2. Run `dotnet test` to keep CI happy.
3. Open a PR—screenshots or sample outputs make reviews a breeze.

Bug reports and feature requests are equally appreciated. If the tool saved you time, drop a ⭐!

---

## 📄 License

MIT © JKamsker — see [`LICENSE`](LICENSE) once added.

---

Made with ☕, Spectre.Console, and a love for well-behaved CLIs.
