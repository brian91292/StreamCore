using StreamCore.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace StreamCore.Chat
{
    /// <summary>
    /// All the message handlers associated with a Twitch stream (PRIVMSG, ROOMSTATE, USERNOTICE, etc).
    /// </summary>
    public class TwitchMessageHandlers
    {
        /// <summary>
        /// Twitch PRIVMSG event handler. *NOT THREAD SAFE, USE CAUTION!*
        /// </summary>
        public static Action<TwitchMessage> PRIVMSG;
        /// <summary>
        /// Twitch ROOMSTATE event handler. *NOT THREAD SAFE, USE CAUTION!*
        /// </summary>
        public static Action<TwitchMessage> ROOMSTATE;
        /// <summary>
        /// Twitch USERNOTICE event handler. *NOT THREAD SAFE, USE CAUTION!*
        /// </summary>
        public static Action<TwitchMessage> USERNOTICE;
        /// <summary>
        /// Twitch USERSTATE event handler. *NOT THREAD SAFE, USE CAUTION!*
        /// </summary>
        public static Action<TwitchMessage> USERSTATE;
        /// <summary>
        /// Twitch CLEARCHAT event handler. *NOT THREAD SAFE, USE CAUTION!*
        /// </summary>
        public static Action<TwitchMessage> CLEARCHAT;
        /// <summary>
        /// Twitch CLEARMSG event handler. *NOT THREAD SAFE, USE CAUTION!*
        /// </summary>
        public static Action<TwitchMessage> CLEARMSG;
        /// <summary>
        /// Twitch MODE event handler. *NOT THREAD SAFE, USE CAUTION!*
        /// </summary>
        public static Action<TwitchMessage> MODE;
        /// <summary>
        /// Twitch JOIN event handler. *NOT THREAD SAFE, USE CAUTION!*
        /// </summary>
        public static Action<TwitchMessage> JOIN;

        private static bool Initialized = false;
        private static Dictionary<string, Action<TwitchMessage>> _messageHandlers = new Dictionary<string, Action<TwitchMessage>>();

        /// <summary>
        /// Initializes the internal message handler system. There is no need to call this function; it's called internally.
        /// </summary>
        public static void Initialize()
        {
            if (Initialized)
                return;

            // Initialize our message handlers
            _messageHandlers.Add("PRIVMSG", PRIVMSG_Handler);
            _messageHandlers.Add("ROOMSTATE", ROOMSTATE_Handler);
            _messageHandlers.Add("USERNOTICE", USERNOTICE_Handler);
            _messageHandlers.Add("USERSTATE", USERSTATE_Handler);
            _messageHandlers.Add("CLEARCHAT", CLEARCHAT_Handler);
            _messageHandlers.Add("CLEARMSG", CLEARMSG_Handler);
            _messageHandlers.Add("MODE", MODE_Handler);
            _messageHandlers.Add("JOIN", JOIN_Handler);
            _messageHandlers.Add("NOTICE", NOTICE_Handler);

            Initialized = true;
        }

        /// <summary>
        /// Invokes the proper message handler associated with the provided twitch message, assuming it exists.
        /// </summary>
        /// <param name="twitchMsg">The twitch message to invoke the message handler for.</param>
        /// <returns></returns>
        public static bool InvokeHandler(TwitchMessage twitchMsg)
        {
            // Call the appropriate handler for this messageType
            if (_messageHandlers.ContainsKey(twitchMsg.messageType))
            {
                _messageHandlers[twitchMsg.messageType]?.Invoke(twitchMsg);
                return true;
            }
            return false;
        }

        private static void SafeInvoke(Action<TwitchMessage> action, TwitchMessage message)
        {
            if (action == null) return;

            foreach(var a in action.GetInvocationList())
            {
                try
                {
                    a?.DynamicInvoke(message);
                }
                catch(Exception ex)
                {
                    Plugin.Log(ex.ToString());
                }
            }
        }

        private static void ParseRoomstateTag(Match t, string channel)
        {
            if (!TwitchWebSocketClient.ChannelInfo.ContainsKey(channel))
                TwitchWebSocketClient.ChannelInfo.Add(channel, new TwitchChannel(channel));

            switch (t.Groups["Tag"].Value)
            {
                case "broadcaster-lang":
                    TwitchWebSocketClient.ChannelInfo[channel].lang = t.Groups["Value"].Value;
                    break;
                case "emote-only":
                    TwitchWebSocketClient.ChannelInfo[channel].emoteOnly = t.Groups["Value"].Value == "1";
                    break;
                case "followers-only":
                    TwitchWebSocketClient.ChannelInfo[channel].followersOnly = t.Groups["Value"].Value == "1";
                    break;
                case "r9k":
                    TwitchWebSocketClient.ChannelInfo[channel].r9k = t.Groups["Value"].Value == "1";
                    break;
                case "rituals":
                    TwitchWebSocketClient.ChannelInfo[channel].rituals = t.Groups["Value"].Value == "1";
                    break;
                case "room-id":
                    TwitchWebSocketClient.ChannelInfo[channel].roomId = t.Groups["Value"].Value;
                    break;
                case "slow":
                    TwitchWebSocketClient.ChannelInfo[channel].slow = t.Groups["Value"].Value == "1";
                    break;
                case "subs-only":
                    TwitchWebSocketClient.ChannelInfo[channel].subsOnly = t.Groups["Value"].Value == "1";
                    break;
            }
        }

        private static void ParseMessageTag(Match t, ref TwitchMessage twitchMsg)
        {
            switch (t.Groups["Tag"].Value)
            {
                case "id":
                    twitchMsg.id = t.Groups["Value"].Value;
                    break;
                case "emotes":
                    twitchMsg.emotes = t.Groups["Value"].Value;
                    break;
                case "badges":
                    twitchMsg.user.badges = t.Groups["Value"].Value;
                    twitchMsg.user.isBroadcaster = twitchMsg.user.badges.Contains("broadcaster/");
                    twitchMsg.user.isSub = twitchMsg.user.badges.Contains("subscriber/");
                    twitchMsg.user.isTurbo = twitchMsg.user.badges.Contains("turbo/");
                    twitchMsg.user.isMod = twitchMsg.user.badges.Contains("moderator/");
                    break;
                case "color":
                    twitchMsg.user.color = t.Groups["Value"].Value;
                    break;
                case "display-name":
                    twitchMsg.user.displayName = t.Groups["Value"].Value;
                    break;
                case "user-id":
                    twitchMsg.user.id = t.Groups["Value"].Value;
                    break;
                case "bits":
                    twitchMsg.bits = int.Parse(t.Groups["Value"].Value);
                    break;
                    //case "flags":
                    //    twitchMsg.user.flags = t.Groups["Value"].Value;
                    //    break;
                    //case "emotes-only":
                    //    twitchMsg.emotesOnly = t.Groups["Value"].Value == "1";
                    //    break;
            }
        }

        private static void PRIVMSG_Handler(TwitchMessage twitchMsg)
        {
            twitchMsg.user.username = twitchMsg.hostString.Split('!')[0];
            twitchMsg.user.displayName = twitchMsg.user.username;
            foreach (Match t in twitchMsg.tags)
                ParseMessageTag(t, ref twitchMsg);
            
            SafeInvoke(PRIVMSG, twitchMsg);
        }

        private static void JOIN_Handler(TwitchMessage twitchMsg)
        {
            if (!TwitchWebSocketClient.ChannelInfo.ContainsKey(twitchMsg.channelName))
                TwitchWebSocketClient.ChannelInfo.Add(twitchMsg.channelName, new TwitchChannel(twitchMsg.channelName));

            Plugin.Log($"Success joining channel #{twitchMsg.channelName} (RoomID: {twitchMsg.roomId})");
            SafeInvoke(JOIN, twitchMsg);
        }

        private static void ROOMSTATE_Handler(TwitchMessage twitchMsg)
        {
            foreach (Match t in twitchMsg.tags)
            ParseRoomstateTag(t, twitchMsg.channelName);

            var channel = TwitchWebSocketClient.ChannelInfo[twitchMsg.channelName];
            if (channel.rooms == null)
                TwitchAPI.GetRoomsForChannelAsync(channel, null);

            SafeInvoke(ROOMSTATE, twitchMsg);
        }

        private static void USERNOTICE_Handler(TwitchMessage twitchMsg)
        {
            foreach (Match t in twitchMsg.tags)
                ParseMessageTag(t, ref twitchMsg);

            SafeInvoke(USERNOTICE, twitchMsg);
        }

        private static void USERSTATE_Handler(TwitchMessage twitchMsg)
        {
            foreach (Match t in twitchMsg.tags)
                ParseMessageTag(t, ref twitchMsg);

            TwitchWebSocketClient.OurTwitchUser = twitchMsg.user;

            SafeInvoke(USERSTATE, twitchMsg);
        }

        private static void CLEARCHAT_Handler(TwitchMessage twitchMsg)
        {
            SafeInvoke(CLEARCHAT, twitchMsg);
        }

        private static void CLEARMSG_Handler(TwitchMessage twitchMsg)
        {
            SafeInvoke(CLEARMSG, twitchMsg);
        }

        private static void MODE_Handler(TwitchMessage twitchMsg)
        {
            //Plugin.Log("MODE message received!");
            SafeInvoke(MODE, twitchMsg);
        }

        private static void NOTICE_Handler(TwitchMessage twitchMsg)
        {
            //SafeInvoke(NOTICE, twitchMsg);
        }
    }
}
