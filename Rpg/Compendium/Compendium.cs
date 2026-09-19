using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rpg.Entities.Components;
using Rpg.Entities.Components.Inventory;
using Rpg.Features;
using Rpg.Health;
using Rpg.Scripting;
using Rpg.Skills;

namespace Rpg;

public static class Compendium
{
    public static event Action<string>? OnFolderRegistered;
    public static event Action<string, string, JsonElement>? OnEntryRegistered;
    public static event Action<string, string>? OnEntryRemoved;

    private class CompendiumEntry
    {
        public string Name;
        public bool IsBase;
        public JsonElement? Data;
        public object? Loaded;
        public string? Note;
    }

    private class FolderData
    {
        public Type Type = typeof(object);
        public Dictionary<string, CompendiumEntry> Entries { get; } = new();
        public Dictionary<object, CompendiumEntry> EntriesByObj { get; } = new();
        public Func<string, JsonElement, object?>? Builder;
        public string Default = "";
    }

    private static readonly Dictionary<string, FolderData> folders = new();
    private static readonly Dictionary<Type, string> typeToFolder = new();
    private static readonly List<string> loadOrder = new();

    public static IEnumerable<string> Folders => loadOrder.AsReadOnly();

    public static void RegisterDefaults()
    {
        RegisterFolder<Midia>("Midia", (fName, json) =>
        {
            string fileName = json.GetProperty("fileName").GetString() ?? "";
            bool parsed = Enum.TryParse(json.GetProperty("type").GetString(), out MidiaType type);
            byte[] data = Convert.FromBase64String(json.GetProperty("data").GetString() ?? "");
            Midia ret;
            if (parsed)
                ret = new Midia(data, type);
            else
                ret = new Midia(data, fileName);
            return ret;
        });
        RegisterFolder<ExprLibrary>("Libraries", (id, json) => ExprLibrary.FromJson(id, json));
        RegisterFolder<InjuryType>("InjuryTypes", (name, json) => new InjuryType(name, json));
        RegisterFolder<DamageType>("DamageTypes", (name, json) => new DamageType(name, json));
        RegisterFolder<Skill>("Skills", Skill.FromJson);
        RegisterFolder<Feature>("Features", Feature.FromJson);
        //TODO: ItemModels
        RegisterFolder<ItemModel>("Items", (name, json) => new ItemModel(name, json));
        //TODO: SkillTree models
        RegisterFolder<SkillTreeModel>("SkillTrees", (id, json) => new SkillTreeModel(id, json));
        RegisterFolder<BodyPosture>("Postures", (id, json) => new BodyPosture(id, json));
        RegisterFolder<BodyModel>("Bodies", (id, json) => new BodyModel(id, json));
        RegisterFolder<string>("Notes", (_, json) => json.GetProperty("text").GetString() ?? "");
    }
    
    public static IEnumerable<(string fName, JsonElement obj)> GetFiles(string folder)
    {
        string path = "Data/" + folder;
        if (!Directory.Exists(path))
            yield break;

        Dictionary<string, JsonObject> processedFiles = new();
        List<(string fName, JsonObject obj)> toProcess = new();
        foreach (string file in Directory.GetFiles(path).OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase))
        {
            string json = File.ReadAllText(file);
            string? fName = null;
            JsonObject? parsed = null;
            try
            {
                var node = JsonNode.Parse(json, null, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
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
                yield return (fName, parsed.ToElement());
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
                else
                {
                    toProcess.Add(next);
                    toProcess.RemoveAt(0);
                }
                continue;
            }

            // Merge the child file onto its parent.
            //
            // - Child values override parent values.
            // - Setting a value to `null` in the child will remove that key from the merged result.
            // - Objects merge recursively and arrays are merged according to JsonHelpers.MergeArrays.
            var result = JsonHelpers.Merge(parentObj, next.obj);
            toProcess.RemoveAt(0);
            processedFiles[next.fName] = result;
            yield return (next.fName, result.ToElement());
        }
    }
    public static void RegisterFolder<T>(string folder, Func<string, JsonElement, T?>? builder = null) where T : class
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

        loadOrder.Add(folder);
        OnFolderRegistered?.Invoke(folder);
    }

    public static object? RegisterHardEntry(string folder, string name, object? data)
    {
        CompendiumEntry entry = new() { Loaded = data, Name = name };
        folders[folder].Entries[name] = entry;
        if (data != null)
            folders[folder].EntriesByObj[data] = entry;
        return data;
    }

    public static T? RegisterHardEntry<T>(string name, T? data) where T : class
    {
        return RegisterHardEntry(GetFolderName<T>(), name, data) as T;
    }

    public static object? RegisterEntry(string folder, string name, JsonElement data)
    {
        CompendiumEntry entry = new() { Data = data, Name = name};
        if (data.TryGetProperty("_note", out var noteVal) && noteVal.ValueKind == JsonValueKind.String)
            entry.Note = noteVal.GetString();
        if (name.EndsWith("_base"))
            entry.IsBase = true;
        folders[folder].Entries[name] = entry;
        if (data.TryGetProperty("default", out var defVal) && defVal.ValueKind == JsonValueKind.True)
            folders[folder].Default = name;
        OnEntryRegistered?.Invoke(folder, name, data);
        if (entry.IsBase)
            return null;
        if (folders[folder].Builder is { } builder)
        {
            try
            {
                object? ret = builder(name, data);
                if (ret is ITaggable taggable && data.TryGetProperty("tags", out var tagsVal) && tagsVal.ValueKind == JsonValueKind.Array)
                    taggable.LoadTags(tagsVal);
                if (ret != null && ret.GetType().IsAssignableTo(folders[folder].Type))
                {
                    entry.Loaded = ret;
                    folders[folder].EntriesByObj[ret] = entry;
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
                Logger.LogError(e.ToString());
                if (e.StackTrace != null)
                    Logger.LogError(e.StackTrace);
                // ignored
            }
        }

        Logger.LogError("Failed to load data: " + folder + "/" + name);
        folders[folder].Entries.Remove(name);
        return null;
    }
    public static T? RegisterEntry<T>(string name, JsonElement data) where T : class
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
    public static bool TryGetEntry<T>(string name, out T entry) where T : class
    {
        entry = GetEntry<T>(name);
        return entry != null;
    }
    public static T GetEntryOrThrow<T>(string name) where T : class
    {
        T? entry = GetEntry<T>(name);
        if (entry == null)
            throw new Exception("Entry not found: " + GetFolderName<T>() + "/" + name);
        return entry;
    }

    /// <summary>
    /// Resolve a list of entry names into their compendium entries, logging a warning
    /// (and skipping) any name that doesn't resolve. Used to parse JSON string lists
    /// (features, skills, etc.) that reference compendium entries.
    /// </summary>
    public static List<T> ResolveEntries<T>(IEnumerable<string>? names, string? warningLabel = null) where T : class
    {
        var result = new List<T>();
        if (names == null) return result;
        foreach (var name in names)
        {
            var entry = GetEntry<T>(name);
            if (entry == null)
                Logger.LogWarning($"Invalid {warningLabel ?? typeof(T).Name} in JSON: {name}");
            else
                result.Add(entry);
        }
        return result;
    }

    public static JsonElement? GetEntryJsonOrNull(string folder, string name)
    {
        return folders.TryGetValue(folder, out var fd) && fd.Entries.TryGetValue(name, out var entry) ? entry.Data : null;
    }

    public static JsonElement? GetEntryJsonOrNull<T>(string name)
    {
        return GetEntryJsonOrNull(GetFolderName<T>(), name);
    }
    public static JsonElement GetEntryJson(string folder, string name)
    {
        if (!folders.TryGetValue(folder, out var fd)) throw new Exception("Invalid folder: " + folder);
        if (!fd.Entries.TryGetValue(name, out var entry) || entry.Data == null) throw new Exception("Data not found: " + folder + "/" + name);
        return entry.Data.Value;
    }
    public static JsonElement GetEntryJson<T>(string name)
    {
        string folder = GetFolderName<T>();
        return GetEntryJson(folder, name);
    }

    public static string? GetEntryName(object entry)
    {
        var folder = GetFolderName(entry.GetType());
        if (!folders.TryGetValue(folder, out var fd)) throw new Exception("Invalid data type: " + folder);
        return fd?.EntriesByObj.GetValueOrDefault(entry)?.Name;
    }

    public static IEnumerable<string> GetEntryNames(string folder)
    {
        if (!folders.TryGetValue(folder, out var fd)) throw new Exception("Invalid data type: " + folder);
        return fd.Entries.Keys.ToArray();
    }

    public static IEnumerable<string> GetEntryNames<T>(bool base_included = true)
    {
        string folder = GetFolderName<T>();
        if (!folders.TryGetValue(folder, out var fd)) throw new Exception("Invalid data type: " + typeof(T));
        if (base_included)
            return fd.Entries.Keys;
        else
            return fd.Entries.Keys.Where(key => !key.EndsWith("base"));
    }

    public static IEnumerable<T> GetEntries<T>() where T : class
    {
        string folder = GetFolderName<T>();
        if (!folders.TryGetValue(folder, out var fd)) throw new Exception("Invalid data type: " + typeof(T));
        foreach (var entry in fd.Entries)
        {
            if (entry.Value.Loaded is T t)
                yield return t;
        }
    }

    public static IEnumerable<object> GetEntries(string folder)
    {
        if (!folders.TryGetValue(folder, out var fd)) throw new Exception("Invalid data type: " + folder);
        foreach (var entry in fd.Entries)
        {
            if (entry.Value.Loaded != null)
                yield return entry.Value.Loaded;
        }
    }

    public static T? FindEntry<T>(Func<T, bool> predicate) where T : class
    {
        string folder = GetFolderName<T>();
        if (!folders.TryGetValue(folder, out var fd)) throw new Exception("Invalid data type: " + typeof(T));
        foreach (var entry in fd.Entries)
        {
            if (entry.Value.Loaded is T t && predicate(t))
                return t;
        }
        throw new Exception("No matching entry found for type: " + typeof(T));
    }

    public static bool EntryExists<T>(string name, bool includeBase = true) where T : class
    {
        string folder = GetFolderName<T>();
        if (!folders.TryGetValue(folder, out var fd)) throw new Exception("Invalid data type: " + typeof(T));
        if (!includeBase)
        {
            return fd.Entries.TryGetValue(name, out var entry) && !entry.IsBase;
        }
        return fd.Entries.ContainsKey(name);
    }
    public static bool IsEntry<T>(string name, bool includeBase = true) where T : class
    {
        return EntryExists<T>(name, includeBase);
    }
    public static bool IsFolder<T>() => IsFolder(typeof(T));

    /// <summary>
    /// True if <paramref name="type"/> is stored in a compendium folder.
    /// <para>
    /// This answers the question rather than throwing when the answer is no: it went through
    /// <c>GetFolderName</c>, which raises "Invalid data type" for anything unregistered, so asking
    /// about an ordinary type (a component, say) failed instead of returning false.
    /// </para>
    /// </summary>
    public static bool IsFolder(Type type)
        => typeToFolder.TryGetValue(type, out var folder) && folders.ContainsKey(folder);
    
    public static T? GetDefaultEntry<T>() where T : class
    {
        string folder = GetFolderName<T>();
        if (!folders.TryGetValue(folder, out var fd)) throw new Exception("Invalid data type: " + typeof(T));
        return GetEntry<T>(fd.Default) ?? throw new Exception("Default entry not found for type: " + typeof(T));
    }
    
    public static string GetFolderName<T>()
    {
        if (!typeToFolder.TryGetValue(typeof(T), out var folder)) throw new Exception("Invalid data type: " + typeof(T));
        return folder;
    }
    public static string GetFolderName(Type type)
    {
        if (!typeToFolder.TryGetValue(type, out var folder)) throw new Exception("Invalid data type: " + type);
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

public class CompendiumEntryRef<T> : ISerializable where T : class
{
    public string Folder { get; }
    public string Name { get; }

    public CompendiumEntryRef(string folder, string name)
    {
        Folder = folder;
        Name = name;
    }

    public CompendiumEntryRef(string name)
    {
        Folder = Compendium.GetFolderName<T>();
        Name = name;
    }

    public CompendiumEntryRef(Stream stream)
    {
        Folder = stream.ReadString();
        Name = stream.ReadString();
    }

    public T? Get()
    {
        return Compendium.GetEntry<T>(Name);
    }
    public T? GetEntry()
    {
        return Get();
    }

    public void ToBytes(Stream stream)
    {
        stream.WriteString(Folder);
        stream.WriteString(Name);
    }
}