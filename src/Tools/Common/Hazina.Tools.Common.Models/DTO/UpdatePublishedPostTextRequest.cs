using System;

namespace Common.Models.DTO
{
    public class UpdatePublishedPostTextRequest
    {
        public string Text { get; set; }
        public DateTime PublishDate { get; set; }
    }
}

