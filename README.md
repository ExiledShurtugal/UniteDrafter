# UniteDrafter

> [!WARNING]
> **Project status: archived**
> UniteDrafter depends on third-party matchup data for Pokemon Unite. That data is incomplete for a number of Pokemon, especially low-usage picks, and in some cases the missing matchup data does not exist upstream at all. For example, Falinks currently has only 20 recorded matchups, leaving 60 unavailable from the source itself.
>
> Because this missing data cannot be recovered from the available source, UniteDrafter cannot be developed into a complete or reliable draft tool in its current form. Development has been stopped, and this repository is now kept as an archive/reference project.



UniteDrafter is a local Pokemon Unite draft helper.

The goal is simple:
- pick Pokemon for both teams
- see matchup-based information during the draft
- make better draft decisions using data instead of guesswork

## Simple Overview

Right now the app can:
- select slots on your team and the enemy team
- search Pokemon from the local database
- assign Pokemon to draft slots
- stop the same Pokemon from being picked twice on the same team
- clear one slot
- reset the whole draft
- show basic matchup information for the selected Pokemon

Current limitation:
- the automated source refresh currently skips a few newer Pokemon pages that do not expose counters yet

Known unsupported Pokemon at the source-data level:
- Articuno
- Meowth
- Moltres
- Sirfetch'd
- Zapdos

UniteAPI currently does not have matchup/counter data for these Pokemon, so there is no source data to import into the local database yet.

## Inspiration

- https://draftgap.com/

## What This Project Is Building Toward

The longer-term goal is a local app that helps answer:
- which picks are strong into the enemy team
- which picks are weak into the enemy team
- which side currently has the statistical edge during a draft

---

## Developer Notes

This repository currently has four main parts:
- [`src/UniteDrafter.Backend`](/c:/Users/joaoc/Desktop/Guto/UniteDrafter/src/UniteDrafter.Backend): CLI entry point and command surface
- [`src/UniteDrafter.Core`](/c:/Users/joaoc/Desktop/Guto/UniteDrafter/src/UniteDrafter.Core): draft-page services, shared models, and SQLite readers
- [`src/UniteDrafter.Frontend`](/c:/Users/joaoc/Desktop/Guto/UniteDrafter/src/UniteDrafter.Frontend): Blazor frontend
- [`src/UniteDrafter.SourceUpdate`](/c:/Users/joaoc/Desktop/Guto/UniteDrafter/src/UniteDrafter.SourceUpdate): source refresh, decryption, schema, and import pipeline

Database-related files live under [`data/Database`](/c:/Users/joaoc/Desktop/Guto/UniteDrafter/data/Database), including:
- the SQLite database file
- the raw guide source JSON files used to populate it

The guide source files now live in [`data/Database/GuideSources`](/c:/Users/joaoc/Desktop/Guto/UniteDrafter/data/Database/GuideSources).

## Data Layer Layout

Inside [`src/UniteDrafter.Core`](/c:/Users/joaoc/Desktop/Guto/UniteDrafter/src/UniteDrafter.Core):
- `Data/Readers/`: database readers and reader interfaces
- `Data/Models/`: shared query/result models
- `Services/`: draft page services, session state, and storage path helpers

Inside [`src/UniteDrafter.SourceUpdate`](/c:/Users/joaoc/Desktop/Guto/UniteDrafter/src/UniteDrafter.SourceUpdate):
- `Data/Schema/`: database creation and schema setup
- `Data/Importing/`: seed import logic
- `Data/Updating/`: source refresh, diagnostics, and payload extraction
- `Decrypter/`: encrypted payload parsing helpers

## Current Architecture

At a high level:
- the Blazor page manages draft-board UI state
- the frontend calls `IDraftPageService`
- `DraftPageService` uses focused data readers
- the readers query the local SQLite database

## Running The App

Run the frontend:

```powershell
dotnet run --project src/UniteDrafter.Frontend
```

If Windows keeps the previous frontend process alive, use the helper script instead:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run-frontend.ps1
```

## Main Workflow

Refresh the downloaded guide sources and rebuild the local database in one command:

```powershell
dotnet run --project UniteDrafter.Backend.csproj -- refresh-db
```

This is the normal command to use after source data changes.
`refresh-db` now refreshes guide files into a staging snapshot first, then only replaces the live source directory and rebuilds SQLite if every requested page succeeds. If any guide refresh fails, the existing local sources and database are left unchanged.

If you only want to rebuild the database from files that are already on disk:

```powershell
dotnet run --project UniteDrafter.Backend.csproj -- rebuild-db
```

If you just want the backend to make sure the SQLite file and schema exist without wiping imported data:

```powershell
dotnet run --project UniteDrafter.Backend.csproj
```

Useful day-to-day commands:

```powershell
dotnet run --project UniteDrafter.Backend.csproj -- search-pokemon blast
dotnet run --project UniteDrafter.Backend.csproj -- matchups blastoise
dotnet run --project UniteDrafter.Backend.csproj -- refresh-db
dotnet run --project UniteDrafter.Backend.csproj -- rebuild-db
```

By default the CLI and frontend both auto-discover the nearest project storage root and resolve the database, guide sources, diagnostics, and browser profile under it. To override that root explicitly, set `UNITE_DRAFTER_STORAGE_ROOT`.

## Debugging Commands

These are lower-level commands mainly useful while investigating source or parser issues:

```powershell
dotnet run --project UniteDrafter.Backend.csproj -- update-sources
dotnet run --project UniteDrafter.Backend.csproj -- update-sources-browser
dotnet run --project UniteDrafter.Backend.csproj -- decrypt-file <input> <output>
dotnet run --project UniteDrafter.Backend.csproj -- decrypt-ids <input>
```

## Tests

Run the database tests:

```powershell
dotnet test tests/Database/DatabaseTests.csproj -v minimal
```

## Near-Term Next Step

The next major milestone is expanding the import/seed pipeline so the database covers the full Pokemon roster instead of only a small manual subset.
