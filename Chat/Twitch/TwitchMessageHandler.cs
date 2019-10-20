using StreamCore.Chat;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace StreamCore.Twitch
{
    /// <summary>
    /// <para>This interface defines the main conduit through which Twitch events will be sent into your mod. </para>
    /// <br>Any class that implements ITwitchMessageHandler will be *automatically* instantiated!</br> DO NOT MANUALLY INSTANTIATE AN INSTANCE <br>OF ANY CLASS THAT IMPLEMENTS ITwitchMessageHandler, as it won't work!</br>
    /// <para>Additionally, if your class extends MonoBehaviour, make sure to call DontDestroyOnLoad on the newly created object if you don't want it to be destroyed when the scene switches :)</para>
    /// </summary>
    public interface ITwitchMessageHandler : IGenericMessageHandler
    {
        /// <summary>
        /// Twitch PRIVMSG event handler. *Note* The callback is NOT on the Unity thread!
        /// </summary>
        /// <param name="twitchMsg">The Twitch message that was received.</param>
        Action<TwitchMessage> Twitch_OnPrivmsgReceived { get; set; }

        /// <summary>
        /// Twitch ROOMSTATE event handler. *Note* The callback is NOT on the Unity thread!
        /// </summary>
        /// <param name="twitchMsg">The Twitch message that was received.</param>
        Action<TwitchMessage> Twitch_OnRoomstateReceived { get; set; }

        /// <summary>
        /// Twitch USERNOTICE event handler. *Note* The callback is NOT on the Unity thread!
        /// </summary>
        /// <param name="twitchMsg">The Twitch message that was received.</param>
        Action<TwitchMessage> Twitch_OnUsernoticeReceived { get; set; }

        /// <summary>
        /// Twitch USERSTATE event handler. *Note* The callback is NOT on the Unity thread!
        /// </summary>
        /// <param name="twitchMsg">The Twitch message that was received.</param>
        /// 
        Action<TwitchMessage> Twitch_OnUserstateReceived { get; set; }
        /// <summary>
        /// Twitch CLEARCHAT event handler. *Note* The callback is NOT on the Unity thread!
        /// </summary>
        /// <param name="twitchMsg">The Twitch message that was received.</param>
        Action<TwitchMessage> Twitch_OnClearchatReceived { get; set; }

        /// <summary>
        /// Twitch CLEARMSG event handler. *Note* The callback is NOT on the Unity thread!
        /// </summary>
        /// <param name="twitchMsg">The Twitch message that was received.</param>
        Action<TwitchMessage> Twitch_OnClearmsgReceived { get; set; }

        /// <summary>
        /// Twitch MODE event handler. *Note* The callback is NOT on the Unity thread!
        /// </summary>
        /// <param name="twitchMsg">The Twitch message that was received.</param>
        Action<TwitchMessage> Twitch_OnModeReceived { get; set; }

        /// <summary>
        /// Twitch JOIN event handler. *Note* The callback is NOT on the Unity thread!
        /// </summary>
        /// <param name="twitchMsg">The Twitch message that was received.</param>
        Action<TwitchMessage> Twitch_OnJoinReceived { get; set; }
    }

    internal class TwitchMessageHandler : GenericMessageHandlerWrapper<ITwitchMessageHandler>
    {
        private static TwitchMessageHandler _instance = null;
        internal static TwitchMessageHandler Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new TwitchMessageHandler();
                }
                return _instance;
            }
        }

        internal static void InvokeRegisteredCallbacks(TwitchMessage message, string assemblyHash)
        {
            foreach(var handler in Instance.messageHandlers)
            {
                foreach (var instance in handler.Value)
                {
                    // Don't invoke the callback if the message was sent by the assembly that the current handler belongs to
                    if (handler.Key == assemblyHash)
                        continue;

                    try
                    {
                        switch (message.messageType)
                        {
                            case "PRIVMSG":
                                Twitch_OnPrivmsgReceived(instance.Value, message);
                                break;
                            case "ROOMSTATE":
                                Twitch_OnRoomstateReceived(instance.Value, message);
                                break;
                            case "USERNOTICE":
                                Twitch_OnUsernoticeReceived(instance.Value, message);
                                break;
                            case "USERSTATE":
                                Twitch_OnUserstateReceived(instance.Value, message);
                                break;
                            case "CLEARCHAT":
                                Twitch_OnClearchatReceived(instance.Value, message);
                                break;
                            case "CLEARMSG":
                                Twitch_OnClearmsgReceived(instance.Value, message);
                                break;
                            case "MODE":
                                Twitch_OnModeReceived(instance.Value, message);
                                break;
                            case "JOIN":
                                Twitch_OnJoinReceived(instance.Value, message);
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

        private static void ParseRoomstateTags(MatchCollection tags, string channel)
        {
            if (!TwitchWebSocketClient.ChannelInfo.ContainsKey(channel))
                TwitchWebSocketClient.ChannelInfo.Add(channel, new TwitchChannel(channel));

            foreach (Match t in tags)
            {
                if (!t.Groups["Value"].Success)
                    continue;

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
        }

        private static void ParseMessageTags(MatchCollection matches, ref TwitchMessage twitchMsg)
        {
            foreach (Match t in matches)
            {
                if (!t.Groups["Value"].Success)
                {
                    Plugin.Log($"Skipping match {t.Groups["Tag"].Value}");
                    continue;
                }

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
                        twitchMsg.user.Twitch.isSub = twitchMsg.user.Twitch.badges.Contains("subscriber/") || twitchMsg.user.Twitch.badges.Contains("founder/");
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
        }

        internal static void Twitch_OnPrivmsgReceived(ITwitchMessageHandler handler, TwitchMessage twitchMsg)
        {
            twitchMsg.user.Twitch.username = twitchMsg.hostString.Split('!')[0];
            twitchMsg.user.displayName = twitchMsg.user.Twitch.username;
            ParseMessageTags(twitchMsg.tags, ref twitchMsg);
            SafeInvokeAction(handler.Twitch_OnPrivmsgReceived, twitchMsg);
        }

        internal static void Twitch_OnRoomstateReceived(ITwitchMessageHandler handler, TwitchMessage twitchMsg)
        {
            ParseRoomstateTags(twitchMsg.tags, twitchMsg.channelName);
            var channel = TwitchWebSocketClient.ChannelInfo[twitchMsg.channelName];
            if (channel.rooms == null)
                TwitchAPI.GetRoomsForChannelAsync(channel, null);
            SafeInvokeAction(handler.Twitch_OnRoomstateReceived, twitchMsg);
        }

        internal static void Twitch_OnUsernoticeReceived(ITwitchMessageHandler handler, TwitchMessage twitchMsg) 
        {
            ParseMessageTags(twitchMsg.tags, ref twitchMsg);
            SafeInvokeAction(handler.Twitch_OnUsernoticeReceived, twitchMsg);
        }

        internal static void Twitch_OnUserstateReceived(ITwitchMessageHandler handler, TwitchMessage twitchMsg) 
        {
            ParseMessageTags(twitchMsg.tags, ref twitchMsg);
            TwitchWebSocketClient.OurTwitchUser = twitchMsg.user.Twitch;
            SafeInvokeAction(handler.Twitch_OnUserstateReceived, twitchMsg);
        }

        internal static void Twitch_OnClearchatReceived(ITwitchMessageHandler handler, TwitchMessage twitchMsg) 
        {
            SafeInvokeAction(handler.Twitch_OnClearchatReceived, twitchMsg);
        }

        internal static void Twitch_OnClearmsgReceived(ITwitchMessageHandler handler, TwitchMessage twitchMsg) 
        {
            SafeInvokeAction(handler.Twitch_OnClearmsgReceived, twitchMsg);
        }

        internal static void Twitch_OnModeReceived(ITwitchMessageHandler handler, TwitchMessage twitchMsg) 
        {
            SafeInvokeAction(handler.Twitch_OnModeReceived, twitchMsg);
        }

        internal static void Twitch_OnJoinReceived(ITwitchMessageHandler handler, TwitchMessage twitchMsg)
        {
            if (!TwitchWebSocketClient.ChannelInfo.ContainsKey(twitchMsg.channelName))
                TwitchWebSocketClient.ChannelInfo.Add(twitchMsg.channelName, new TwitchChannel(twitchMsg.channelName));

            Plugin.Log($"Success joining channel #{twitchMsg.channelName}");
            SafeInvokeAction(handler.Twitch_OnJoinReceived, twitchMsg);
        }
    }
}
