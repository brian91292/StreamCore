using StreamCore.SimpleJSON;
using StreamCore.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StreamCore.YouTube
{
    public class YouTubeBroadcastDetails
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
            if (snippet.HasKey("actualStartTime"))
                actualStartTime = DateTime.Parse(snippet["actualStartTime"].Value);
            if (snippet.HasKey("actualEndTime"))
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

    public class YouTubeLiveBroadcastInfo
    {
        public string kind { get; internal set; } = "";
        public string etag { get; internal set; } = "";
        public string id { get; internal set; } = "";
        public YouTubeBroadcastDetails snippet { get; internal set; } = new YouTubeBroadcastDetails();
        public YouTubeBroadcastStatus status { get; internal set; } = new YouTubeBroadcastStatus();
    }

    public class YouTubeLiveBroadcast
    {
        public static string kind { get; internal set; } = "";
        public static string etag { get; internal set; } = "";
        public static YouTubeLiveBroadcastInfo currentBroadcast { get; internal set; }
        public static Dictionary<string, YouTubeLiveBroadcastInfo> broadcasts { get; internal set; } = new Dictionary<string, YouTubeLiveBroadcastInfo>();
        
        internal static bool Update(string json)
        {
            // Handle any json parsing errors
            if (json == string.Empty)
                return false;

            // Parse the broadcast info into a json node, making sure it's not null
            JSONNode node = JSON.Parse(json);
            if (node == null || node.IsNull)
                return false;

            // If the data has an error node, print out the entire json string for debugging purposes
            if (node.HasKey("error") )
            {
                Plugin.Log(json);
                return false;
            }

            // Read in the json data to our data structs
            kind = node["kind"].Value;
            etag = node["etag"].Value;

            YouTubeLiveBroadcastInfo tmpCurrentBroadcast = null;
            // Iterate through each broadcast, updating the info for each one along the way
            foreach (JSONObject item in node["items"].AsArray)
            {
                string broadcastId = item["id"].Value;
                if (!broadcasts.TryGetValue(broadcastId, out var broadcast))
                {
                    // If the current broadcast does not exist in the dict, add it
                    broadcast = new YouTubeLiveBroadcastInfo();
                    broadcasts[broadcastId] = broadcast;
                }

                // Update the broadcast info accordingly
                broadcast.kind = item["kind"].Value;
                broadcast.etag = item["etag"].Value;
                broadcast.id = item["id"].Value;
                broadcast.snippet.Update(item["snippet"].AsObject);
                broadcast.status.Update(item["status"].AsObject);

                // Store our default or live broadcast id so we always have some chat to display even if the user isn't live
                if (broadcast.snippet.liveChatId != "" && broadcast.status.recordingStatus == "recording" || (tmpCurrentBroadcast == null && broadcast.snippet.isDefaultBroadcast))
                {
                    tmpCurrentBroadcast = broadcast;
                    Plugin.Log($"Broadcast \"{broadcast.snippet.title}\" (ID: {broadcast.id}, ChannelID: {broadcast.snippet.channelId}) with description \"{broadcast.snippet.description}\" status is \"{broadcast.status.recordingStatus}\" (isDefaultBroadcast? {broadcast.snippet.isDefaultBroadcast})");
                }
                Thread.Sleep(0);
            }
            // Finally, store our current broadcast into a global variable for others to utilize
            currentBroadcast = tmpCurrentBroadcast;
            return true;
        }

        internal static void Refresh()
        {
            try
            {
                Plugin.Log($"Requesting live broadcast info...");
                HttpWebRequest web = (HttpWebRequest)WebRequest.Create("https://www.googleapis.com/youtube/v3/liveBroadcasts?part=id%2Csnippet%2Cstatus%2ccontentDetails&broadcastStatus=all&broadcastType=all&maxResults=50");
                web.Method = "GET";
                web.Headers.Add("Authorization", $"{YouTubeOAuthToken.tokenType} {YouTubeOAuthToken.accessToken}");
                web.Accept = "application/json";
                web.UserAgent = "StreamCoreClient";

                using (HttpWebResponse resp = (HttpWebResponse)web.GetResponse())
                {
                    if (resp.StatusCode == HttpStatusCode.OK)
                    {
                        using (Stream dataStream = resp.GetResponseStream())
                        {
                            using (StreamReader reader = new StreamReader(dataStream))
                            {
                                string ret = reader.ReadToEnd();
                                Update(ret);
                                Plugin.Log($"There are currently {broadcasts.Count} broadcasts being tracked.");// Ret: {ret}");

                                //Plugin.Log($"Broadcast \"{broadcast.Value.snippet.title}\" (ID: {broadcast.Value.id}, ChannelID: {broadcast.Value.snippet.channelId}) with description \"{broadcast.Value.snippet.description}\" status is \"{broadcast.Value.status.recordingStatus}\"");
                            }
                        }
                    }
                    else
                    {
                        Plugin.Log($"Error: {resp.StatusCode}");
                    }
                }
            }
            catch (WebException ex)
            {
                switch (((HttpWebResponse)ex.Response).StatusCode)
                {
                    // If we hit an unauthorized exception, the users auth token has expired
                    case HttpStatusCode.Unauthorized:
                        Plugin.Log("User is unauthorized!");
                        // Try to refresh the users auth token, forcing it through even if our local timestamp says it's not expired
                        if (!YouTubeOAuthToken.Refresh(true))
                        {
                            YouTubeOAuthToken.Invalidate();
                            YouTubeOAuthToken.Generate();
                        }
                        break;
                    case HttpStatusCode.Forbidden:
                        Plugin.Log("The linked YouTube account is not enabled for live streaming! Enable live streaming, then try again!");
                        break;
                    default:
                        Plugin.Log($"WebException: {ex.ToString()}, Message: {ex.Message}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log(ex.ToString());
            }
        }
    }
}
