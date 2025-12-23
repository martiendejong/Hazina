using System.Text.Json.Serialization;

namespace DevGPT.GenerationTools.Services.Store
{
    public class PagesList : ChatResponse<PagesList>
    {
        public PagesList()
        {
        }

        public List<string> Pages {  get; set; }

        [JsonIgnore]
        public override PagesList _example => new PagesList { Pages = new List<string>() { "https://meubelluxe.nl/", "https://meubelluxe.nl/eetkamerstoelen/", "https://meubelluxe.nl/over-ons/" } };

        [JsonIgnore]
        public override string _signature => "{Pages: string[]}";
    }
}

