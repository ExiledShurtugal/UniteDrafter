using UniteDrafter.Commands;
using UniteDrafter.Data;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 3 && string.Equals(args[0], "decrypt-file", StringComparison.OrdinalIgnoreCase))
        {
            DecryptFileCommand.Execute(args[1], args[2]);
            return;
        }

        if (args.Length == 2 && string.Equals(args[0], "decrypt-ids", StringComparison.OrdinalIgnoreCase))
        {
            DecryptIdsCommand.Execute(args[1]);
            return;
        }

        if (args.Length == 2 && string.Equals(args[0], "matchups", StringComparison.OrdinalIgnoreCase))
        {
            MatchupsCommand.Execute(args[1]);
            return;
        }

        if (args.Length == 2 && string.Equals(args[0], "search-pokemon", StringComparison.OrdinalIgnoreCase))
        {
            SearchPokemonCommand.Execute(args[1]);
            return;
        }

        if (args.Length >= 1 && string.Equals(args[0], "update-sources", StringComparison.OrdinalIgnoreCase))
        {
            UpdateSourcesCommand.Execute(args.Skip(1).ToArray());
            return;
        }

        if (args.Length >= 1 && string.Equals(args[0], "update-sources-browser", StringComparison.OrdinalIgnoreCase))
        {
            UpdateSourcesCommand.Execute(["--browser", .. args.Skip(1)]);
            return;
        }

        if (args.Length >= 1 && string.Equals(args[0], "refresh-db", StringComparison.OrdinalIgnoreCase))
        {
            RefreshDatabaseCommand.Execute(args.Skip(1).ToArray());
            return;
        }

        DatabaseInitializer.Initialize();
    }
}
