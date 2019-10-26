using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace StreamCore.YouTube
{
    internal class YouTubeCallbackListener
    {
        internal static int port = 64209;
        private static HttpServer _server;

        internal static void RunServer()
        {
            // Stop the server if it's already running
            if (_server != null && _server.IsListening)
                StopServer();

            Random rand = new Random();
            while (true)
            {
                try
                {
                    // Randomly select a port and try to start the server on it
                    port = rand.Next(49215, 65535);
                    _server = new HttpServer(port);

                    // Try to start the server on the randomly selected port
                    _server.Start();

                    // If an exception wasn't thrown, we're good
                    break;

                }
                catch { }
                Thread.Sleep(0);
            }

            // Setup our OnGet callback
            _server.OnGet += (s, e) => {
                HttpServer_OnGet(e);
            };

            Plugin.Log("Starting HTTP server on port " + port);
        }

        internal static void StopServer()
        {
            if (_server != null)
            {
                Plugin.Log("Stopping HTTP server");
                if (_server.IsListening)
                    _server.Stop();
            }
        }

        private static void HttpServer_OnGet(HttpRequestEventArgs e)
        {
            var request = e.Request;
            var response = e.Response;
            byte[] data = null;

            switch (request.Url.LocalPath)
            {
                case "/callback":
                    // If we successfully exchange our code for an auth token, request a listing of live broadcast info
                    if (YouTubeOAuthToken.Exchange(request.QueryString["code"]))
                    {
                        response.StatusCode = 307;
                        response.Redirect($"http://localhost:{port}/success");

                        // Start the YouTubeConnection
                        YouTubeConnection.Start();
                    }
                    else
                    {
                        response.StatusCode = 401;
                        response.Redirect($"http://localhost:{port}/failure");
                    }

                    // Close the response then stop the server
                    response.Close();
                    break;
                case "/success":
                    data = Encoding.UTF8.GetBytes("Success linking YouTube account! You may now close this page and return to the game.");
                    response.WriteContent(data);
                    response.StatusCode = 200;
                    response.Close();
                    StopServer();
                    break;
                case "/failure":
                    data = Encoding.UTF8.GetBytes("Failed to link YouTube account.");
                    response.WriteContent(data);
                    response.StatusCode = 200;
                    response.Close();
                    StopServer();
                    break;
                default:
                    response.StatusCode = 404;
                    StopServer();
                    break;
            }
        }
    }
}