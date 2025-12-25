using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.ChatShared
{
    public interface IChatController
    {
        ReadOnlyObservableCollection<string> AgentsAndFlows { get; }
        string DefaultAgentOrFlow { get; }
        void AttachStreaming(Action<string,string,string> onInterimMessage);
        Task<string> SendMessageAsync(string text, CancellationToken token, string agentOrFlow);
    }
}
