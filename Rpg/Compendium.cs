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


    public static void RegisterDefaults()
    {
        RegisterFolder<Skill>("Skills", 
            (id, json) => Skills.Exists(id) ? Skills.Get(id) : Skill.FromJson(id, json));
        RegisterFolder<Feature>("Features",
            (name, json) => Features.Exists(name) ? Features.Get(name) : Feature.FromJson(name, json));
        RegisterFolder<Body>("Bodies", (_, json) => Body.NewBody(json));
        RegisterFolder<Item>("Items", (name, json) => new Item(name, json));
    }
    
    public static IEnumerable<(string fName, JsonObject obj)> GetFiles(string folder)
    {
        string path = "Data/" + folder;
        if (!Directory.Exists(path))
            yield break;

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
                    parsed = obj;
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
            loaded[folder][name] = ret;
            return ret;
        }
        else
        {
            object? ret = builder(name, data);
            if (ret != null && ret.GetType().IsAssignableTo(types[folder]))
            {
                loaded[folder][name] = ret;
                return ret;
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
