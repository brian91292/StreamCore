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
    public interface IYouTubeMessageHandler : IGenericMessageHandler
    {
        Action<YouTubeMessage> YouTube_OnMessageReceived { get; set; }
    }


    internal class YouTubeMessageHandler : GenericMessageHandler<IYouTubeMessageHandler>
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
