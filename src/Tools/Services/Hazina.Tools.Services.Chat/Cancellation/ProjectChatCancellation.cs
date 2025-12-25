using System.Collections.Generic;
using System.Threading;

namespace Hazina.Tools.Services.Chat
{
    public class ProjectChatCancellation
    {
        public string ChatId { get; set; }
        public List<CancellationTokenSource> Cancels { get; set; } = new List<CancellationTokenSource>();
        public void Cancel()
        {
            for (var i = Cancels.Count - 1; i >= 0; i--)
            {
                var cancel = Cancels[i];
                cancel.Cancel();
            }
        }
    }
}

