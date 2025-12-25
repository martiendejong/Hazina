using System.Collections.Generic;

namespace Hazina.Tools.Models.Social
{
    public class ConnectedFacebookPage
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
        public string Token { get; set; }
        public List<ConnectedFacebookPost>? Posts { get; set; }
    }
}

