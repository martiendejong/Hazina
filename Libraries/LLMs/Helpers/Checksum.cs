using System.Security.Cryptography;

public class Checksum
{
    public static string CalculateChecksumFromString(string fileContents)
    {
        using (var sha256 = SHA256.Create())
        {
            var contentBytes = System.Text.Encoding.UTF8.GetBytes(fileContents);
            var hashBytes = sha256.ComputeHash(contentBytes);
            return Convert.ToHexString(hashBytes);
        }
    }

    public static string CalculateChecksum(string filePath)
    {
        if (!File.Exists(filePath))
            return "";
        using (var sha256 = SHA256.Create())
        using (var fileStream = File.OpenRead(filePath))
        {
            var hashBytes = sha256.ComputeHash(fileStream);
            return Convert.ToHexString(hashBytes); // Converts to a readable hex string
        }
    }
}