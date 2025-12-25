using DevGPTStore.Models;
﻿namespace DevGPTStore.Models
{
    public class ApiSettings
    {
        public string OpenApiKey { get; set; }
        public string GeminiApiKey { get; set; }
        public string SignalRHubCorsOrigin { get; set; }
        public string ApiCorsOrigin { get; set; }
    }
}
