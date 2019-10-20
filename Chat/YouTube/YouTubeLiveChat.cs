using StreamCore.Chat;
using StreamCore.SimpleJSON;
using StreamCore.Utils;
using System;
using System.Collections.Concurrent;
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
        private static Task _sendMessageThread = null;
        private static ConcurrentQueue<string> _sendQueue = new ConcurrentQueue<string>();



        private static void SendMessageLoop(string message)
        {
            while (!Globals.IsApplicationExiting)
            {
                Thread.Sleep(500);
                if (_sendQueue.Count > 0 && _sendQueue.TryPeek(out var messageToSend))
                {
                    try
                    {
                        HttpWebRequest web = (HttpWebRequest)WebRequest.Create($"https://www.googleapis.com/youtube/v3/liveChat/messages?part=snippet");
                        web.Method = "POST";
                        web.Headers.Add("Authorization", $"{YouTubeOAuthToken.tokenType} {YouTubeOAuthToken.accessToken}");
                        web.ContentType = "application/json";

                        JSONObject container = new JSONObject();
                        container["snippet"] = new JSONObject();
                        container["snippet"]["liveChatId"] = new JSONString(YouTubeLiveBroadcast.currentBroadcast.snippet.liveChatId);
                        container["snippet"]["type"] = new JSONString("textMessageEvent");
                        container["snippet"]["textMessageDetails"] = new JSONObject();
                        container["snippet"]["textMessageDetails"]["messageText"] = new JSONString(message);
                        string snippetString = container.ToString();
                        Plugin.Log($"Sending {snippetString}");
                        var postData = Encoding.ASCII.GetBytes(snippetString);
                        web.ContentLength = postData.Length;

                        using (var stream = web.GetRequestStream())
                            stream.Write(postData, 0, postData.Length);

                        using (HttpWebResponse resp = (HttpWebResponse)web.GetResponse())
                        {
                            if (resp.StatusCode != HttpStatusCode.OK)
                            {
                                using (Stream dataStream = resp.GetResponseStream())
                                {
                                    using (StreamReader reader = new StreamReader(dataStream))
                                    {
                                        var response = reader.ReadToEnd();
                                        Plugin.Log($"Status: {resp.StatusCode} ({resp.StatusDescription}), Response: {response}");
                                        continue;
                                    }
                                }

                            }

                            using (Stream dataStream = resp.GetResponseStream())
                            {
                                using (StreamReader reader = new StreamReader(dataStream))
                                {
                                    // Read the response into a JSON objecet
                                    var json = JSON.Parse(reader.ReadToEnd()).AsObject;

                                    // Then create a new YouTubeMessage object from it and send it along to the other StreamCore clients, excluding the assembly that sent the message
                                    var newMessage = new YouTubeMessage();
                                    newMessage.Update(json);
                                    YouTubeMessageHandler.InvokeRegisteredCallbacks(newMessage, Assembly.GetCallingAssembly().GetHashCode().ToString());
                                    _sendQueue.TryDequeue(out var gone);
                                }
                            }
                        }
                    }
                    catch(ThreadAbortException ex)
                    {
                        return;
                    }
                    catch(Exception ex)
                    {
                        // Failed the send the message for some other reason, it will be retried next iteration
                        Plugin.Log($"Failed to send YouTube message, trying again in a few seconds! {ex.ToString()}");
                        Thread.Sleep(2500);
                    }
                }
            }
        }

        public static void SendMessage(string message)
        {
            if(_sendMessageThread == null)
            {
                _sendMessageThread = Task.Run(() => SendMessageLoop(message));
            }

            if (YouTubeLiveBroadcast.currentBroadcast == null)
            {
                _sendQueue.Enqueue(message);
            }
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

                YouTubeMessageHandler.InvokeRegisteredCallbacks(newMessage, String.Empty);
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
                // Read the response and log it
                using (Stream dataStream = ex.Response.GetResponseStream())
                {
                    using (StreamReader reader = new StreamReader(dataStream))
                    {
                        var response = reader.ReadToEnd();
                        Plugin.Log($"Status: {ex.Status}, Response: {response}");
                    }
                }

                switch (((HttpWebResponse)ex.Response).StatusCode)
                {
                    // If we hit an unauthorized exception, the users auth token has expired
                    case HttpStatusCode.Unauthorized:
                        // Try to refresh the users auth token, forcing it through even if our local timestamp says it's not expired
                        if (!YouTubeOAuthToken.Refresh(true))
                        {
                            // If the refresh fails, we need to request permission from the user again as they probably unapproved our app
                            YouTubeOAuthToken.Invalidate();
                            YouTubeOAuthToken.Generate();
                        }
                        break;
                    case HttpStatusCode.Forbidden:
                        YouTubeConnection.Stop();
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
