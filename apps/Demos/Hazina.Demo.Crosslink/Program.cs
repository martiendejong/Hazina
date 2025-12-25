using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        // Load OpenAI settings using Hazina libraries
        var openAIConfig = OpenAIConfig.Load();
        string apikey = openAIConfig.ApiKey;
        var llmClient = new OpenAIClientWrapper(openAIConfig);

        // Prepare document stores for werkzoekenden and bedrijven with your in-repo DocumentStore components
        var werkzoekendenEmbeddingStore = new EmbeddingFileStore(@"c:\stores\crosslink\werkzoekenden.embed", llmClient);
        var werkzoekendenTextStore = new TextFileStore(@"c:\stores\crosslink\werkzoekenden");
        var werkzoekendenChunkStore = new ChunkFileStore(@"c:\stores\crosslink\werkzoekenden.chunks");
        var werkzoekendenMetadataStore = new DocumentMetadataFileStore(@"c:\stores\crosslink\werkzoekenden.metadata");
        var werkzoekenden = new DocumentStore(werkzoekendenEmbeddingStore, werkzoekendenTextStore, werkzoekendenChunkStore, werkzoekendenMetadataStore, llmClient);

        var bedrijvenEmbeddingStore = new EmbeddingFileStore(@"c:\stores\crosslink\bedrijven.embed", llmClient);
        var bedrijvenTextStore = new TextFileStore(@"c:\stores\crosslink\bedrijven");
        var bedrijvenChunkStore = new ChunkFileStore(@"c:\stores\crosslink\bedrijven.chunks");
        var bedrijvenMetadataStore = new DocumentMetadataFileStore(@"c:\stores\crosslink\bedrijven.metadata");
        var bedrijven = new DocumentStore(bedrijvenEmbeddingStore, bedrijvenTextStore, bedrijvenChunkStore, bedrijvenMetadataStore, llmClient);

        // Example CV data (this would be loaded dynamically in a production scenario)
        var candidateCV = @"**Curriculum Vitae**\n        \n**Persoonlijke gegevens**\nNaam: Mark van den Berg\nAdres: Keizersgracht 112, 1015 CV Amsterdam\nTelefoonnummer: 06-12345678\nE-mail: markvandenberg@email.com\nGeboortedatum: 12 mei 1990\nNationaliteit: Nederlands\nRijbewijs: B\n\n---\n**Profiel**\nAnalytische en gedreven IT-professional met een passie voor softwareontwikkeling en procesoptimalisatie. Ik ben een probleemoplosser die graag complexe vraagstukken analyseert en vertaalt naar effici+½nte technologische oplossingen. Mijn sterke communicatieve vaardigheden maken mij een bruggenbouwer tussen techniek en eindgebruikers. ... (etc, rest of CV as per sample)";

        Console.WriteLine("Vacatures vinden op basis van het volgende CV:");
        Console.WriteLine(candidateCV);
        Console.WriteLine();

        // Find relevant jobs/vacancies using bedrijven store's interface, using RelevantItems
        List<string> relevantVacancies = await bedrijven.RelevantItems(candidateCV);

        int matchCount = 0;
        foreach (var vacature in relevantVacancies)
        {
            matchCount++;
            Console.WriteLine("BEGIN MATCH");
            Console.WriteLine("Match gevonden met vacature:");
            Console.WriteLine(vacature);
            Console.WriteLine();

            // Use DevGPT.OpenAI and Hazina chat classes for interview/chat simulation
            var chatClient = llmClient;
            var messages = new List<HazinaChatMessage>
            {
                new HazinaChatMessage { Role = HazinaMessageRole.Assistant, Text = "De CV van de kandidaat: " + candidateCV },
                new HazinaChatMessage { Role = HazinaMessageRole.Assistant, Text = "De vacature: " + vacature },
                new HazinaChatMessage { Role = HazinaMessageRole.Assistant, Text = "Genereer een sollicitatiegesprek tussen de kandidaat en de interviewer..." }
            };
            var simulatedInterviewResponse = await chatClient.GetResponse(
                messages,
                HazinaChatResponseFormat.Text,
                null,
                new List<ImageData>(), default);
            string simulatedInterview = simulatedInterviewResponse.Result;

            // Analyse the match
            var analysisMessages = new List<HazinaChatMessage>
            {
                new HazinaChatMessage { Role = HazinaMessageRole.Assistant, Text = "De CV van de kandidaat: " + candidateCV },
                new HazinaChatMessage { Role = HazinaMessageRole.Assistant, Text = "De vacature: " + vacature },
                new HazinaChatMessage { Role = HazinaMessageRole.Assistant, Text = "Het gesimuleerde gesprek: " + simulatedInterview },
                new HazinaChatMessage { Role = HazinaMessageRole.Assistant, Text = "Geef een voorlopige analyse conclusie beoordeling van de match en het gesimuleerde gesprek. Geef de match een score tussen 0 en 100..." }
            };
            var analysisResponse = await chatClient.GetResponse(
                analysisMessages,
                HazinaChatResponseFormat.Text,
                null,
                new List<ImageData>(), default);
            string analysis = analysisResponse.Result;

            string report = $@"Er is een match gevonden met een CV en een vacature.\n\nDe CV:\n{candidateCV}\n\nDe vacature:\n{vacature}\n\nOp basis van de match van CV en vacature wordt het volgende gesprek gesimuleerd:\n{simulatedInterview}\n\nHier volgt de analyse van de match tot zover.\n{analysis}\n";

            File.WriteAllText($@"c:\\stores\\crosslink\\Match {matchCount}.txt", report);

            Console.WriteLine("Op basis van de match van CV en vacature wordt het volgende gesprek gesimuleerd:");
            Console.WriteLine(simulatedInterview);
            Console.WriteLine("EINDE MATCH");
            Console.WriteLine();
            Console.WriteLine("Druk op enter om verder te gaan.");
            Console.ReadLine();
        }
    }
}
