using UniteDrafter.Commands;
using UniteDrafter.Storage;

class Program
{
    static void Main(string[] args)
    {
        if (BackendCommandDispatcher.TryExecute(args))
        {
            return;
        }

        var summary = DatabaseBootstrapper.EnsureInitialized();
        Console.WriteLine(summary.CreatedSchema
            ? $"Database schema initialized at: {summary.DatabasePath}"
            : $"Database ready at: {summary.DatabasePath}");
    }
}
