//using EnhancedTwitchChat.Bot;
using StreamCore.Config;
using StreamCore.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;

namespace StreamCore.Chat
{
    /// <summary>
    /// The main Twitch websocket client.
    /// </summary>
    public class TwitchWebSocketClient
    {
        private static readonly Regex _twitchMessageRegex = new Regex(@"(@(?<Tags>[\S@\/;\-=,#]+) )?(:(?<HostName>[a-z0-9\.!@_]+) )?(?<!#|\S)()(?<MessageType>[A-Z0-9]+(?!\S))( \*)?( (#)?(?<ChannelName>[a-z0-9_:-]+))?( :(?<Message>.*))?", RegexOptions.Compiled);
        private static readonly Regex _tagRegex = new Regex(@"(?<Tag>[^@^;^=]+)=(?<Value>[^;\s]+)", RegexOptions.Compiled);

        private static Random _rand = new Random();
        private static WebSocket _ws;

        /// <summary>
        /// True if the client has been initialized already.
        /// </summary>
        public static bool Initialized { get; private set; } = false;

        /// <summary>
        /// True if the client is connected to Twitch.
        /// </summary>
        public static bool Connected { get; private set; } = false;

        /// <summary>
        /// True if the user has entered valid login details.
        /// </summary>
        public static bool LoggedIn { get; private set; } = true;

        /// <summary>
        /// The last time the client established a connection to the Twitch servers.
        /// </summary>
        public static DateTime ConnectionTime { get; private set; }

        /// <summary>
        /// A dictionary of channel information for every channel we've joined during this session, the key is the channel name.
        /// </summary>
        public static Dictionary<string, TwitchChannel> ChannelInfo { get; private set; } = new Dictionary<string, TwitchChannel>();

        /// <summary>
        /// A reference to the currently logged in Twitch user, will say **Invalid Twitch User** if the user is not logged in.
        /// </summary>
        public static TwitchUser OurTwitchUser { get; set; } = new TwitchUser("*Invalid Twitch User*");

        /// <summary>
        /// Callback for when the user changes the TwitchChannelName in TwitchLoginInfo.ini. *NOT THREAD SAFE, USE CAUTION!*
        /// </summary>
        public static Action<string> OnTwitchChannelUpdated;

        /// <summary>
        /// Callback for when TwitchLoginInfo.ini is updated *NOT THREAD SAFE, USE CAUTION!*
        /// </summary>
        public static Action OnConfigUpdated;

        /// <summary>
        /// Callback that occurs when a connection to the Twitch servers is successfully established. *NOT THREAD SAFE, USE CAUTION!*
        /// </summary>
        public static Action OnConnected;

        /// <summary>
        /// Callback that occurs when we get disconnected from the Twitch servers. *NOT THREAD SAFE, USE CAUTION!*
        /// </summary>
        public static Action OnDisconnected;
        
        /// <summary>
        /// True if the TwitchChannelName in TwitchLoginInfo.ini is valid, and we've joined the channel successfully.
        /// </summary>
        public static bool IsChannelValid
        {
            get
            {
                return ChannelInfo.ContainsKey(TwitchLoginConfig.Instance.TwitchChannelName) && ChannelInfo[TwitchLoginConfig.Instance.TwitchChannelName].roomId != String.Empty;
            }
        }

        private static DateTime _sendLimitResetTime = DateTime.Now;
        private static Queue<string> _sendQueue = new Queue<string>();
        
        private static int _reconnectCooldown = 500;
        private static int _fullReconnects = -1;
        private static string _lastChannel = "";

        private static int _messagesSent = 0;
        private static int _sendResetInterval = 30;
        private static int _messageLimit
        {
            get { return (OurTwitchUser.isBroadcaster || OurTwitchUser.isMod) ? 100 : 20; } // Defines how many messages can be sent within _sendResetInterval without causing a global ban on twitch
        }

        
        /// <summary>
        /// Call this function once OnApplicationStart if you intend to use the Twitch chat API
        /// </summary>
        public static void Initialize()
        {
            if (Initialized)
                return;

            TwitchMessageHandlers.Initialize();

            // Stop config updated callback when we haven't switched channels
            _lastChannel = TwitchLoginConfig.Instance.TwitchChannelName;

            TwitchLoginConfig.Instance.ConfigChangedEvent += Instance_ConfigChangedEvent;

            Initialized = true;

            Task.Run(() =>
            {
                // Sleep for a second before connecting, to allow for other plugins to register their callbacks incase they depend on a connected callback or something
                Thread.Sleep(1000);
                Connect();
            });
        }

        private static void Instance_ConfigChangedEvent(TwitchLoginConfig obj)
        {
            LoggedIn = true;

            if (Connected)
            {
                if (TwitchLoginConfig.Instance.TwitchChannelName != _lastChannel)
                {
                    if (_lastChannel != String.Empty)
                        PartChannel(_lastChannel);
                    if (TwitchLoginConfig.Instance.TwitchChannelName != String.Empty)
                        JoinChannel(TwitchLoginConfig.Instance.TwitchChannelName);
                    ConnectionTime = DateTime.Now;

                    // Invoke OnTwitchChannelUpdated event
                    if(OnTwitchChannelUpdated != null)
                    {
                        foreach(var e in OnTwitchChannelUpdated.GetInvocationList())
                        {
                            try
                            {
                                e?.DynamicInvoke(TwitchLoginConfig.Instance.TwitchChannelName);
                            }
                            catch (Exception ex)
                            {
                                Plugin.Log(ex.ToString());
                            }
                        }
                    }
                }
                _lastChannel = TwitchLoginConfig.Instance.TwitchChannelName;
            }

            // Invoke OnConfigUpdated event
            if (OnConfigUpdated != null)
            {
                foreach (var e in OnConfigUpdated.GetInvocationList())
                {
                    try
                    {
                        e?.DynamicInvoke();
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log(ex.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// Shuts down the websocket client, called internally. There is no need to call this function.
        /// </summary>
        public static void Shutdown()
        {
            if (Connected)
            {
                Connected = false;
                if (_ws.IsConnected)
                    _ws.Close();
            }
        }

        private static void Connect(bool isManualReconnect = false)
        {
            // If they entered invalid login info before, wait here indefinitely until they edit the config manually
            while (!LoggedIn && !Globals.IsApplicationExiting)
                Thread.Sleep(500);

            if (Globals.IsApplicationExiting)
                return;

            Plugin.Log("Reconnecting!");

            try
            {
                if (_ws != null && _ws.IsConnected)
                {
                    Plugin.Log("Closing existing connnection to Twitch!");
                    _ws.Close();
                }
            }
            catch (Exception ex)
            {
                Plugin.Log(ex.ToString());
            }
            _fullReconnects++;

            try
            {
                // Create our websocket object and setup the callbacks
                using (_ws = new WebSocket("wss://irc-ws.chat.twitch.tv:443"))
                {
                    _ws.OnOpen += (sender, e) =>
                    {
                        // Reset our reconnect cooldown timer
                        _reconnectCooldown = 1000;

                        Plugin.Log("Connected to Twitch!");
                        _ws.Send("CAP REQ :twitch.tv/tags twitch.tv/commands twitch.tv/membership");

                        string username = TwitchLoginConfig.Instance.TwitchUsername;
                        if (username == String.Empty || TwitchLoginConfig.Instance.TwitchOAuthToken == String.Empty)
                            username = "justinfan" + _rand.Next(10000, 1000000);
                        else
                            _ws.Send($"PASS {TwitchLoginConfig.Instance.TwitchOAuthToken}");
                        _ws.Send($"NICK {username}");

                        if (TwitchLoginConfig.Instance.TwitchChannelName != String.Empty)
                            _ws.Send($"JOIN #{TwitchLoginConfig.Instance.TwitchChannelName}");

                        // Display a message in the chat informing the user whether or not the connection to the channel was successful
                        ConnectionTime = DateTime.Now;

                        // Invoke OnConnected event
                        if (OnConnected != null)
                        {
                            foreach (var ev in OnConnected.GetInvocationList())
                            {
                                try
                                {
                                    ev?.DynamicInvoke();
                                }
                                catch (Exception ex)
                                {
                                    Plugin.Log(ex.ToString());
                                }
                            }
                        }
                        Connected = true;
                    };

                    _ws.OnClose += (sender, e) =>
                    {
                        Plugin.Log("Twitch connection terminated.");
                        Connected = false;
                    };

                    _ws.OnError += (sender, e) =>
                    {
                        Plugin.Log($"An error occured in the twitch connection! Error: {e.Message}, Exception: {e.Exception}");
                        Connected = false;
                    };

                    _ws.OnMessage += Ws_OnMessage;

                    // Then start the connection
                    _ws.Connect();

                    // Create a new task to reconnect automatically if the connection dies for some unknown reason
                    Task.Run(() =>
                    {
                        try
                        {
                            DateTime nextPing = DateTime.Now.AddSeconds(30);
                            while (Connected && _ws.ReadyState == WebSocketState.Open)
                            {
                                //Plugin.Log("Connected and alive!");
                                Thread.Sleep(500);

                                if (nextPing < DateTime.Now)
                                {
                                    if (!_ws.IsAlive)
                                    {
                                        Plugin.Log("Ping failed, reconnecting!");
                                        break;
                                    }
                                    nextPing = DateTime.Now.AddSeconds(30);
                                }
                            }
                        }
                        catch(ThreadAbortException)
                        {
                            return;
                        }
                        catch (Exception ex)
                        {
                            Plugin.Log(ex.ToString());
                        }

                        // Invoke OnDisconnected event
                        if (OnDisconnected != null)
                        {
                            foreach (var ev in OnDisconnected.GetInvocationList())
                            {
                                try
                                {
                                    ev?.DynamicInvoke();
                                }
                                catch (Exception ex)
                                {
                                    Plugin.Log(ex.ToString());
                                }
                            }
                        }

                        if (!isManualReconnect)
                        {
                            Thread.Sleep(Math.Max(_reconnectCooldown *= 2, 120000));
                            Connect();
                        }
                    });
                    ProcessSendQueue(_fullReconnects);
                }
            }
            catch (ThreadAbortException)
            {
                // This usually gets hit if our application is exiting or something
                return;
            }
            catch (Exception ex)
            {
                Plugin.Log(ex.ToString());
                // Try to reconnect for any exception in the websocket client other than a ThreadAbortException
                Thread.Sleep(Math.Max(_reconnectCooldown *= 2, 120000));
                Connect();
            }
        }

        private static void ProcessSendQueue(int fullReconnects)
        {
            while(!Globals.IsApplicationExiting && _fullReconnects == fullReconnects)
            {
                if (LoggedIn && _ws.ReadyState == WebSocketState.Open)
                {
                    if (_sendLimitResetTime < DateTime.Now)
                    {
                        _messagesSent = 0;
                        _sendLimitResetTime = DateTime.Now.AddSeconds(_sendResetInterval);
                    }

                    if (_sendQueue.Count > 0)
                    {
                        if (_messagesSent < _messageLimit)
                        {
                            string msg = _sendQueue.Dequeue();
                            Plugin.Log($"Sending message {msg}");
                            _ws.Send(msg);
                            OnMessageReceived(msg, true);
                            _messagesSent++;
                        }
                    }
                }
                Thread.Sleep(250);
            }
            Plugin.Log("Exiting!");
        }

        /// <summary>
        /// Sends a raw message to the Twitch server.
        /// </summary>
        /// <param name="msg">The raw message to be sent.</param>
        public static void SendRawMessage(string msg)
        {
            if (LoggedIn && _ws.ReadyState == WebSocketState.Open)
                _sendQueue.Enqueue(msg);
        }

        /// <summary>
        /// Sends an escaped chat message to the current TwitchChannelName that has been entered in TwitchLoginInfo.ini.
        /// </summary>
        /// <param name="msg">The chat message to be sent.</param>
        public static void SendMessage(string msg)
        {
            SendCommand($"\uFEFF{msg}");
        }

        /// <summary>
        /// Sends an unescaped chat command to the current TwitchChannelName that has been entered in TwitchLoginInfo.ini.
        /// </summary>
        /// <param name="command">The chat command to be sent.</param>
        public static void SendCommand(string command)
        {
            if (LoggedIn && _ws.ReadyState == WebSocketState.Open && command.Length > 0)
                _sendQueue.Enqueue($"PRIVMSG #{TwitchLoginConfig.Instance.TwitchChannelName} :{command}");
        }

        /// <summary>
        /// Joins the specified Twitch channel.
        /// </summary>
        /// <param name="channel">The Twitch channel name to join.</param>
        public static void JoinChannel(string channel)
        {
            if (LoggedIn && _ws.ReadyState == WebSocketState.Open)
                SendRawMessage($"JOIN #{channel}");
        }

        /// <summary>
        /// Exits the specified Twitch channel.
        /// </summary>
        /// <param name="channel">The Twitch channel name to part from.</param>
        public static void PartChannel(string channel)
        {
            if (channel == TwitchLoginConfig.Instance.TwitchChannelName)
            {
                throw new Exception("Cannot part from the channel defined in TwitchLoginConfig.ini.");
            }

            if (LoggedIn && _ws.ReadyState == WebSocketState.Open)
                SendRawMessage($"PART #{channel}");
        }

        /// <summary>
        /// Joins the specified TwitchRoom.
        /// </summary>
        /// <param name="room">The TwitchRoom to join.</param>
        public static void JoinRoom(TwitchRoom room)
        {
            if (LoggedIn && _ws.ReadyState == WebSocketState.Open)
                SendRawMessage($"JOIN #chatrooms:{room.ownerId}:{room.id}");
        }

        /// <summary>
        /// Exits the specified TwitchRoom.
        /// </summary>
        /// <param name="room">The TwitchRoom to part from.</param>
        public static void PartRoom(TwitchRoom room)
        {
            if (LoggedIn && _ws.ReadyState == WebSocketState.Open)
                SendRawMessage($"PART #chatrooms:{room.ownerId}:{room.id}");
        }

        private static void OnMessageReceived(string rawMessage, bool isSendCallback = false)
        {
            try
            {
                Plugin.Log($"RawMsg: {rawMessage}");

                var messageType = _twitchMessageRegex.Match(rawMessage);
                if (messageType.Length == 0)
                {
                    Plugin.Log($"Unhandled message: {rawMessage}");
                    return;
                }

                if(!messageType.Groups["MessageType"].Success)
                {
                    Plugin.Log($"Failed to get messageType for message {rawMessage}");
                    return;
                }
                
                // Instantiate our twitch message
                TwitchMessage twitchMsg = new TwitchMessage();
                twitchMsg.rawMessage = rawMessage;
                twitchMsg.messageType = messageType.Groups["MessageType"].Value;
                twitchMsg.tags = _tagRegex.Matches(rawMessage);
                if (messageType.Groups["Message"].Success)
                    twitchMsg.message =  messageType.Groups["Message"].Value;
                if (messageType.Groups["HostName"].Success)
                    twitchMsg.hostString = messageType.Groups["HostName"].Value;
                if (messageType.Groups["ChannelName"].Success)
                    twitchMsg.channelName = messageType.Groups["ChannelName"].Value;

                // If this is a callback from the send function, populate it with our twitch users info/the current room info
                if (isSendCallback)
                {
                    twitchMsg.user = OurTwitchUser;
                    twitchMsg.hostString = OurTwitchUser.displayName;
                }

                // If the login fails, disconnect the websocket
                if(twitchMsg.messageType == "NOTICE")
                {
                    if (twitchMsg.message.StartsWith("Login authentication failed")) {
                        Plugin.Log($"Invalid Twitch login info! Closing connection!");
                        LoggedIn = false;
                        _ws.Close();
                    }
                }
                TwitchMessageHandlers.InvokeHandler(twitchMsg);
            }
            catch (Exception ex)
            {
                Plugin.Log(ex.ToString());
            }
        }
        
        private static void Ws_OnMessage(object sender, MessageEventArgs ev)
        {
            try
            {
                if (!ev.IsText) return;
                
                //Plugin.Log($"RawMsg: {ev.Data}");
                string rawMessage = ev.Data.TrimEnd();
                if (rawMessage.StartsWith("PING"))
                {
                    Plugin.Log("Ping... Pong.");
                    _ws.Send("PONG :tmi.twitch.tv");
                    return;
                }
                
                OnMessageReceived(rawMessage);
            }
            catch (Exception ex)
            {
                Plugin.Log(ex.ToString());
            }
        }
    }
}
