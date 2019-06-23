using StreamCore.SimpleJSON;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StreamCore.YouTube
{
    public class Thumbnail
    {
        public string url { get; internal set; } = "";
        public int width { get; internal set; } = 0;
        public int height { get; internal set; } = 0;

        internal void Update(JSONObject thumbnail)
        {
            url = thumbnail["url"].Value;
            width = thumbnail["width"].AsInt;
            height = thumbnail["height"].AsInt;
        }
    }

    public class Thumbnails
    {
        public Thumbnail @default { get; internal set; } = new Thumbnail();
        public Thumbnail medium { get; internal set; } = new Thumbnail();
        public Thumbnail high { get; internal set; } = new Thumbnail();

        internal void Update(JSONObject thumbnails)
        {
            @default.Update(thumbnails["default"].AsObject);
            medium.Update(thumbnails["medium"].AsObject);
            high.Update(thumbnails["high"].AsObject);
        }
    }
}
