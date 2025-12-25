using System;

namespace Hazina.Tools.Models.Social
{
    public class SocialMediaAddress
    {
        public string Type { get; set; }
        public string Url { get; set; }
        public string Label { get; set; }
        public SocialMediaAddressStatus Status { get; set; } = SocialMediaAddressStatus.Initial;
        public string? Guid { get; set; }
        public int ImportedComments { get; set; }
        public DateTime ImportStartTime { get; set; }
        public DateTime ImportEndTime { get; set; }
    }
}
