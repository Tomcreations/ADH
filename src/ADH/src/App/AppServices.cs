using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using AestikModLoader.Common;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AestikModLoader.App
{
    public sealed class DiscoveryResult
    {
        public GameInstallInfo Install { get; set; }
        public List<string> LogLines { get; private set; }

        public DiscoveryResult()
        {
            LogLines = new List<string>();
        }
    }

    public sealed class ImportPreviewInfo
    {
        public ModEntry Entry { get; set; }
        public List<string> DeclaredDependencies { get; private set; }
        public List<string> MissingDependencies { get; private set; }
        public List<string> Warnings { get; private set; }
        public List<string> ManifestCandidates { get; private set; }
        public string SelectedManifestRelativePath { get; set; }
        public string SourcePath { get; set; }
        public string SourceKind { get; set; }

        public ImportPreviewInfo()
        {
            DeclaredDependencies = new List<string>();
            MissingDependencies = new List<string>();
            Warnings = new List<string>();
            ManifestCandidates = new List<string>();
        }
    }

    public static class SteamLocator
    {
        private const string GameName = "Aestik";
        private const string AppId = "2199330";

        public static DiscoveryResult DiscoverGame(bool activeSearch)
        {
            DiscoveryResult result = new DiscoveryResult();
            List<string> libraryRoots = new List<string>();

            string steamPath = ReadSteamPath();
            if (!string.IsNullOrEmpty(steamPath))
            {
                result.LogLines.Add("Steam path: " + steamPath);
                libraryRoots.Add(steamPath);
                string libraryFile = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                libraryRoots.AddRange(ReadLibraryFolders(libraryFile));
            }
            else
            {
                result.LogLines.Add("Steam path not found in registry.");
            }

            NormalizeAndDistinct(libraryRoots);

            foreach (string library in libraryRoots)
            {
                GameInstallInfo info = CheckLibrary(library, result.LogLines);
                if (info != null)
                {
                    result.Install = info;
                    return result;
                }
            }

            if (activeSearch)
            {
                result.LogLines.Add("Running active search through Steam common folders.");
                foreach (string library in libraryRoots)
                {
                    GameInstallInfo found = SearchCommonFolders(library, result.LogLines);
                    if (found != null)
                    {
                        result.Install = found;
                        return result;
                    }
                }

                result.LogLines.Add("Trying broad drive scan for Aestik.exe.");
                GameInstallInfo broad = BroadSearch(result.LogLines);
                if (broad != null)
                {
                    result.Install = broad;
                    return result;
                }
            }

            result.LogLines.Add("Aestik was not found.");
            return result;
        }

        private static string ReadSteamPath()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("SteamPath");
                        if (value is string)
                        {
                            return ((string)value).Replace('/', '\\');
                        }
                    }
                }
            }
            catch
            {
            }

            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"Software\WOW6432Node\Valve\Steam"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("InstallPath");
                        if (value is string)
                        {
                            return (string)value;
                        }
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static IEnumerable<string> ReadLibraryFolders(string path)
        {
            List<string> result = new List<string>();
            if (!File.Exists(path))
            {
                return result;
            }

            string text = File.ReadAllText(path, Encoding.UTF8);
            MatchCollection matches = Regex.Matches(text, "\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    string value = match.Groups[1].Value.Replace("\\\\", "\\");
                    if (!string.IsNullOrEmpty(value))
                    {
                        result.Add(value);
                    }
                }
            }

            return result;
        }

        private static void NormalizeAndDistinct(List<string> roots)
        {
            for (int i = roots.Count - 1; i >= 0; i--)
            {
                string current = roots[i];
                if (string.IsNullOrEmpty(current))
                {
                    roots.RemoveAt(i);
                    continue;
                }

                current = current.Trim().Trim('"');
                current = current.Replace('/', '\\');
                current = Path.GetFullPath(current);
                roots[i] = current;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = roots.Count - 1; i >= 0; i--)
            {
                if (!seen.Add(roots[i]))
                {
                    roots.RemoveAt(i);
                }
            }
        }

        private static GameInstallInfo CheckLibrary(string libraryPath, List<string> logs)
        {
            try
            {
                string steamapps = Path.Combine(libraryPath, "steamapps");
                string manifestPath = Path.Combine(steamapps, "appmanifest_" + AppId + ".acf");
                if (!File.Exists(manifestPath))
                {
                    return null;
                }

                string manifestText = File.ReadAllText(manifestPath, Encoding.UTF8);
                string name = GetManifestValue(manifestText, "name");
                string installDir = GetManifestValue(manifestText, "installdir");

                logs.Add("Found manifest in " + libraryPath + " for " + name + ".");

                string gameRoot = Path.Combine(steamapps, "common", installDir);
                if (ValidateGameRoot(gameRoot, logs))
                {
                    return CreateInstallInfo(libraryPath, installDir, gameRoot, manifestPath, true);
                }
            }
            catch (Exception ex)
            {
                logs.Add("Manifest check failed in " + libraryPath + ": " + ex.Message);
            }

            return null;
        }

        private static GameInstallInfo SearchCommonFolders(string libraryPath, List<string> logs)
        {
            string common = Path.Combine(libraryPath, "steamapps", "common");
            if (!Directory.Exists(common))
            {
                return null;
            }

            string[] candidates = Directory.GetDirectories(common);
            for (int i = 0; i < candidates.Length; i++)
            {
                string folder = candidates[i];
                if (ValidateGameRoot(folder, logs))
                {
                    logs.Add("Active search found game folder: " + folder);
                    return CreateInstallInfo(libraryPath, Path.GetFileName(folder), folder, null, false);
                }
            }

            return null;
        }

        private static GameInstallInfo BroadSearch(List<string> logs)
        {
            List<string> roots = new List<string>();
            try
            {
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    if (drive.DriveType == DriveType.Fixed && drive.IsReady)
                    {
                        roots.Add(drive.RootDirectory.FullName);
                    }
                }
            }
            catch
            {
            }

            foreach (string root in roots)
            {
                try
                {
                    string[] matches = Directory.GetFiles(root, "Aestik.exe", SearchOption.AllDirectories);
                    for (int i = 0; i < matches.Length; i++)
                    {
                        string exe = matches[i];
                        string folder = Path.GetDirectoryName(exe);
                        if (ValidateGameRoot(folder, logs))
                        {
                            logs.Add("Broad scan found game executable at " + exe);
                            return CreateInstallInfo(null, Path.GetFileName(folder), folder, null, false);
                        }
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private static GameInstallInfo CreateInstallInfo(string steamPath, string installDir, string gameRoot, string manifestPath, bool viaManifest)
        {
            GameInstallInfo info = new GameInstallInfo();
            info.SteamPath = steamPath;
            info.LibraryPath = steamPath;
            info.InstallDir = installDir;
            info.GameRoot = Path.GetFullPath(gameRoot);
            info.ExecutablePath = Path.Combine(info.GameRoot, "Aestik.exe");
            info.ManagedPath = Path.Combine(info.GameRoot, "Aestik_Data", "Managed");
            info.ManifestPath = manifestPath;
            info.AppId = AppId;
            info.FoundViaSteamManifest = viaManifest;
            return info;
        }

        private static bool ValidateGameRoot(string root, List<string> logs)
        {
            if (string.IsNullOrEmpty(root))
            {
                return false;
            }

            string exe = Path.Combine(root, "Aestik.exe");
            string managed = Path.Combine(root, "Aestik_Data", "Managed", "Assembly-CSharp.dll");
            if (File.Exists(exe) && File.Exists(managed))
            {
                return true;
            }

            logs.Add("Rejected candidate: " + root);
            return false;
        }

        private static string GetManifestValue(string text, string key)
        {
            Match match = Regex.Match(text, "\"" + Regex.Escape(key) + "\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value;
            }

            return null;
        }
    }

    public static class AppPaths
    {
        public static string GetAppRoot()
        {
            string location = typeof(AppPaths).Assembly.Location;
            if (!string.IsNullOrEmpty(location))
            {
                string dir = Path.GetDirectoryName(location);
                if (!string.IsNullOrEmpty(dir))
                {
                    return dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }
            }

            return AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        public static string GetUserDataRoot()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ADH");
        }

        public static string GetSettingsPath()
        {
            return Path.Combine(GetUserDataRoot(), "settings.txt");
        }

        public static string GetDefaultModsRoot(GameInstallInfo game)
        {
            if (game == null)
            {
                return null;
            }

            return Path.Combine(game.GameRoot, "Aestik_Data", "ModLoader", "Mods");
        }

        public static string GetModLoaderRoot(GameInstallInfo game)
        {
            if (game == null)
            {
                return null;
            }

            return Path.Combine(game.GameRoot, "Aestik_Data", "ModLoader");
        }

        public static string GetDefaultPacksRoot(GameInstallInfo game)
        {
            if (game == null)
            {
                return null;
            }

            return Path.Combine(game.GameRoot, "Aestik_Data", "ModLoader", "Packs");
        }

        public static string GetLoaderSettingsPath(GameInstallInfo game)
        {
            string root = GetModLoaderRoot(game);
            return string.IsNullOrEmpty(root) ? null : Path.Combine(root, "settings.txt");
        }

        public static string GetVanillaLaunchFlagPath(GameInstallInfo game)
        {
            string root = GetModLoaderRoot(game);
            return string.IsNullOrEmpty(root) ? null : Path.Combine(root, "vanilla.once");
        }

        public static string GetRuntimeDllPath()
        {
            string[] candidates = new string[]
            {
                Path.Combine(GetAppRoot(), "ADH.Runtime.dll")
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                if (File.Exists(candidates[i]))
                {
                    return candidates[i];
                }
            }

            return candidates[0];
        }

        public static string GetGameAssemblyPath(GameInstallInfo game)
        {
            return game == null || string.IsNullOrEmpty(game.ManagedPath) ? null : Path.Combine(game.ManagedPath, "Assembly-CSharp.dll");
        }

        public static string GetGameAssemblyBackupPath(GameInstallInfo game)
        {
            return game == null || string.IsNullOrEmpty(game.ManagedPath) ? null : Path.Combine(game.ManagedPath, "Assembly-CSharp.ADH.original.dll");
        }
    }

    public sealed class LauncherSettings
    {
        public string LastGameRoot { get; set; }
        public bool AutoSearch { get; set; }
        public bool RememberWindow { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public static LauncherSettings Load()
        {
            LauncherSettings settings = new LauncherSettings();
            settings.AutoSearch = true;
            settings.RememberWindow = true;
            settings.Width = 1280;
            settings.Height = 860;

            Dictionary<string, string> values = SimpleManifest.LoadPairs(AppPaths.GetSettingsPath());
            string value;
            if (values.TryGetValue("last_game_root", out value))
            {
                settings.LastGameRoot = value;
            }
            if (values.TryGetValue("auto_search", out value))
            {
                settings.AutoSearch = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
            }
            if (values.TryGetValue("remember_window", out value))
            {
                settings.RememberWindow = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
            }
            if (values.TryGetValue("width", out value))
            {
                int parsed;
                if (int.TryParse(value, out parsed))
                {
                    settings.Width = parsed;
                }
            }
            if (values.TryGetValue("height", out value))
            {
                int parsed;
                if (int.TryParse(value, out parsed))
                {
                    settings.Height = parsed;
                }
            }

            return settings;
        }

        public void Save()
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            values["last_game_root"] = LastGameRoot ?? "";
            values["auto_search"] = AutoSearch ? "true" : "false";
            values["remember_window"] = RememberWindow ? "true" : "false";
            values["width"] = Width.ToString();
            values["height"] = Height.ToString();
            SimpleManifest.SavePairs(AppPaths.GetSettingsPath(), values);
        }
    }

    public static class ModRepository
    {
        private const string LoaderVersion = "2.0.0";
        private const string BootstrapTypeName = "AestikModLoader.Runtime.LoaderBootstrap";
        private const string BootstrapMethodName = "Initialize";
        private const string EnemyLifecycleBridgeTypeName = "AestikModLoader.Runtime.EnemyLifecycleBridge";
        private const string EnemyLifecycleMethodName = "NotifyEnemyHealthStarted";
        private const string EnemyTriggerMethodName = "NotifyEnemyHealthTriggered";

        public static bool IsLoaderInstalled(GameInstallInfo game)
        {
            if (game == null)
            {
                return false;
            }

            string runtimeDll = Path.Combine(game.ManagedPath, "ADH.Runtime.dll");
            return File.Exists(runtimeDll) && IsBootstrapPatched(game);
        }

        public static List<ModEntry> LoadMods(GameInstallInfo game)
        {
            List<ModEntry> result = new List<ModEntry>();
            string root = AppPaths.GetDefaultModsRoot(game);
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                return result;
            }

            string[] folders = EnumerateCandidateFolders(root);
            foreach (string folder in folders)
            {
                ModEntry entry = LoadSingleMod(folder);
                if (entry != null)
                {
                    result.Add(entry);
                }
            }

            return result.OrderByDescending(delegate(ModEntry mod) { return mod.Manifest != null && mod.Manifest.IsEnabled; })
                         .ThenByDescending(delegate(ModEntry mod)
                         {
                             int priority;
                             return mod.Manifest != null && int.TryParse(mod.Manifest.Priority, out priority) ? priority : 0;
                         })
                         .ThenByDescending(delegate(ModEntry mod) { return mod.LastModifiedUtc; })
                         .ThenByDescending(delegate(ModEntry mod)
                         {
                             return mod.HasUpdate;
                         })
                         .ThenBy(delegate(ModEntry mod) { return mod.Manifest != null ? mod.Manifest.Name : Path.GetFileName(mod.FolderPath); })
                         .ToList();
        }

        public static string[] EnumerateCandidateFolders(string root)
        {
            List<string> folders = new List<string>();
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                return folders.ToArray();
            }

            foreach (string folder in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
            {
                if (HasModPayload(folder))
                {
                    folders.Add(folder);
                }
            }

            return folders.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        public static ModEntry LoadSingleMod(string folder)
        {
            if (!Directory.Exists(folder))
            {
                return null;
            }

            string manifestPath = FindManifestPath(folder);
            ModManifest manifest = manifestPath != null ? SimpleManifest.ReadModManifest(manifestPath) : null;
            if (manifest == null)
            {
                string[] dlls = Directory.GetFiles(folder, "*.dll", SearchOption.AllDirectories);
                if (dlls.Length == 0)
                {
                    return null;
                }

                manifest = new ModManifest();
                manifest.Id = SimpleManifest.MakeSafeId(Path.GetFileName(folder));
                manifest.Name = Path.GetFileName(folder);
                manifest.Version = "1.0.0";
                manifest.Author = "Unknown";
                manifest.Description = "Imported mod";
                manifest.Entry = Path.GetFileName(dlls[0]);
                manifest.Enabled = "true";
                manifest.Kind = "code";
                manifest.Priority = "0";
                manifest.Category = "General";
                manifest.TrustLevel = "Unofficial";
                manifest.TestingStatus = string.Empty;
                manifestPath = Path.Combine(folder, "adh-manifest.json");
                SimpleManifest.WriteModManifest(manifestPath, manifest);
            }

            string entryPath = ResolveEntryPath(folder, manifest);
            ModEntry entry = new ModEntry();
            entry.Manifest = manifest;
            entry.FolderPath = folder;
            entry.ManifestPath = manifestPath;
            entry.EntryPath = entryPath;
            entry.EntryExists = !string.IsNullOrEmpty(entryPath) && File.Exists(entryPath);
            entry.LastModifiedUtc = GetFolderWriteTimeUtc(folder);
            entry.IsPack = string.Equals(manifest.Kind, "pack", StringComparison.OrdinalIgnoreCase);
            entry.HasUpdate = IsOutOfDate(manifest);
            entry.IsInstalled = true;
            if (string.Equals(manifest.Kind, "pack", StringComparison.OrdinalIgnoreCase))
            {
                entry.Status = "Pack";
                if (string.IsNullOrEmpty(entry.EntryPath))
                {
                    entry.EntryExists = true;
                }
            }
            else
            {
                entry.Status = entry.EntryExists ? "Ready" : "Missing entry";
            }
            return entry;
        }

        public static void SetEnabled(ModEntry mod, bool enabled)
        {
            if (mod == null || mod.Manifest == null || string.IsNullOrEmpty(mod.ManifestPath))
            {
                return;
            }

            mod.Manifest.IsEnabled = enabled;
            SimpleManifest.WriteModManifest(mod.ManifestPath, mod.Manifest);
        }

        public static ModEntry ImportModFile(GameInstallInfo game, string sourcePath, string selectedManifestRelativePath)
        {
            string defaultModsRoot = AppPaths.GetDefaultModsRoot(game);
            string defaultPacksRoot = AppPaths.GetDefaultPacksRoot(game);
            if (string.IsNullOrEmpty(defaultModsRoot) || string.IsNullOrEmpty(defaultPacksRoot))
            {
                throw new InvalidOperationException("Game is not detected.");
            }

            string safeName = SimpleManifest.MakeSafeId(Path.GetFileNameWithoutExtension(sourcePath));

            string ext = Path.GetExtension(sourcePath).ToLowerInvariant();
            if (ext == ".dll")
            {
                Directory.CreateDirectory(defaultModsRoot);
                string targetFolder = Path.Combine(defaultModsRoot, safeName);
                if (Directory.Exists(targetFolder))
                {
                    targetFolder = Path.Combine(defaultModsRoot, safeName + "-" + DateTime.Now.Ticks.ToString());
                }
                Directory.CreateDirectory(targetFolder);
                string targetDll = Path.Combine(targetFolder, Path.GetFileName(sourcePath));
                File.Copy(sourcePath, targetDll, true);

                ModManifest manifest = new ModManifest();
                manifest.Id = safeName;
                manifest.Name = Path.GetFileNameWithoutExtension(sourcePath);
                manifest.Version = "1.0.0";
                manifest.Author = "Unknown";
                manifest.Description = "Imported DLL mod";
                manifest.Entry = Path.GetFileName(targetDll);
                manifest.Enabled = "true";
                manifest.Kind = "code";
                manifest.Priority = "0";
                manifest.Category = "General";
                manifest.TrustLevel = "Unofficial";
                manifest.TestingStatus = string.Empty;
                string manifestPath = Path.Combine(targetFolder, "adh-manifest.json");
                SimpleManifest.WriteModManifest(manifestPath, manifest);
                return ResolveImportedEntry(targetFolder);
            }

            if (ext == ".zip")
            {
                Directory.CreateDirectory(defaultModsRoot);
                Directory.CreateDirectory(defaultPacksRoot);
                string targetFolder = Path.Combine(defaultModsRoot, safeName);
                if (Directory.Exists(targetFolder))
                {
                    targetFolder = Path.Combine(defaultModsRoot, safeName + "-" + DateTime.Now.Ticks.ToString());
                }
                Directory.CreateDirectory(targetFolder);

                ZipFile.ExtractToDirectory(sourcePath, targetFolder);
                string manifestPath = ResolveManifestFromSelection(targetFolder, selectedManifestRelativePath);
                if (string.IsNullOrEmpty(manifestPath))
                {
                    throw new InvalidOperationException("There was an issue installing this package. It needs adh-manifest.json or manifest.json.");
                }

                ModEntry imported = LoadSingleMod(Path.GetDirectoryName(manifestPath));
                if (imported == null)
                {
                    throw new InvalidOperationException("The selected manifest could not be loaded as a valid ADH mod or pack.");
                }

                if (string.Equals(imported.Manifest.Kind, "pack", StringComparison.OrdinalIgnoreCase))
                {
                    string packTarget = Path.Combine(defaultPacksRoot, Path.GetFileName(imported.FolderPath));
                    if (Directory.Exists(packTarget))
                    {
                        packTarget = Path.Combine(defaultPacksRoot, Path.GetFileName(imported.FolderPath) + "-" + DateTime.Now.Ticks.ToString());
                    }
                    if (!string.Equals(Path.GetFullPath(imported.FolderPath), Path.GetFullPath(packTarget), StringComparison.OrdinalIgnoreCase))
                    {
                        if (Directory.Exists(packTarget))
                        {
                            Directory.Delete(packTarget, true);
                        }

                        Directory.Move(imported.FolderPath, packTarget);
                        imported = LoadSingleMod(packTarget);
                    }
                }

                imported.Manifest.SelectedManifestRelativePath = MakeRelativePath(targetFolder, manifestPath);
                return imported;
            }

            throw new NotSupportedException("Only .dll and .zip imports are supported right now.");
        }

        public static ImportPreviewInfo AnalyzeImportSource(GameInstallInfo game, string sourcePath, IEnumerable<ModEntry> existingMods, string selectedManifestRelativePath)
        {
            ImportPreviewInfo preview = new ImportPreviewInfo();
            preview.SourcePath = sourcePath;
            preview.SourceKind = Path.GetExtension(sourcePath).ToLowerInvariant();
            preview.SelectedManifestRelativePath = selectedManifestRelativePath;

            string tempRoot = Path.Combine(Path.GetTempPath(), "AestikModLoaderPreview", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            try
            {
                string stagedRoot = tempRoot;
                if (File.Exists(sourcePath))
                {
                    if (preview.SourceKind == ".dll")
                    {
                        string targetDll = Path.Combine(tempRoot, Path.GetFileName(sourcePath));
                        File.Copy(sourcePath, targetDll, true);
                        ModManifest manifest = new ModManifest();
                        manifest.Id = SimpleManifest.MakeSafeId(Path.GetFileNameWithoutExtension(sourcePath));
                        manifest.Name = Path.GetFileNameWithoutExtension(sourcePath);
                        manifest.Version = "1.0.0";
                        manifest.Author = "Unknown";
                        manifest.Description = "Imported DLL mod";
                        manifest.Entry = Path.GetFileName(targetDll);
                        manifest.Enabled = "true";
                        manifest.Kind = "code";
                        manifest.Priority = "0";
                        manifest.Category = "General";
                        manifest.TrustLevel = "Unofficial";
                        manifest.TestingStatus = string.Empty;
                        SimpleManifest.WriteModManifest(Path.Combine(tempRoot, "adh-manifest.json"), manifest);
                    }
                    else if (preview.SourceKind == ".zip")
                    {
                        ZipFile.ExtractToDirectory(sourcePath, tempRoot);
                        preview.ManifestCandidates.AddRange(FindManifestCandidatesInArchive(sourcePath));
                    }
                }
                else if (Directory.Exists(sourcePath))
                {
                    CopyDirectory(sourcePath, tempRoot);
                    preview.ManifestCandidates.AddRange(FindManifestCandidates(tempRoot).Select(delegate(string item)
                    {
                        return MakeRelativePath(tempRoot, item);
                    }));
                }

                string selectedManifestPath = ResolveManifestForPreview(tempRoot, preview.ManifestCandidates, preview.SourceKind, preview.SelectedManifestRelativePath);
                if (!string.IsNullOrEmpty(selectedManifestPath))
                {
                    preview.SelectedManifestRelativePath = MakeRelativePath(tempRoot, selectedManifestPath);
                }

                ModEntry entry = !string.IsNullOrEmpty(selectedManifestPath)
                    ? LoadSingleMod(Path.GetDirectoryName(selectedManifestPath))
                    : (LoadSingleMod(tempRoot) ?? LoadSingleModFromCandidates(tempRoot));
                preview.Entry = entry;
                if (entry != null && entry.Manifest != null)
                {
                    preview.DeclaredDependencies.AddRange(SplitImportList(entry.Manifest.Depends));
                    foreach (string dep in preview.DeclaredDependencies)
                    {
                        if (!HasExistingMod(existingMods, dep))
                        {
                            preview.MissingDependencies.Add(dep);
                        }
                    }
                    if (!string.IsNullOrEmpty(entry.Manifest.Description) && entry.Manifest.Description.Length > 240)
                    {
                        preview.Warnings.Add("Description is long; it may indicate a large feature set.");
                    }
                    if (string.Equals(entry.Manifest.Kind, "pack", StringComparison.OrdinalIgnoreCase))
                    {
                        preview.Warnings.Add("This looks like a pack/import bundle rather than a code mod.");
                    }
                }
                else if (preview.SourceKind == ".zip")
                {
                    preview.Warnings.Add("There was an issue reading this package. It needs adh-manifest.json or manifest.json.");
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempRoot))
                    {
                        Directory.Delete(tempRoot, true);
                    }
                }
                catch
                {
                }
            }

            return preview;
        }

        private static bool HasExistingMod(IEnumerable<ModEntry> existingMods, string token)
        {
            if (existingMods == null || string.IsNullOrEmpty(token))
            {
                return false;
            }

            foreach (ModEntry mod in existingMods)
            {
                if (mod == null || mod.Manifest == null)
                {
                    continue;
                }

                if (string.Equals(mod.Manifest.Id, token, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(mod.Manifest.Name, token, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<string> SplitImportList(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return Enumerable.Empty<string>();
            }

            return value.Split(new char[] { ',', ';', '\n', '\r', '|' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(delegate(string item) { return item.Trim(); })
                        .Where(delegate(string item) { return item.Length > 0; });
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (string directory in Directory.GetDirectories(source))
            {
                string name = Path.GetFileName(directory);
                CopyDirectory(directory, Path.Combine(target, name));
            }

            foreach (string file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
            }
        }

        private static ModEntry ResolveImportedEntry(string targetFolder)
        {
            ModEntry direct = LoadSingleMod(targetFolder);
            if (direct != null)
            {
                return direct;
            }

            string[] candidates = EnumerateCandidateFolders(targetFolder);
            for (int i = 0; i < candidates.Length; i++)
            {
                ModEntry entry = LoadSingleMod(candidates[i]);
                if (entry != null)
                {
                    return entry;
                }
            }

            return null;
        }

        private static ModEntry LoadSingleModFromCandidates(string root)
        {
            string[] candidates = EnumerateCandidateFolders(root);
            for (int i = 0; i < candidates.Length; i++)
            {
                ModEntry entry = LoadSingleMod(candidates[i]);
                if (entry != null)
                {
                    return entry;
                }
            }

            return null;
        }

        public static string[] FindManifestOptions(string sourcePath)
        {
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath) || !string.Equals(Path.GetExtension(sourcePath), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                return new string[0];
            }

            return FindManifestCandidatesInArchive(sourcePath).ToArray();
        }

        public static void RemoveMod(ModEntry mod)
        {
            if (mod == null || string.IsNullOrEmpty(mod.FolderPath))
            {
                return;
            }

            if (Directory.Exists(mod.FolderPath))
            {
                Directory.Delete(mod.FolderPath, true);
            }
        }

        public static void EnsureLoaderInstalled(GameInstallInfo game)
        {
            if (game == null)
            {
                throw new InvalidOperationException("Game is not detected.");
            }

            Directory.CreateDirectory(game.ManagedPath);
            Directory.CreateDirectory(Path.Combine(game.GameRoot, "Aestik_Data", "ModLoader", "logs"));
            Directory.CreateDirectory(Path.Combine(game.GameRoot, "Aestik_Data", "ModLoader", "Mods"));
            Directory.CreateDirectory(Path.Combine(game.GameRoot, "Aestik_Data", "ModLoader", "Packs"));

            string runtimeDll = AppPaths.GetRuntimeDllPath();
            string targetDll = Path.Combine(game.ManagedPath, "ADH.Runtime.dll");
            if (!File.Exists(runtimeDll))
            {
                throw new FileNotFoundException("Runtime DLL was not found next to the launcher.", runtimeDll);
            }

            File.Copy(runtimeDll, targetDll, true);
            PatchGameAssembly(game, targetDll);
            SimpleManifest.SavePairs(Path.Combine(game.GameRoot, "Aestik_Data", "ModLoader", "settings.txt"),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "installed", "true" },
                    { "runtime_dll", "ADH.Runtime.dll" },
                    { "game_root", game.GameRoot },
                    { "loader_version", LoaderVersion }
                });
        }

        public static void UninstallLoader(GameInstallInfo game)
        {
            if (game == null)
            {
                return;
            }

            RestoreOriginalGameAssembly(game);

            string[] targetDlls = new string[]
            {
                Path.Combine(game.ManagedPath, "ADH.Runtime.dll")
            };

            for (int i = 0; i < targetDlls.Length; i++)
            {
                if (File.Exists(targetDlls[i]))
                {
                    File.Delete(targetDlls[i]);
                }
            }
        }

        public static void RestoreOriginalGameAssembly(GameInstallInfo game)
        {
            string currentAssembly = AppPaths.GetGameAssemblyPath(game);
            string backupAssembly = AppPaths.GetGameAssemblyBackupPath(game);
            if (string.IsNullOrEmpty(currentAssembly) || string.IsNullOrEmpty(backupAssembly))
            {
                return;
            }

            if (!File.Exists(backupAssembly))
            {
                return;
            }

            File.Copy(backupAssembly, currentAssembly, true);
        }

        public static bool IsBootstrapPatched(GameInstallInfo game)
        {
            string currentAssembly = AppPaths.GetGameAssemblyPath(game);
            if (string.IsNullOrEmpty(currentAssembly) || !File.Exists(currentAssembly))
            {
                return false;
            }

            try
            {
                using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(currentAssembly))
                {
                    MethodDefinition method = GetBootstrapTargetMethod(assembly, false);
                    if (ContainsMethodCall(method, BootstrapTypeName, BootstrapMethodName))
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static void PatchGameAssembly(GameInstallInfo game, string runtimeDll)
        {
            string currentAssembly = AppPaths.GetGameAssemblyPath(game);
            string backupAssembly = AppPaths.GetGameAssemblyBackupPath(game);
            if (string.IsNullOrEmpty(currentAssembly) || !File.Exists(currentAssembly))
            {
                throw new FileNotFoundException("Assembly-CSharp.dll was not found.", currentAssembly);
            }

            if (string.IsNullOrEmpty(backupAssembly))
            {
                throw new InvalidOperationException("Backup path could not be resolved.");
            }

            if (!File.Exists(backupAssembly))
            {
                File.Copy(currentAssembly, backupAssembly, true);
            }

            string tempAssembly = currentAssembly + ".adh.tmp";
            if (File.Exists(tempAssembly))
            {
                File.Delete(tempAssembly);
            }

            DefaultAssemblyResolver resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(game.ManagedPath);
            resolver.AddSearchDirectory(AppPaths.GetAppRoot());

            ReaderParameters readParameters = new ReaderParameters();
            readParameters.ReadSymbols = false;
            readParameters.AssemblyResolver = resolver;

            using (AssemblyDefinition gameAssembly = AssemblyDefinition.ReadAssembly(backupAssembly, readParameters))
            using (AssemblyDefinition runtimeAssembly = AssemblyDefinition.ReadAssembly(runtimeDll, readParameters))
            {
                TypeDefinition bootstrapType = runtimeAssembly.MainModule.Types.FirstOrDefault(delegate(TypeDefinition item)
                {
                    return string.Equals(item.FullName, BootstrapTypeName, StringComparison.Ordinal);
                });

                if (bootstrapType == null)
                {
                    throw new InvalidOperationException("LoaderBootstrap type was not found in the runtime assembly.");
                }

                MethodDefinition bootstrapMethod = bootstrapType.Methods.FirstOrDefault(delegate(MethodDefinition item)
                {
                    return string.Equals(item.Name, BootstrapMethodName, StringComparison.Ordinal) && item.IsStatic;
                });

                if (bootstrapMethod == null)
                {
                    throw new InvalidOperationException("Loader bootstrap method was not found in the runtime assembly.");
                }

                MethodReference importedBootstrap = gameAssembly.MainModule.ImportReference(bootstrapMethod);
                MethodDefinition targetMethod = GetBootstrapTargetMethod(gameAssembly, true);
                if (!ContainsMethodCall(targetMethod, BootstrapTypeName, BootstrapMethodName))
                {
                    ILProcessor processor = targetMethod.Body.GetILProcessor();
                    if (targetMethod.Body.Instructions.Count == 0)
                    {
                        processor.Append(processor.Create(OpCodes.Call, importedBootstrap));
                        processor.Append(processor.Create(OpCodes.Ret));
                    }
                    else
                    {
                        Instruction first = targetMethod.Body.Instructions[0];
                        processor.InsertBefore(first, processor.Create(OpCodes.Call, importedBootstrap));
                    }
                }

                PatchEnemyHealthLifecycle(gameAssembly, runtimeAssembly);

                gameAssembly.Write(tempAssembly);
            }

            File.Copy(tempAssembly, currentAssembly, true);
            File.Delete(tempAssembly);
        }

        private static void PatchEnemyHealthLifecycle(AssemblyDefinition gameAssembly, AssemblyDefinition runtimeAssembly)
        {
            if (gameAssembly == null || runtimeAssembly == null)
            {
                return;
            }

            TypeDefinition bridgeType = runtimeAssembly.MainModule.Types.FirstOrDefault(delegate(TypeDefinition item)
            {
                return string.Equals(item.FullName, EnemyLifecycleBridgeTypeName, StringComparison.Ordinal);
            });

            if (bridgeType == null)
            {
                return;
            }

            MethodDefinition notifyMethod = bridgeType.Methods.FirstOrDefault(delegate(MethodDefinition item)
            {
                return string.Equals(item.Name, EnemyLifecycleMethodName, StringComparison.Ordinal) && item.IsStatic;
            });

            MethodDefinition triggerMethod = bridgeType.Methods.FirstOrDefault(delegate(MethodDefinition item)
            {
                return string.Equals(item.Name, EnemyTriggerMethodName, StringComparison.Ordinal) && item.IsStatic;
            });

            if (notifyMethod == null)
            {
                return;
            }

            TypeDefinition enemyHealthType = gameAssembly.MainModule.Types.FirstOrDefault(delegate(TypeDefinition item)
            {
                return string.Equals(item.Name, "EnemyHealth", StringComparison.Ordinal);
            });

            if (enemyHealthType == null)
            {
                return;
            }

            MethodDefinition startMethod = enemyHealthType.Methods.FirstOrDefault(delegate(MethodDefinition item)
            {
                return string.Equals(item.Name, "Start", StringComparison.Ordinal) && !item.IsStatic;
            });

            if (startMethod == null || !startMethod.HasBody)
            {
                return;
            }

            if (ContainsMethodCall(startMethod, EnemyLifecycleBridgeTypeName, EnemyLifecycleMethodName))
            {
                return;
            }

            MethodReference importedNotify = gameAssembly.MainModule.ImportReference(notifyMethod);
            ILProcessor processor = startMethod.Body.GetILProcessor();
            Instruction[] rets = startMethod.Body.Instructions.Where(delegate(Instruction item)
            {
                return item.OpCode == OpCodes.Ret;
            }).ToArray();

            if (rets.Length == 0)
            {
                processor.Append(processor.Create(OpCodes.Ldarg_0));
                processor.Append(processor.Create(OpCodes.Call, importedNotify));
                processor.Append(processor.Create(OpCodes.Ret));
                return;
            }

            for (int i = 0; i < rets.Length; i++)
            {
                Instruction loadThis = processor.Create(OpCodes.Ldarg_0);
                Instruction callNotify = processor.Create(OpCodes.Call, importedNotify);
                processor.InsertBefore(rets[i], loadThis);
                processor.InsertAfter(loadThis, callNotify);
            }

            PatchEnemyHealthTrigger(gameAssembly, enemyHealthType, triggerMethod);
        }

        private static void PatchEnemyHealthTrigger(AssemblyDefinition gameAssembly, TypeDefinition enemyHealthType, MethodDefinition triggerMethod)
        {
            if (gameAssembly == null || enemyHealthType == null || triggerMethod == null)
            {
                return;
            }

            MethodDefinition onTriggerEnterMethod = enemyHealthType.Methods.FirstOrDefault(delegate(MethodDefinition item)
            {
                return string.Equals(item.Name, "OnTriggerEnter2D", StringComparison.Ordinal) &&
                    !item.IsStatic &&
                    item.Parameters.Count == 1;
            });

            if (onTriggerEnterMethod == null || !onTriggerEnterMethod.HasBody)
            {
                return;
            }

            if (ContainsMethodCall(onTriggerEnterMethod, EnemyLifecycleBridgeTypeName, EnemyTriggerMethodName))
            {
                return;
            }

            MethodReference importedTrigger = gameAssembly.MainModule.ImportReference(triggerMethod);
            ILProcessor processor = onTriggerEnterMethod.Body.GetILProcessor();
            Instruction first = onTriggerEnterMethod.Body.Instructions[0];
            processor.InsertBefore(first, processor.Create(OpCodes.Ldarg_0));
            processor.InsertBefore(first, processor.Create(OpCodes.Ldarg_1));
            processor.InsertBefore(first, processor.Create(OpCodes.Call, importedTrigger));
        }

        private static bool ContainsMethodCall(MethodDefinition method, string declaringTypeName, string methodName)
        {
            if (method == null || !method.HasBody)
            {
                return false;
            }

            for (int i = 0; i < method.Body.Instructions.Count; i++)
            {
                Instruction instruction = method.Body.Instructions[i];
                if (instruction.OpCode != OpCodes.Call && instruction.OpCode != OpCodes.Callvirt)
                {
                    continue;
                }

                MethodReference reference = instruction.Operand as MethodReference;
                if (reference == null)
                {
                    continue;
                }

                if (string.Equals(reference.DeclaringType.FullName, declaringTypeName, StringComparison.Ordinal) &&
                    string.Equals(reference.Name, methodName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static MethodDefinition GetBootstrapTargetMethod(AssemblyDefinition assembly, bool createIfMissing)
        {
            if (assembly == null)
            {
                return null;
            }

            TypeDefinition moduleType = assembly.MainModule.Types.FirstOrDefault(delegate(TypeDefinition item)
            {
                return string.Equals(item.Name, "<Module>", StringComparison.Ordinal);
            });

            if (moduleType == null)
            {
                return null;
            }

            MethodDefinition method = moduleType.Methods.FirstOrDefault(delegate(MethodDefinition item)
            {
                return string.Equals(item.Name, ".cctor", StringComparison.Ordinal);
            });

            if (method != null || !createIfMissing)
            {
                return method;
            }

            method = new MethodDefinition(".cctor",
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig |
                MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                assembly.MainModule.TypeSystem.Void);
            moduleType.Methods.Add(method);
            return method;
        }

        private static string FindManifestPath(string folder)
        {
            string[] candidates = new string[]
            {
                "adh-manifest.json",
                "manifest.json"
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                string exact = FindExactFileInDirectory(folder, candidates[i]);
                if (!string.IsNullOrEmpty(exact))
                {
                    return exact;
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
                "adh-manifest.json",
                "manifest.json"
            };

            for (int i = 0; i < manifestCandidates.Length; i++)
            {
                if (!string.IsNullOrEmpty(FindExactFileInDirectory(folder, manifestCandidates[i])))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<string> FindManifestCandidates(string root)
        {
            List<string> results = new List<string>();
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                return results;
            }

            string[] files = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string name = Path.GetFileName(files[i]);
                if (string.Equals(name, "adh-manifest.json", StringComparison.Ordinal) ||
                    string.Equals(name, "manifest.json", StringComparison.Ordinal))
                {
                    results.Add(files[i]);
                }
            }

            return results.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(delegate(string item) { return item.Length; }).ToList();
        }

        private static string ResolveManifestForPreview(string root, IList<string> manifestCandidates, string sourceKind, string selectedManifestRelativePath)
        {
            if (!string.Equals(sourceKind, ".zip", StringComparison.OrdinalIgnoreCase))
            {
                return FindManifestPath(root);
            }

            if (manifestCandidates == null || manifestCandidates.Count == 0)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(selectedManifestRelativePath))
            {
                string selectedPath = Path.Combine(root, selectedManifestRelativePath);
                if (File.Exists(selectedPath))
                {
                    return selectedPath;
                }
            }

            string exact = manifestCandidates.FirstOrDefault(delegate(string item)
            {
                return string.Equals(Path.GetFileName(item), "adh-manifest.json", StringComparison.Ordinal);
            });
            if (!string.IsNullOrEmpty(exact))
            {
                return Path.Combine(root, exact);
            }

            if (manifestCandidates.Count == 1)
            {
                return Path.Combine(root, manifestCandidates[0]);
            }

            return null;
        }

        private static string ResolveManifestFromSelection(string root, string selectedManifestRelativePath)
        {
            List<string> candidates = FindManifestCandidates(root);
            if (candidates.Count == 0)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(selectedManifestRelativePath))
            {
                string requested = Path.GetFullPath(Path.Combine(root, selectedManifestRelativePath));
                return candidates.FirstOrDefault(delegate(string item)
                {
                    return string.Equals(Path.GetFullPath(item), requested, StringComparison.OrdinalIgnoreCase);
                });
            }

            string adhManifest = candidates.FirstOrDefault(delegate(string item)
            {
                return string.Equals(Path.GetFileName(item), "adh-manifest.json", StringComparison.Ordinal);
            });
            if (!string.IsNullOrEmpty(adhManifest))
            {
                return adhManifest;
            }

            if (candidates.Count == 1)
            {
                return candidates[0];
            }

            return null;
        }

        private static string MakeRelativePath(string root, string fullPath)
        {
            Uri rootUri = new Uri(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
            Uri fileUri = new Uri(Path.GetFullPath(fullPath));
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(fileUri).ToString()).Replace('/', Path.DirectorySeparatorChar);
        }

        private static string FindExactFileInDirectory(string folder, string fileName)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return null;
            }

            string[] files = Directory.GetFiles(folder, "*", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                if (string.Equals(Path.GetFileName(files[i]), fileName, StringComparison.Ordinal))
                {
                    return files[i];
                }
            }

            return null;
        }

        private static List<string> FindManifestCandidatesInArchive(string sourcePath)
        {
            List<string> results = new List<string>();
            using (ZipArchive archive = ZipFile.OpenRead(sourcePath))
            {
                for (int i = 0; i < archive.Entries.Count; i++)
                {
                    ZipArchiveEntry entry = archive.Entries[i];
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        continue;
                    }

                    if (string.Equals(entry.Name, "adh-manifest.json", StringComparison.Ordinal) ||
                        string.Equals(entry.Name, "manifest.json", StringComparison.Ordinal))
                    {
                        string normalized = entry.FullName.Replace('/', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);
                        results.Add(normalized);
                    }
                }
            }

            return results.Distinct(StringComparer.Ordinal).OrderBy(delegate(string item) { return item.Length; }).ToList();
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

        private static bool IsOutOfDate(ModManifest manifest)
        {
            if (manifest == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(manifest.LoaderVersion))
            {
                return false;
            }

            return !string.Equals(manifest.LoaderVersion, LoaderVersion, StringComparison.OrdinalIgnoreCase);
        }

        private static DateTime GetFolderWriteTimeUtc(string folder)
        {
            try
            {
                return Directory.GetLastWriteTimeUtc(folder);
            }
            catch
            {
                return DateTime.MinValue;
            }
        }
    }
}
