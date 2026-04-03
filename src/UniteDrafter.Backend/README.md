# UniteDrafter CLI

This folder contains the CLI/backend-side app logic for UniteDrafter.

For the full project overview, frontend notes, and repo structure, see the root [README](/c:/Users/joaoc/Desktop/Guto/UniteDrafter/README.md).

## What This CLI Does

`Program.cs` supports these execution modes:
- initialize or rebuild the local database
- decrypt encrypted page JSON into readable JSON
- inspect ID mappings from one encrypted file
- search Pokemon names from the database
- print matchup data for one Pokemon

## Commands

Initialize or rebuild the database:

```powershell
dotnet run --project UniteDrafter.Backend.csproj
```

Decrypt one encrypted page file into readable JSON:

```powershell
dotnet run --project UniteDrafter.Backend.csproj -- decrypt-file "data/Database/JsonsManually/best-builds-movesets-and-guide-for-blastoise.json" "tests/Decrypter/TestResults/decrypted_blastoise.json"
```

Inspect ID values from one encrypted file:

```powershell
dotnet run --project UniteDrafter.Backend.csproj -- decrypt-ids "data/Database/JsonsManually/best-builds-movesets-and-guide-for-alolanraichu.json"
```

Search Pokemon by name:

```powershell
dotnet run --project UniteDrafter.Backend.csproj -- search-pokemon blast
```

Print matchups for one Pokemon:

```powershell
dotnet run --project UniteDrafter.Backend.csproj -- matchups Blastoise
```
