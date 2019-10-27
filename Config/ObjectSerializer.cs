using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace StreamCore.Config
{
    public class ObjectSerializer
    {
        private static readonly ConcurrentDictionary<Type, Func<FieldInfo, string, object>> ConvertFromString = new ConcurrentDictionary<Type, Func<FieldInfo, string, object>>();
        private static readonly ConcurrentDictionary<Type, Func<FieldInfo, object, string>> ConvertToString = new ConcurrentDictionary<Type, Func<FieldInfo, object, string>>();
        private static void InitTypeHandlers()
        {
            // String handlers
            ConvertFromString.TryAdd(typeof(string), (fieldInfo, value) => { return (value.StartsWith("\"") && value.EndsWith("\"") ? value.Substring(1, value.Length - 2) : value); });
            ConvertToString.TryAdd(typeof(string), (fieldInfo, obj) => { return $"\"{((string)obj.GetField(fieldInfo.Name))}\""; });

            // Bool handlers
            ConvertFromString.TryAdd(typeof(bool), (fieldInfo, value) => { return (value.Equals("true", StringComparison.CurrentCultureIgnoreCase) || value.Equals("1")); });
            ConvertToString.TryAdd(typeof(bool), (fieldInfo, obj) => { return ((bool)obj.GetField(fieldInfo.Name)).ToString(); });

            // Generic handler
            ConvertFromString.TryAdd(typeof(object), (fieldInfo, value) =>
            {
                // If the generic handler was called, try to figure out how to convert the data
                if(CreateDynamicFieldConverter(fieldInfo))
                {
                    return ConvertFromString[fieldInfo.FieldType].DynamicInvoke(fieldInfo, value);
                }
                return null;
            });
            ConvertToString.TryAdd(typeof(object), (fieldInfo, obj) => 
            {
                // If the generic handler was called, try to figure out how to convert the data
                if (CreateDynamicFieldConverter(fieldInfo))
                {
                    return (string)ConvertToString[fieldInfo.FieldType].DynamicInvoke(fieldInfo, obj);
                }
                return null;
            });
        }

        private static bool CreateDynamicFieldConverter(FieldInfo fieldInfo)
        {
            var functions = fieldInfo.FieldType.GetRuntimeMethods();
            foreach (var func in functions)
            {
                switch(func.Name)
                {
                    case "TryParse":
                        var parameters = func.GetParameters();
                        if (parameters.Count() != 2)
                            continue;

                        ConvertFromString.TryAdd(fieldInfo.FieldType, (fi, v) =>
                        {
                            //Plugin.Log($"Parsing type {fi.FieldType.Name} from field {fi.Name}");
                            object ret = null;
                            try
                            {
                                var p = new object[] { v, null };
                                if ((bool)func.Invoke(null, p))
                                {
                                    ret = p[1];
                                }
                            }
                            catch (Exception ex)
                            {
                                Plugin.Log($"Error while parsing type {fi.FieldType.Name} from field {fi.Name}! {ex.ToString()}");
                            }
                            //Plugin.Log($"{fi.Name}={ret.ToString()}");
                            return ret;
                        });
                        ConvertToString.TryAdd(fieldInfo.FieldType, (fi, v) => { return v.GetField(fi.Name).ToString(); });
                        return true;
                }
            }
            return false;
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
                    // Invoke our convertFromString method if it exists
                    if (!ConvertFromString.TryGetValue(fieldInfo.FieldType, out var convertFromString))
                    {
                        // If not, call the default conversion handler and pray for the best
                        ConvertFromString.TryGetValue(typeof(object), out convertFromString);
                    }
                    fieldInfo.SetValue(obj, convertFromString.Invoke(fieldInfo, value));
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
                // Invoke our convertFromString method if it exists
                if (!ConvertToString.TryGetValue(field.FieldType, out var convertToString))
                {
                    // If not, call the default conversion handler and pray for the best
                    ConvertToString.TryGetValue(typeof(object), out convertToString);
                }
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
