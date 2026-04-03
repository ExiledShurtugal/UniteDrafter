using Microsoft.Data.Sqlite;

namespace UniteDrafter.Data;

public abstract class SqliteDataReaderBase
{
    private static readonly object SqliteInitLock = new();
    private static bool sqliteInitialized;

    private readonly string databasePath;

    protected SqliteDataReaderBase(string databasePath)
    {
        this.databasePath = Path.GetFullPath(databasePath);
    }

    protected SqliteConnection OpenConnection()
    {
        EnsureSqliteInitialized();

        var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        return connection;
    }

    private static void EnsureSqliteInitialized()
    {
        if (sqliteInitialized)
        {
            return;
        }

        lock (SqliteInitLock)
        {
            if (sqliteInitialized)
            {
                return;
            }

            SQLitePCL.Batteries_V2.Init();
            sqliteInitialized = true;
        }
    }
}
