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

namespace StreamCore.YouTube
{
    internal class YouTubeOAuthToken
    {
        private static string _clientId = String.Empty;
        private static string _clientSecret = String.Empty;
        private static readonly string _requestedScope = WebSocketSharp.Net.HttpUtility.UrlEncode("https://www.googleapis.com/auth/youtube");
        private static string _codeVerifier = "";
        private static string _redirectUrl
        {
            get => WebSocketSharp.Net.HttpUtility.UrlEncode($"http://localhost:{YouTubeCallbackListener.port}/callback");
        }

        internal static string accessToken = "";
        internal static string refreshToken = "";
        internal static DateTime expireTime;
        internal static string scope = "";
        internal static string tokenType = "";

        internal static bool isExpired { get => expireTime <= DateTime.UtcNow.Subtract(new TimeSpan(0, 1, 0)); }
        
        internal static bool Initialize(string ClientIdSecretPath)
        {
            try
            {
                // Parse the credentials into a JSON document
                JSONNode OAuthJSON = JSON.Parse(File.ReadAllText(ClientIdSecretPath));
                if (OAuthJSON == null)
                {
                    Plugin.Log("OAuthJSON was null!");
                    return false;
                }

                // Make sure the installed object is present- this indicates the user created the right type of credentials
                if (!OAuthJSON["installed"].IsObject)
                {
                    Plugin.Log("Missing \"installed\" section from YouTubeOAuth.json! Aborting!");
                    return false;
                }

                // Try to parse out the client id and client secret
                _clientId = OAuthJSON["installed"]["client_id"].Value;
                _clientSecret = OAuthJSON["installed"]["client_secret"].Value;
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log($"An error occurred while trying to parse YouTubeOAuth.json. {ex.ToString()}");
                return false;
            }
        }

        internal static void Generate()
        {
            _codeVerifier = Utilities.randomDataBase64url(32);
            string code_challenge = Utilities.base64urlencodeNoPadding(Utilities.sha256(_codeVerifier));

            // Run a local http server on a random port, then launch a web browser for the user to approve our app
            YouTubeCallbackListener.RunServer();
            Process.Start($"https://accounts.google.com/o/oauth2/v2/auth?client_id={_clientId}&access_type=offline&redirect_uri={_redirectUrl}&response_type=code&scope={_requestedScope}&code_challenge={code_challenge}&code_challenge_method=S256");
        }

        internal static void Invalidate()
        {
            // Stop polling for updates
            YouTubeConnection.Stop();

            // Set our local token to have expired an hour ago
            expireTime = DateTime.UtcNow.Subtract(new TimeSpan(1, 0, 0));
            Save();

            // If we fail to refresh the auth token, the user probably unapproved our app; so we need to request approval again
            File.Delete(Path.Combine(Globals.DataPath, "YouTubeOAuthToken.json"));
        }

        private static string Authenticate(bool refresh, byte[] postData)
        {
            // Create and setup the web request
            HttpWebRequest web = (HttpWebRequest)WebRequest.Create($"https://www.googleapis.com/oauth2/v4/token");
            web.Method = "POST";
            web.Host = "www.googleapis.com";
            web.ContentType = "application/x-www-form-urlencoded";
            web.ContentLength = postData.Length;

            // Write our post data to the request stream
            using (var stream = web.GetRequestStream())
                stream.Write(postData, 0, postData.Length);

            string token = "";
            using (HttpWebResponse resp = (HttpWebResponse)web.GetResponse())
            {
                if (resp.StatusCode != HttpStatusCode.OK)
                {
                    Plugin.Log($"Error: {resp.StatusCode}, Status: {resp.StatusCode} ({resp.StatusDescription})");
                    return "";
                }

                // Read our token into a string
                using (Stream dataStream = resp.GetResponseStream())
                {
                    using (StreamReader reader = new StreamReader(dataStream))
                    {
                        token = reader.ReadToEnd();
                    }
                }
            }
            return token;
        }

        internal static bool Exchange(string code)
        {
            try
            {
                // Submit our authorization code request
                string token = Authenticate(true, Encoding.ASCII.GetBytes($"code={code}&client_id={_clientId}&client_secret={_clientSecret}&redirect_uri={_redirectUrl}&grant_type=authorization_code&code_verifier={_codeVerifier}"));

                //Read the response in then save the new auth token
                if (!Update(token))
                {
                    Plugin.Log("Code was invalid! Failed to retrieve access/refresh token!");
                    return false;
                }

                Plugin.Log("Success retrieving auth/refresh token!");
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
                // If we aren't forcing the refresh and our token isn't expired, we can assume the auth token is valid
                if (!forceRefresh && !isExpired)
                {
                    Plugin.Log("Auth token is valid!");
                    return true;
                }

                // Submit our refresh token request
                string token = Authenticate(true, Encoding.ASCII.GetBytes($"refresh_token={refreshToken}&client_id={_clientId}&client_secret={_clientSecret}&redirect_uri={_redirectUrl}&grant_type=refresh_token"));

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

            if (save)
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
