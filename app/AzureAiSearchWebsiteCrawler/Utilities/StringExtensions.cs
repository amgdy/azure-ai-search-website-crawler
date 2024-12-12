using System.Security.Cryptography;
using System.Text;

namespace AzureAiSearchWebsiteCrawler.Utilities;

internal static class StringExtensions
{
    public static string ComputeSha512Hash(this string inputString)
    {
        ArgumentNullException.ThrowIfNull(inputString);

        byte[] bytes = SHA512.HashData(Encoding.UTF8.GetBytes(inputString));
        var builder = new StringBuilder();
        foreach (byte b in bytes)
        {
            builder.Append(b.ToString("x2"));
        }
        return builder.ToString();
    }

    public static string ComputeSha256Hash(this string inputString)
    {
        ArgumentNullException.ThrowIfNull(inputString);

        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(inputString));
        var builder = new StringBuilder();
        foreach (byte b in bytes)
        {
            builder.Append(b.ToString("x2"));
        }
        return builder.ToString();
    }

}
