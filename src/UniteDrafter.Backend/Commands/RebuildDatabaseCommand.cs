using UniteDrafter.SourceUpdate.Data;

namespace UniteDrafter.Commands;

public static class RebuildDatabaseCommand
{
    public static void Execute(string[] args)
    {
        Console.WriteLine("Rebuilding database from local source files...");
        DatabaseRebuilder.RebuildFromSources(
            jsonSourceDirectories: args.Length == 0 ? null : args);
    }
}
