using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StreamCore.Twitch
{
    public class TwitchChannel
    {
        public string name = "";
        public string roomId = "";
        public string lang = "";
        public bool emoteOnly;
        public bool followersOnly;
        public bool subsOnly;
        public bool r9k;
        public bool rituals;
        public bool slow;
        public List<TwitchRoom> rooms = null;
        public TwitchChannel(string channel)
        {
            name = channel;
        }
    }
}
