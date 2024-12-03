using System.Security.Cryptography;
using System.Text;

namespace CompatChecker.Helpers;

internal class HashGenerator
{
    public static string GenerateHash(string input)
    {
        var byteArray = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(byteArray);
        var hashString = new StringBuilder();
        foreach (var b in hashBytes)
            hashString.Append(b.ToString("x2"));
        return hashString.ToString();
    }
}