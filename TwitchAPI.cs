using StreamCore.Chat;
using StreamCore.Config;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace StreamCore
{
    public class TwitchAPI
    {
        public static void GetRoomsForChannel(TwitchChannel channel)
        {
            if (!TwitchWebSocketClient.LoggedIn)
                return;

            Plugin.Log($"Getting rooms for channel {channel.name}");

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(
                  $"https://api.twitch.tv/kraken/chat/{channel.roomId}/rooms");
                request.Credentials = CredentialCache.DefaultCredentials;

                request.Method = "GET";
                request.Accept = "application/vnd.twitchtv.v5+json";
                request.Headers.Set("Authorization", $"OAuth {TwitchLoginConfig.Instance.TwitchOAuthToken.Replace("oauth:", "")}");
                request.Headers.Set("Client-ID", "jg6ij5z8mf8jr8si22i5uq8tobnmde");
                
                WebResponse response = request.GetResponse();
                Stream dataStream = response.GetResponseStream();
                StreamReader reader = new StreamReader(dataStream);

                string responseFromServer = reader.ReadToEnd();
                
                reader.Close();
                response.Close();

                // Join the rooms
                channel.rooms = TwitchRoom.FromJson(responseFromServer);
            }
            catch(Exception ex)
            {
                Plugin.Log(ex.ToString());
            }
        }
    }
}
