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
        bool ChatCallbacksReady { get; set; }
    }

    internal class GenericMessageHandler<T> where T : IGenericMessageHandler
    {
        internal static Dictionary<Type, IGenericMessageHandler> registeredInstances = new Dictionary<Type, IGenericMessageHandler>();
        internal ConcurrentDictionary<string, Dictionary<Type, T>> messageHandlers = new ConcurrentDictionary<string, Dictionary<Type, T>>();

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
                        if(GenericMessageHandler<IGenericMessageHandler>.registeredInstances.TryGetValue(t, out var instance))
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
                            GenericMessageHandler<IGenericMessageHandler>.registeredInstances.Add(t, messageHandlers[assemblyHash][t]);
                        }
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        // now look at ex.LoaderExceptions - this is an Exception[], so:
                        foreach (Exception inner in ex.LoaderExceptions)
                        {
                            // write details of "inner", in particular inner.Message
                            Plugin.Log($"Inner: {inner.Message}");
                        }
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
}
