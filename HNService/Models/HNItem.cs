using HNService.Convertors;
using System.Text.Json.Serialization;

namespace HNService.Models
{
    internal class HNItem
    {
        public int Id { get; set; } //The item's unique id.
        [JsonIgnore]
        public bool Deleted { get; set; } // true if the item is deleted.
        public string? Type { get; set; } //"job", "story", "comment", "poll", or "pollopt".
        public string? By { get; set; } // The username of the item's author.
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime Time { get; set; } //time Creation date of the item, in Unix Time.
        [JsonIgnore]
        public string? Text { get; set; }  //The comment, story or poll text. HTML.
        [JsonIgnore]
        public bool Dead { get; set; } //  true if the item is dead.
        [JsonIgnore]
        public int Parent { get; set; } //he comment's parent: either another comment or the relevant story.
        [JsonIgnore]
        public int Poll { get; set; }    //The pollopt's associated poll.
        public List<int>? Kids { get; set; } //The ids of the item's comments, in ranked display order.
        public string? Url { get; set; } //The URL of the story.
        public int Score { get; set; } //The story's score, or the votes for a pollopt.
        public string? Title { get; set; }  // The title of the story, poll or job.HTML.
        [JsonIgnore]
        public List<int>? Parts { get; set; } // A list of related pollopts, in display order.
        public int Descendants { get; set; } //In the case of stories or polls, the total comment count.
    }
    public class HNData
    {
        public string? title { get; set; }  
        public string? uri { get; set; }
        public string? postedBy { get; set; } 
        public DateTime time { get; set; } 
        public int score { get; set; }
        public int commentCount { get; set; } 
    }
}
