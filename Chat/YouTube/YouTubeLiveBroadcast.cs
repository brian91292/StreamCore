using StreamCore.SimpleJSON;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StreamCore.YouTube
{
    public class YouTubeBroadcastInfo
    {
        public DateTime publishedAt { get; internal set; } = new DateTime();
        public string channelId { get; internal set; } = "";
        public string title { get; internal set; } = "";
        public string description { get; internal set; } = "";
        public Thumbnails thumbnails { get; internal set; } = new Thumbnails();
        public DateTime scheduledStartTime { get; internal set; } = new DateTime();
        public DateTime actualStartTime { get; internal set; } = new DateTime();
        public DateTime actualEndTime { get; internal set; } = new DateTime();
        public bool isDefaultBroadcast { get; internal set; }
        public string liveChatId { get; internal set; } = "";
        
        internal void Update(JSONObject snippet)
        {
            publishedAt = DateTime.Parse(snippet["publishedAt"].Value);
            channelId = snippet["channelId"].Value;
            title = snippet["title"].Value;
            description = snippet["description"].Value;
            thumbnails.Update(snippet["thumbnails"].AsObject);
            scheduledStartTime = DateTime.Parse(snippet["scheduledStartTime"].Value);
            if(snippet.HasKey("actualStartTime"))
                actualStartTime = DateTime.Parse(snippet["actualStartTime"].Value);
            if(snippet.HasKey("actualEndTime"))
                actualEndTime = DateTime.Parse(snippet["actualEndTime"].Value);
            isDefaultBroadcast = snippet["isDefaultBroadcast"].AsBool;
            if (snippet.HasKey("liveChatId"))
                liveChatId = snippet["liveChatId"].Value;
            else
                liveChatId = "";
        }
    }

    public class YouTubeBroadcastStatus
    {
        public string lifeCycleStatus { get; internal set; } = "";
        public string privacyStatus { get; internal set; } = "";
        public string recordingStatus { get; internal set; } = "";

        internal void Update(JSONObject status)
        {
            lifeCycleStatus = status["lifeCycleStatus"].Value;
            privacyStatus = status["privacyStatus"].Value;
            recordingStatus = status["recordingStatus"].Value;
        }
    }

    public class YouTubeLiveBroadcast
    {
        public string kind { get; internal set; } = "";
        public string etag { get; internal set; } = "";
        public string id { get; internal set; } = "";
        public YouTubeBroadcastInfo snippet { get; internal set; } = new YouTubeBroadcastInfo();
        public YouTubeBroadcastStatus status { get; internal set; } = new YouTubeBroadcastStatus();
    }
}
