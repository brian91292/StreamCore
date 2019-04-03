using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace StreamCore.Config
{
    class ObjectSerializer 
    {
        private static Dictionary<Type, Func<string, object>> ConvertFromString = new Dictionary<Type, Func<string, object>>();
        private static Dictionary<Type, Func<FieldInfo, object, string>> ConvertToString = new Dictionary<Type, Func<FieldInfo, object, string>>();
        private static void InitTypeHandlers()
        {
            // String handlers
            ConvertFromString.Add(typeof(string), (value) => { return (value.StartsWith("\"") && value.EndsWith("\"") ? value.Substring(1, value.Length - 2) : value); });
            ConvertToString.Add(typeof(string), (fieldInfo, obj) => { return $"\"{((string)obj.GetField(fieldInfo.Name))}\""; });

            // Short handlers
            ConvertFromString.Add(typeof(short), (value) => { short.TryParse(value, out var ret); return ret; });
            ConvertToString.Add(typeof(short), (fieldInfo, obj) => { return ((short)obj.GetField(fieldInfo.Name)).ToString(); });

            // Int handlers
            ConvertFromString.Add(typeof(int), (value) => { int.TryParse(value, out var ret); return ret; });
            ConvertToString.Add(typeof(int), (fieldInfo, obj) => { return ((int)obj.GetField(fieldInfo.Name)).ToString(); });

            // Long handlers
            ConvertFromString.Add(typeof(long), (value) => { long.TryParse(value, out var ret); return ret; });
            ConvertToString.Add(typeof(long), (fieldInfo, obj) => { return ((long)obj.GetField(fieldInfo.Name)).ToString(); });

            // Float handlers
            ConvertFromString.Add(typeof(float), (value) => { float.TryParse(value, out var ret); return ret; });
            ConvertToString.Add(typeof(float), (fieldInfo, obj) => { return ((float)obj.GetField(fieldInfo.Name)).ToString(); });

            // Double handlers
            ConvertFromString.Add(typeof(double), (value) => { double.TryParse(value, out var ret); return ret; });
            ConvertToString.Add(typeof(double), (fieldInfo, obj) => { return ((double)obj.GetField(fieldInfo.Name)).ToString(); });

            // Bool handlers
            ConvertFromString.Add(typeof(bool), (value) => { return (value.Equals("true", StringComparison.CurrentCultureIgnoreCase) || value.Equals("1")); });
            ConvertToString.Add(typeof(bool), (fieldInfo, obj) => { return ((bool)obj.GetField(fieldInfo.Name)).ToString(); });
        }

        public static void Load(object obj, string path)
        {
            if (ConvertFromString.Count == 0)
                InitTypeHandlers();

            if (File.Exists(path))
            {
                string[] lines = File.ReadAllLines(path);
                foreach (string line in lines)
                {
                    string[] parts = line.Split(new char[] { '=' }, 2);
                    if (parts.Length <= 1)
                        continue;

                    string key = parts[0];
                    string value = parts[1];

                    var fieldInfo = obj.GetType().GetField(key);
                    if (ConvertFromString.TryGetValue(fieldInfo.FieldType, out var convertFromString))
                        fieldInfo.SetValue(obj, convertFromString.Invoke(parts[1]));
                }
            }
        }

        public static void Save(object obj, string path)
        {
            if (ConvertToString.Count == 0)
                InitTypeHandlers();
            
            List<string> serializedClass = new List<string>();
            foreach (var field in obj.GetType().GetFields())
            {
                if (ConvertToString.TryGetValue(field.FieldType, out var convertToString))
                    serializedClass.Add($"{field.Name}={convertToString.Invoke(field, obj)}");
            }
            if (path != string.Empty && serializedClass.Count > 0)
            {
                string tmpPath = $"{path}.tmp";
                File.WriteAllLines(tmpPath, serializedClass.ToArray());
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(tmpPath, path);
            }
        }
    }
}
