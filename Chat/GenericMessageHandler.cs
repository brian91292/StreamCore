using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StreamCore.Chat
{
    public class GenericMessageHandler<T> where T : GenericChatMessage
    {
        protected static bool Initialized = false;
        protected static Dictionary<string, Action<T, string>> _messageHandlers = new Dictionary<string, Action<T, string>>();
        internal static bool InvokeHandler(T message, string assemblyHash)
        {
            // Call the appropriate handler for this messageType
            if (_messageHandlers.TryGetValue(message.messageType, out var handler))
            {
                handler?.Invoke(message, assemblyHash);
                return true;
            }
            return false;
        }

        protected static void SafeInvoke(Dictionary<string, Action> dict, string invokerHash)
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
                        a?.DynamicInvoke();
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log(ex.ToString());
                    }
                }
            }
        }

        protected static void SafeInvoke<A>(Dictionary<string, Action<A>> dict, A data, string invokerHash)
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
                        a?.DynamicInvoke(data);
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log(ex.ToString());
                    }
                }
            }
        }

        protected static void SafeInvoke<A, B>(Dictionary<string, Action<A, B>> dict, A data1, B data2, string invokerHash)
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
                        a?.DynamicInvoke(data1, data2);
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log(ex.ToString());
                    }
                }
            }
        }
    }
}
