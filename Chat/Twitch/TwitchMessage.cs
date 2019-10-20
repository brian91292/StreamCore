using StreamCore.Chat;
using StreamCore.SimpleJSON;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace StreamCore.Twitch
{
    public class TwitchMessage : GenericChatMessage
    {
        public string rawMessage { get; set; } = "";
        public string hostString { get; set; } = "";
        public string messageType { get; set; } = "";
        public string channelName { get; set; } = "";
        public string roomId { get; set; } = "";
        public string emotes { get; set; } = "";
        public int bits { get; set; } = 0;

        /// <summary>
        /// All the tags associated with the current TwitchMessage. Tag = match.Groups["Tag"].Value, Value = match.Groups["Value"].Value
        /// </summary>
        public MatchCollection tags;
    }
}
