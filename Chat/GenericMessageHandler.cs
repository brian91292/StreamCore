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
    
    internal class GenericMessageHandlerWrapper<T> where T : IGenericMessageHandler
    {
        private static Dictionary<Type, IGenericMessageHandler> registeredInstances = new Dictionary<Type, IGenericMessageHandler>();
        internal ConcurrentDictionary<string, Dictionary<Type, T>> messageHandlers = new ConcurrentDictionary<string, Dictionary<Type, T>>();

        internal static IEnumerator CreateGlobalMessageHandlers()
        {
            // Attempt to initialize message handlers for each of our chat services
            TwitchMessageHandler.Instance.InitializeMessageHandlers();
            YouTubeMessageHandler.Instance.InitializeMessageHandlers();

            bool initTwitch = false, initYouTube = false;
            // Iterate through all the message handlers that were registered
            foreach (var instance in GenericMessageHandler.registeredInstances)
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

        // Scan all loaded assemblies and try to find any types that implement ITwitchMessageHandler
        internal void InitializeMessageHandlers()
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                string assemblyHash = a.GetHashCode().ToString();

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
                        if (!messageHandlers.ContainsKey(assemblyHash))
                        {
                            messageHandlers[assemblyHash] = new Dictionary<Type, T>();
                        }

                        bool foundExistingInstance = false;
                        // If any other message handler has already instantiated an instance of this type, use that instance
                        if(GenericMessageHandler.registeredInstances.TryGetValue(t, out var instance))
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
                            GenericMessageHandler.registeredInstances.Add(t, messageHandlers[assemblyHash][t]);
                        }
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        // Ignore any ReflectionTypeLoadExceptions and continue
                    }
                }
            }
        }

        protected static void SafeInvokeAction<M>(Action<M> action, M message) where M : GenericChatMessage
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
    }
    // Used to access static variables of the GenericMessageHandler class 
    internal class GenericMessageHandler : GenericMessageHandlerWrapper<IGenericMessageHandler> { }
}
