using HazinaStore.Models;
﻿namespace HazinaStore.Models
{
    public class ApiSettings
    {
        public string OpenApiKey { get; set; }
        public string GeminiApiKey { get; set; }
        public string SignalRHubCorsOrigin { get; set; }
        public string ApiCorsOrigin { get; set; }
    }
}
