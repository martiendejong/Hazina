using System;
using System.Net.Http;
using System.Threading.Tasks;

public class ImageChecker
{
    private static readonly HttpClient httpClient = new HttpClient();

    public static async Task<bool> IsImageUrlAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        try
        {
            // Probeer eerst HEAD request om alleen headers te krijgen
            using (var request = new HttpRequestMessage(HttpMethod.Head, url))
            {
                var response = await httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                    return false;

                if (response.Content.Headers.ContentType != null &&
                    response.Content.Headers.ContentType.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // Sommige servers ondersteunen geen HEAD, dus fallback op GET met minimal content
            using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                if (!response.IsSuccessStatusCode)
                    return false;

                if (response.Content.Headers.ContentType != null &&
                    response.Content.Headers.ContentType.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Exception kan optreden bij slechte URL, netwerkfouten etc.
            return false;
        }

        return false;
    }
}
