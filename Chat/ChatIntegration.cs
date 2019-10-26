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
    /// <summary>
    /// Don't implement this interface! This is a generic interface that does nothing!
    /// </summary>
    public interface IGenericChatIntegration
    {
        bool IsPluginReady { get; set; }
    }
    /// <summary>
    /// Implement this interface to initialize all available chat services
    /// </summary>
    public interface IGlobalChatIntegration : IGenericChatIntegration { }
    /// <summary>
    /// Implement this interface to initialize the YouTube chat service
    /// </summary>
    public interface IYouTubeIntegration : IGenericChatIntegration { }
    /// <summary>
    /// Implement this nterface to initialize the Twitch chat service
    /// </summary>
    public interface ITwitchIntegration : IGenericChatIntegration { }


    public class GlobalChatHandler
    {
        internal static Dictionary<Type, IGenericChatIntegration> registeredInstances = new Dictionary<Type, IGenericChatIntegration>();

        internal static ChatIntegration<ITwitchIntegration> Twitch = null;
        internal static ChatIntegration<IYouTubeIntegration> YouTube = null;

        internal static IEnumerator InitGlobalChatHandlers()
        {
            Twitch = new ChatIntegration<ITwitchIntegration>();
            YouTube = new ChatIntegration<IYouTubeIntegration>();

            bool initTwitch = false, initYouTube = false;
            // Iterate through all the message handlers that were registered
            foreach (var instance in registeredInstances)
            {
                var instanceType = instance.Value.GetType();
                var typeName = instanceType.Name;

                // Wait for all the registered handlers to be ready
                if (!instance.Value.IsPluginReady)
                {
                    Plugin.Log($"Instance of type {typeName} wasn't ready! Waiting until it is...");
                    yield return new WaitUntil(() => instance.Value.IsPluginReady);
                    Plugin.Log($"Instance of type {typeName} is ready!");
                }

                // Mark the correct services for initialization based on type
                if (typeof(ITwitchIntegration).IsAssignableFrom(instanceType))
                {
                    initTwitch = true;
                }
                if (typeof(IYouTubeIntegration).IsAssignableFrom(instanceType))
                {
                    initYouTube = true;
                }
                if (typeof(IGlobalChatIntegration).IsAssignableFrom(instanceType))
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

    internal class ChatIntegration<T> where T : IGenericChatIntegration
    {
        internal ConcurrentDictionary<string, Dictionary<Type, T>> chatHandlers = new ConcurrentDictionary<string, Dictionary<Type, T>>();

        internal ChatIntegration()
        {
            InitializeMessageHandlers();
        }

        // Scan all loaded assemblies and try to find any types that implement IChatIntegration
        private void InitializeMessageHandlers()
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
                        if (!chatHandlers.ContainsKey(assemblyHash))
                        {
                            chatHandlers[assemblyHash] = new Dictionary<Type, T>();
                        }

                        bool foundExistingInstance = false;
                        // If any other message handler has already instantiated an instance of this type, use that instance
                        if (GlobalChatHandler.registeredInstances.TryGetValue(t, out var instance))
                        {
                            chatHandlers[assemblyHash][t] = (T)instance;
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
                                chatHandlers[assemblyHash][t] = (T)(object)new GameObject(t.Name + "_Instance").AddComponent(t);
                            }
                            else
                            {
                                chatHandlers[assemblyHash][t] = (T)Activator.CreateInstance(t);
                            }
                            GlobalChatHandler.registeredInstances.Add(t, chatHandlers[assemblyHash][t]);
                        }
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        // Ignore any ReflectionTypeLoadExceptions and continue
                    }
                }
            }
        }
    }
}
