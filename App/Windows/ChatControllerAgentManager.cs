using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevGPT.ChatShared;

namespace DevGPT.App.Windows
{
    public class ChatControllerAgentManager : IChatController
    {
        private readonly AgentManager _agentManager;
        private readonly ObservableCollection<string> _agentsAndFlows;
        private Action<string, string, string> _onInterim;

        public ChatControllerAgentManager(AgentManager agentManager)
        {
            _agentManager = agentManager ?? throw new ArgumentNullException(nameof(agentManager));
            var agentNames = (_agentManager.Agents ?? Array.Empty<DevGPTAgent>()).Select(a => a.Name).Where(n => !string.IsNullOrWhiteSpace(n));
            var flowNames = (_agentManager.Flows ?? Array.Empty<DevGPTFlow>()).Select(f => f.Name).Where(n => !string.IsNullOrWhiteSpace(n));
            _agentsAndFlows = new ObservableCollection<string>(agentNames.Concat(flowNames).Distinct().ToList());
        }

        public ReadOnlyObservableCollection<string> AgentsAndFlows => new ReadOnlyObservableCollection<string>(_agentsAndFlows);

        public string DefaultAgentOrFlow => _agentsAndFlows.FirstOrDefault();

        public void AttachStreaming(Action<string, string, string> onInterimMessage)
        {
            _onInterim = onInterimMessage;
        }

        public async Task<string> SendMessageAsync(string text, CancellationToken token, string agentOrFlow)
        {
            if (string.IsNullOrWhiteSpace(agentOrFlow))
            {
                agentOrFlow = DefaultAgentOrFlow;
            }

            if (string.IsNullOrWhiteSpace(agentOrFlow))
            {
                return string.Empty;
            }

            var isAgent = _agentManager.Agents?.Any(a => a.Name == agentOrFlow) == true;

            // No true streaming hook available here; return final response.
            if (isAgent)
            {
                return await _agentManager.SendMessage(text, token, agentOrFlow);
            }
            else
            {
                if (agentOrFlow.StartsWith("FLOW: "))
                    agentOrFlow = agentOrFlow.Substring(6);
                return await _agentManager.SendMessage_Flow(text, token, agentOrFlow);
            }
        }
    }
}
