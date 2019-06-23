using StreamCore.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        #region Message Handler Dictionaries
        private static Dictionary<string, Action<TwitchMessage>> _PRIVMSG_CALLBACKS = new Dictionary<string, Action<TwitchMessage>>();
        private static Dictionary<string, Action<TwitchMessage>> _ROOMSTATE_CALLBACKS = new Dictionary<string, Action<TwitchMessage>>();
        private static Dictionary<string, Action<TwitchMessage>> _USERNOTICE_CALLBACKS = new Dictionary<string, Action<TwitchMessage>>();
        private static Dictionary<string, Action<TwitchMessage>> _USERSTATE_CALLBACKS = new Dictionary<string, Action<TwitchMessage>>();
        private static Dictionary<string, Action<TwitchMessage>> _CLEARCHAT_CALLBACKS = new Dictionary<string, Action<TwitchMessage>>();
        private static Dictionary<string, Action<TwitchMessage>> _CLEARMSG_CALLBACKS = new Dictionary<string, Action<TwitchMessage>>();
        private static Dictionary<string, Action<TwitchMessage>> _MODE_CALLBACKS = new Dictionary<string, Action<TwitchMessage>>();
        private static Dictionary<string, Action<TwitchMessage>> _JOIN_CALLBACKS = new Dictionary<string, Action<TwitchMessage>>();
        #endregion

        /// <summary>
        /// Twitch PRIVMSG event handler. *Note* The callback is NOT on the Unity thread!
        /// </summary>
        public static Action<TwitchMessage> PRIVMSG
        {
            set { lock(_PRIVMSG_CALLBACKS) {  _PRIVMSG_CALLBACKS[Assembly.GetCallingAssembly().GetHashCode().ToString()] = value; } }
            get { return _PRIVMSG_CALLBACKS.TryGetValue(Assembly.GetCallingAssembly().GetHashCode().ToString(), out var callback) ? callback : null; }
        }

        /// <summary>
        /// Twitch ROOMSTATE event handler. *Note* The callback is NOT on the Unity thread!
        /// </summary>
        public static Action<TwitchMessage> ROOMSTATE
        {
            set { lock (_ROOMSTATE_CALLBACKS) { _ROOMSTATE_CALLBACKS[Assembly.GetCallingAssembly().GetHashCode().ToString()] = value; } }
            get { return _ROOMSTATE_CALLBACKS.TryGetValue(Assembly.GetCallingAssembly().GetHashCode().ToString(), out var callback) ? callback : null; }
        }

        /// <summary>
        /// Twitch USERNOTICE event handler. *Note* The callback is NOT on the Unity thread!
        /// </summary>
        public static Action<TwitchMessage> USERNOTICE
        {
            set { lock (_USERNOTICE_CALLBACKS) { _USERNOTICE_CALLBACKS[Assembly.GetCallingAssembly().GetHashCode().ToString()] = value; } }
            get { return _USERNOTICE_CALLBACKS.TryGetValue(Assembly.GetCallingAssembly().GetHashCode().ToString(), out var callback) ? callback : null; }
        }

        /// <summary>
        /// Twitch USERSTATE event handler. *Note* The callback is NOT on the Unity thread!
        /// </summary>
        public static Action<TwitchMessage> USERSTATE
        {
            set { lock (_USERSTATE_CALLBACKS) { _USERSTATE_CALLBACKS[Assembly.GetCallingAssembly().GetHashCode().ToString()] = value; } }
            get { return _USERSTATE_CALLBACKS.TryGetValue(Assembly.GetCallingAssembly().GetHashCode().ToString(), out var callback) ? callback : null; }
        }

        /// <summary>
        /// Twitch CLEARCHAT event handler. *Note* The callback is NOT on the Unity thread!
        /// </summary>
        public static Action<TwitchMessage> CLEARCHAT
        {
            set { lock (_CLEARCHAT_CALLBACKS) { _CLEARCHAT_CALLBACKS[Assembly.GetCallingAssembly().GetHashCode().ToString()] = value; } }
            get { return _CLEARCHAT_CALLBACKS.TryGetValue(Assembly.GetCallingAssembly().GetHashCode().ToString(), out var callback) ? callback : null; }
        }

        /// <summary>
        /// Twitch CLEARMSG event handler. *Note* The callback is NOT on the Unity thread!
        /// </summary>
        public static Action<TwitchMessage> CLEARMSG
        {
            set { lock (_CLEARMSG_CALLBACKS) { _CLEARMSG_CALLBACKS[Assembly.GetCallingAssembly().GetHashCode().ToString()] = value; } }
            get { return _CLEARMSG_CALLBACKS.TryGetValue(Assembly.GetCallingAssembly().GetHashCode().ToString(), out var callback) ? callback : null; }
        }

        /// <summary>
        /// Twitch MODE event handler. *Note* The callback is NOT on the Unity thread!
        /// </summary>
        public static Action<TwitchMessage> MODE
        {
            set { lock (_MODE_CALLBACKS) { _MODE_CALLBACKS[Assembly.GetCallingAssembly().GetHashCode().ToString()] = value; } }
            get { return _MODE_CALLBACKS.TryGetValue(Assembly.GetCallingAssembly().GetHashCode().ToString(), out var callback) ? callback : null; }
        }

        /// <summary>
        /// Twitch JOIN event handler. *Note* The callback is NOT on the Unity thread!
        /// </summary>
        public static Action<TwitchMessage> JOIN
        {
            set { lock (_JOIN_CALLBACKS) { _JOIN_CALLBACKS[Assembly.GetCallingAssembly().GetHashCode().ToString()] = value; } }
            get { return _JOIN_CALLBACKS.TryGetValue(Assembly.GetCallingAssembly().GetHashCode().ToString(), out var callback) ? callback : null; }
        }


        private static bool Initialized = false;
        private static Dictionary<string, Action<TwitchMessage, string>> _messageHandlers = new Dictionary<string, Action<TwitchMessage, string>>();

        internal static void Initialize()
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

        internal static bool InvokeHandler(TwitchMessage twitchMsg, string assemblyHash)
        {
            // Call the appropriate handler for this messageType
            if (_messageHandlers.TryGetValue(twitchMsg.messageType, out var handler))
            {
                handler?.Invoke(twitchMsg, assemblyHash);
                return true;
            }
            return false;
        }

        private static void SafeInvoke(Dictionary<string, Action<TwitchMessage>> dict, TwitchMessage message, string invokerHash)
        {
            foreach (var instance in dict)
            {
                string assemblyHash = instance.Key;
                // Don't invoke the callback if it was registered by the assembly that sent the message which invoked this callback (no more vindaloop :D)
                if (assemblyHash == invokerHash)
                    continue;

                var action = instance.Value;
                if (action == null) return;

                foreach (var a in action.GetInvocationList())
                {
                    try
                    {
                        a?.DynamicInvoke(message);
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log(ex.ToString());
                    }
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
                    twitchMsg.user.Twitch.badges = t.Groups["Value"].Value;
                    twitchMsg.user.Twitch.isBroadcaster = twitchMsg.user.Twitch.badges.Contains("broadcaster/");
                    twitchMsg.user.Twitch.isSub = twitchMsg.user.Twitch.badges.Contains("subscriber/");
                    twitchMsg.user.Twitch.isTurbo = twitchMsg.user.Twitch.badges.Contains("turbo/");
                    twitchMsg.user.Twitch.isMod = twitchMsg.user.Twitch.badges.Contains("moderator/");
                    twitchMsg.user.Twitch.isVip = twitchMsg.user.Twitch.badges.Contains("vip/");
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

        private static void PRIVMSG_Handler(TwitchMessage twitchMsg, string invokerHash)
        {
            twitchMsg.user.Twitch.username = twitchMsg.hostString.Split('!')[0];
            twitchMsg.user.displayName = twitchMsg.user.Twitch.username;
            foreach (Match t in twitchMsg.tags)
                ParseMessageTag(t, ref twitchMsg);
            
            SafeInvoke(_PRIVMSG_CALLBACKS, twitchMsg, invokerHash);
        }

        private static void JOIN_Handler(TwitchMessage twitchMsg, string invokerHash)
        {
            if (!TwitchWebSocketClient.ChannelInfo.ContainsKey(twitchMsg.channelName))
                TwitchWebSocketClient.ChannelInfo.Add(twitchMsg.channelName, new TwitchChannel(twitchMsg.channelName));

            Plugin.Log($"Success joining channel #{twitchMsg.channelName}");
            SafeInvoke(_JOIN_CALLBACKS, twitchMsg, invokerHash);
        }

        private static void ROOMSTATE_Handler(TwitchMessage twitchMsg, string invokerHash)
        {
            foreach (Match t in twitchMsg.tags)
            ParseRoomstateTag(t, twitchMsg.channelName);

            var channel = TwitchWebSocketClient.ChannelInfo[twitchMsg.channelName];
            if (channel.rooms == null)
                TwitchAPI.GetRoomsForChannelAsync(channel, null);

            SafeInvoke(_ROOMSTATE_CALLBACKS, twitchMsg, invokerHash);
        }

        private static void USERNOTICE_Handler(TwitchMessage twitchMsg, string invokerHash)
        {
            foreach (Match t in twitchMsg.tags)
                ParseMessageTag(t, ref twitchMsg);

            SafeInvoke(_USERNOTICE_CALLBACKS, twitchMsg, invokerHash);
        }

        private static void USERSTATE_Handler(TwitchMessage twitchMsg, string invokerHash)
        {
            foreach (Match t in twitchMsg.tags)
                ParseMessageTag(t, ref twitchMsg);

            TwitchWebSocketClient.OurTwitchUser = twitchMsg.user.Twitch;

            SafeInvoke(_USERSTATE_CALLBACKS, twitchMsg, invokerHash);
        }

        private static void CLEARCHAT_Handler(TwitchMessage twitchMsg, string invokerHash)
        {
            SafeInvoke(_CLEARCHAT_CALLBACKS, twitchMsg, invokerHash);
        }

        private static void CLEARMSG_Handler(TwitchMessage twitchMsg, string invokerHash)
        {
            SafeInvoke(_CLEARMSG_CALLBACKS, twitchMsg, invokerHash);
        }

        private static void MODE_Handler(TwitchMessage twitchMsg, string invokerHash)
        {
            //Plugin.Log("MODE message received!");
            SafeInvoke(_MODE_CALLBACKS, twitchMsg, invokerHash);
        }

        private static void NOTICE_Handler(TwitchMessage twitchMsg, string invokerHash)
        {
            //    SafeInvoke(NOTICE, twitchMsg, invokerHash);
        }
    }
}
