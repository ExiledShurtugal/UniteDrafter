# UniteDrafter CLI Commands

This folder contains `Program.cs`, which supports three execution modes.

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
