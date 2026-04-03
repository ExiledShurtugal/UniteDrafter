# UniteDrafter

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

This repository currently has three main parts:
- [`src/UniteDrafter.Backend`](/c:/Users/joaoc/Desktop/Guto/UniteDrafter/src/UniteDrafter.Backend): core app logic, CLI commands, services, and data layer
- [`src/UniteDrafter.Frontend`](/c:/Users/joaoc/Desktop/Guto/UniteDrafter/src/UniteDrafter.Frontend): Blazor frontend
- [`src/Decrypter`](/c:/Users/joaoc/Desktop/Guto/UniteDrafter/src/Decrypter): helpers for decrypting and parsing source data

Database-related files live under [`data/Database`](/c:/Users/joaoc/Desktop/Guto/UniteDrafter/data/Database), including:
- the SQLite database file
- the raw guide source JSON files used to populate it

The guide source files now live in [`data/Database/GuideSources`](/c:/Users/joaoc/Desktop/Guto/UniteDrafter/data/Database/GuideSources).

## Data Layer Layout

Inside [`src/UniteDrafter.Backend/Data`](/c:/Users/joaoc/Desktop/Guto/UniteDrafter/src/UniteDrafter.Backend/Data):
- `Readers/`: database readers and reader interfaces
- `Schema/`: database creation and schema setup
- `Importing/`: seed import logic
- `Models/`: shared query/result models

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

If Windows keeps the previous frontend process alive and locks `UniteDrafter.Backend.dll`, use the helper script instead:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run-frontend.ps1
```

## Main Workflow

Refresh the downloaded guide sources and rebuild the local database in one command:

```powershell
dotnet run --project UniteDrafter.Backend.csproj -- refresh-db
```

This is the normal command to use after source data changes.

If you only want to rebuild the database from files that are already on disk:

```powershell
dotnet run --project UniteDrafter.Backend.csproj
```

Useful day-to-day commands:

```powershell
dotnet run --project UniteDrafter.Backend.csproj -- search-pokemon blast
dotnet run --project UniteDrafter.Backend.csproj -- matchups blastoise
dotnet run --project UniteDrafter.Backend.csproj -- refresh-db
```

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
