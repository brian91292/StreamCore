using StreamCore.Chat;
using StreamCore.SimpleJSON;
using StreamCore.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StreamCore.YouTube
{
    public class TextMessageDetails
    {
        public string messageText { get; internal set; }

        internal void Update(string messageTtext)
        {
            this.messageText = messageText;
        }
    }

    public class YouTubeMessageDetails
    {
        public string type { get; internal set; } = "";
        public string liveChatId { get; internal set; } = "";
        public string authorChannelId { get; internal set; } = "";
        public DateTime publishedAt { get; internal set; } = new DateTime();
        public bool hasDisplayContent { get; internal set; } = false;
        public string displayMessage { get; internal set; } = "";
        public TextMessageDetails textMessageDetails { get; internal set; } = new TextMessageDetails();

        internal void Update(JSONObject info)
        {
            type = info["type"].Value;
            liveChatId = info["liveChatId"].Value;
            authorChannelId = info["authorChannelId"].Value;
            publishedAt = DateTime.Parse(info["publishedAt"].Value);
            hasDisplayContent = info["hasDisplayContent"].AsBool;
            displayMessage = info["displayMessage"].Value;
            textMessageDetails.Update(info["textMessageDetails"]["messageText"].Value);
        }
    }

    public class YouTubeUser : GenericChatUser
    {
        public string channelUrl { get; internal set; } = "";
        public string profileImageUrl { get; internal set; } = "";
        public bool isVerified { get; internal set; } = false;
        public bool isChatOwner { get; internal set; } = false;
        public bool isChatSponsor { get; internal set; } = false;
        public bool isChatModerator { get; internal set; } = false;

        internal void Update(JSONObject author)
        {
            id = author["channelId"].Value;
            channelUrl = author["channelUrl"].Value;
            displayName = author["displayName"].Value;
            profileImageUrl = author["profileImageUrl"].Value;
            isVerified = author["isVerified"].AsBool;
            isChatOwner = author["isChatOwner"].AsBool;
            isChatSponsor = author["isChatSponsor"].AsBool;
            isChatModerator = author["isChatModerator"].AsBool;
        }
    }

    public class YouTubeMessage : GenericChatMessage
    {
        public string kind { get; set; } = "";
        public string etag { get; set; } = "";
        public YouTubeMessageDetails snippet { get;  set; } = new YouTubeMessageDetails();

        internal void Update(JSONObject chatMsg)
        {
            snippet.Update(chatMsg["snippet"].AsObject);
            YouTubeUser newUser = new YouTubeUser();
            newUser.Update(chatMsg["authorDetails"].AsObject);
            user = newUser;
            kind = chatMsg["kind"].Value;
            etag = chatMsg["etag"].Value;
            id = chatMsg["etag"].Value;
            message = snippet.displayMessage;
        }
    }
    
    public class YouTubeLiveChat
    {
        public static string kind { get; internal set; } = "";
        public static string etag { get; internal set; } = "";
        private static string _nextPageToken { get; set; } = "";
        private static int _pollingIntervalMillis { get; set; } = 0;

        #region Message Handler Dictionaries
        private static Dictionary<string, Action<YouTubeMessage>> _onMessageReceived_Callbacks = new Dictionary<string, Action<YouTubeMessage>>();
        #endregion

        /// <summary>
        /// YouTube OnMessageReceived event handler. *Note* The callback is NOT on the Unity thread!
        /// </summary>
        public static Action<YouTubeMessage> OnMessageReceived
        {
            set { lock (_onMessageReceived_Callbacks) { _onMessageReceived_Callbacks[Assembly.GetCallingAssembly().GetHashCode().ToString()] = value; } }
            get { return _onMessageReceived_Callbacks.TryGetValue(Assembly.GetCallingAssembly().GetHashCode().ToString(), out var callback) ? callback : null; }
        }

        internal static void Process(string json)
        {
            // Handle any json parsing errors
            if (json == string.Empty)
                return;

            // Parse the chat info into a json node, making sure it's not null
            JSONNode node = JSON.Parse(json);
            if (node == null || node.IsNull)
                return;

            // If the data has an error node, print out the entire json string for debugging purposes
            if (node.HasKey("error"))
            {
                Plugin.Log(json);
                return;
            }

            // Read in the json data to our data structs
            kind = node["kind"].Value;
            etag = node["etag"].Value;
            _nextPageToken = node["nextPageToken"].Value;
            _pollingIntervalMillis = node["pollingIntervalMillis"].AsInt;

            // Iterate through each message, invoking any regstered callbacks along the way
            foreach (JSONObject item in node["items"].AsArray)
            {
                YouTubeMessage newMessage = new YouTubeMessage();
                newMessage.Update(item);
                
                foreach (var instance in _onMessageReceived_Callbacks)
                {
                    //string assemblyHash = instance.Key;
                    //// Don't invoke the callback if it was registered by the assembly that sent the message which invoked this callback (no more vindaloop :D)
                    //if (assemblyHash == invokerHash)
                    //    continue;

                    var action = instance.Value;
                    if (action == null) return;

                    foreach (var a in action.GetInvocationList())
                    {
                        try
                        {
                            a?.DynamicInvoke(newMessage);
                        }
                        catch (Exception ex)
                        {
                            Plugin.Log(ex.ToString());
                        }
                    }
                }
                Thread.Sleep(0);
            }
        }
        
        internal static void Refresh()
        {
            try
            {
                // Wait a few seconds then return if the current broadcast is null
                if(YouTubeLiveBroadcast.currentBroadcast == null)
                {
                    Thread.Sleep(5000);
                    return;
                }

                //Plugin.Log($"Requesting chat messages for live chat with id {YouTubeChannel.liveOrDefaultChatId}...");
                HttpWebRequest web = (HttpWebRequest)WebRequest.Create($"https://www.googleapis.com/youtube/v3/liveChat/messages?liveChatId={YouTubeLiveBroadcast.currentBroadcast.snippet.liveChatId}&part=id%2Csnippet%2CauthorDetails{(_nextPageToken!=""? $"&pageToken={_nextPageToken}" : "")}");
                web.Method = "GET";
                web.Headers.Add("Authorization", $"{YouTubeOAuthToken.tokenType} {YouTubeOAuthToken.accessToken}");
                web.Accept = "application/json";

                using (HttpWebResponse resp = (HttpWebResponse)web.GetResponse())
                {
                    if (resp.StatusCode == HttpStatusCode.OK)
                    {
                        using (Stream dataStream = resp.GetResponseStream())
                        {
                            using (StreamReader reader = new StreamReader(dataStream))
                            {
                                string ret = reader.ReadToEnd();
                                Process(ret);
                                //Plugin.Log($"Chat: {ret}");
                            }
                        }
                    }
                    else
                    {
                        Plugin.Log($"Error: {resp.StatusCode.ToString()}");
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
                            // If the refresh fails, we need to request permission from the user again as they probably unapproved our app
                            YouTubeOAuthToken.Invalidate();
                            YouTubeOAuthToken.Generate();
                        }
                        break;
                    case HttpStatusCode.Forbidden:
                        Plugin.Log("The linked YouTube account is not enabled for live streaming, or the oauth quota has been reached.");
                        YouTubeConnection.Stop();
                        break;
                    default:
                        Plugin.Log($"WebException: {ex.ToString()}, Message: {ex.Message}");
                        break;
                }
                _pollingIntervalMillis = 3000;
            }
            catch (Exception ex)
            {
                Plugin.Log(ex.ToString());
                _pollingIntervalMillis = 3000;
            }
            Thread.Sleep(_pollingIntervalMillis);
        }
    }
}
