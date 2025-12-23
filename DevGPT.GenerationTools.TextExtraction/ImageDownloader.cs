namespace Common.Utilities
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Threading.Tasks;

    public class ImageDownloader
    {
        private static readonly HttpClient httpClient = new HttpClient();

        public static async Task DownloadImageAsync(Uri uri, string filePath)
        {
            // Get image bytes from URI
            var imageBytes = await httpClient.GetByteArrayAsync(uri);

            // Save as PNG
            await File.WriteAllBytesAsync(filePath, imageBytes);
        }
    }
}
