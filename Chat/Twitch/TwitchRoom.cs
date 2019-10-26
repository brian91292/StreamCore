using StreamCore.SimpleJSON;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StreamCore.Twitch
{
    public class TwitchRoom
    {
        public string id;
        public string ownerId;
        public string name;
        public string topic;
        public string minimumAllowedRole;
        public bool isPreviewable;
        public string channelName
        {
            get
            {
                return $"chatrooms:{ownerId}:{id}";
            }
        }

        public static List<TwitchRoom> FromJson(string json)
        {
            if (json == string.Empty)
                return new List<TwitchRoom>();

            JSONNode node = JSON.Parse(json);
            if (node == null || node.IsNull)
                return new List<TwitchRoom>();

            if (node["_total"].AsInt == 0)
                return new List<TwitchRoom>();

            List<TwitchRoom> rooms = new List<TwitchRoom>();
            foreach(JSONObject room in node["rooms"].AsArray)
            {
                rooms.Add(new TwitchRoom()
                {
                    id = room["_id"].Value,
                    ownerId = room["owner_id"].Value,
                    name = room["name"].Value,
                    topic = room["topic"].Value,
                    isPreviewable = room["is_previewable"].AsBool,
                    minimumAllowedRole = room["minimum_allowed_role"].Value
                });
            }
            return rooms;
        }
    }
}
