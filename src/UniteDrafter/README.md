# UniteDrafter CLI Commands

This folder contains `Program.cs`, which supports five execution modes.

## 1) Initialize / refresh the database (default)
Run with no args:

```powershell
dotnet run
```

What it does:
- Creates/opens `data/Database/unitedrafter.db`
- Recreates the schema
- Seeds Pokemon + matchup data from JSON files

## 2) Decrypt a page JSON file into readable JSON

```powershell
dotnet run -- decrypt-file "data/Database/JsonsManually/best-builds-movesets-and-guide-for-blastoise.json" "tests/Decrypter/TestResults/decrypted_blastoise.json"
```

What it does:
- Reads encrypted Next.js page JSON
- Finds `pageProps.e` blob
- Decrypts and writes pretty JSON to the output path

## 3) Inspect ID mapping from one encrypted file

```powershell
dotnet run -- decrypt-ids "data/Database/JsonsManually/best-builds-movesets-and-guide-for-alolanraichu.json"
```

What it prints:
- `pokemon.name.en`
- `pokemon.id` (Pokedex ID)
- `counters.pokemonId` (UniteAPI ID)
- `counters.pokemonId(raw)` directly from decrypted payload

## 4) Show all matchups for one Pokemon

```powershell
dotnet run -- matchups Blastoise
```

What it does:
- Opens `data/Database/unitedrafter.db`
- Finds all matchup rows for the given Pokemon
- Orders them by highest win rate first
- Prints the full matchup list for that Pokemon

If no exact matchup list is found, it also suggests close Pokemon name matches.

## 5) Search Pokemon by partial name

```powershell
dotnet run -- search-pokemon blast
```

What it does:
- Opens `data/Database/unitedrafter.db`
- Searches Pokemon names case-insensitively
- Returns partial matches such as `Blastoise` for `blast`
