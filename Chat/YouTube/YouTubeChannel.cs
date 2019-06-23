using StreamCore.SimpleJSON;
using StreamCore.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace StreamCore.YouTube
{
    public class YouTubeChannel
    {
        public static string kind { get; internal set; } = "";
        public static string etag { get; internal set; } = "";
        public static string liveOrDefaultChatId { get; internal set; } = "";
        public static Dictionary<string, LiveBroadcast> broadcasts { get; internal set; } = new Dictionary<string, LiveBroadcast>();
        
        internal static bool Update(string json)
        {
            // Handle any json parsing errors
            if (json == string.Empty)
                return false;
            JSONNode node = JSON.Parse(json);
            if (node == null || node.IsNull)
                return false;

            if (node.HasKey("error") )
            {
                Plugin.Log(json);
                return false;
            }

            kind = node["kind"].Value;
            etag = node["etag"].Value;

            string currentChannel = "";
            foreach (JSONObject item in node["items"].AsArray)
            {
                string broadcastId = item["id"].Value;
                if (!broadcasts.TryGetValue(broadcastId, out var broadcast))
                {
                    // If the current broadcast does not exist in the dict, add it
                    broadcast = new LiveBroadcast();
                    broadcasts[broadcastId] = broadcast;
                }

                // Update the broadcast info accordingly
                broadcast.kind = item["kind"].Value;
                broadcast.etag = item["etag"].Value;
                broadcast.id = item["id"].Value;
                broadcast.snippet.Update(item["snippet"].AsObject);
                broadcast.status.Update(item["status"].AsObject);

                // Store our default or live broadcast id so we always have some chat to display even if the user isn't live
                if (broadcast.snippet.liveChatId != "" && broadcast.status.recordingStatus == "recording" || (currentChannel == "" && broadcast.snippet.isDefaultBroadcast))
                {
                    currentChannel = broadcast.snippet.liveChatId;
                    Plugin.Log($"Default broadcast \"{broadcast.snippet.title}\" (ID: {broadcast.id}, ChannelID: {broadcast.snippet.channelId}) with description \"{broadcast.snippet.description}\" status is \"{broadcast.status.recordingStatus}\"");
                }
            }
            liveOrDefaultChatId = currentChannel;
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
