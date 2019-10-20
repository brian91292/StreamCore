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
    /// <para>This interface defines the main conduit through which YouTube events will be sent into your mod. </para>
    /// <br>Any class that implements IYouTubeMessageHandler will be *automatically* instantiated!</br> DO NOT MANUALLY INSTANTIATE AN INSTANCE <br>OF ANY CLASS THAT IMPLEMENTS IYouTubeMessageHandler, as it won't work!</br>
    /// <para>Additionally, if your class extends MonoBehaviour, make sure to call DontDestroyOnLoad on the newly created object if you don't want it to be destroyed when the scene switches :)</para>
    /// </summary>
    public interface IYouTubeMessageHandler : IGenericMessageHandler
    {
        /// <summary>
        /// YouTube OnMessageReceived event handler. *Note* The callback is NOT on the Unity thread!
        /// </summary>
        /// <param name="twitchMsg">The Twitch message that was received.</param>
        Action<YouTubeMessage> YouTube_OnMessageReceived { get; set; }
    }


    internal class YouTubeMessageHandler : GenericMessageHandlerWrapper<IYouTubeMessageHandler>
    {
        private static YouTubeMessageHandler _instance = null;
        internal static YouTubeMessageHandler Instance
        {
            get
            {
                if(_instance == null)
                {
                    _instance = new YouTubeMessageHandler();
                }
                return _instance;
            }
        }

        internal static void InvokeRegisteredCallbacks(YouTubeMessage message, string assemblyHash)
        {
            foreach (var handler in Instance.messageHandlers)
            {
                foreach (var instance in handler.Value)
                {
                    // Don't invoke the callback if the message was sent by the assembly that the current handler belongs to
                    if (handler.Key == assemblyHash)
                        continue;

                    try
                    {
                        switch (message.kind)
                        {
                            case "youtube#liveChatMessage":
                                instance.Value.YouTube_OnMessageReceived(message);
                                break;
                            default:
                                Plugin.Log($"Unhandled YouTube message type {message.kind}");
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

        internal static void YouTube_OnMessageReceived(IYouTubeMessageHandler handler, YouTubeMessage youTubeMessage)
        {
            SafeInvokeAction(handler.YouTube_OnMessageReceived, youTubeMessage);
        }
    }
}
