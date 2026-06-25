using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AestikModLoader.Common;
using UnityEngine;

namespace AestikModLoader.Runtime
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class ModInfoAttribute : Attribute
    {
        public ModInfoAttribute(string id, string name, string version)
        {
            Id = id;
            Name = name;
            Version = version;
        }

        public string Id { get; private set; }
        public string Name { get; private set; }
        public string Version { get; private set; }
        public string Author { get; set; }
        public string Description { get; set; }
    }

    public interface IAestikMod
    {
        void Initialize(ModContext context);
        void Shutdown();
    }

    public sealed class ModContext
    {
        public ModContext(string gameRoot, string modsRoot, Action<string> log)
        {
            GameRoot = gameRoot;
            ModsRoot = modsRoot;
            Log = log;
        }

        public string GameRoot { get; private set; }
        public string ModsRoot { get; private set; }
        public Action<string> Log { get; private set; }
    }

    internal sealed class LoadedMod
    {
        public string FolderPath { get; set; }
        public ModManifest Manifest { get; set; }
        public Assembly Assembly { get; set; }
        public IAestikMod Instance { get; set; }
    }

    public static class EnemyLifecycleBridge
    {
        public static event Action<Component> EnemyHealthStarted;
        public static event Action<Component, Collider2D> EnemyHealthTriggered;

        public static void NotifyEnemyHealthStarted(Component enemyHealth)
        {
            try
            {
                try
                {
                    string name = enemyHealth != null && enemyHealth.gameObject != null ? enemyHealth.gameObject.name : "<null>";
                    Debug.Log("[Aestik Mod Loader] EnemyLifecycleBridge saw EnemyHealth.Start on " + name);
                }
                catch
                {
                }

                Action<Component> handler = EnemyHealthStarted;
                if (handler != null)
                {
                    handler(enemyHealth);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    Debug.Log("[Aestik Mod Loader] EnemyLifecycleBridge error: " + ex.Message);
                }
                catch
                {
                }
            }
        }

        public static void NotifyEnemyHealthTriggered(Component enemyHealth, Collider2D other)
        {
            try
            {
                Action<Component, Collider2D> handler = EnemyHealthTriggered;
                if (handler != null)
                {
                    handler(enemyHealth, other);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    Debug.Log("[Aestik Mod Loader] EnemyLifecycleBridge trigger error: " + ex.Message);
                }
                catch
                {
                }
            }
        }
    }

    public static class LoaderBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        public static void Initialize()
        {
            ModLoader.Initialize();
        }
    }

    public static class ModLoader
    {
        private const string LoaderVersion = "2.0.0";
        private static readonly object Gate = new object();
        private static readonly List<LoadedMod> LoadedMods = new List<LoadedMod>();
        private static readonly Dictionary<string, string> AssemblyLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static bool initialized;
        private static string gameRoot;
        private static string modsRoot;
        private static string modLoaderRoot;
        private static string logPath;
        private static ModContext context;

        public static void Initialize()
        {
            lock (Gate)
            {
                if (initialized)
                {
                    return;
                }
                initialized = true;
            }

            try
            {
                ResolvePaths();
                Directory.CreateDirectory(modsRoot);
                Directory.CreateDirectory(Path.GetDirectoryName(logPath));

                if (ConsumeVanillaLaunchFlag())
                {
                    SafeLog("Vanilla launch requested. Skipping mod bootstrap.");
                    return;
                }

                AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
                AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

                Log("Aestik Mod Loader bootstrap started.");
                LoadMods();
            }
            catch (Exception ex)
            {
                SafeLog("Bootstrap failed: " + ex);
            }
        }

        private static void ResolvePaths()
        {
            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            string managedDir = Path.GetDirectoryName(assemblyPath);
            string dataDir = Path.GetFullPath(Path.Combine(managedDir, ".."));
            gameRoot = Path.GetFullPath(Path.Combine(dataDir, ".."));
            modLoaderRoot = Path.Combine(dataDir, "ModLoader");
            modsRoot = Path.Combine(modLoaderRoot, "Mods");
            logPath = Path.Combine(modLoaderRoot, "logs", "runtime.log");
            context = new ModContext(gameRoot, modsRoot, Log);
        }

        private static bool ConsumeVanillaLaunchFlag()
        {
            try
            {
                string flagPath = Path.Combine(modLoaderRoot, "vanilla.once");
                if (!File.Exists(flagPath))
                {
                    return false;
                }

                File.Delete(flagPath);
                return true;
            }
            catch (Exception ex)
            {
                SafeLog("Failed to process vanilla launch flag: " + ex.Message);
                return false;
            }
        }

        private static void LoadMods()
        {
            LoadedMods.Clear();
            AssemblyLookup.Clear();

            List<LoadedMod> candidates = new List<LoadedMod>();
            string[] modDirs = Directory.GetDirectories(modsRoot, "*", SearchOption.AllDirectories);
            for (int i = 0; i < modDirs.Length; i++)
            {
                string folder = modDirs[i];
                try
                {
                    if (!HasModPayload(folder))
                    {
                        continue;
                    }

                    ModManifest manifest = TryReadManifest(folder);
                    if (manifest == null)
                    {
                        continue;
                    }

                    string entryPath = ResolveEntryPath(folder, manifest);
                    if (!string.Equals(manifest.Kind, "pack", StringComparison.OrdinalIgnoreCase) && (string.IsNullOrEmpty(entryPath) || !File.Exists(entryPath)))
                    {
                        Log("Missing entry for mod: " + manifest.Name);
                        continue;
                    }

                    RegisterDependencyPaths(folder);

                    candidates.Add(new LoadedMod
                    {
                        FolderPath = folder,
                        Manifest = manifest,
                        Assembly = null,
                        Instance = null
                    });
                }
                catch (Exception ex)
                {
                    Log("Failed to index mod in " + folder + ": " + ex.Message);
                }
            }

            List<LoadedMod> ordered = OrderCandidates(candidates);
            for (int i = 0; i < ordered.Count; i++)
            {
                LoadedMod candidate = ordered[i];
                ModManifest manifest = candidate.Manifest;

                if (!manifest.IsEnabled)
                {
                    Log("Skipping disabled mod: " + manifest.Name);
                    continue;
                }

                if (!string.IsNullOrEmpty(manifest.LoaderVersion) && !string.Equals(manifest.LoaderVersion, LoaderVersion, StringComparison.OrdinalIgnoreCase))
                {
                    Log("Mod targets loader version " + manifest.LoaderVersion + ": " + manifest.Name);
                }

                try
                {
                    string entryPath = ResolveEntryPath(candidate.FolderPath, manifest);
                    if (!string.IsNullOrEmpty(entryPath) && File.Exists(entryPath))
                    {
                        Assembly assembly = Assembly.LoadFrom(entryPath);
                        RegisterAssemblyPath(assembly, entryPath);
                        IAestikMod instance = CreateModInstance(assembly);
                        if (instance == null)
                        {
                            Log("No IAestikMod entry point found in " + manifest.Name);
                            continue;
                        }

                        candidate.Assembly = assembly;
                        candidate.Instance = instance;
                        instance.Initialize(context);
                        LoadedMods.Add(candidate);
                        Log("Loaded mod: " + manifest.Name + " " + manifest.Version);
                    }
                    else if (string.Equals(manifest.Kind, "pack", StringComparison.OrdinalIgnoreCase))
                    {
                        LoadedMods.Add(candidate);
                        Log("Registered pack: " + manifest.Name);
                    }
                }
                catch (Exception ex)
                {
                    Log("Failed to load mod in " + candidate.FolderPath + ": " + ex.Message);
                }
            }

            Log("Loaded " + LoadedMods.Count.ToString() + " mod(s).");
        }

        private static List<LoadedMod> OrderCandidates(List<LoadedMod> candidates)
        {
            Dictionary<string, LoadedMod> byId = new Dictionary<string, LoadedMod>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, LoadedMod> byName = new Dictionary<string, LoadedMod>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, HashSet<string>> beforeEdges = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < candidates.Count; i++)
            {
                LoadedMod mod = candidates[i];
                string id = mod.Manifest != null ? mod.Manifest.Id : null;
                string name = mod.Manifest != null ? mod.Manifest.Name : null;
                if (!string.IsNullOrEmpty(id) && !byId.ContainsKey(id))
                {
                    byId[id] = mod;
                }
                if (!string.IsNullOrEmpty(name) && !byName.ContainsKey(name))
                {
                    byName[name] = mod;
                }

                string sourceKey = !string.IsNullOrEmpty(id) ? id : name;
                if (!string.IsNullOrEmpty(sourceKey) && mod.Manifest != null)
                {
                    foreach (string before in SplitList(mod.Manifest.Before))
                    {
                        if (!beforeEdges.ContainsKey(before))
                        {
                            beforeEdges[before] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        }

                        beforeEdges[before].Add(sourceKey);
                    }
                }
            }

            List<LoadedMod> ordered = new List<LoadedMod>();
            HashSet<string> visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            List<LoadedMod> sorted = candidates.OrderByDescending(delegate(LoadedMod mod)
            {
                int priority;
                return mod.Manifest != null && int.TryParse(mod.Manifest.Priority, out priority) ? priority : 0;
            }).ThenByDescending(delegate(LoadedMod mod)
            {
                return mod != null && mod.Manifest != null ? mod.Manifest.IsEnabled : false;
            }).ThenByDescending(delegate(LoadedMod mod)
            {
                return mod != null ? mod.FolderPath : "";
            }).ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                Visit(sorted[i], ordered, visiting, added, byId, byName, beforeEdges);
            }

            return ordered;
        }

        private static void Visit(LoadedMod mod, List<LoadedMod> ordered, HashSet<string> visiting, HashSet<string> added, Dictionary<string, LoadedMod> byId, Dictionary<string, LoadedMod> byName, Dictionary<string, HashSet<string>> beforeEdges)
        {
            if (mod == null || mod.Manifest == null)
            {
                return;
            }

            string key = !string.IsNullOrEmpty(mod.Manifest.Id) ? mod.Manifest.Id : mod.Manifest.Name;
            if (!string.IsNullOrEmpty(key))
            {
                if (added.Contains(key))
                {
                    return;
                }
                if (visiting.Contains(key))
                {
                    Log("Dependency cycle detected around " + mod.Manifest.Name);
                    return;
                }
                visiting.Add(key);
            }

            foreach (string dependency in SplitList(mod.Manifest.Depends))
            {
                LoadedMod dep;
                if (TryFindMod(dependency, byId, byName, out dep))
                {
                    Visit(dep, ordered, visiting, added, byId, byName, beforeEdges);
                }
                else
                {
                    Log("Missing dependency " + dependency + " for " + mod.Manifest.Name);
                }
            }

            foreach (string after in SplitList(mod.Manifest.After))
            {
                LoadedMod dep;
                if (TryFindMod(after, byId, byName, out dep))
                {
                    Visit(dep, ordered, visiting, added, byId, byName, beforeEdges);
                }
            }

            string modKey = !string.IsNullOrEmpty(mod.Manifest.Id) ? mod.Manifest.Id : mod.Manifest.Name;
            HashSet<string> waiting;
            if (!string.IsNullOrEmpty(modKey) && beforeEdges.TryGetValue(modKey, out waiting))
            {
                foreach (string follower in waiting)
                {
                    LoadedMod dep;
                    if (TryFindMod(follower, byId, byName, out dep))
                    {
                        Visit(dep, ordered, visiting, added, byId, byName, beforeEdges);
                    }
                }
            }

            if (key != null)
            {
                added.Add(key);
                visiting.Remove(key);
            }

            ordered.Add(mod);
        }

        private static bool TryFindMod(string token, Dictionary<string, LoadedMod> byId, Dictionary<string, LoadedMod> byName, out LoadedMod mod)
        {
            mod = null;
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            if (byId.TryGetValue(token, out mod))
            {
                return true;
            }

            if (byName.TryGetValue(token, out mod))
            {
                return true;
            }

            return false;
        }

        private static IEnumerable<string> SplitList(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return Enumerable.Empty<string>();
            }

            return value.Split(new char[] { ',', ';', '\n', '\r', '|' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(delegate(string item) { return item.Trim(); })
                        .Where(delegate(string item) { return item.Length > 0; });
        }

        private static ModManifest TryReadManifest(string folder)
        {
            string[] candidates = new string[]
            {
                Path.Combine(folder, "manifest.txt"),
                Path.Combine(folder, "manifest.json"),
                Path.Combine(folder, "mod.txt"),
                Path.Combine(folder, "mod.json"),
                Path.Combine(folder, "mod.manifest"),
                Path.Combine(folder, "mod.ini")
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                if (!File.Exists(candidates[i]))
                {
                    continue;
                }

                ModManifest manifest = SimpleManifest.ReadModManifest(candidates[i]);
                if (manifest != null)
                {
                    manifest.Entry = NormalizeEntry(folder, manifest.Entry);
                    return manifest;
                }
            }

            string[] dlls = Directory.GetFiles(folder, "*.dll", SearchOption.AllDirectories);
            if (dlls.Length == 0)
            {
                return null;
            }

            string entry = dlls[0];
            ModManifest fallback = new ModManifest();
            fallback.Id = SimpleManifest.MakeSafeId(Path.GetFileNameWithoutExtension(entry));
            fallback.Name = Path.GetFileNameWithoutExtension(entry);
            fallback.Version = "1.0.0";
            fallback.Author = "Unknown";
            fallback.Description = "Imported code mod";
            fallback.Entry = entry;
            fallback.Enabled = "true";
            fallback.Kind = "code";
            return fallback;
        }

        private static string ResolveEntryPath(string folder, ModManifest manifest)
        {
            if (manifest == null)
            {
                return null;
            }

            if (string.IsNullOrEmpty(manifest.Entry))
            {
                string[] dlls = Directory.GetFiles(folder, "*.dll", SearchOption.AllDirectories);
                if (dlls.Length > 0)
                {
                    return dlls[0];
                }
                return null;
            }

            if (Path.IsPathRooted(manifest.Entry))
            {
                return manifest.Entry;
            }

            return Path.GetFullPath(Path.Combine(folder, manifest.Entry));
        }

        private static string NormalizeEntry(string folder, string entry)
        {
            if (string.IsNullOrEmpty(entry))
            {
                return entry;
            }

            if (Path.IsPathRooted(entry))
            {
                return entry;
            }

            return Path.GetFullPath(Path.Combine(folder, entry));
        }

        private static void RegisterDependencyPaths(string folder)
        {
            if (!Directory.Exists(folder))
            {
                return;
            }

            string[] dlls = Directory.GetFiles(folder, "*.dll", SearchOption.AllDirectories);
            for (int i = 0; i < dlls.Length; i++)
            {
                string path = dlls[i];
                string key = Path.GetFileNameWithoutExtension(path);
                if (!AssemblyLookup.ContainsKey(key))
                {
                    AssemblyLookup[key] = path;
                }
            }
        }

        private static void RegisterAssemblyPath(Assembly assembly, string path)
        {
            if (assembly == null)
            {
                return;
            }

            string name = assembly.GetName().Name;
            if (!AssemblyLookup.ContainsKey(name))
            {
                AssemblyLookup[name] = path;
            }
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            AssemblyName requested = new AssemblyName(args.Name);
            string path;
            if (AssemblyLookup.TryGetValue(requested.Name, out path) && File.Exists(path))
            {
                try
                {
                    return Assembly.LoadFrom(path);
                }
                catch
                {
                }
            }

            string[] fallback = Directory.GetFiles(modsRoot, requested.Name + ".dll", SearchOption.AllDirectories);
            if (fallback.Length > 0)
            {
                try
                {
                    AssemblyLookup[requested.Name] = fallback[0];
                    return Assembly.LoadFrom(fallback[0]);
                }
                catch
                {
                }
            }

            return null;
        }

        private static bool HasModPayload(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return false;
            }

            string[] manifestCandidates = new string[]
            {
                Path.Combine(folder, "manifest.txt"),
                Path.Combine(folder, "manifest.json"),
                Path.Combine(folder, "mod.txt"),
                Path.Combine(folder, "mod.json"),
                Path.Combine(folder, "mod.manifest"),
                Path.Combine(folder, "mod.ini")
            };

            for (int i = 0; i < manifestCandidates.Length; i++)
            {
                if (File.Exists(manifestCandidates[i]))
                {
                    return true;
                }
            }

            try
            {
                return Directory.GetFiles(folder, "*.dll", SearchOption.TopDirectoryOnly).Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private static IAestikMod CreateModInstance(Assembly assembly)
        {
            Type[] types = assembly.GetTypes();
            for (int i = 0; i < types.Length; i++)
            {
                Type type = types[i];
                if (type.IsAbstract || type.IsInterface)
                {
                    continue;
                }

                if (!typeof(IAestikMod).IsAssignableFrom(type))
                {
                    continue;
                }

                return (IAestikMod)Activator.CreateInstance(type);
            }

            return null;
        }

        private static void OnProcessExit(object sender, EventArgs e)
        {
            ShutdownMods();
        }

        private static void ShutdownMods()
        {
            for (int i = LoadedMods.Count - 1; i >= 0; i--)
            {
                try
                {
                    if (LoadedMods[i].Instance != null)
                    {
                        LoadedMods[i].Instance.Shutdown();
                    }
                }
                catch (Exception ex)
                {
                    SafeLog("Shutdown error: " + ex.Message);
                }
            }
        }

        private static void Log(string message)
        {
            SafeLog(message);
            try
            {
                Debug.Log("[Aestik Mod Loader] " + message);
            }
            catch
            {
            }
        }

        private static void SafeLog(string message)
        {
            try
            {
                string dir = Path.GetDirectoryName(logPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.AppendAllText(logPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}
