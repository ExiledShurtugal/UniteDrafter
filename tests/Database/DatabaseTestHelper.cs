using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace UniteDrafter.Tests.Database;

internal sealed class DatabaseTestHelper : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        "UniteDrafter.DatabaseTests",
        Guid.NewGuid().ToString("N"));

    public string CreateDatabasePath(string fileName = "unitedrafter.test.db")
    {
        return Path.Combine(_tempRoot, "db", fileName);
    }

    public string CreateSeedDirectory(string directoryName)
    {
        return Path.Combine(_tempRoot, directoryName);
    }

    public SqliteConnection OpenConnection(string databasePath)
    {
        var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        return connection;
    }

    public void WriteSeedFile(string seedDirectory, string fileName, object payload)
    {
        Directory.CreateDirectory(seedDirectory);
        var path = Path.Combine(seedDirectory, fileName);
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(path, json);
    }

    public object CreatePokemonPayload(
        int uniteApiId,
        int pokedexId,
        string pokemonName,
        string pokemonImg,
        object[] matchups)
    {
        return new
        {
            pokemon = new
            {
                id = pokedexId,
                name = new
                {
                    en = pokemonName
                },
                icons = new
                {
                    square = pokemonImg
                }
            },
            counters = new
            {
                pokemonId = uniteApiId,
                all = matchups
            }
        };
    }

    public object CreateMatchup(int opponentUniteApiId, string opponentName, string opponentImg, double winRate)
    {
        return new
        {
            pokemonId = opponentUniteApiId,
            name = opponentName,
            img = opponentImg,
            winRate
        };
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
