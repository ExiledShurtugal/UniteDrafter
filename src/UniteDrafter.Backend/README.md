# UniteDrafter CLI

This folder contains the CLI/backend-side app logic for UniteDrafter.

For the full project overview, frontend notes, and repo structure, see the root [README](/c:/Users/joaoc/Desktop/Guto/UniteDrafter/README.md).

## What This CLI Does

`Program.cs` supports these execution modes:
- refresh source files and rebuild the database in one command
- safely initialize the local database schema for normal startup
- rebuild the local database from source files on demand
- decrypt encrypted page JSON into readable JSON
- inspect ID mappings from one encrypted file
- search Pokemon names from the database
- print matchup data for one Pokemon
- update raw source JSON files from UniteAPI

## Main Workflow

Fetch the latest supported source files and rebuild the database in one pass:

```powershell
dotnet run --project UniteDrafter.Backend.csproj -- refresh-db
```

This is the primary command to use during normal development.

If you already have fresh guide source files on disk and only want to rebuild SQLite:

```powershell
dotnet run --project UniteDrafter.Backend.csproj -- rebuild-db
```

If you just want to ensure the SQLite file and schema exist without wiping existing imported data:

```powershell
dotnet run --project UniteDrafter.Backend.csproj
```

Useful day-to-day helpers:

```powershell
dotnet run --project UniteDrafter.Backend.csproj -- search-pokemon blast
dotnet run --project UniteDrafter.Backend.csproj -- matchups Blastoise
```

`refresh-db` uses the browser fetcher by default and refreshes guide files into a staging snapshot first. The live guide-source directory and SQLite database are only updated if every requested source page succeeds; otherwise the staged files are discarded and the existing local data is left unchanged.

Known unsupported Pokemon at the source-data level:
- Articuno
- Meowth
- Moltres
- Sirfetch'd
- Zapdos

UniteAPI currently does not have matchup/counter data for those Pokemon, so there is no source data for the refresh pipeline to import into local matchup rows yet.

## Source Refresh Commands

Refresh the database with the browser fetcher explicitly:

```powershell
dotnet run --project UniteDrafter.Backend.csproj -- refresh-db --browser
```

Update all guide source files by discovering guide URLs from UniteAPI's Pokemon index:

```powershell
dotnet run --project UniteDrafter.Backend.csproj -- update-sources
```

Update all guide source files with a real Edge browser session for Cloudflare-protected pages:

```powershell
dotnet run --project UniteDrafter.Backend.csproj -- update-sources-browser
```

Update one guide source file explicitly:

```powershell
dotnet run --project UniteDrafter.Backend.csproj -- update-sources blastoise
```

If Cloudflare serves a challenge page to the CLI request, reuse your browser session cookie header:

```powershell
dotnet run --project UniteDrafter.Backend.csproj -- update-sources --cookie-file ".secrets/uniteapi-cookie.txt"
```

The cookie file should contain the raw `Cookie` header value copied from a working browser request.

Browser mode stores a reusable local Edge profile by default at `.playwright/uniteapi-edge-profile`, so once you pass Cloudflare in the opened browser session, later refreshes should be smoother.

The CLI resolves its database, source, diagnostics, and browser-profile paths from the shared storage root. By default it auto-discovers the nearest project root; set `UNITE_DRAFTER_STORAGE_ROOT` to override that location explicitly.

## Debugging Commands

Decrypt one encrypted page file into readable JSON:

```powershell
dotnet run --project UniteDrafter.Backend.csproj -- decrypt-file "data/Database/GuideSources/best-builds-movesets-and-guide-for-blastoise.json" "tests/Decrypter/TestResults/decrypted_blastoise.json"
```

Inspect ID values from one encrypted file:

```powershell
dotnet run --project UniteDrafter.Backend.csproj -- decrypt-ids "data/Database/GuideSources/best-builds-movesets-and-guide-for-alolanraichu.json"
```
