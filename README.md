# Codex Auth Manager

A .NET command-line tool to manage multiple Codex authentication tokens with versioning, backup, and easy switching between identities.

## Features

- **Import Multiple Tokens**: Automatically scan and import all `*auth.json` files from your Codex folder
- **Token Versioning**: Immutable token versioning - old tokens are never deleted, only new versions are created
- **Identity Management**: List, view, activate, and manage multiple identities
- **Automatic Backups**: Database backups are created before any destructive operations
- **Rollback Support**: Restore previous token versions if needed
- **JWT Metadata Extraction**: Automatically extracts email, plan type, and subscription info from tokens
- **Environment Support**: Separate development and production environments

## Installation

### Install as .NET Tool

```bash
dotnet pack src/CodexAuthManager.Cli/CodexAuthManager.Cli.csproj
dotnet tool install --global --add-source ./src/CodexAuthManager.Cli/bin/Debug CodexAuthManager
```

### Build from Source

```bash
dotnet build
```

## Usage

### Import Tokens

Scan the `%userprofile%\.codex` folder and import all `*auth.json` files:

```bash
codex-tokens import
```

This will:
- Find all files ending with `auth.json` (e.g., `auth.json`, `1-dccx-auth.json`, `1-misi-auth.json`)
- Extract JWT metadata (email, plan type, etc.)
- Create database entries with version 1 for each identity
- Create a backup of the database

### List All Identities

```bash
codex-tokens list
```

Output:
```
Found 3 identity/identities:

ID    Email                            Plan       Active   Last Updated
---------------------------------------------------------------------------------
1     user1@example.com                plus       ✓        2025-10-22 13:45
2     user2@example.com                free                2025-10-21 10:30
3     user3@example.com                plus                2025-10-20 15:20
```

### Show Identity Details

Show the currently active identity:
```bash
codex-tokens show
```

Show a specific identity by ID or email:
```bash
codex-tokens show 2
codex-tokens show user@example.com
```

Output includes:
- Identity details (email, user ID, account ID, plan type)
- All token versions with timestamps
- Current active version

### Activate an Identity

Switch to a different identity (updates `%userprofile%\.codex\auth.json`):

```bash
codex-tokens activate 2
codex-tokens activate user@example.com
```

This will:
- Create a database backup
- Set the specified identity as active
- Write its current token to `%userprofile%\.codex\auth.json`

### Remove Identities

Delete one or more identities:

```bash
codex-tokens remove 2
codex-tokens remove user@example.com
codex-tokens remove 1 2 3
```

### Rollback to Previous Version

Restore a previous token version (creates a new version based on the old one):

```bash
# Rollback active identity to previous version
codex-tokens rollback

# Rollback specific identity
codex-tokens rollback 2
codex-tokens rollback user@example.com

# Rollback to a specific version number
codex-tokens rollback 2 --version 3
```

## How Token Versioning Works

Tokens are **immutable** - they are never modified or deleted, only new versions are created:

1. **Import**: Creates version 1 for each new identity
2. **Update**: If a token changes during import, a new version is created (e.g., version 2, 3, etc.)
3. **Rollback**: Creates a new version based on an old version (doesn't delete the current version)

Example flow:
- Import creates **v1** with token A
- Token refreshes, import creates **v2** with token B (v1 still exists)
- Need to go back? Rollback creates **v3** based on v1's data
- Can still roll forward to v2 if needed

## Environment Configuration

### Production Environment (Default)

Uses actual system paths:
- Codex folder: `%userprofile%\.codex`
- Database: `%appdata%\CodexManager\tokens.db`
- Backups: `%appdata%\CodexManager\backups`

### Development Environment

Use separate paths to avoid interfering with production data:

```bash
# Use --dev flag
codex-tokens --dev import
codex-tokens --dev list

# Or set environment variable
set CODEX_ENV=development
codex-tokens import
```

Development paths:
- Codex folder: `%appdata%\CodexManager-Dev\.codex`
- Database: `%appdata%\CodexManager-Dev\tokens-dev.db`
- Backups: `%appdata%\CodexManager-Dev\backups`

## Data Storage

### Database Location

- **Production**: `%appdata%\CodexManager\tokens.db`
- **Development**: `%appdata%\CodexManager-Dev\tokens-dev.db`

### Backups

Automatic backups are created before:
- Activating an identity
- Removing identities
- Rolling back versions

Backups are stored as:
- `%appdata%\CodexManager\backups\tokens-backup-20251022-134530.db`

The tool automatically keeps only the last 10 backups.

## Database Schema

### Identities Table
- `Id`: Auto-increment primary key
- `Email`: User email from JWT
- `AccountId`: Codex account ID
- `UserId`: Codex user ID
- `PlanType`: Subscription plan (e.g., "plus", "free")
- `IsActive`: Whether this is the currently active identity
- `CreatedAt`, `UpdatedAt`: Timestamps

### TokenVersions Table
- `Id`: Auto-increment primary key
- `IdentityId`: Foreign key to Identities
- `VersionNumber`: Sequential version number (1, 2, 3, ...)
- `IdToken`, `AccessToken`, `RefreshToken`: Token data
- `AccountId`: Account ID from tokens
- `OpenAiApiKey`: Optional API key
- `LastRefresh`: When the token was last refreshed
- `CreatedAt`: When this version was created
- `IsCurrent`: Whether this is the active version for the identity

## Security Notes

- Tokens are stored in a local SQLite database
- The database contains sensitive authentication data
- Backups also contain sensitive data
- Consider encrypting the `%appdata%\CodexManager` folder if needed

## Troubleshooting

### No auth.json files found
Make sure you have Codex installed and have logged in at least once. Auth files should be in `%userprofile%\.codex`.

### Database locked error
Close any other instances of the tool or applications that might be accessing the database.

### Permission errors
Run the tool with appropriate permissions for the `%appdata%` and `%userprofile%` directories.

## Development

### Project Structure

```
CodexAuthManager/
├── src/
│   ├── CodexAuthManager.Core/      # Core domain models, data, and services
│   │   ├── Models/                 # Domain models
│   │   ├── Data/                   # Repository layer
│   │   ├── Services/               # Business logic
│   │   ├── Infrastructure/         # File system and path providers
│   │   └── Abstractions/           # Interfaces
│   └── CodexAuthManager.Cli/       # Command-line interface
│       ├── Commands/               # Command implementations
│       └── Program.cs              # Entry point
├── tests/
│   └── CodexAuthManager.Tests/    # Unit tests
└── docs/
    └── plan.md                     # Implementation plan
```

### Building

```bash
dotnet build
```

### Running Without Installation

```bash
dotnet run --project src/CodexAuthManager.Cli -- list
dotnet run --project src/CodexAuthManager.Cli -- --dev import
```

### Testing

```bash
dotnet test
```

## License

[Add your license here]

## Author

JKamsker
