# Mod Info
StreamCore is an IPA based mod designed to allow developers to create interactive chat experiences with ease. At the time of writing StreamCore supports Twitch and YouTube.

# Config File
Config files can be found in the `UserData\StreamCore` folder. This folder will be referenced as the `config folder` for the remainder of this readme.

# Twitch Config
Twitch configuration is very straightforward, click the link below to generate an OAuth token and copy it into the Twitch login file described below.

### TwitchLoginInfo.ini
| Option | Description |
| - | - |
| **TwitchChannelName** | The name of the Twitch channel whos chat you want to join (this is your Twitch username if you want to join your own channel) |
| **TwitchUsername** | Your twitch username for the account you want to send messages as in chat (only matters if you're using the request bot) |
| **TwitchOAuthToken** | The oauth token corresponding to the TwitchUsername entered above ([Click here to generate an oauth token](https://twitchapps.com/tmi/))  |

# YouTube Config

## Important Info About the YouTube API
YouTube configuration is not so straightforward. It also isn't good for longer streams since the YouTube API is absolutely terrible and uses your YouTube APIv3 data quota to *read the chat*. 

What this means in layman's terms is that in a given day, you'll only be able to use StreamCore for about ~2 ish hours before you run out of quota. Sorry about this, I'll be implementing a way to use multiple sets of auth info in the future to "fix" this.

## YouTube Setup Instructions
### Step 1
First, head over to the [Google Developer API Console](https://console.developers.google.com), and if you don't have one already create a project. It doesn't matter what you name this project, but I would name it something related to StreamCore.

### Step 2
After you've created a project, click on the Credentials tab on the left and click the "create credentials" button, then click "OAuth client ID"

![Credentials](https://i.imgur.com/bE8HPzc.png)

### Step 3
Select application type "other" and enter a name, then click "Create".
![Credentials](https://i.imgur.com/m9ueTKI.png)

### Step 4
From the Credentials tab, click on the new OAuth credential you just created. You should see a screen with the client id/client secret for the OAuth credential you just created, click the "Download JSON" button and save it to your computer.
![OAuth](https://i.imgur.com/5yJInIR.png)

### Step 5
Rename the JSON file you just downloaded to `YouTubeClientId.json` and copy it into the StreamCore config folder. 

### Step 6
Now that we've got our OAuth credential, we need to enable the YouTube Data APIv3. To do this, head over to the [Google API Library](https://console.developers.google.com/apis/library).

### Step 7
Search for the "YouTube Data API v3" and click on it, then click "Enable".
![Library](https://i.imgur.com/ZT8eezQ.png)

### Step 8
Start up the game and a browser window should popup with the Google OAuth consent screen asking you to approve access to your account. After granting approval, StreamCore will automatically start reading chat from your live broadcast.


# Basic StreamCore Implementation (for devs)
Implementing StreamCore into your plugin is very simple, and can be accomplished in just a few steps.

## StreamCore version 2.x (Current Version)
### Step 1
Implement `ITwitchMessageHandler` or `IYouTubeMessageHandler` into the class which you want to receive the chat callbacks

**Note:** StreamCore will automatically instantiate instances of any classes that implement an `IGenericMessageHandler` in OnApplicationStart, so make sure you don't instantiate these classes anywhere in your own code!

### Step 2
Setup the chat message callbacks that were defined by the interface you implemented above.

### Step 3
After you have setup the chat message callbacks and your class is ready, set the `ChatCallbacksReady` property to `true` (this is part of the `IGenericMessageHandler` interface, so you'll see what I mean once you do step 1).

**Note:** As long as `ChatCallbacksReady` is set to false, StreamCore will not try to establish a connection to any chat services. This means you can effectively block StreamCore from establishing any chat connections for as long as you need until your class is ready.

### Example
```cs
using StreamCore;
using StreamCore.Chat;
using StreamCore.YouTube;
using StreamCore.Twitch;

namespace YourModsNamespace
{
    public class ChatMessageHandler : MonoBehaviour, ITwitchMessageHandler, IYouTubeMessageHandler 
    {
        public bool ChatCallbacksReady { get; set; } = false;
        public Action<TwitchMessage> Twitch_OnPrivmsgReceived { get; set; }
        public Action<TwitchMessage, TwitchChannel> Twitch_OnRoomstateReceived { get; set;  }
        public Action<TwitchMessage> Twitch_OnUsernoticeReceived { get; set;  }
        public Action<TwitchMessage> Twitch_OnUserstateReceived { get; set;  }
        public Action<TwitchMessage> Twitch_OnClearchatReceived { get; set;  }
        public Action<TwitchMessage> Twitch_OnClearmsgReceived { get; set;  }
        public Action<TwitchMessage> Twitch_OnModeReceived { get; set;  }
        public Action<TwitchMessage> Twitch_OnJoinReceived { get; set;  }
        public Action<YouTubeMessage> YouTube_OnMessageReceived { get; set; }
        
        public void Awake()
        {
            // Setup chat message callbacks
            Twitch_OnPrivmsgReceived += (twitchMsg) => {
                // do stuff with twitchMsg here
            };
            
            YouTube_OnMessageReceived += (youtubeMsg) => {
                // do stuff with youtubeMsg here
            };
            
            // Signal to StreamCore that this class is ready to receive chat callbacks
            ChatCallbacksReady = true;
        }
    }
}
```


## StreamCore versions < 2.0 (Old/Legacy Version)
### Step 1
Initialize StreamCore in `OnApplicationStart`

*It's important that you base your client around calling `Initialize` in `OnApplicationStart`, to ensure you don't miss any callbacks if any other plugins call `Initialize` in `OnApplicationStart`.*

Make sure to include `StreamCore.Chat`:
```cs
using StreamCore.Chat;
```

Then call `Initialize` for the chat service you want to initialize. For now, since only Twitch is supported, we'll just initialize `TwitchWebSocketClient`.
```cs
public void OnApplicationStart()
{
  TwitchWebSocketClient.Initialize();
}
```

### Step 2
Subscribe to any callbacks you want to receive. You probably want to do this in `OnApplicationStart` as well.

*This can be time sensitive for callbacks such as `TwitchWebSocketClient.OnConnected`, as if you don't subscribe in time you might miss the callback. StreamCore will delay the connection after calling `Initialize` for 1 second, which should allow all plugins that utilize StreamCore to subscribe to these events in time.*

```cs
TwitchWebSocketClient.OnConnected += () => 
{
  Console.WriteLine("Connected to Twitch!");
}

TwitchMessageHandlers.PRIVMSG += (twitchMessage) => 
{
  Console.WriteLine($"Received PRIVMSG from {twitchMessage.user.displayName} in channel {twitchMessage.channelName}. Message: {twitchMessage.message}");
};
```

