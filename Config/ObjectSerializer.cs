using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace StreamCore.Config
{
    public class ObjectSerializer
    {
        private static readonly Regex _configRegex = new Regex(@"(?<Name>[^=\/\/#\s]+)\s*=\s*(?<Value>"".+ ""|[^#\/\/\s]+)?\s*(([\/\/]{2,2}|[#])(?<Comment>.+)?)?", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly ConcurrentDictionary<Type, Func<FieldInfo, string, object>> ConvertFromString = new ConcurrentDictionary<Type, Func<FieldInfo, string, object>>();
        private static readonly ConcurrentDictionary<Type, Func<FieldInfo, object, string>> ConvertToString = new ConcurrentDictionary<Type, Func<FieldInfo, object, string>>();
        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> Comments = new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>();
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
                            Plugin.Log($"{fi.Name}={ret.ToString()}");
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
                if(!Comments.TryGetValue(path, out var comments))
                {
                    comments = new ConcurrentDictionary<string, string>();
                }
                var matches = _configRegex.Matches(File.ReadAllText(path));
                foreach (Match match in matches)
                {
                    // Grab the name, which has to exist or the regex wouldn't have matched
                    var name = match.Groups["Name"].Value;

                    // Check if any comments existed
                    if(match.Groups["Comment"].Success)
                    {
                        // Then store them in memory so we can write them back later on
                        comments[name] = match.Groups["Comment"].Value.TrimEnd(new char[] { '\n', '\r' });
                    }

                    // If there's no value, continue on at this point
                    if (!match.Groups["Value"].Success)
                        continue;
                    var value = match.Groups["Value"].Value;

                    // Otherwise, read the value in with the appropriate handler
                    var fieldInfo = obj.GetType().GetField(name);
                    // Invoke our ConvertFromString method if it exists
                    if (!ConvertFromString.TryGetValue(fieldInfo.FieldType, out var convertFromString))
                    {
                        // If not, call the default conversion handler and pray for the best
                        ConvertFromString.TryGetValue(typeof(object), out convertFromString);
                    }
                    // Call the appropriate handler based on the type
                    fieldInfo.SetValue(obj, convertFromString.Invoke(fieldInfo, value));
                }
                Comments[path] = comments;
            }
        }

        public static void Save(object obj, string path)
        {
            if (ConvertToString.Count == 0)
                InitTypeHandlers();

            Comments.TryGetValue(path, out var commentDict);

            List<string> serializedClass = new List<string>();
            foreach (var field in obj.GetType().GetFields())
            {
                // Invoke our convertFromString method if it exists
                if (!ConvertToString.TryGetValue(field.FieldType, out var convertToString))
                {
                    // If not, call the default conversion handler and pray for the best
                    ConvertToString.TryGetValue(typeof(object), out convertToString);
                }
                commentDict.TryGetValue(field.Name, out var comment);
                serializedClass.Add($"{field.Name}={convertToString.Invoke(field, obj)}{(comment!=null?" //"+comment:"")}");
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
