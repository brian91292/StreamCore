using StreamCore.SimpleJSON;
using StreamCore.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net.NetworkInformation;
using StreamCore.Utilities;

namespace StreamCore.YouTube
{
    internal class YouTubeOAuthToken
    {
        private static readonly string _clientId =  //redacted for now
        private static readonly string _requestedScope = WebSocketSharp.Net.HttpUtility.UrlEncode("https://www.googleapis.com/auth/youtube");
        private static string _redirectUrl
        {
            get => WebSocketSharp.Net.HttpUtility.UrlEncode($"http://localhost:{YouTubeAuthServer.port}/callback");
        }

        internal static string accessToken = "";
        internal static string refreshToken = "";
        internal static DateTime expireTime;
        internal static string scope = "";
        internal static string tokenType = "";

        internal static bool isExpired { get => expireTime <= DateTime.UtcNow.Subtract(new TimeSpan(0, 1, 0)); }
        
        internal static void Generate()
        {
            YouTubeAuthServer.RunServer();
            Process.Start($"https://accounts.google.com/o/oauth2/v2/auth?client_id={_clientId}&redirect_uri={_redirectUrl}&response_type=code&scope={_requestedScope}");
        }
        
        internal static bool Exchange(string code)
        {
            try
            {
                HttpWebRequest web = (HttpWebRequest)WebRequest.Create($"https://brian91292.dev/youtube/oauth2/token?data={code}&redirect={_redirectUrl}");
                web.Method = "GET";

                HttpWebResponse resp = (HttpWebResponse)web.GetResponse();
                if (resp.StatusCode != HttpStatusCode.OK)
                {
                    Plugin.Log($"Error: {resp.StatusCode}, Status: {resp.StatusCode} ({resp.StatusDescription})");
                    return false;
                }
                
                // Update our token we got from the exchange
                Stream dataStream = resp.GetResponseStream();
                StreamReader reader = new StreamReader(dataStream);
                string token = reader.ReadToEnd();
                Update(token);
                //Plugin.Log($"Exchange: {token}");
                reader.Close();
                resp.Close();

                return true;
            }
            catch (WebException ex)
            {
                Plugin.Log($"Error: {ex.ToString()}");
            }
            return false;
        }

        internal static bool Refresh(bool forceRefresh = false)
        {
            try
            {
                if (!forceRefresh && !isExpired)
                {
                    Plugin.Log("Auth token is valid!");
                    return true;
                }

                HttpWebRequest web = (HttpWebRequest)WebRequest.Create($"https://brian91292.dev/youtube/oauth2/token?data={refreshToken}&redirect={_redirectUrl}&refresh");
                web.Method = "GET";

                HttpWebResponse resp = (HttpWebResponse)web.GetResponse();
                if (resp.StatusCode != HttpStatusCode.OK)
                {
                    Plugin.Log($"Error: {resp.StatusCode}, Status: {resp.StatusCode} ({resp.StatusDescription})");
                    return false;
                }

                // Read our token into a string
                Stream dataStream = resp.GetResponseStream();
                StreamReader reader = new StreamReader(dataStream);
                string token = reader.ReadToEnd();
                //Plugin.Log($"Refresh: {token}");
                reader.Close();
                resp.Close();

                //Read the response in then save the new auth token
                if (!Update(token))
                {
                    Plugin.Log("Refresh token was invalid! Failed to refresh access token!");
                    return false;
                }

                // Setup another event to be triggered a minute before our new token expires
                TaskHelper.ScheduleUniqueActionAtTime("YouTubeOAuthRefresh", () => Refresh(), expireTime.Subtract(new TimeSpan(0, 1, 0)));

                Plugin.Log("Success refreshing auth token!");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log($"Exception: {ex.ToString()}");
            }
            return false;
        }

        internal static bool Update(string json, bool save = true)
        {
            // Handle any json parsing errors
            if (json == string.Empty)
                return false;
            JSONNode node = JSON.Parse(json);
            if (node == null || node.IsNull)
                return false;

            // If an error key exists, it indicates that the user probably 
            // created a new auth token or revoked access to our app.
            if (node.HasKey("error") || !node.HasKey("access_token"))
            {
                Plugin.Log(json);
                return false;
            }

            if (node.HasKey("access_token"))
                accessToken = node["access_token"].Value;
            if (node.HasKey("refresh_token"))
                refreshToken = node["refresh_token"].Value;
            if (node.HasKey("expires_in"))
                expireTime = DateTime.UtcNow.AddSeconds(node["expires_in"].AsInt);
            else if (node.HasKey("expire_time"))
                expireTime = DateTimeOffset.FromUnixTimeSeconds(node["expire_time"].AsInt).UtcDateTime;
            if (node.HasKey("scope"))
                scope = node["scope"].Value;
            if (node.HasKey("token_type"))
                tokenType = node["token_type"].Value;

            if(save)
                Save();
            return true;
        }

        private static void Save()
        {
            JSONObject obj = new JSONObject();
            obj.Add("access_token", new JSONString(accessToken));
            obj.Add("refresh_token", new JSONString(refreshToken));
            obj.Add("expire_time", new JSONNumber(((DateTimeOffset)expireTime).ToUnixTimeSeconds()));
            obj.Add("scope", new JSONString(scope));
            obj.Add("token_type", new JSONString(tokenType));
            File.WriteAllText(Path.Combine(Globals.DataPath, "YouTubeOAuthToken.json"), obj.ToString());
        }
    }
}
