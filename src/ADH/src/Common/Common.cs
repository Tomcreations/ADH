using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace AestikModLoader.Common
{
    public sealed class GameInstallInfo
    {
        public string SteamPath { get; set; }
        public string LibraryPath { get; set; }
        public string InstallDir { get; set; }
        public string GameRoot { get; set; }
        public string ExecutablePath { get; set; }
        public string ManagedPath { get; set; }
        public string ManifestPath { get; set; }
        public string AppId { get; set; }
        public bool FoundViaSteamManifest { get; set; }
    }

    public sealed class ModManifest
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public string Description { get; set; }
        public string Entry { get; set; }
        public string Enabled { get; set; }
        public string Kind { get; set; }
        public string Priority { get; set; }
        public string Depends { get; set; }
        public string After { get; set; }
        public string Before { get; set; }
        public string LoaderVersion { get; set; }
        public string SourceUrl { get; set; }
        public string DownloadUrl { get; set; }
        public string Category { get; set; }
        public string TrustLevel { get; set; }
        public string TestingStatus { get; set; }
        public string SelectedManifestRelativePath { get; set; }

        public bool IsEnabled
        {
            get
            {
                return string.Equals(Enabled, "true", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(Enabled, "1", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(Enabled, "yes", StringComparison.OrdinalIgnoreCase);
            }
            set
            {
                Enabled = value ? "true" : "false";
            }
        }
    }

    public sealed class ModEntry
    {
        public ModManifest Manifest { get; set; }
        public string FolderPath { get; set; }
        public string ManifestPath { get; set; }
        public string EntryPath { get; set; }
        public bool EntryExists { get; set; }
        public string Status { get; set; }
        public DateTime LastModifiedUtc { get; set; }
        public bool HasUpdate { get; set; }
        public bool IsPack { get; set; }
        public bool IsInstalled { get; set; }
    }

    public static class SimpleManifest
    {
        public static Dictionary<string, string> LoadPairs(string path)
        {
            Dictionary<string, string> data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path))
            {
                return data;
            }

            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0)
                {
                    continue;
                }
                if (line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith(";", StringComparison.Ordinal))
                {
                    continue;
                }

                int idx = line.IndexOf('=');
                if (idx < 0)
                {
                    idx = line.IndexOf(':');
                }
                if (idx < 0)
                {
                    continue;
                }

                string key = line.Substring(0, idx).Trim();
                string value = line.Substring(idx + 1).Trim();
                data[key] = value;
            }

            return data;
        }

        public static void SavePairs(string path, IDictionary<string, string> data)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using (StreamWriter writer = new StreamWriter(path, false, Encoding.UTF8))
            {
                foreach (KeyValuePair<string, string> pair in data)
                {
                    writer.WriteLine(pair.Key + "=" + pair.Value);
                }
            }
        }

        public static ModManifest ReadModManifest(string path)
        {
            if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return ReadJsonModManifest(path);
            }

            Dictionary<string, string> values = LoadPairs(path);
            if (values.Count == 0)
            {
                return null;
            }

            ModManifest manifest = new ModManifest();
            string value;
            if (values.TryGetValue("id", out value)) manifest.Id = value;
            if (values.TryGetValue("name", out value)) manifest.Name = value;
            if (values.TryGetValue("version", out value)) manifest.Version = value;
            if (values.TryGetValue("author", out value)) manifest.Author = value;
            if (values.TryGetValue("description", out value)) manifest.Description = value;
            if (values.TryGetValue("entry", out value)) manifest.Entry = value;
            if (values.TryGetValue("enabled", out value)) manifest.Enabled = value;
            if (values.TryGetValue("kind", out value)) manifest.Kind = value;
            if (values.TryGetValue("priority", out value)) manifest.Priority = value;
            if (values.TryGetValue("depends", out value)) manifest.Depends = value;
            if (values.TryGetValue("after", out value)) manifest.After = value;
            if (values.TryGetValue("before", out value)) manifest.Before = value;
            if (values.TryGetValue("loader_version", out value)) manifest.LoaderVersion = value;
            if (values.TryGetValue("source_url", out value)) manifest.SourceUrl = value;
            if (values.TryGetValue("download_url", out value)) manifest.DownloadUrl = value;
            if (values.TryGetValue("category", out value)) manifest.Category = value;
            if (values.TryGetValue("trust_level", out value)) manifest.TrustLevel = value;
            if (values.TryGetValue("testing_status", out value)) manifest.TestingStatus = value;

            if (string.IsNullOrEmpty(manifest.Enabled))
            {
                manifest.Enabled = "true";
            }

            if (string.IsNullOrEmpty(manifest.Name))
            {
                manifest.Name = Path.GetFileName(Path.GetDirectoryName(path));
            }

            if (string.IsNullOrEmpty(manifest.Id))
            {
                manifest.Id = MakeSafeId(manifest.Name);
            }

            if (string.IsNullOrEmpty(manifest.Version))
            {
                manifest.Version = "1.0.0";
            }

            if (string.IsNullOrEmpty(manifest.Kind))
            {
                manifest.Kind = "code";
            }

            if (string.IsNullOrEmpty(manifest.Priority))
            {
                manifest.Priority = "0";
            }

            if (string.IsNullOrEmpty(manifest.Category))
            {
                manifest.Category = "General";
            }

            if (string.IsNullOrEmpty(manifest.LoaderVersion))
            {
                manifest.LoaderVersion = "2.0.0";
            }

            if (string.IsNullOrEmpty(manifest.TrustLevel))
            {
                manifest.TrustLevel = "Unofficial";
            }

            if (string.IsNullOrEmpty(manifest.TestingStatus))
            {
                manifest.TestingStatus = string.Empty;
            }

            return manifest;
        }

        private static ModManifest ReadJsonModManifest(string path)
        {
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                ModManifest manifest = new ModManifest();
                manifest.Id = ExtractJsonString(json, "id");
                manifest.Name = ExtractJsonString(json, "name");
                manifest.Version = ExtractJsonString(json, "version");
                manifest.Author = ExtractJsonString(json, "author");
                manifest.Description = ExtractJsonString(json, "description");
                manifest.Entry = ExtractJsonString(json, "entry");
                manifest.Enabled = ExtractJsonString(json, "enabled");
                manifest.Kind = ExtractJsonString(json, "kind");
                manifest.Priority = ExtractJsonString(json, "priority");
                manifest.Depends = ExtractJsonString(json, "depends");
                manifest.After = ExtractJsonString(json, "after");
                manifest.Before = ExtractJsonString(json, "before");
                manifest.LoaderVersion = ExtractJsonString(json, "loader_version");
                manifest.SourceUrl = ExtractJsonString(json, "source_url");
                manifest.DownloadUrl = ExtractJsonString(json, "download_url");
                manifest.Category = ExtractJsonString(json, "category");
                manifest.TrustLevel = ExtractJsonString(json, "trust_level");
                manifest.TestingStatus = ExtractJsonString(json, "testing_status");
                if (string.IsNullOrEmpty(manifest.Enabled))
                {
                    manifest.Enabled = "true";
                }
                if (string.IsNullOrEmpty(manifest.Kind))
                {
                    manifest.Kind = "code";
                }
                if (string.IsNullOrEmpty(manifest.Category))
                {
                    manifest.Category = "General";
                }
                if (string.IsNullOrEmpty(manifest.Priority))
                {
                    manifest.Priority = "0";
                }
                if (string.IsNullOrEmpty(manifest.LoaderVersion))
                {
                    manifest.LoaderVersion = "2.0.0";
                }
                if (string.IsNullOrEmpty(manifest.TrustLevel))
                {
                    manifest.TrustLevel = "Unofficial";
                }
                if (string.IsNullOrEmpty(manifest.TestingStatus))
                {
                    manifest.TestingStatus = string.Empty;
                }
                if (string.IsNullOrEmpty(manifest.Id))
                {
                    manifest.Id = MakeSafeId(manifest.Name);
                }
                if (string.IsNullOrEmpty(manifest.Name))
                {
                    manifest.Name = Path.GetFileName(Path.GetDirectoryName(path));
                }
                if (string.IsNullOrEmpty(manifest.Version))
                {
                    manifest.Version = "1.0.0";
                }
                return manifest;
            }
            catch
            {
                return null;
            }
        }

        private static string ExtractJsonString(string json, string key)
        {
            string pattern = "\"" + key + "\"\\s*:\\s*\"([^\"]*)\"";
            System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(json, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value;
            }
            return null;
        }

        public static void WriteModManifest(string path, ModManifest manifest)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            values["id"] = manifest.Id ?? "";
            values["name"] = manifest.Name ?? "";
            values["version"] = manifest.Version ?? "";
            values["author"] = manifest.Author ?? "";
            values["description"] = manifest.Description ?? "";
            values["entry"] = manifest.Entry ?? "";
            values["enabled"] = manifest.Enabled ?? "true";
            values["kind"] = manifest.Kind ?? "code";
            values["priority"] = manifest.Priority ?? "0";
            values["depends"] = manifest.Depends ?? "";
            values["after"] = manifest.After ?? "";
            values["before"] = manifest.Before ?? "";
            values["loader_version"] = manifest.LoaderVersion ?? "";
            values["source_url"] = manifest.SourceUrl ?? "";
            values["download_url"] = manifest.DownloadUrl ?? "";
            values["category"] = manifest.Category ?? "General";
            values["trust_level"] = manifest.TrustLevel ?? "Unofficial";
            values["testing_status"] = manifest.TestingStatus ?? "";
            SavePairs(path, values);
        }

        public static string MakeSafeId(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "mod-" + DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
            }

            StringBuilder builder = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = char.ToLowerInvariant(text[i]);
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_')
                {
                    builder.Append(c);
                }
                else if (char.IsWhiteSpace(c) || c == '.' || c == '+')
                {
                    builder.Append('-');
                }
            }

            string result = builder.ToString().Trim('-');
            if (result.Length == 0)
            {
                result = "mod-" + DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
            }

            return result;
        }
    }
}
