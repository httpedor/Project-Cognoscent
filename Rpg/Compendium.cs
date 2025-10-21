using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rpg.Inventory;

namespace Rpg;

public static class Compendium
{
    public static event Action<string>? OnFolderRegistered;
    public static event Action<string, string, JsonObject>? OnEntryRegistered;
    public static event Action<string, string>? OnEntryRemoved;

    private class CompendiumEntry
    {
        public JsonObject? Data { get; set; }
        public object? Loaded { get; set; }
        public string? Note { get; set; }
    }

    private class FolderData
    {
        public Type Type { get; set; } = typeof(object);
        public Dictionary<string, CompendiumEntry> Entries { get; } = new();
        public Func<string, JsonObject, object?>? Builder { get; set; }
    }

    private static readonly Dictionary<string, FolderData> folders = new();
    private static readonly Dictionary<Type, string> typeToFolder = new();
    
    public static IEnumerable<string> Folders => folders.Keys;

    private static JsonObject Merge(JsonObject first, JsonObject second)
    {
        JsonObject result = new JsonObject();

        // Copy from first, skipping nulls
        foreach (var kvp in first)
        {
            if (kvp.Value is not null)
                result[kvp.Key] = kvp.Value.DeepClone();
        }

        // Merge from second
        foreach (var kvp in second)
        {
            if (kvp.Value is null)
            {
                result.Remove(kvp.Key); // Remove the key if null
                continue;
            }

            if (result[kvp.Key] is JsonObject obj1 && kvp.Value is JsonObject obj2)
            {
                result[kvp.Key] = Merge(obj1, obj2);
            }
            else if (result[kvp.Key] is JsonArray arr1 && kvp.Value is JsonArray arr2)
            {
                result[kvp.Key] = MergeArraysByName(arr1, arr2);
            }
            else
            {
                result[kvp.Key] = kvp.Value.DeepClone();
            }
        }

        return result;
    }

    private static JsonArray MergeArraysByName(JsonArray first, JsonArray second)
    {
        var merged = new JsonArray();
        var map = new Dictionary<string, JsonObject>();

        void AddOrMerge(JsonNode? node)
        {

            if (node is JsonObject obj)
            {
                JsonValue? idVal = null;
                string[] possibleKeys = ["name", "id"];
                foreach (var key in possibleKeys)
                {
                    if (obj[key] is JsonValue val && val.GetValueKind() == JsonValueKind.String)
                    {
                        idVal = val;
                        break;
                    }
                }
                if (idVal is { } nameVal && nameVal.GetValue<string>() is { } name)
                {
                    if (map.TryGetValue(name, out var existing))
                    {
                        map[name] = Merge(existing, obj);
                    }
                    else
                    {
                        map[name] = (JsonObject)obj.DeepClone();
                    }
                }
                else
                    merged.Add(node.DeepClone());
            }
            else
            {
                if (node is not null)
                    merged.Add(node.DeepClone());
            }

        }

        foreach (var item in first) AddOrMerge(item);
        foreach (var item in second) AddOrMerge(item);

        foreach (var obj in map.Values)
            merged.Add(obj);

        return merged;
    }

    public static void RegisterDefaults()
    {
        RegisterFolder<Skill>("Skills", 
            (id, json) => Skills.Exists(id) ? Skills.Get(id) : Skill.FromJson(id, json));
        RegisterFolder<Feature>("Features",
            (name, json) => Features.Exists(name) ? Features.Get(name) : Feature.FromJson(name, json));
        RegisterFolder<BodyModel>("Bodies", (_, json) => new BodyModel(json));
        RegisterFolder<Item>("Items", (name, json) => new Item(name, json));
        RegisterFolder<SkillTree>("SkillTrees", (_, json) => new SkillTree(json));
        RegisterFolder<Midia>("Midia", (fName, json) =>
        {
            string fileName = json["fileName"]!.GetValue<string>();
            bool parsed = Enum.TryParse(json["type"]?.GetValue<string>(), out MidiaType type);
            byte[] data = Convert.FromBase64String(json["data"]!.GetValue<string>());
            Midia ret;
            if (parsed)
                ret = new Midia(data, type);
            else
                ret = new Midia(data, fileName);
            return ret;
        });
        RegisterFolder<string>("Notes", (_, json) => json["text"]?.GetValue<string>() ?? "");
    }
    
    public static IEnumerable<(string fName, JsonObject obj)> GetFiles(string folder)
    {
        string path = "Data/" + folder;
        if (!Directory.Exists(path))
            yield break;

        Dictionary<string, JsonObject> processedFiles = new();
        List<(string fName, JsonObject obj)> toProcess = new();
        foreach (string file in Directory.GetFiles(path))
        {
            string json = File.ReadAllText(file);
            string? fName = null;
            JsonObject? parsed = null;
            try
            {
                var node = JsonNode.Parse(json);
                if (node is JsonObject obj)
                {
                    fName = file[(file.LastIndexOf('\\') + 1)..file.LastIndexOf('.')];
                    if (obj.ContainsKey("parent"))
                        toProcess.Add((fName, obj));
                    else
                    {
                        processedFiles[fName] = obj;
                        parsed = obj;
                    }
                }
            }
            catch (JsonException e)
            {
                Logger.LogError("Couldn't read JSON file " + file);
                Logger.LogError(e.ToString());
            }
            if (fName != null && parsed != null)
                yield return (fName, parsed);
        }

        while (toProcess.Count > 0)
        {
            var next = toProcess.First();
            string parent = next.obj["parent"]!.GetValue<string>();
            if (!processedFiles.TryGetValue(parent, out JsonObject? parentObj))
            {
                if (!toProcess.Select(x => x.fName).Contains(parent))
                {
                    Logger.LogWarning($"File {next.fName} is child of {parent} but it doesn't exist.");
                    toProcess.RemoveAt(0);
                }
                continue;
            }

            var result = Merge(parentObj, next.obj);
            toProcess.RemoveAt(0);
            processedFiles[next.fName] = result;
            yield return (next.fName, result);
        }
    }
    public static void RegisterFolder<T>(string folder, Func<string, JsonObject, T?>? builder = null) where T : class
    {
        folders[folder] = new FolderData { Type = typeof(T) };
        if (builder != null)
            folders[folder].Builder = builder;
        
        typeToFolder[typeof(T)] = folder;
        foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
        {
            if (!type.IsSubclassOf(typeof(T))) continue;
            
            typeToFolder[type] = folder;
        }
        
        OnFolderRegistered?.Invoke(folder);
    }

    public static object? RegisterEntry(string folder, string name, JsonObject data)
    {
        if (data == null)
        {
            if (folders[folder].Builder is not { } builder) return null;
            object? ret = builder(name, data);
            folders[folder].Entries[name] = new CompendiumEntry { Data = null, Loaded = ret };
            return ret;
        }
        else
        {
            CompendiumEntry entry = new() { Data = data };
            if (data["_note"] is JsonValue noteVal && noteVal.GetValueKind() == JsonValueKind.String)
                entry.Note = noteVal.GetValue<string>();
            folders[folder].Entries[name] = entry;
            OnEntryRegistered?.Invoke(folder, name, data);
            if (folders[folder].Builder is { } builder)
            {
                try
                {
                    object? ret = builder(name, data);
                    if (ret != null && ret.GetType().IsAssignableTo(folders[folder].Type))
                    {
                        entry.Loaded = ret;
                        return ret;
                    }
                    else
                    {
                        if (ret != null)
                            Logger.LogError("Data type mismatch: " + folder + "/" + name + " is not of type " + folders[folder].Type);
                        else
                            Logger.LogError("Failed to build entry: " + folder + "/" + name);
                    }
                }
                catch (Exception e)
                {

                    Logger.LogError("Exception occurred while building entry: " + folder + "/" + name);
                    Logger.LogError(e.Message);
                    // ignored
                }
            }

            Logger.LogError("Failed to load data: " + folder + "/" + name);
            folders[folder].Entries.Remove(name);
            return null;
        }
    }
    public static T? RegisterEntry<T>(string name, JsonObject data) where T : class
    {
        return RegisterEntry(GetFolderName<T>(), name, data) as T;
    }

    public static void RemoveEntry(string folder, string name)
    {
        if (folders.TryGetValue(folder, out var fd))
        {
            fd.Entries.Remove(name);
        }
        OnEntryRemoved?.Invoke(folder, name);
    }
    public static void RemoveEntry<T>(string name)
    {
        string folder = GetFolderName<T>();
        RemoveEntry(folder, name);
    }

    public static T? GetEntry<T>(string name) where T : class
    {
        string folder = GetFolderName<T>();
        if (!folders.TryGetValue(folder, out var fd)) return null;
        if (!fd.Entries.TryGetValue(name, out var entry)) return null;
        object? found = entry.Loaded;
        if (found != null && !found.GetType().IsSubclassOf(typeof(T)) && found.GetType() != typeof(T))
        {
            Logger.LogError("Data type mismatch: " + folder + "/" + name + " is not of type " + typeof(T));
            return null;
        }
        return (T?)found;
    }

    public static JsonObject? GetEntryJsonOrNull(string folder, string name)
    {
        return folders.TryGetValue(folder, out var fd) && fd.Entries.TryGetValue(name, out var entry) ? entry.Data : null;
    }

    public static JsonObject? GetEntryJsonOrNull<T>(string name)
    {
        return GetEntryJsonOrNull(GetFolderName<T>(), name);
    }
    public static JsonObject GetEntryJson(string folder, string name)
    {
        if (!folders.TryGetValue(folder, out var fd)) throw new Exception("Invalid folder: " + folder);
        if (!fd.Entries.TryGetValue(name, out var entry) || entry.Data == null) throw new Exception("Data not found: " + folder + "/" + name);
        return entry.Data;
    }
    public static JsonObject GetEntryJson<T>(string name)
    {
        string folder = GetFolderName<T>();
        return GetEntryJson(folder, name);
    }

    public static IEnumerable<string> GetEntryNames(string folder)
    {
        if (!folders.TryGetValue(folder, out var fd)) throw new Exception("Invalid data type: " + folder);
        return fd.Entries.Keys.ToArray();
    }

    public static IEnumerable<string> GetEntryNames<T>()
    {
        string folder = GetFolderName<T>();
        if (!folders.TryGetValue(folder, out var fd)) throw new Exception("Invalid data type: " + typeof(T));
        return fd.Entries.Keys.ToArray();
    }
    
    public static string GetFolderName<T>()
    {
        if (!typeToFolder.TryGetValue(typeof(T), out var folder)) throw new Exception("Invalid data type: " + typeof(T));
        return folder;
    }

    public static int GetEntryCount(string folderName)
    {
        return folders.TryGetValue(folderName, out var fd) ? fd.Entries.Count : 0;
    }

    public static int GetEntryCount<T>()
    { 
        return GetEntryCount(GetFolderName<T>());
    }

    public static void ClearFolder(string folder)
    {
        if (!folders.TryGetValue(folder, out var fd)) return;
        foreach (string entry in fd.Entries.Keys.ToArray())
            RemoveEntry(folder, entry);
    }
    public static void Clear()
    {
        foreach (string folder in Folders.ToArray())
        {
            ClearFolder(folder);
        }
    }
    
}
