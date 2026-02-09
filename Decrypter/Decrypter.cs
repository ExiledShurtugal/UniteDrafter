using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

namespace UniteDrafter.Decrypter
{
    public static class Decrypter
    {
        // --- Base64 decode tolerant ---
        public static byte[] B64DecodeLoose(string s)
        {
            s = s.Trim().Replace("-", "+").Replace("_", "/");
            int mod = s.Length % 4;
            if (mod == 1) throw new Exception("Invalid base64 length (mod 4 == 1)");
            if (mod == 2 || mod == 3) s += new string('=', 4 - mod);
            return Convert.FromBase64String(s);
        }

        // --- Split blob for key + ciphertext ---
        public static (string keyStr, string encB64) SplitBlobGuess(string blob)
        {
            blob = blob.Trim().Trim('"').Trim('\'');
            var L = blob.Length;
            foreach (var keyLen in new int[] { 21, 22, 20, 23 })
            {
                if (L <= keyLen + 10) continue;
                var keyStr = blob.Substring(L - keyLen);
                var encB64 = blob.Substring(0, L - keyLen);
                try
                {
                    _ = B64DecodeLoose(encB64);
                    return (keyStr, encB64);
                }
                catch { }
            }

            int lastEq = blob.LastIndexOf('=');
            if (lastEq != -1 && lastEq < L - 5)
            {
                var possibleKey = blob.Substring(lastEq + 1);
                var possibleEnc = blob.Substring(0, lastEq + 1);
                if (possibleKey.Length >= 18 && possibleKey.Length <= 30)
                    return (possibleKey, possibleEnc);
            }

            throw new Exception("Could not split blob into key + encB64");
        }

        // --- AES-CTR decrypt ---
        public static string DecryptBlob(string blob)
        {
            var (keyStr, encB64) = SplitBlobGuess(blob);
            var key = SHA256.HashData(Encoding.UTF8.GetBytes(keyStr));
            var raw = B64DecodeLoose(encB64);

            if (raw.Length < 17) throw new Exception("Could not split blob into key + encB64");

            var iv = raw[..16];
            var ct = raw[16..];
            var pt = new byte[ct.Length];
            var counterBlock = (byte[])iv.Clone();

            using var aes = Aes.Create();
            aes.Key = key;
            aes.Mode = CipherMode.ECB; // CTR uses ECB internally
            aes.Padding = PaddingMode.None;
            using var encryptor = aes.CreateEncryptor();

            int blockCount = (ct.Length + 15) / 16;
            for (int i = 0; i < blockCount; i++)
            {
                byte[] keystreamBlock = new byte[16];
                encryptor.TransformBlock(counterBlock, 0, 16, keystreamBlock, 0);

                for (int j = 0; j < 16 && (i * 16 + j) < ct.Length; j++)
                    pt[i * 16 + j] = (byte)(ct[i * 16 + j] ^ keystreamBlock[j]);

                IncrementCounter(counterBlock);
            }

            return Encoding.UTF8.GetString(pt);
        }

        private static void IncrementCounter(byte[] counter)
        {
            for (int i = 15; i >= 0; i--)
            {
                if (++counter[i] != 0) break;
            }
        }

        public static string? FindPagePropsA(JsonElement data)
        {
            if (data.TryGetProperty("pageProps", out var pp) &&
                pp.TryGetProperty("a", out var aProp))
                return aProp.GetString();

            if (data.TryGetProperty("props", out var props) &&
                props.TryGetProperty("pageProps", out var pp2) &&
                pp2.TryGetProperty("a", out var aProp2))
                return aProp2.GetString();

            foreach (var child in data.EnumerateObject())
            {
                var result = RecursiveFindPageProps(child.Value);
                if (result != null) return result;
            }
            return null;
        }

        private static string? RecursiveFindPageProps(JsonElement node)
        {
            if (node.ValueKind == JsonValueKind.Object)
            {
                if (node.TryGetProperty("pageProps", out var pp) &&
                    pp.TryGetProperty("a", out var aProp))
                    return aProp.GetString();

                foreach (var child in node.EnumerateObject())
                {
                    var r = RecursiveFindPageProps(child.Value);
                    if (r != null) return r;
                }
            }
            else if (node.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in node.EnumerateArray())
                {
                    var r = RecursiveFindPageProps(el);
                    if (r != null) return r;
                }
            }
            return null;
        }

        // --- Test function ---
        public static void TestDecrypt(string filePath)
        {
            string jsonText = File.ReadAllText(filePath);
            using var doc = JsonDocument.Parse(jsonText);
            var aBlob = FindPagePropsA(doc.RootElement);
            if (aBlob == null) throw new Exception("pageProps.a not found");

            string decryptedJson = DecryptBlob(aBlob);
            Console.WriteLine("Decrypted JSON preview (first 500 chars):"); 
            Console.WriteLine(decryptedJson.Substring(0, Math.Min(500, decryptedJson.Length)));
        }
    }
}
