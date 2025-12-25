using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace Hazina.App.ExplorerIntegration
{
    public static class ChatService
    {
        public static async Task<Hazina.ChatShared.ChatWindow> CreateChatWindowAsync(string folder)
        {
            var apiKey = Microsoft.Win32.Registry.GetValue("HKEY_CURRENT_USER\\Software\\Hazina\\OpenAI", "ApiKey", null) as string;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                var dlg = new EmbedDialog(folder);
                dlg.Title = "Provide OpenAI Key";
                dlg.FiltersBox.Visibility = Visibility.Collapsed;
                dlg.SaveKeyBox.IsChecked = true;
                if (dlg.ShowDialog() == true)
                {
                    apiKey = dlg.OpenAIKey;
                    if (dlg.SaveKeyToRegistry)
                        Microsoft.Win32.Registry.SetValue("HKEY_CURRENT_USER\\Software\\Hazina\\OpenAI", "ApiKey", apiKey);
                }
            }

            var config = new OpenAIConfig(apiKey);
            var llm = new OpenAIClientWrapper(config);
            var factory = new AgentFactory(apiKey, config.LogPath);
            var creator = new QuickAgentCreator(factory, llm);
            var store = creator.CreateStore(new StorePaths(folder), new System.IO.DirectoryInfo(folder).Name);
            var agent = await factory.CreateUnregisteredAgent(
                name: "ExplorerAgent",
                systemPrompt: "You are a helpful coding assistant. Use tools to read files and, when appropriate, modify them using full-file updates only.",
                stores: new[] { (store as IDocumentStore, true) },
                function: new[] { "git", "dotnet" },
                agents: System.Array.Empty<string>(),
                flows: System.Array.Empty<string>(),
                isCoder: true
            );

            var controller = new ChatControllerExplorer(agent);
            return new Hazina.ChatShared.ChatWindow(controller);
        }
    }
}

