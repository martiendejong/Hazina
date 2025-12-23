using System.Collections.Generic;

namespace DevGPTStore.Models
{
    public class ContentPlanningData
    {
        public List<ContentHookPlanning> ContentHooks { get; set; } = new();
    }

    public class ContentHookPlanning
    {
        public string Id { get; set; }
        public int Frequency { get; set; }
    }
}


