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
    //Typename, Type
    private static readonly Dictionary<string, Type> types = new();
    private static readonly Dictionary<Type, string> names = new();
    
    private static Dictionary<string, Dictionary<string, JsonObject>> files = new();
    private static Dictionary<string, Dictionary<string, object>> loaded = new();
    private static Dictionary<string, Func<string, JsonObject, object?>> builders = new();
    
    public static IEnumerable<string> Folders => types.Keys;

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
        RegisterFolder<Body>("Bodies", (_, json) => Body.NewBody(json));
        RegisterFolder<Item>("Items", (name, json) => new Item(name, json));
        RegisterFolder<SkillTree>("SkillTrees", (_, json) => new SkillTree(json));
        RegisterFolder<Midia>("Midia", (fName, json) =>
        {
            string fileName = json["fileName"].GetValue<string>();
            bool parsed = Enum.TryParse(json["type"]?.GetValue<string>(), out MidiaType type);
            byte[] data = Convert.FromBase64String(json["data"].GetValue<string>());
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
                Console.WriteLine("Couldn't read JSON file " + file);
                Console.WriteLine(e);
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
                    Console.WriteLine($"Warning: File {next.fName} is child of {parent} but it doesn't exist.");
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
        types[folder] = typeof(T);
        names[typeof(T)] = folder;
        foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
        {
            if (!type.IsSubclassOf(typeof(T))) continue;
            
            names[type] = folder;
        }

        files[folder] = new Dictionary<string, JsonObject>();
        loaded[folder] = new Dictionary<string, object>();
        if (builder != null)
            RegisterFolderBuilder(builder);
        
        OnFolderRegistered?.Invoke(folder);
    }

    public static void RegisterFolderBuilder<T>(Func<string, JsonObject, T?> builder) where T : class
    {
        builders[GetFolderName<T>()] = builder;
    }

    public static object? RegisterEntry(string folder, string name, JsonObject data)
    {
        files[folder][name] = data;
        OnEntryRegistered?.Invoke(folder, name, data);
        if (!builders.TryGetValue(folder, out Func<string, JsonObject, object?>? builder)) return null;

        if (data == null)
        {
            object? ret = builder(name, data);
            files[folder].Remove(name);
            loaded[folder][name] = ret;
            return ret;
        }
        else
        {
            try
            {
                object? ret = builder(name, data);
                if (ret != null && ret.GetType().IsAssignableTo(types[folder]))
                {
                    loaded[folder][name] = ret;
                    return ret;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                // ignored
            }
        }

        Console.WriteLine("Failed to load data: " + folder + "/" + name);
        files[folder].Remove(name);
        return null;
    }
    public static T? RegisterEntry<T>(string name, JsonObject data) where T : class
    {
        return RegisterEntry(GetFolderName<T>(), name, data) as T;
    }

    public static void RemoveEntry(string folder, string name)
    {
        if (files.TryGetValue(folder, out var value))
        {
            value.Remove(name);
        }
        if (loaded.TryGetValue(folder, out var loadedValue))
        {
            loadedValue.Remove(name);
        }
        OnEntryRemoved?.Invoke(folder, name);
    }
    public static void RemoveEntry<T>(string name)
    {
        string folder = GetFolderName<T>();
        RemoveEntry(folder, name);
    }

    public static T? GetEntryObject<T>(string name) where T : class
    {
        string folder = GetFolderName<T>();
        object? found = loaded[folder].GetValueOrDefault(name);
        if (found != null && !found.GetType().IsSubclassOf(typeof(T)) && found.GetType() != typeof(T))
        {
            Console.WriteLine("Data type mismatch: " + folder + "/" + name + " is not of type " + typeof(T));
            return null;
        }
        return (T?)loaded[folder].GetValueOrDefault(name);
    }

    public static JsonObject? GetEntryOrNull(string folder, string name)
    {
        return files.TryGetValue(folder, out var value) ? value.GetValueOrDefault(name) : null;
    }

    public static JsonObject? GetEntryOrNull<T>(string name)
    {
        return GetEntryOrNull(GetFolderName<T>(), name);
    }
    public static JsonObject GetEntry(string folder, string name)
    {
        return files[folder].GetValueOrDefault(name) ?? throw new Exception("Data not found: " + folder + "/" + name);
    }
    public static JsonObject GetEntry<T>(string name)
    {
        string folder = GetFolderName<T>();
        return files[folder].GetValueOrDefault(name) ?? throw new Exception("Data not found: " + folder + "/" + name);
    }

    public static IEnumerable<string> GetEntryNames(string folder)
    {
        if (!files.TryGetValue(folder, out var value))
            throw new Exception("Invalid data type: " + folder);
        return value.Keys.ToArray();
    }

    public static IEnumerable<string> GetEntryNames<T>()
    {
        string folder = names[typeof(T)];
        if (!files.TryGetValue(folder, out var value))
            throw new Exception("Invalid data type: " + typeof(T));

        return value.Keys.ToArray();
    }
    
    public static string GetFolderName<T>()
    {
        if (!names.ContainsKey(typeof(T)))
            throw new Exception("Invalid data type: " + typeof(T));
        return names[typeof(T)];
    }

    public static int GetEntryCount(string folderName)
    {
        files.TryGetValue(folderName, out var value);
        return value?.Count ?? 0;
    }

    public static int GetEntryCount<T>()
    { 
        return GetEntryCount(GetFolderName<T>());
    }

    public static void ClearFolder(string folder)
    {
        foreach (string entry in GetEntryNames(folder))
            RemoveEntry(folder, entry);
    }
    public static void Clear()
    {
        foreach (string folder in Folders)
        {
            ClearFolder(folder);
        }
    }
    
}
