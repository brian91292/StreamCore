using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using System.Drawing;
using IllusionInjector;
using IllusionPlugin;
using System.Reflection;
using System.IO.Compression;
using StreamCore.Chat;
using System.Security.Cryptography;

namespace StreamCore.Utils
{
    public class Utilities
    {
        public static void EmptyDirectory(string directory, bool delete = true)
        {
            if (Directory.Exists(directory))
            {
                var directoryInfo = new DirectoryInfo(directory);
                foreach (System.IO.FileInfo file in directoryInfo.GetFiles()) file.Delete();
                foreach (System.IO.DirectoryInfo subDirectory in directoryInfo.GetDirectories()) subDirectory.Delete(true);

                if (delete) Directory.Delete(directory);
            }
        }

        public static void MoveFilesRecursively(DirectoryInfo source, DirectoryInfo target)
        {
            foreach (DirectoryInfo dir in source.GetDirectories())
                MoveFilesRecursively(dir, target.CreateSubdirectory(dir.Name));
            foreach (FileInfo file in source.GetFiles())
            {
                string newFilePath = Path.Combine(target.FullName, file.Name);
                if (File.Exists(newFilePath))
                {
                    try
                    {
                        File.Delete(newFilePath);
                    }
                    catch (Exception)
                    {
                        //Plugin.Log($"Failed to delete file {Path.GetFileName(newFilePath)}! File is in use!");
                        string filesToDelete = Path.Combine(Environment.CurrentDirectory, "FilesToDelete");
                        if (!Directory.Exists(filesToDelete))
                            Directory.CreateDirectory(filesToDelete);
                        File.Move(newFilePath, Path.Combine(filesToDelete, file.Name));
                        //Plugin.Log("Moved file into FilesToDelete directory!");
                    }
                }
                file.MoveTo(newFilePath);
            }
        }

        public static IEnumerator ExtractZip(string zipPath, string extractPath)
        {
            if (File.Exists(zipPath))
            {
                bool extracted = false;
                try
                {
                    ZipFile.ExtractToDirectory(zipPath, ".requestcache");
                    extracted = true;
                }
                catch (Exception)
                {
                    Plugin.Log($"An error occured while trying to extract \"{zipPath}\"!");
                    yield break;
                }

                yield return new WaitForSeconds(0.25f);

                File.Delete(zipPath);
                try
                {
                    if (extracted)
                    {
                        if (!Directory.Exists(extractPath))
                            Directory.CreateDirectory(extractPath);

                        MoveFilesRecursively(new DirectoryInfo($"{Environment.CurrentDirectory}\\.requestcache"), new DirectoryInfo(extractPath));
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log($"An exception occured while trying to move files into their final directory! {e.ToString()}");
                }
            }
        }
        
        public enum DownloadType
        {
            Raw,
            Texture,
            Audio,
            AssetBundle
        }

        private static UnityWebRequest WebRequestForType(string url, DownloadType type, AudioType audioType = AudioType.OGGVORBIS)
        {
            switch(type)
            {
                case DownloadType.Raw:
                    return UnityWebRequest.Get(url);
                case DownloadType.Texture:
                    return UnityWebRequestTexture.GetTexture(url);
                case DownloadType.Audio:
                    return UnityWebRequestMultimedia.GetAudioClip(url, audioType);
                case DownloadType.AssetBundle:
                    return UnityWebRequestAssetBundle.GetAssetBundle(url);
            }
            return null;
        }

        public static IEnumerator Download(string url, DownloadType type, Action<UnityWebRequest> beforeSend, Action<UnityWebRequest> downloadCompleted, Action<UnityWebRequest> downloadFailed = null)
        {
            using (UnityWebRequest web = WebRequestForType(url, type))
            {
                if (web == null) yield break;

                beforeSend?.Invoke(web);

                // Send the web request
                yield return web.SendWebRequest();

                // Write the error if we encounter one
                if (web.isNetworkError || web.isHttpError)
                {
                    downloadFailed?.Invoke(web);
                    Plugin.Log($"Http error {web.responseCode} occurred during web request to url {url}. Error: {web.error}");
                    yield break;
                }
                downloadCompleted?.Invoke(web);
            }
        }

        public static IEnumerator DownloadSpriteAsync(string url, Action<Sprite> downloadCompleted)
        {
            yield return Download(url, DownloadType.Texture, null, (web) =>
            {
                downloadCompleted?.Invoke(LoadSpriteFromTexture(DownloadHandlerTexture.GetContent(web)));
            });
        }

        public static IEnumerator DownloadFile(string url, string path)
        {
            yield return Download(url, DownloadType.Raw, null, (web) =>
            {
                byte[] data = web.downloadHandler.data;
                try
                {
                    if (!Directory.Exists(Path.GetDirectoryName(path)))
                        Directory.CreateDirectory(Path.GetDirectoryName(path));

                    File.WriteAllBytes(path, data);
                }
                catch (Exception)
                {
                    Plugin.Log("Failed to download file!");
                }
            });
        }

        public static bool IsModInstalled(string modName)
        {
            foreach (IPlugin p in PluginManager.Plugins)
            {
                if (p.Name == modName)
                {
                    return true;
                }
            }
            return false;
        }
        
        public static string randomDataBase64url(uint length)
        {
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            byte[] bytes = new byte[length];
            rng.GetBytes(bytes);
            return base64urlencodeNoPadding(bytes);
        }
        
        public static byte[] sha256(string inputStirng)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(inputStirng);
            SHA256Managed sha256 = new SHA256Managed();
            return sha256.ComputeHash(bytes);
        }
        
        public static string base64urlencodeNoPadding(byte[] buffer)
        {
            string base64 = Convert.ToBase64String(buffer);

            // Converts base64 to base64url.
            base64 = base64.Replace("+", "-");
            base64 = base64.Replace("/", "_");
            // Strips padding.
            base64 = base64.Replace("=", "");

            return base64;
        }

        private static readonly Regex _stripHtmlRegex = new Regex("<.*?>", RegexOptions.Compiled);
        public static string EscapeHTML(string input)
        {
            return _stripHtmlRegex.Replace(input, m => m.Value.Insert(1, "\uFEFF"));
        }

        public static Texture2D LoadTextureRaw(byte[] file)
        {
            if (file.Count() > 0)
            {
                Texture2D Tex2D = new Texture2D(2, 2);
                if (Tex2D.LoadImage(file))
                    return Tex2D;
            }
            return null;
        }

        public static Texture2D LoadTextureFromFile(string FilePath)
        {
            if (File.Exists(FilePath))
                return LoadTextureRaw(File.ReadAllBytes(FilePath));

            return null;
        }

        public static Texture2D LoadTextureFromResources(string resourcePath)
        {
            return LoadTextureRaw(GetResource(Assembly.GetCallingAssembly(), resourcePath));
        }

        public static Sprite LoadSpriteRaw(byte[] image, float PixelsPerUnit = 100.0f)
        {
            return LoadSpriteFromTexture(LoadTextureRaw(image), PixelsPerUnit);
        }

        public static Sprite LoadSpriteFromTexture(Texture2D SpriteTexture, float PixelsPerUnit = 100.0f)
        {
            if (SpriteTexture)
                return Sprite.Create(SpriteTexture, new Rect(0, 0, SpriteTexture.width, SpriteTexture.height), new Vector2(0, 0), PixelsPerUnit);
            return null;
        }

        public static Sprite LoadSpriteFromFile(string FilePath, float PixelsPerUnit = 100.0f)
        {
            return LoadSpriteFromTexture(LoadTextureFromFile(FilePath), PixelsPerUnit);
        }

        public static Sprite LoadSpriteFromResources(string resourcePath, float PixelsPerUnit = 100.0f)
        {
            return LoadSpriteRaw(GetResource(Assembly.GetCallingAssembly(), resourcePath), PixelsPerUnit);
        }

        public static byte[] GetResource(Assembly asm, string ResourceName)
        {
            System.IO.Stream stream = asm.GetManifestResourceStream(ResourceName);
            byte[] data = new byte[stream.Length];
            stream.Read(data, 0, (int)stream.Length);
            return data;
        }

        public static void PrintHierarchy(Transform transform, string spacing = "|-> ")
        {
            spacing = spacing.Insert(1, "  ");
            var tempList = transform.Cast<Transform>().ToList();
            foreach (var child in tempList)
            {
                Console.WriteLine($"{spacing}{child.name}");
                PrintHierarchy(child, "|" + spacing);
            }
        }
    };
}
