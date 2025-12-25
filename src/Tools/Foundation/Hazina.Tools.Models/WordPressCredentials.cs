namespace DevGPT.GenerationTools.Models
{
    /// <summary>
    /// Struct to store WordPress credential information.
    /// </summary>
    public class WordPressCredentials
    {
        public string BaseUrl { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public WordPressCredentials(string baseUrl, string username, string password)
        {
            BaseUrl = baseUrl;
            Username = username;
            Password = password;
        }
    }
}
