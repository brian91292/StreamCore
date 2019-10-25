using StreamCore.Twitch;
using StreamCore.YouTube;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace StreamCore.Chat
{
    public interface IGenericMessageHandler
    {
        /// <summary>
        /// Set this property to true once all your chat callbacks have been instantiated. StreamCore won't attempt to connect to any chat service until all handlers have set this value to true!
        /// </summary>
        bool ChatCallbacksReady { get; set; }
    }

    public enum GlobalMessageTypes
    {
        OnMessageReceived,
        OnSingleMessageDeleted,
        OnAllMessagesDeleted
    }

    public interface IGlobalMessageHandler : IGenericMessageHandler
    {
        /// <summary>
        /// Global message handler for text chat events
        /// </summary>
        Action<GenericChatMessage> Global_OnMessageReceived { get; set; }

        /// <summary>
        /// Global message handler for message deleted events
        /// </summary>
        Action<GenericChatMessage> Global_OnSingleMessageDeleted { get; set; }

        /// <summary>
        /// Global message handler for when all of a users messages are deleted
        /// </summary>
        Action<GenericChatMessage> Global_OnAllMessagesDeleted { get; set; }
    }

    /// <summary>
    /// Global handler for all supported chat clients
    /// </summary>
    public class GlobalMessageHandler : GlobalMessageHandlerWrapper<IGlobalMessageHandler>
    {
        internal static Dictionary<Type, IGenericMessageHandler> registeredInstances = new Dictionary<Type, IGenericMessageHandler>();
        private static GlobalMessageHandler _instance = null;
        internal static GlobalMessageHandler Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new GlobalMessageHandler();
                }
                return _instance;
            }
        }

        /// <summary>
        /// Sends a message to all connected chat clients
        /// </summary>
        /// <param name="message">The message to be sent.</param>
        public static void SendMessage(string message)
        {
            if (YouTubeConnection.initialized && YouTubeLiveBroadcast.currentBroadcast != null)
            {
                YouTubeLiveChat.SendMessage(message);
            }
            if (TwitchWebSocketClient.Initialized && TwitchWebSocketClient.IsChannelValid)
            {
                TwitchWebSocketClient.SendMessage(message);
            }
        }

        internal static void InvokeRegisteredCallbacks(GlobalMessageTypes type, GenericChatMessage message, string assemblyHash)
        {
            foreach (var handler in Instance.messageHandlers)
            {
                foreach (var instance in handler.Value)
                {
                    // Don't invoke the callback if the message was sent by the assembly that the current handler belongs to
                    if (handler.Key == assemblyHash)
                        continue;

                    switch(type)
                    {
                        case GlobalMessageTypes.OnMessageReceived:
                            instance.Value.Global_OnMessageReceived(message);
                            break;
                        case GlobalMessageTypes.OnSingleMessageDeleted:
                            instance.Value.Global_OnSingleMessageDeleted(message);
                            break;
                        case GlobalMessageTypes.OnAllMessagesDeleted:
                            instance.Value.Global_OnAllMessagesDeleted(message);
                            break;
                        default:
                            Plugin.Log($"Unhandled message type {type.ToString()}");
                            break;
                    }
                    
                }
            }
        }

        internal static IEnumerator CreateGlobalMessageHandlers()
        {
            // Attempt to initialize message handlers for each of our chat services
            TwitchMessageHandler.Instance.InitializeMessageHandlers();
            YouTubeMessageHandler.Instance.InitializeMessageHandlers();
            GlobalMessageHandler.Instance.InitializeMessageHandlers();

            bool initTwitch = false, initYouTube = false;
            // Iterate through all the message handlers that were registered
            foreach (var instance in GlobalMessageHandler.registeredInstances)
            {
                var instanceType = instance.Value.GetType();
                var typeName = instanceType.Name;

                // Wait for all the registered handlers to be ready
                if (!instance.Value.ChatCallbacksReady)
                {
                    Plugin.Log($"Instance of type {typeName} wasn't ready! Waiting until it is...");
                    yield return new WaitUntil(() => instance.Value.ChatCallbacksReady);
                    Plugin.Log($"Instance of type {typeName} is ready!");
                }

                // Mark the correct services for initialization based on type
                if (typeof(ITwitchMessageHandler).IsAssignableFrom(instanceType))
                {
                    initTwitch = true;
                }
                if (typeof(IYouTubeMessageHandler).IsAssignableFrom(instanceType))
                {
                    initYouTube = true;
                }
                if(typeof(IGlobalMessageHandler).IsAssignableFrom(instanceType))
                {
                    initTwitch = true;
                    initYouTube = true;
                }
            }

            // Initialize the appropriate streaming services
            if (initTwitch)
            {
                TwitchWebSocketClient.Initialize_Internal();
            }
            if (initYouTube)
            {
                YouTubeConnection.Initialize_Internal();
            }
        }
    }

    /// <summary>
    /// An internal wrapper for the IGenericMessageHandler interface
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class GlobalMessageHandlerWrapper<T> where T : IGenericMessageHandler
    {
        internal ConcurrentDictionary<string, Dictionary<Type, T>> messageHandlers = new ConcurrentDictionary<string, Dictionary<Type, T>>();

        // Scan all loaded assemblies and try to find any types that implement ITwitchMessageHandler
        internal void InitializeMessageHandlers()
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                // Try to load in the types from the current assembly, if it throws an exception then continue gracefully
                Type[] types = new Type[0];
                try { types = a.GetTypes(); }
                catch { continue; }

                foreach (Type t in types)
                {
                    try
                    {
                        // Type doesn't extend ITwitchMessageHandler, continue
                        if (typeof(T) == t || !typeof(T).IsAssignableFrom(t))
                            continue;

                        // Create a new type to handler dictionary, so we can check if a handler already exists for a given type
                        string assemblyHash = a.GetHashCode().ToString();
                        if (!messageHandlers.ContainsKey(assemblyHash))
                        {
                            messageHandlers[assemblyHash] = new Dictionary<Type, T>();
                        }

                        bool foundExistingInstance = false;
                        // If any other message handler has already instantiated an instance of this type, use that instance
                        if(GlobalMessageHandler.registeredInstances.TryGetValue(t, out var instance))
                        {
                            messageHandlers[assemblyHash][t] = (T)instance;
                            Plugin.Log($"Reusing existing instance for type {t.Name}");
                            foundExistingInstance = true;
                        }

                        // If we didn't find an existing instance of the class, instantiate a new one
                        if (!foundExistingInstance)
                        {
                            Plugin.Log($"Instantiating {typeof(T).Name} of type {t.Name} from assembly {a.GetName()}");

                            // Determine how to go about instantiating the object
                            if (t.IsSubclassOf(typeof(MonoBehaviour)))
                            {
                                messageHandlers[assemblyHash][t] = (T)(object)new GameObject(t.Name + "_Instance").AddComponent(t);
                            }
                            else
                            {
                                messageHandlers[assemblyHash][t] = (T)Activator.CreateInstance(t);
                            }
                            GlobalMessageHandler.registeredInstances.Add(t, messageHandlers[assemblyHash][t]);
                        }
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        // Ignore any ReflectionTypeLoadExceptions and continue
                    }
                }
            }
        }

        protected static void SafeInvokeAction<M>(Action<M> action, M message)
        {
            if (action == null)
                return;

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

        protected static void SafeInvokeAction<M,D>(Action<M,D> action, M message, D data)
        {
            if (action == null)
                return;

            foreach (var a in action.GetInvocationList())
            {
                try
                {
                    a?.DynamicInvoke(message, data);
                }
                catch (Exception ex)
                {
                    Plugin.Log(ex.ToString());
                }
            }
        }
    }
}
