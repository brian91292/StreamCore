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
    internal class YouTubeAuthServer
    {
        internal static int port = 64209;
        private static HttpServer _server;

        public static void RunServer()
        {
            Random rand = new Random();
            while(true)
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
                catch {}
                Thread.Sleep(0);
            }

            // Setup our OnGet callback
            _server.OnGet += (s, e) => {
                HttpServer_OnGet(e);
            };

            Plugin.Log("Starting HTTP server on port " + port);
        }

        public static void StopServer()
        {
            if (_server != null)
            {
                Plugin.Log("Stopping HTTP server");
                if (_server.IsListening)
                    _server.Stop();
            }
        }

        public static void HttpServer_OnGet(HttpRequestEventArgs e)
        {
            var request = e.Request;
            var response = e.Response;

            string[] parts = request.RawUrl.Split(new char[] { '?' }, 2);
            if (parts.Length != 2)
            {
                return;
            }
            string url = parts[0];
            string query = parts[1];


            if(url == "/callback")
            {
                response.StatusCode = 307;

                string code = "";
                // Find the code param
                foreach (string param in query.Split(new char[] { '&' }))
                {
                    if (param.StartsWith("code="))
                        code = param.Split(new char[] { '=' })[1];
                }

                // If we successfully exchange our code for an auth token, request a listing of live broadcast info
                if (YouTubeOAuthToken.Exchange(code))
                {
                    // Redirect to our success page
                    response.Redirect("https://brian91292.dev/youtube/?success");
                    YouTubeConnection.StartServiceMonitors();
                }
                else
                    // Redirect to our error page
                    response.Redirect("https://brian91292.dev/youtube/?error");

                // Close the response then stop the server
                response.Close();
                StopServer();

                return;
            }
            response.StatusCode = 404;
        }
    }
}
