using System;
using UniteDrafter.Decrypter;

class Program
{
    static void Main()
    {
        try
        {
            string path = "JsonsManually/rankings.json"; // path to the JSON file to test
            Decrypter.TestDecrypt(path);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}
