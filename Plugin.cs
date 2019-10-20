using StreamCore.Chat;
using StreamCore.Config;
using IllusionPlugin;
using System;
using System.Collections;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using StreamCore.YouTube;
using StreamCore.Utils;

namespace StreamCore
{
    public class Plugin : IPlugin
    {
        public static readonly string ModuleName = "Stream Core";

        public string Name => ModuleName;
        public string Version => "2.0.0";

        public static Plugin Instance { get; private set; }
        
        private readonly TwitchLoginConfig TwitchLoginConfig = new TwitchLoginConfig();

        private static readonly object _loggerLock = new object();
        public static void Log(string text,
                [CallerFilePath] string file = "",
                [CallerMemberName] string member = "",
                [CallerLineNumber] int line = 0)
        {
            lock(_loggerLock) 
                Console.WriteLine($"{ModuleName}::{Path.GetFileName(file)}->{member}({line}): {text}");
        }

        private IEnumerator InitMessageHandlers()
        {
            TwitchMessageHandler.Instance.InitializeMessageHandlers();
            YouTubeMessageHandler.Instance.InitializeMessageHandlers();

            foreach (var instance in GenericMessageHandler<IGenericMessageHandler>.registeredInstances)
            {
                var instanceType = instance.Value.GetType();
                var typeName = instanceType.Name;

                // Wait for all the registered assemblies to be ready
                if (!instance.Value.ChatCallbacksReady)
                {
                    Log($"Instance of type {typeName} wasn't ready! Waiting until it is...");
                    yield return new WaitUntil(() => instance.Value.ChatCallbacksReady);
                    Log($"Instance of type {typeName} is ready!");
                }
                if (typeof(ITwitchMessageHandler).IsAssignableFrom(instanceType))
                {
                    TwitchWebSocketClient.Initialize_Internal();
                }
                if (typeof(IYouTubeMessageHandler).IsAssignableFrom(instanceType))
                {
                    YouTubeConnection.Initialize_Internal();
                }
            }
            
        }

        public void OnApplicationStart()
        {
            if (Instance != null) return;
            Instance = this;

            SharedMonoBehaviour.StartCoroutine(InitMessageHandlers());

            SceneManager.activeSceneChanged += SceneManager_activeSceneChanged;
            SceneManager.sceneLoaded += SceneManager_sceneLoaded;
        }

        private void SceneManager_activeSceneChanged(Scene from, Scene to)
        {
            if (to.name == "MenuCore")
                Globals.IsAtMainMenu = true;
            else if (to.name == "GameCore")
                Globals.IsAtMainMenu = false;
        }

        private void SceneManager_sceneLoaded(Scene arg0, LoadSceneMode arg1)
        {
            if(arg0.name == "MenuCore")
            {
                TwitchLoginConfig.Save(true);
            }
        }

        public void OnApplicationQuit()
        {
            SceneManager.activeSceneChanged -= SceneManager_activeSceneChanged;
            SceneManager.sceneLoaded -= SceneManager_sceneLoaded;

            Globals.IsApplicationExiting = true;

            // Cancel all running tasks
            TaskHelper.CancelAllTasks();

            // Shutdown our twitch client if it's initialized
            TwitchWebSocketClient.Shutdown();
        }

        public void OnLevelWasLoaded(int level)
        {
        }

        public void OnLevelWasInitialized(int level)
        {
        }

        public void OnUpdate()
        {
        }

        public void OnFixedUpdate()
        {
        }
    }
}
