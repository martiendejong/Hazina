using System;
using System.Collections.Generic;
using DevGPTStore.Models;

namespace DevGPTStore.Models
{
    public class CommentInfo : AEmbedding, IEmbedding
    {
        public string PostId { get; set; }
        public string Name { get; set; }
        public DateTime PublicationDate { get; set; }
        public string Message { get; set; }
        public string PostUrl { get; set; }
        public string ViewSource { get; set; }
        public int CommentsCount { get; set; }
        public int ReactionsCount { get; set; }
        public int SharesCount { get; set; }
        public int ViewsCount { get; set; }
        public int LikeCount { get; set; }
        public int WowCount { get; set; }
        public int LoveCount { get; set; }
        public int AngerCount { get; set; }
        public int HahaCount { get; set; }
        public int SupportCount { get; set; }
        public int SorryCount { get; set; }
        public string Id { get => PostId; set => PostId = value; }
        public string Content => ToDescriptiveString();

        public string ToDescriptiveString()
        {
            return $"{PostId}: {Message}";
            //return $"Post ID: {PostId}\n" +
            //       $"Name: {Name}\n" +
            //       $"Publication Date: {PublicationDate:yyyy-MM-dd HH:mm:ss}\n" +
            //       $"Message: {Message}\n" +
            //       $"Post URL: {PostUrl}\n" +
            //       $"View Source: {ViewSource}\n" +
            //       $"Comments Count: {CommentsCount}\n" +
            //       $"Reactions Count: {ReactionsCount}\n" +
            //       $"Shares Count: {SharesCount}\n" +
            //       $"Views Count: {ViewsCount}\n" +
            //       $"Like Count: {LikeCount}\n" +
            //       $"Wow Count: {WowCount}\n" +
            //       $"Love Count: {LoveCount}\n" +
            //       $"Anger Count: {AngerCount}\n" +
            //       $"Haha Count: {HahaCount}\n" +
            //       $"Support Count: {SupportCount}\n" +
            //       $"Sorry Count: {SorryCount}";
        }
    }
}
