using StreamCore.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace StreamCore.YouTube
{
    public class YouTubeMessageHandlers : GenericMessageHandler<YouTubeMessage>
    {
        #region Message Handler Dictionaries
        private static Dictionary<string, Action<YouTubeMessage>> _onMessageReceived_Callbacks = new Dictionary<string, Action<YouTubeMessage>>();
        private static Dictionary<string, Action> _onInitialize_Callbacks = new Dictionary<string, Action>();
        private static Dictionary<string, Action<YouTubeLiveBroadcastInfo>> _onConnectedToLiveChat_Callbacks = new Dictionary<string, Action<YouTubeLiveBroadcastInfo>>();
        private static Dictionary<string, Action<string>> _onYouTubeError = new Dictionary<string, Action<string>>();
        #endregion

        public static Action OnInitialize
        {
            set { lock (_onInitialize_Callbacks) { _onInitialize_Callbacks[Assembly.GetCallingAssembly().GetHashCode().ToString()] = value; } }
            get { return _onInitialize_Callbacks.TryGetValue(Assembly.GetCallingAssembly().GetHashCode().ToString(), out var callback) ? callback : null; }
        }

        public static Action<YouTubeLiveBroadcastInfo> OnConnectedToLiveChat
        {
            set { lock (_onConnectedToLiveChat_Callbacks) { _onConnectedToLiveChat_Callbacks[Assembly.GetCallingAssembly().GetHashCode().ToString()] = value; } }
            get { return _onConnectedToLiveChat_Callbacks.TryGetValue(Assembly.GetCallingAssembly().GetHashCode().ToString(), out var callback) ? callback : null; }
        }

        /// <summary>
        /// YouTube OnMessageReceived event handler. *Note* The callback is NOT on the Unity thread!
        /// </summary>
        public static Action<YouTubeMessage> OnMessageReceived
        {
            set { lock (_onMessageReceived_Callbacks) { _onMessageReceived_Callbacks[Assembly.GetCallingAssembly().GetHashCode().ToString()] = value; } }
            get { return _onMessageReceived_Callbacks.TryGetValue(Assembly.GetCallingAssembly().GetHashCode().ToString(), out var callback) ? callback : null; }
        }

        public static Action<string> OnYouTubeError
        {
            set { lock (_onYouTubeError) { _onYouTubeError[Assembly.GetCallingAssembly().GetHashCode().ToString()] = value; } }
            get { return _onYouTubeError.TryGetValue(Assembly.GetCallingAssembly().GetHashCode().ToString(), out var callback) ? callback : null; }
        }

        internal static void Initialize()
        {
            if (Initialized)
                return;

            // Initialize our message handlers
            _messageHandlers.Add("youtube#onInitialize", OnInitialize_Handler);
            _messageHandlers.Add("youtube#onConnectedToLiveChat", OnConnectedToLiveChat_Handler);
            _messageHandlers.Add("youtube#liveChatMessage", OnMessageReceived_Handler);
            _messageHandlers.Add("youtube#onError", OnYouTubeError_Handler);

            Initialized = true;
        }

        internal static void OnInitialize_Handler(YouTubeMessage message, string assemblyHash) 
        {
            SafeInvoke(_onInitialize_Callbacks, assemblyHash);
        }

        internal static void OnConnectedToLiveChat_Handler(YouTubeMessage message, string assemblyHash)
        {
            SafeInvoke(_onConnectedToLiveChat_Callbacks, YouTubeLiveBroadcast.currentBroadcast, assemblyHash);
        }

        internal static void OnMessageReceived_Handler(YouTubeMessage message, string assemblyHash)
        {
            SafeInvoke(_onMessageReceived_Callbacks, message, assemblyHash);
        }

        internal static void OnYouTubeError_Handler(YouTubeMessage message, string assemblyHash)
        {
            SafeInvoke(_onYouTubeError, YouTubeConnection.lastError, assemblyHash);
        }
    }
}
