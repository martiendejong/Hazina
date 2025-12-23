namespace DevGPTStore.Services
{
    public class WordPressCategory
    {
        public int id { get; set; }
        public int Id { get => id; set => id = value; }
        public string name { get; set; }
        public string description { get; set; }
        public int parent { get; set; }
        public int count { get; set; }
        public string slug { get; set; }
    }
}
