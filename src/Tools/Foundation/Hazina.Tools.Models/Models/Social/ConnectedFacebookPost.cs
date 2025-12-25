
using System.Collections.Generic;

namespace Hazina.Tools.Models.Social
{
    public class ConnectedFacebookPost
    {
        public string Id { get; set; }
        public string Message { get; set; } = "";
        public string Created { get; set; }
        public string Url { get; set; }

        public List<ConnectedFacebookComment> Comments { get; set; }

        public Dictionary<string, int> Reactions { get; set; }
    }
}

