using System;
using System.Threading.Tasks;

namespace DevGPT.GenerationTools.Services.Chat
{
    public class ConversationStarterActions
    {
        public const string readwebpage = "readwebpage";
        public const string includeresult = "includeresult";

        public async static Task<string> Execute(ConversationStarterQuestion question)
        {
            switch (question.Action)
            {
                case readwebpage:
                    try
                    {
                        var text = await WebPageScraper.ScrapeWebPage(question.Answer);
                        return $@"{question.Name}
Q: {question.Question}
A: {question.Answer}
Result:
{text}";
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"Kan de pagina {question.Answer} niet lezen. Fout: {e.Message}");
                    }
                case includeresult:
                    return $@"{question.Name}
Q: {question.Question}
A: {question.Answer}";
            }

            throw new NotImplementedException("Functie bestaat niet");
        }
    }
}

