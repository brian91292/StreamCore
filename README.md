# Mod Info
StreamCore is an IPA based mod designed to allow developers to create interactive chat experiences with ease.

# Config
Config files can be found in the `UserData\StreamCore` folder.

### TwitchLoginInfo.ini
| Option | Description |
| - | - |
| **TwitchChannelName** | The name of the Twitch channel whos chat you want to join (this is your Twitch username if you want to join your own channel) |
| **TwitchUsername** | Your twitch username for the account you want to send messages as in chat (only matters if you're using the request bot) |
| **TwitchOAuthToken** | The oauth token corresponding to the TwitchUsername entered above ([Click here to generate an oauth token](https://twitchapps.com/tmi/))  |


# Basic Implementation (for devs)
Implementing StreamCore into your plugin is very simple, and can be accomplished in just a few steps.

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
Subscribe to any callbacks you want to receive. 

*This can be time sensitive for callbacks such as `TwitchWebSocketClient.OnConnected`, as if you don't subscribe in time you might miss the callback. StreamCore will delay the connection after calling `Initialize` for 1 second, which should allow all plugins that utilize StreamCore to subscribe to this event in time.*

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

