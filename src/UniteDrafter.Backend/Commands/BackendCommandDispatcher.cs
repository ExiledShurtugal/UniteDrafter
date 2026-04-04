namespace UniteDrafter.Commands;

public static class BackendCommandDispatcher
{
    public static bool TryExecute(string[] args)
    {
        if (args.Length == 0)
        {
            return false;
        }

        var command = args[0];
        var commandArgs = args.Skip(1).ToArray();

        if (args.Length == 3 && string.Equals(command, "decrypt-file", StringComparison.OrdinalIgnoreCase))
        {
            DecryptFileCommand.Execute(args[1], args[2]);
            return true;
        }

        if (args.Length == 2 && string.Equals(command, "decrypt-ids", StringComparison.OrdinalIgnoreCase))
        {
            DecryptIdsCommand.Execute(args[1]);
            return true;
        }

        if (args.Length == 2 && string.Equals(command, "matchups", StringComparison.OrdinalIgnoreCase))
        {
            MatchupsCommand.Execute(args[1]);
            return true;
        }

        if (args.Length == 2 && string.Equals(command, "search-pokemon", StringComparison.OrdinalIgnoreCase))
        {
            SearchPokemonCommand.Execute(args[1]);
            return true;
        }

        if (string.Equals(command, "update-sources", StringComparison.OrdinalIgnoreCase))
        {
            UpdateSourcesCommand.Execute(commandArgs);
            return true;
        }

        if (string.Equals(command, "update-sources-browser", StringComparison.OrdinalIgnoreCase))
        {
            UpdateSourcesCommand.Execute(["--browser", .. commandArgs]);
            return true;
        }

        if (string.Equals(command, "refresh-db", StringComparison.OrdinalIgnoreCase))
        {
            RefreshDatabaseCommand.Execute(commandArgs);
            return true;
        }

        if (string.Equals(command, "rebuild-db", StringComparison.OrdinalIgnoreCase))
        {
            RebuildDatabaseCommand.Execute(commandArgs);
            return true;
        }

        return false;
    }
}
