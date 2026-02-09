using System;
using UniteDrafter.Decrypter;

class Program
{
    static void Main()
    {
        try
        {
            string path = "JsonsManually/rankings.json"; // caminho para o JSON que queres testar
            Decrypter.TestDecrypt(path);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro: " + ex.Message);
        }
    }
}
