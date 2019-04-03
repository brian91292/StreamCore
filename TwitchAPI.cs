using StreamCore.Chat;
using StreamCore.Config;
using StreamCore.SimpleJSON;
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
        // My twitch client id, get your own (╯°□°)╯︵ ┻━┻
        private static readonly string ClientId = "jg6ij5z8mf8jr8si22i5uq8tobnmde";


        public static void GetRoomsForChannelAsync(TwitchChannel channel, Action<bool, TwitchChannel> onCompleted)
        {
            Task.Run(() =>
            {
                bool success = GetRoomsForChannel(channel) != null;
                onCompleted?.Invoke(success, channel);
            });
        }
        
        public static List<TwitchRoom> GetRoomsForChannel(TwitchChannel channel)
        {
            if (!TwitchWebSocketClient.LoggedIn)
            {
                return null;
            }

            Plugin.Log($"Getting rooms for channel {channel.name}");

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create($"https://api.twitch.tv/kraken/chat/{channel.roomId}/rooms");
                request.Credentials = CredentialCache.DefaultCredentials;

                request.Method = "GET";
                request.Accept = "application/vnd.twitchtv.v5+json";
                request.Headers.Set("Authorization", $"OAuth {TwitchLoginConfig.Instance.TwitchOAuthToken.Replace("oauth:", "")}");
                request.Headers.Set("Client-ID", ClientId);

                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Stream dataStream = response.GetResponseStream();
                    StreamReader reader = new StreamReader(dataStream);

                    channel.rooms = TwitchRoom.FromJson(reader.ReadToEnd());

                    foreach (TwitchRoom r in channel.rooms)
                        Plugin.Log($"Room: {r.name}, ChannelName: {r.channelName}");

                    reader.Close();
                }
                response.Close();
            }
            catch (Exception ex)
            {
                Plugin.Log(ex.ToString());
            }
            return channel.rooms;
        }
    }
}
