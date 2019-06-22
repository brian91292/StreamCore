using StreamCore.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace StreamCore.YouTube
{
    public class YouTubeConnection
    {
        public static void Initialize()
        {
            Plugin.Log("Initializing!");
            
            string tokenPath = Path.Combine(Globals.DataPath, "YouTubeOAuthToken.json");
            if (!File.Exists(tokenPath))
            {
                Plugin.Log("YouTubeOAuthToken.json does not exist, generating new auth token!");
                // If we haven't already retrieved an oauth token, generate a new one
                YouTubeOAuthToken.Generate();
                return;
            }

            Plugin.Log("Auth token file exists!");

            // Read our oauth token in from file
            if(!YouTubeOAuthToken.Update(File.ReadAllText(tokenPath), false))
            {
                Plugin.Log("Failed to parse oauth token file, generating new auth token!");
                // If we fail to parse the file, generate a new oauth token
                YouTubeOAuthToken.Generate();
                return;
            }

            Plugin.Log("Success parsing oauth token file!");

            // Check if our auth key is expired, and if so refresh it
            if (!YouTubeOAuthToken.Refresh())
            {
                Plugin.Log("Failed to refresh access token, generating new auth token!");
                // If we fail to refresh our access token, generate a new one
                YouTubeOAuthToken.Generate();
                return;
            }

            // Finally, request our live broadcast info if everything went well
            StartServiceMonitors();
        }
        
        internal static void StartServiceMonitors(bool isRetry = false)
        {
            TaskHelper.ScheduleUniqueActionAtTime("YouTubeOAuthRefresh", () => YouTubeOAuthToken.Refresh(), YouTubeOAuthToken.expireTime.Subtract(new TimeSpan(0, 1, 0)));
            TaskHelper.ScheduleUniqueRepeatingAction("YouTubeBroadcastInfoRefresh", () =>
            {
                try
                {
                    Plugin.Log($"Requesting live broadcast info (isRetry: {isRetry})");
                    HttpWebRequest web = (HttpWebRequest)WebRequest.Create("https://www.googleapis.com/youtube/v3/liveBroadcasts?part=id%2Csnippet%2CcontentDetails%2Cstatus&mine=true");
                    web.Method = "GET";
                    web.Headers.Add("Authorization", $"{YouTubeOAuthToken.tokenType} {YouTubeOAuthToken.accessToken}");
                    web.Accept = "application/json";

                    HttpWebResponse resp = (HttpWebResponse)web.GetResponse();
                    if (resp.StatusCode == HttpStatusCode.OK)
                    {
                        Stream dataStream = resp.GetResponseStream();
                        StreamReader reader = new StreamReader(dataStream);
                        string ret = reader.ReadToEnd();
                        Plugin.Log($"Resp: {ret}");
                        reader.Close();
                    }
                    else
                    {
                        Plugin.Log($"Error: {resp.StatusCode}");
                    }
                    resp.Close();
                }
                catch (WebException ex)
                {
                    switch (((HttpWebResponse)ex.Response).StatusCode)
                    {
                        // If we hit an unauthorized exception, the users auth token has expired
                        case HttpStatusCode.Unauthorized:
                            if (!isRetry)
                            {
                                // Try to refresh the users auth token, forcing it through even if our local timestamp says it's not expired
                                if (!YouTubeOAuthToken.Refresh(true))
                                {
                                    // If we fail to refresh the auth token, the user probably unapproved our app or manually browsed to the youtube-auth page replacing their old token
                                    File.Delete(Path.Combine(Globals.DataPath, "YouTubeOAuthToken.json"));
                                    YouTubeOAuthToken.Generate();
                                }
                            }
                            break;
                        case HttpStatusCode.Forbidden:
                            Plugin.Log("The linked YouTube account is not enabled for live streaming! Enable live streaming, then try again!");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log(ex.ToString());
                }
            }, 60000);
        }
    }
}
