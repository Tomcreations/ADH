using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using AestikModLoader.Common;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace AestikModLoader.App
{
    public sealed class HtmlLauncherForm : Form
    {
        private const int ModPageSize = 18;
        private const string UiHost = "adh.app";
        private const string CatalogApiUrl = "https://adh.tomcreations.org/api/catalog";
        private const string UploadPortalUrl = "https://adh.tomcreations.org/ModUpload";
        private const string BackgroundAssetName = "ADH-Background.png";
        private const string LogoAssetName = "ADH-Logo.ico";
        private const int DwmwaUseImmersiveDarkMode = 20;
        private readonly JavaScriptSerializer serializer;
        private readonly WebView2 webView;
        private readonly LauncherSettings settings;
        private readonly List<string> activity;
        private readonly Panel titleButtonPanel;
        private readonly Panel loadingOverlay;
        private readonly Label loadingTitle;
        private readonly Label loadingCopy;
        private readonly Button minimizeButton;
        private readonly Button maximizeButton;
        private readonly Button closeButton;
        private string installingModKey;
        private int installProgressPercent;
        private string installStatusText;
        private GameInstallInfo currentGame;
        private List<ModEntry> loadedMods;
        private List<ModEntry> loadedPacks;
        private List<ModEntry> catalogMods;
        private ModEntry selectedMod;
        private string uiPath;
        private bool uiReady;
        private bool disposed;

        public HtmlLauncherForm()
        {
            serializer = new JavaScriptSerializer();
            settings = LauncherSettings.Load();
            activity = new List<string>();
            loadedMods = new List<ModEntry>();
            loadedPacks = new List<ModEntry>();
            catalogMods = new List<ModEntry>();
            installingModKey = string.Empty;
            installStatusText = string.Empty;
            Size backgroundSize = GetBackgroundImageSize();
            Size workingArea = Screen.PrimaryScreen != null ? Screen.PrimaryScreen.WorkingArea.Size : new Size(1600, 900);
            int preferredWidth = backgroundSize.Width > 0 ? Math.Max(1420, (backgroundSize.Width * 82) / 100) : 1480;
            int preferredHeight = backgroundSize.Height > 0 ? Math.Max(780, (backgroundSize.Height * 82) / 100) : 820;
            int windowWidth = Math.Min(workingArea.Width, Math.Max(1360, Math.Min(preferredWidth, Math.Max(1360, settings.Width))));
            int windowHeight = Math.Min(workingArea.Height, Math.Max(760, Math.Min(preferredHeight, Math.Max(760, settings.Height))));

            Text = "ADH";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Color.FromArgb(7, 10, 18);
            ForeColor = Color.White;
            ShowIcon = true;
            MinimumSize = new Size(1280, 720);
            Size = new Size(windowWidth, windowHeight);
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            DoubleBuffered = true;
            ApplyBrandIcon();

            webView = new WebView2();
            webView.Dock = DockStyle.Fill;
            webView.DefaultBackgroundColor = Color.FromArgb(7, 10, 18);
            Controls.Add(webView);

            loadingOverlay = new Panel();
            loadingOverlay.Dock = DockStyle.Fill;
            loadingOverlay.BackColor = Color.FromArgb(7, 10, 18);

            loadingTitle = new Label();
            loadingTitle.AutoSize = true;
            loadingTitle.Text = "Loading ADH";
            loadingTitle.Font = new Font("Segoe UI Semibold", 15f, FontStyle.Bold);
            loadingTitle.ForeColor = Color.White;
            loadingTitle.BackColor = Color.Transparent;

            loadingCopy = new Label();
            loadingCopy.AutoSize = true;
            loadingCopy.MaximumSize = new Size(460, 0);
            loadingCopy.Text = "Preparing the launcher UI and mod data.";
            loadingCopy.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
            loadingCopy.ForeColor = Color.FromArgb(186, 197, 222);
            loadingCopy.BackColor = Color.Transparent;

            loadingOverlay.Controls.Add(loadingTitle);
            loadingOverlay.Controls.Add(loadingCopy);
            Controls.Add(loadingOverlay);

            titleButtonPanel = new Panel();
            titleButtonPanel.Size = new Size(138, 57);
            titleButtonPanel.BackColor = Color.FromArgb(18, 20, 37);
            titleButtonPanel.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            minimizeButton = CreateTitleButton();
            maximizeButton = CreateTitleButton();
            closeButton = CreateTitleButton();

            minimizeButton.Location = new Point(0, 0);
            maximizeButton.Location = new Point(46, 0);
            closeButton.Location = new Point(92, 0);

            minimizeButton.Click += delegate { WindowState = FormWindowState.Minimized; };
            maximizeButton.Click += delegate { ToggleMaximize(); };
            closeButton.Click += delegate { Close(); };

            WireTitleButtonHover(minimizeButton, Color.FromArgb(46, 49, 58), Color.White);
            WireTitleButtonHover(maximizeButton, Color.FromArgb(46, 49, 58), Color.White);
            WireTitleButtonHover(closeButton, Color.FromArgb(232, 17, 35), Color.White);

            minimizeButton.Paint += TitleButtonPaint;
            maximizeButton.Paint += TitleButtonPaint;
            closeButton.Paint += TitleButtonPaint;

            titleButtonPanel.Controls.Add(minimizeButton);
            titleButtonPanel.Controls.Add(maximizeButton);
            titleButtonPanel.Controls.Add(closeButton);
            Controls.Add(titleButtonPanel);
            titleButtonPanel.BringToFront();
            CenterLoadingOverlay();

            Load += HtmlLauncherForm_Load;
            FormClosing += HtmlLauncherForm_FormClosing;
            Resize += HtmlLauncherForm_Resize;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyDarkTitleBar();
        }

        private void HtmlLauncherForm_Resize(object sender, EventArgs e)
        {
            titleButtonPanel.Location = new Point(Math.Max(0, ClientSize.Width - titleButtonPanel.Width), 0);
            CenterLoadingOverlay();
        }

        private async void HtmlLauncherForm_Load(object sender, EventArgs e)
        {
            try
            {
                await EnsureWebViewAsync();
                uiPath = Path.Combine(AppPaths.GetAppRoot(), "adh-ui.html");
                if (!File.Exists(uiPath))
                {
                    throw new FileNotFoundException("Missing launcher UI file: " + uiPath);
                }

                SyncBackgroundAsset();
                webView.CoreWebView2.SetVirtualHostNameToFolderMapping(UiHost, AppPaths.GetAppRoot(), CoreWebView2HostResourceAccessKind.Allow);
                webView.CoreWebView2.Navigate("https://" + UiHost + "/adh-ui.html");
                AddActivity("Launcher loaded.");
                await DiscoverAndRefreshAsync(settings.AutoSearch);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "ADH UI failed to load", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
        }

        private void HtmlLauncherForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            disposed = true;
            settings.Width = Width;
            settings.Height = Height;
            settings.AutoSearch = settings.AutoSearch;
            settings.RememberWindow = settings.RememberWindow;
            settings.Save();
        }

        private async System.Threading.Tasks.Task EnsureWebViewAsync()
        {
            if (webView.CoreWebView2 != null)
            {
                return;
            }

            string environmentFolder = Path.Combine(AppPaths.GetUserDataRoot(), "webview2");
            Directory.CreateDirectory(environmentFolder);
            CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, environmentFolder);
            await webView.EnsureCoreWebView2Async(env);
            webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
            webView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
        }

        private void CoreWebView2_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            uiReady = e != null && e.IsSuccess;
            loadingOverlay.Visible = !uiReady;
            if (uiReady)
            {
                webView.BringToFront();
                titleButtonPanel.BringToFront();
            }
            PushState();
        }

        private void CenterLoadingOverlay()
        {
            int blockWidth = Math.Max(loadingTitle.Width, loadingCopy.Width);
            int totalHeight = loadingTitle.Height + 10 + loadingCopy.Height;
            int originX = Math.Max(24, (ClientSize.Width - blockWidth) / 2);
            int originY = Math.Max(24, (ClientSize.Height - totalHeight) / 2);
            loadingTitle.Location = new Point(originX, originY);
            loadingCopy.Location = new Point(originX, originY + loadingTitle.Height + 10);
        }

        private void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            if (disposed)
            {
                return;
            }

            try
            {
                Dictionary<string, object> payload = serializer.DeserializeObject(e.WebMessageAsJson) as Dictionary<string, object>;
                if (payload == null)
                {
                    return;
                }

                string action = GetString(payload, "action");
                switch (action)
                {
                    case "ready":
                    case "init":
                        PushState();
                        break;
                    case "discover":
                        DiscoverAndRefreshAsync(GetBool(payload, "activeSearch"));
                        break;
                    case "install":
                        InstallLoader();
                        break;
                    case "launch":
                    case "launchModded":
                        LaunchModdedGame();
                        break;
                    case "launchVanilla":
                        LaunchVanillaGame();
                        break;
                    case "import":
                        ImportMod();
                        break;
                    case "openUploadPortal":
                        OpenUploadPortal();
                        break;
                    case "openDeveloperMods":
                        OpenUploadPortal("code");
                        break;
                    case "openDeveloperPacks":
                        OpenUploadPortal("pack");
                        break;
                    case "openDeveloperManage":
                        OpenUploadPortal("manage");
                        break;
                    case "openWebsite":
                        OpenRepository(GetString(payload, "key"));
                        break;
                    case "installCatalogMod":
                        InstallCatalogMod(GetString(payload, "key"));
                        break;
                    case "refresh":
                        RefreshMods();
                        break;
                    case "openModsFolder":
                        OpenModsFolder();
                        break;
                    case "openPacksFolder":
                        OpenPacksFolder();
                        break;
                    case "openModFolder":
                        OpenModFolder(GetString(payload, "key"));
                        break;
                    case "openPackFolder":
                        OpenPackFolder(GetString(payload, "key"));
                        break;
                    case "openAppData":
                        OpenAppData();
                        break;
                    case "openGameFolder":
                        OpenGameFolder();
                        break;
                    case "openConsole":
                        OpenConsoleWindow();
                        break;
                    case "toggleMod":
                        SetModEnabled(GetString(payload, "key"), GetBool(payload, "enabled"));
                        break;
                    case "DeleteMod":
                    case "removeMod":
                        RemoveMod(GetString(payload, "key"));
                        break;
                    case "selectMod":
                        SelectMod(GetString(payload, "key"));
                        break;
                    case "clearRuntimeLog":
                        ClearRuntimeLog();
                        break;
                    case "setAutoSearch":
                        settings.AutoSearch = GetBool(payload, "value");
                        settings.Save();
                        PushState();
                        break;
                    case "setRememberWindow":
                        settings.RememberWindow = GetBool(payload, "value");
                        settings.Save();
                        PushState();
                        break;
                }
            }
            catch (Exception ex)
            {
                AddActivity("Action failed: " + ex.Message);
                PushState();
            }
        }

        private async System.Threading.Tasks.Task DiscoverAndRefreshAsync(bool activeSearch)
        {
            AddActivity(activeSearch ? "Running active Steam search." : "Checking cached Steam path.");
            DiscoveryResult result = null;
            await System.Threading.Tasks.Task.Run(delegate
            {
                result = SteamLocator.DiscoverGame(activeSearch);
            });

            currentGame = result != null ? result.Install : null;
            catalogMods = FetchCatalogMods();
            if (currentGame == null)
            {
                loadedMods = new List<ModEntry>();
                loadedPacks = new List<ModEntry>();
                AddActivity("Aestik was not found.");
                PushState();
                return;
            }

            EnsureLoaderReady();
            AddActivity("Detected " + currentGame.GameRoot);
            loadedMods = ModRepository.LoadMods(currentGame);
            loadedPacks = LoadPackFolders(currentGame);
            selectedMod = loadedMods.Count > 0 ? loadedMods[0] : catalogMods.FirstOrDefault();
            AddActivity("Loaded " + loadedMods.Count + " installed mods, " + catalogMods.Count + " catalog mods, and " + loadedPacks.Count + " packs.");
            PushState();
        }

        private void PushState()
        {
            if (!uiReady || webView.CoreWebView2 == null)
            {
                return;
            }

            LauncherStateDto state = BuildState();
            string json = serializer.Serialize(state);
            webView.CoreWebView2.PostWebMessageAsJson(json);
        }

        private LauncherStateDto BuildState()
        {
            LauncherStateDto state = new LauncherStateDto();
            state.title = "ADH";
            state.status = currentGame == null ? "Aestik not detected" : (LoaderInstalled() ? "Ready" : "Needs attention");
            state.gameDetected = currentGame != null;
            state.gameRoot = currentGame != null ? currentGame.GameRoot : "";
            state.steamState = currentGame == null ? "Not found" : "Steam linked";
            state.loaderInstalled = LoaderInstalled();
            state.modsCount = loadedMods != null ? loadedMods.Count : 0;
            state.catalogCount = catalogMods != null ? catalogMods.Count : 0;
            state.packsCount = loadedPacks != null ? loadedPacks.Count : 0;
            state.autoSearch = settings.AutoSearch;
            state.rememberWindow = settings.RememberWindow;
            state.activity = activity.Skip(Math.Max(0, activity.Count - 6)).ToList();
            state.mods = BuildModDtos(BuildCombinedMods());
            state.packs = BuildModDtos(loadedPacks);
            state.selectedKey = selectedMod != null ? selectedMod.FolderPath : "";
            return state;
        }

        private List<LauncherModDto> BuildModDtos(IEnumerable<ModEntry> entries)
        {
            List<LauncherModDto> result = new List<LauncherModDto>();
            if (entries == null)
            {
                return result;
            }

            foreach (ModEntry entry in entries)
            {
                if (entry == null || entry.Manifest == null)
                {
                    continue;
                }

                NormalizeManifestPresentation(entry.Manifest);

                LauncherModDto dto = new LauncherModDto();
                dto.key = entry.FolderPath;
                dto.id = entry.Manifest.Id;
                dto.name = entry.Manifest.Name;
                dto.version = entry.Manifest.Version;
                dto.author = entry.Manifest.Author;
                dto.description = entry.Manifest.Description;
                dto.entryExists = entry.EntryExists;
                dto.enabled = entry.Manifest.IsEnabled;
                dto.kind = entry.Manifest.Kind;
                dto.category = entry.Manifest.Category;
                dto.status = entry.Status;
                dto.hasUpdate = entry.HasUpdate;
                dto.loaderVersion = entry.Manifest.LoaderVersion;
                dto.entryPath = entry.EntryPath;
                dto.lastModifiedUtc = entry.LastModifiedUtc.ToString("o");
                dto.sourceUrl = entry.Manifest.SourceUrl;
                dto.downloadUrl = entry.Manifest.DownloadUrl;
                dto.trustLevel = entry.Manifest.TrustLevel;
                dto.testingStatus = entry.Manifest.TestingStatus;
                dto.installed = entry.IsInstalled;
                dto.installing = string.Equals(installingModKey, entry.FolderPath, StringComparison.OrdinalIgnoreCase);
                dto.installProgressPercent = dto.installing ? installProgressPercent : 0;
                dto.installStatusText = dto.installing ? installStatusText : string.Empty;
                dto.depends = SplitList(entry.Manifest.Depends);
                dto.after = SplitList(entry.Manifest.After);
                dto.before = SplitList(entry.Manifest.Before);
                result.Add(dto);
            }

            Dictionary<string, int> priorities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (ModEntry entry in entries)
            {
                if (entry != null && entry.Manifest != null)
                {
                    int priority;
                    priorities[entry.FolderPath] = int.TryParse(entry.Manifest.Priority, out priority) ? priority : 0;
                }
            }

            return result.OrderByDescending(delegate(LauncherModDto mod)
            {
                return mod.installed;
            }).ThenByDescending(delegate(LauncherModDto mod)
            {
                int priority;
                return priorities.TryGetValue(mod.key, out priority) ? priority : 0;
            }).ToList();
        }

        private List<ModEntry> BuildCombinedMods()
        {
            Dictionary<string, ModEntry> combined = new Dictionary<string, ModEntry>(StringComparer.OrdinalIgnoreCase);

            foreach (ModEntry entry in catalogMods ?? new List<ModEntry>())
            {
                string identity = GetModIdentityKey(entry);
                if (string.IsNullOrEmpty(identity))
                {
                    continue;
                }

                combined[identity] = CloneEntry(entry);
            }

            foreach (ModEntry entry in loadedMods ?? new List<ModEntry>())
            {
                string identity = GetModIdentityKey(entry);
                if (string.IsNullOrEmpty(identity))
                {
                    continue;
                }

                ModEntry merged = CloneEntry(entry);
                ModEntry catalogEntry;
                if (combined.TryGetValue(identity, out catalogEntry) && catalogEntry != null && catalogEntry.Manifest != null)
                {
                    if (string.IsNullOrEmpty(merged.Manifest.SourceUrl))
                    {
                        merged.Manifest.SourceUrl = catalogEntry.Manifest.SourceUrl;
                    }
                    if (string.IsNullOrEmpty(merged.Manifest.DownloadUrl))
                    {
                        merged.Manifest.DownloadUrl = catalogEntry.Manifest.DownloadUrl;
                    }
                    if (string.IsNullOrEmpty(merged.Manifest.TrustLevel) || string.Equals(merged.Manifest.TrustLevel, "Unofficial", StringComparison.OrdinalIgnoreCase))
                    {
                        merged.Manifest.TrustLevel = catalogEntry.Manifest.TrustLevel;
                    }
                    if (string.IsNullOrEmpty(merged.Manifest.TestingStatus))
                    {
                        merged.Manifest.TestingStatus = catalogEntry.Manifest.TestingStatus;
                    }
                }

                merged.IsInstalled = true;
                combined[identity] = merged;
            }

            return combined.Values.ToList();
        }

        private static string GetModIdentityKey(ModEntry entry)
        {
            if (entry == null || entry.Manifest == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(entry.Manifest.Name))
            {
                return SimpleManifest.MakeSafeId(entry.Manifest.Name);
            }

            if (!string.IsNullOrWhiteSpace(entry.Manifest.Id))
            {
                return SimpleManifest.MakeSafeId(entry.Manifest.Id);
            }

            return string.Empty;
        }

        private List<string> SplitList(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return new List<string>();
            }

            return value.Split(new char[] { ',', ';', '\n', '\r', '|' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(delegate(string item) { return item.Trim(); })
                        .Where(delegate(string item) { return item.Length > 0; })
                        .ToList();
        }

        private static ModEntry CloneEntry(ModEntry source)
        {
            if (source == null || source.Manifest == null)
            {
                return null;
            }

            ModManifest manifest = new ModManifest();
            manifest.Id = source.Manifest.Id;
            manifest.Name = source.Manifest.Name;
            manifest.Version = source.Manifest.Version;
            manifest.Author = source.Manifest.Author;
            manifest.Description = source.Manifest.Description;
            manifest.Entry = source.Manifest.Entry;
            manifest.Enabled = source.Manifest.Enabled;
            manifest.Kind = source.Manifest.Kind;
            manifest.Priority = source.Manifest.Priority;
            manifest.Depends = source.Manifest.Depends;
            manifest.After = source.Manifest.After;
            manifest.Before = source.Manifest.Before;
            manifest.LoaderVersion = source.Manifest.LoaderVersion;
            manifest.SourceUrl = source.Manifest.SourceUrl;
            manifest.DownloadUrl = source.Manifest.DownloadUrl;
            manifest.Category = source.Manifest.Category;
            manifest.TrustLevel = source.Manifest.TrustLevel;
            manifest.TestingStatus = source.Manifest.TestingStatus;
            NormalizeManifestPresentation(manifest);

            ModEntry clone = new ModEntry();
            clone.Manifest = manifest;
            clone.FolderPath = source.FolderPath;
            clone.ManifestPath = source.ManifestPath;
            clone.EntryPath = source.EntryPath;
            clone.EntryExists = source.EntryExists;
            clone.Status = source.Status;
            clone.LastModifiedUtc = source.LastModifiedUtc;
            clone.HasUpdate = source.HasUpdate;
            clone.IsPack = source.IsPack;
            clone.IsInstalled = source.IsInstalled;
            return clone;
        }

        private List<ModEntry> FetchCatalogMods()
        {
            List<ModEntry> results = new List<ModEntry>();
            string json = string.Empty;

            try
            {
                using (System.Net.WebClient client = new System.Net.WebClient())
                {
                    client.Encoding = Encoding.UTF8;
                    json = client.DownloadString(CatalogApiUrl);
                }
                AddActivity("Loaded remote catalog.");
            }
            catch (Exception ex)
            {
                AddActivity("Remote catalog unavailable: " + ex.Message);
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return results;
            }

            try
            {
                object raw = serializer.DeserializeObject(json);
                Dictionary<string, object> envelope = raw as Dictionary<string, object>;
                object entriesObject = envelope != null && envelope.ContainsKey("entries") ? envelope["entries"] : raw;
                object[] entries = entriesObject as object[];
                if (entries == null)
                {
                    return results;
                }

                for (int i = 0; i < entries.Length; i++)
                {
                    Dictionary<string, object> item = entries[i] as Dictionary<string, object>;
                    if (item == null)
                    {
                        continue;
                    }

                    ModManifest manifest = new ModManifest();
                    manifest.Id = GetString(item, "id");
                    manifest.Name = GetString(item, "name");
                    manifest.Version = GetString(item, "version");
                    manifest.Author = GetString(item, "author");
                    manifest.Description = GetString(item, "description");
                    manifest.Kind = string.IsNullOrEmpty(GetString(item, "kind")) ? "code" : GetString(item, "kind");
                    manifest.Category = string.IsNullOrEmpty(GetString(item, "category")) ? "General" : GetString(item, "category");
                    manifest.SourceUrl = GetString(item, "sourceUrl");
                    manifest.DownloadUrl = GetString(item, "downloadUrl");
                    manifest.TrustLevel = string.IsNullOrEmpty(GetString(item, "trustLevel")) ? "Unofficial" : GetString(item, "trustLevel");
                    manifest.TestingStatus = string.Empty;
                    manifest.LoaderVersion = string.IsNullOrEmpty(GetString(item, "loaderVersion")) ? "2.0.0" : GetString(item, "loaderVersion");
                    manifest.Enabled = "false";
                    manifest.Priority = "0";
                    if (string.IsNullOrEmpty(manifest.Id))
                    {
                        manifest.Id = SimpleManifest.MakeSafeId(manifest.Name);
                    }
                    if (string.Equals(manifest.TrustLevel, "Official", StringComparison.OrdinalIgnoreCase))
                    {
                        manifest.SourceUrl = string.Empty;
                    }

                    ModEntry entry = new ModEntry();
                    entry.Manifest = manifest;
                    entry.FolderPath = "catalog:" + manifest.Id;
                    entry.ManifestPath = string.Empty;
                    entry.EntryPath = manifest.DownloadUrl;
                    entry.EntryExists = false;
                    entry.IsInstalled = false;
                    entry.IsPack = string.Equals(manifest.Kind, "pack", StringComparison.OrdinalIgnoreCase);
                    entry.Status = string.Equals(manifest.TrustLevel, "Official", StringComparison.OrdinalIgnoreCase) ? "Official" : "Catalog";
                    entry.LastModifiedUtc = DateTime.UtcNow;
                    results.Add(entry);
                }
            }
            catch (Exception ex)
            {
                AddActivity("Catalog parse failed: " + ex.Message);
            }

            return results;
        }

        private bool LoaderInstalled()
        {
            return ModRepository.IsLoaderInstalled(currentGame);
        }

        private void InstallLoader()
        {
            if (currentGame == null)
            {
                AddActivity("Install skipped: game not detected.");
                PushState();
                return;
            }

            ModRepository.EnsureLoaderInstalled(currentGame);
            AddActivity("Loader installed to Managed.");
            PushState();
        }

        private void EnsureLoaderReady()
        {
            if (currentGame == null)
            {
                return;
            }

            try
            {
                ModRepository.EnsureLoaderInstalled(currentGame);
                AddActivity("Synced loader bootstrap into Aestik.");
            }
            catch (Exception ex)
            {
                AddActivity("Loader sync failed: " + ex.Message);
            }
        }

        private void LaunchModdedGame()
        {
            if (currentGame == null)
            {
                AddActivity("Launch skipped: game not detected.");
                PushState();
                return;
            }

            try
            {
                EnsureLoaderReady();
                ClearVanillaLaunchFlag();
                Process.Start(new ProcessStartInfo("steam://rungameid/" + currentGame.AppId) { UseShellExecute = true });
                AddActivity("Launching modded game through Steam.");
            }
            catch (Exception ex)
            {
                AddActivity("Launch failed: " + ex.Message);
            }

            PushState();
        }

        private void LaunchVanillaGame()
        {
            if (currentGame == null)
            {
                AddActivity("Vanilla launch skipped: game not detected.");
                PushState();
                return;
            }

            try
            {
                ModRepository.RestoreOriginalGameAssembly(currentGame);
                ArmVanillaLaunchFlag();
                Process.Start(new ProcessStartInfo("steam://rungameid/" + currentGame.AppId) { UseShellExecute = true });
                AddActivity("Launching vanilla game through Steam.");
            }
            catch (Exception ex)
            {
                AddActivity("Vanilla launch failed: " + ex.Message);
            }

            PushState();
        }

        private void ArmVanillaLaunchFlag()
        {
            string flagPath = AppPaths.GetVanillaLaunchFlagPath(currentGame);
            if (string.IsNullOrEmpty(flagPath))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(flagPath));
            File.WriteAllText(flagPath, "once");
        }

        private void ClearVanillaLaunchFlag()
        {
            string flagPath = AppPaths.GetVanillaLaunchFlagPath(currentGame);
            if (string.IsNullOrEmpty(flagPath))
            {
                return;
            }

            if (File.Exists(flagPath))
            {
                File.Delete(flagPath);
            }
        }

        private void ImportMod()
        {
            if (currentGame == null)
            {
                AddActivity("Import skipped: game not detected.");
                PushState();
                return;
            }

            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "Import mod";
            dialog.Filter = "Mod packages (*.dll;*.zip)|*.dll;*.zip|All files (*.*)|*.*";
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                ImportPreviewInfo preview = ModRepository.AnalyzeImportSource(currentGame, dialog.FileName, loadedMods, null);
                if (string.Equals(preview.SourceKind, ".zip", StringComparison.OrdinalIgnoreCase) &&
                    preview.ManifestCandidates.Count > 1 &&
                    string.IsNullOrWhiteSpace(preview.SelectedManifestRelativePath))
                {
                    string chosenManifest = PromptForManifestChoice(preview.ManifestCandidates);
                    if (string.IsNullOrWhiteSpace(chosenManifest))
                    {
                        return;
                    }

                    preview = ModRepository.AnalyzeImportSource(currentGame, dialog.FileName, loadedMods, chosenManifest);
                    preview.SelectedManifestRelativePath = preview.ManifestCandidates.FirstOrDefault(delegate(string item)
                    {
                        return string.Equals(item, chosenManifest, StringComparison.OrdinalIgnoreCase);
                    }) ?? chosenManifest;
                }
                AttachOptionalSourceUrl(preview);
                string summary = BuildImportSummary(preview);
                if (MessageBox.Show(this, summary, "Import preview", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes)
                {
                    return;
                }

                ModEntry imported = ModRepository.ImportModFile(currentGame, dialog.FileName, preview.SelectedManifestRelativePath);
                if (imported != null && imported.Manifest != null && !string.IsNullOrWhiteSpace(preview.Entry.Manifest.SourceUrl))
                {
                    imported.Manifest.SourceUrl = preview.Entry.Manifest.SourceUrl;
                    if (string.IsNullOrEmpty(imported.Manifest.TrustLevel))
                    {
                        imported.Manifest.TrustLevel = "Unofficial";
                    }
                    imported.Manifest.TestingStatus = string.Empty;
                    SimpleManifest.WriteModManifest(imported.ManifestPath, imported.Manifest);
                }
                AddActivity("Imported " + Path.GetFileName(dialog.FileName));
                RefreshMods();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AttachOptionalSourceUrl(ImportPreviewInfo preview)
        {
            if (preview == null || preview.Entry == null || preview.Entry.Manifest == null)
            {
                return;
            }

            string url = PromptForText("Optional Website URL", "Add a website URL for this mod if you have one.");
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            preview.Entry.Manifest.SourceUrl = url.Trim();
        }

        private string BuildImportSummary(ImportPreviewInfo preview)
        {
            if (preview == null || preview.Entry == null || preview.Entry.Manifest == null)
            {
                return "No mod metadata could be detected.\n\nContinue importing?";
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Name: " + preview.Entry.Manifest.Name);
            builder.AppendLine("Version: " + preview.Entry.Manifest.Version);
            builder.AppendLine("Author: " + (preview.Entry.Manifest.Author ?? "Unknown"));
            builder.AppendLine("Description: " + (preview.Entry.Manifest.Description ?? "No description"));
            builder.AppendLine("Trust: " + (preview.Entry.Manifest.TrustLevel ?? "Unofficial"));
            builder.AppendLine("Website: " + (!string.IsNullOrEmpty(preview.Entry.Manifest.SourceUrl) ? preview.Entry.Manifest.SourceUrl : "None provided"));
            builder.AppendLine("Kind: " + preview.Entry.Manifest.Kind);
            builder.AppendLine("Dependencies: " + (preview.DeclaredDependencies.Count > 0 ? string.Join(", ", preview.DeclaredDependencies.ToArray()) : "None declared"));
            if (preview.MissingDependencies.Count > 0)
            {
                builder.AppendLine("Missing: " + string.Join(", ", preview.MissingDependencies.ToArray()));
            }
            if (preview.Warnings.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Warnings:");
                for (int i = 0; i < preview.Warnings.Count; i++)
                {
                    builder.AppendLine("- " + preview.Warnings[i]);
                }
            }
            builder.AppendLine();
            builder.Append("Import this mod?");
            return builder.ToString();
        }

        private void RefreshMods()
        {
            if (currentGame == null)
            {
                return;
            }

            catalogMods = FetchCatalogMods();
            loadedMods = ModRepository.LoadMods(currentGame);
            loadedPacks = LoadPackFolders(currentGame);
            if (selectedMod != null)
            {
                selectedMod = loadedMods.FirstOrDefault(delegate(ModEntry mod) { return mod != null && mod.FolderPath == selectedMod.FolderPath; });
            }
            PushState();
        }

        private void InstallCatalogMod(string key)
        {
            ModEntry mod = FindCatalogMod(key);
            if (mod == null || mod.IsInstalled)
            {
                return;
            }

            if (currentGame == null)
            {
                AddActivity("Install skipped: game not detected.");
                PushState();
                return;
            }

            if (string.IsNullOrWhiteSpace(mod.Manifest.DownloadUrl))
            {
                if (!string.IsNullOrWhiteSpace(mod.Manifest.SourceUrl))
                {
                    OpenRepository(key);
                }
                return;
            }

            Uri baseUri = new Uri(CatalogApiUrl);
            Uri downloadUri = new Uri(baseUri, mod.Manifest.DownloadUrl);
            string fileName = Path.GetFileName(downloadUri.LocalPath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = SimpleManifest.MakeSafeId(mod.Manifest.Name);
            }

            string tempPath = Path.Combine(Path.GetTempPath(), fileName);
            try
            {
                installingModKey = mod.FolderPath;
                installProgressPercent = 0;
                installStatusText = "Preparing download";
                PushState();

                using (System.Net.WebClient client = new System.Net.WebClient())
                {
                    client.DownloadProgressChanged += delegate(object sender, System.Net.DownloadProgressChangedEventArgs args)
                    {
                        installProgressPercent = Math.Max(0, Math.Min(100, args.ProgressPercentage));
                        installStatusText = installProgressPercent >= 100
                            ? "Installing package"
                            : "Downloading " + installProgressPercent + "%";
                        PushState();
                    };

                    client.DownloadFile(downloadUri, tempPath);
                }

                tempPath = EnsureDownloadedPackageExtension(tempPath, mod);
                installProgressPercent = 100;
                installStatusText = "Packaging mod";
                PushState();

                ModEntry imported = ModRepository.ImportModFile(currentGame, tempPath, null);
                if (imported == null)
                {
                    throw new InvalidOperationException("The downloaded package did not contain a valid ADH mod payload.");
                }

                AddActivity("Installed " + mod.Manifest.Name + " from catalog.");
                installStatusText = "Installed";
                RefreshMods();
            }
            catch (Exception ex)
            {
                installStatusText = "Install failed";
                AddActivity("Catalog install failed: " + ex.Message);
                PushState();
                MessageBox.Show(this, ex.Message, "Catalog install failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                installingModKey = string.Empty;
                installProgressPercent = 0;
                installStatusText = string.Empty;
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch
                {
                }
            }
        }

        private static string EnsureDownloadedPackageExtension(string downloadedPath, ModEntry mod)
        {
            if (string.IsNullOrWhiteSpace(downloadedPath) || !File.Exists(downloadedPath))
            {
                return downloadedPath;
            }

            string ext = Path.GetExtension(downloadedPath);
            if (string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".dll", StringComparison.OrdinalIgnoreCase))
            {
                return downloadedPath;
            }

            string inferredExtension = InferPackageExtension(downloadedPath, mod);
            string renamedPath = downloadedPath + inferredExtension;
            if (File.Exists(renamedPath))
            {
                File.Delete(renamedPath);
            }

            File.Move(downloadedPath, renamedPath);
            return renamedPath;
        }

        private static string InferPackageExtension(string downloadedPath, ModEntry mod)
        {
            try
            {
                using (FileStream stream = File.OpenRead(downloadedPath))
                {
                    byte[] header = new byte[4];
                    int read = stream.Read(header, 0, header.Length);
                    if (read >= 4 &&
                        header[0] == 0x50 &&
                        header[1] == 0x4B &&
                        header[2] == 0x03 &&
                        header[3] == 0x04)
                    {
                        return ".zip";
                    }

                    if (read >= 2 &&
                        header[0] == 0x4D &&
                        header[1] == 0x5A)
                    {
                        return ".dll";
                    }
                }
            }
            catch
            {
            }

            if (mod != null &&
                mod.Manifest != null &&
                string.Equals(mod.Manifest.Kind, "pack", StringComparison.OrdinalIgnoreCase))
            {
                return ".zip";
            }

            return ".zip";
        }

        private ModEntry FindCatalogMod(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            return catalogMods.FirstOrDefault(delegate(ModEntry item)
            {
                return item != null && string.Equals(item.FolderPath, key, StringComparison.OrdinalIgnoreCase);
            });
        }

        private List<ModEntry> LoadPackFolders(GameInstallInfo game)
        {
            List<ModEntry> packs = new List<ModEntry>();
            string root = AppPaths.GetDefaultPacksRoot(game);
            if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
            {
                string[] folders = ModRepository.EnumerateCandidateFolders(root);
                for (int i = 0; i < folders.Length; i++)
                {
                    ModEntry entry = ModRepository.LoadSingleMod(folders[i]);
                    if (entry != null && string.Equals(entry.Manifest.Kind, "pack", StringComparison.OrdinalIgnoreCase))
                    {
                        packs.Add(entry);
                    }
                }
            }

            string modsRoot = AppPaths.GetDefaultModsRoot(game);
            if (!string.IsNullOrEmpty(modsRoot) && Directory.Exists(modsRoot))
            {
                string[] folders = ModRepository.EnumerateCandidateFolders(modsRoot);
                for (int i = 0; i < folders.Length; i++)
                {
                    ModEntry entry = ModRepository.LoadSingleMod(folders[i]);
                    if (entry != null && string.Equals(entry.Manifest.Kind, "pack", StringComparison.OrdinalIgnoreCase))
                    {
                        packs.Add(entry);
                    }
                }
            }

            return packs;
        }

        private void SetModEnabled(string key, bool enabled)
        {
            ModEntry mod = FindMod(key);
            if (mod == null || !mod.IsInstalled)
            {
                return;
            }

            mod.Manifest.IsEnabled = enabled;
            ModRepository.SetEnabled(mod, enabled);
            AddActivity((enabled ? "Enabled " : "Disabled ") + mod.Manifest.Name);
            RefreshMods();
        }

        private void RemoveMod(string key)
        {
            ModEntry mod = FindMod(key);
            if (mod == null)
            {
                return;
            }

            if (MessageBox.Show(this, "Remove " + mod.Manifest.Name + "?", "Confirm removal", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            ModRepository.RemoveMod(mod);
            AddActivity("Deleted " + mod.Manifest.Name);
            if (selectedMod != null && string.Equals(selectedMod.FolderPath, mod.FolderPath, StringComparison.OrdinalIgnoreCase))
            {
                selectedMod = null;
            }
            RefreshMods();
        }

        private ModEntry FindMod(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            ModEntry mod = loadedMods.FirstOrDefault(delegate(ModEntry item) { return item != null && string.Equals(item.FolderPath, key, StringComparison.OrdinalIgnoreCase); });
            if (mod != null)
            {
                return mod;
            }

            mod = loadedPacks.FirstOrDefault(delegate(ModEntry item) { return item != null && string.Equals(item.FolderPath, key, StringComparison.OrdinalIgnoreCase); });
            if (mod != null)
            {
                return mod;
            }

            return FindCatalogMod(key);
        }

        private void SelectMod(string key)
        {
            selectedMod = FindMod(key);
            PushState();
        }

        private void OpenModsFolder()
        {
            if (currentGame == null)
            {
                return;
            }

            string root = AppPaths.GetDefaultModsRoot(currentGame);
            Directory.CreateDirectory(root);
            Process.Start("explorer.exe", root);
        }

        private void OpenModFolder(string key)
        {
            ModEntry mod = FindMod(key);
            if (mod == null || string.IsNullOrEmpty(mod.FolderPath))
            {
                OpenModsFolder();
                return;
            }

            Process.Start("explorer.exe", mod.FolderPath);
        }

        private void OpenPacksFolder()
        {
            if (currentGame == null)
            {
                return;
            }

            string root = AppPaths.GetDefaultPacksRoot(currentGame);
            Directory.CreateDirectory(root);
            Process.Start("explorer.exe", root);
        }

        private void OpenPackFolder(string key)
        {
            ModEntry mod = FindMod(key);
            if (mod == null || string.IsNullOrEmpty(mod.FolderPath))
            {
                OpenPacksFolder();
                return;
            }

            Process.Start("explorer.exe", mod.FolderPath);
        }

        private void OpenAppData()
        {
            string root = AppPaths.GetUserDataRoot();
            Directory.CreateDirectory(root);
            Process.Start("explorer.exe", root);
        }

        private void OpenRepository(string key)
        {
            ModEntry mod = FindMod(key);
            if (mod == null || mod.Manifest == null || string.IsNullOrWhiteSpace(mod.Manifest.SourceUrl))
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(mod.Manifest.SourceUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Repository open failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenUploadPortal()
        {
            OpenUploadPortal(string.Empty);
        }

        private void OpenUploadPortal(string mode)
        {
            string url = UploadPortalUrl;
            if (!string.IsNullOrWhiteSpace(mode))
            {
                url += "?mode=" + Uri.EscapeDataString(mode);
            }

            UploadPortalForm portal = new UploadPortalForm(url);
            portal.Show(this);
        }

        private void OpenGameFolder()
        {
            if (currentGame == null || string.IsNullOrEmpty(currentGame.GameRoot))
            {
                return;
            }

            Process.Start("explorer.exe", currentGame.GameRoot);
        }

        private string PromptForText(string title, string description)
        {
            Form prompt = new Form();
            prompt.Text = title;
            prompt.StartPosition = FormStartPosition.CenterParent;
            prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
            prompt.MinimizeBox = false;
            prompt.MaximizeBox = false;
            prompt.ClientSize = new Size(520, 160);
            prompt.BackColor = Color.FromArgb(18, 24, 39);
            prompt.ForeColor = Color.White;

            Label label = new Label();
            label.Text = description;
            label.Location = new Point(16, 16);
            label.Size = new Size(488, 36);

            TextBox textBox = new TextBox();
            textBox.Location = new Point(16, 62);
            textBox.Size = new Size(488, 28);

            Button ok = new Button();
            ok.Text = "OK";
            ok.DialogResult = DialogResult.OK;
            ok.Location = new Point(348, 108);
            ok.Size = new Size(74, 30);

            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(430, 108);
            cancel.Size = new Size(74, 30);

            prompt.Controls.Add(label);
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(ok);
            prompt.Controls.Add(cancel);
            prompt.AcceptButton = ok;
            prompt.CancelButton = cancel;

            return prompt.ShowDialog(this) == DialogResult.OK ? textBox.Text.Trim() : string.Empty;
        }

        private string PromptForManifestChoice(IList<string> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return string.Empty;
            }

            Form prompt = new Form();
            prompt.Text = "Choose Manifest";
            prompt.StartPosition = FormStartPosition.CenterParent;
            prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
            prompt.MinimizeBox = false;
            prompt.MaximizeBox = false;
            prompt.ClientSize = new Size(620, 280);
            prompt.BackColor = Color.FromArgb(18, 24, 39);
            prompt.ForeColor = Color.White;

            Label label = new Label();
            label.Text = "This zip contains multiple possible manifest files. Choose the one ADH should use.";
            label.Location = new Point(16, 16);
            label.Size = new Size(588, 34);

            ListBox list = new ListBox();
            list.Location = new Point(16, 58);
            list.Size = new Size(588, 156);
            list.BackColor = Color.FromArgb(10, 16, 28);
            list.ForeColor = Color.White;
            for (int i = 0; i < candidates.Count; i++)
            {
                list.Items.Add(candidates[i]);
            }
            if (list.Items.Count > 0)
            {
                list.SelectedIndex = 0;
            }

            Button ok = new Button();
            ok.Text = "Use Manifest";
            ok.DialogResult = DialogResult.OK;
            ok.Location = new Point(434, 232);
            ok.Size = new Size(82, 30);

            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(522, 232);
            cancel.Size = new Size(82, 30);

            prompt.Controls.Add(label);
            prompt.Controls.Add(list);
            prompt.Controls.Add(ok);
            prompt.Controls.Add(cancel);
            prompt.AcceptButton = ok;
            prompt.CancelButton = cancel;

            return prompt.ShowDialog(this) == DialogResult.OK && list.SelectedItem != null
                ? Convert.ToString(list.SelectedItem)
                : string.Empty;
        }

        private void ApplyDarkTitleBar()
        {
            if (Environment.OSVersion.Version.Major < 10)
            {
                return;
            }

            try
            {
                int enabled = 1;
                DwmSetWindowAttribute(Handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));
            }
            catch
            {
            }
        }

        private void ToggleMaximize()
        {
            WindowState = WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal
                : FormWindowState.Maximized;
        }

        private static Button CreateTitleButton()
        {
            Button button = new Button();
            button.Size = new Size(46, 57);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.TabStop = false;
            button.Margin = Padding.Empty;
            button.Text = string.Empty;
            button.ForeColor = Color.White;
            button.BackColor = Color.Transparent;
            button.UseVisualStyleBackColor = false;
            button.Cursor = Cursors.Default;
            return button;
        }

        private static void WireTitleButtonHover(Button button, Color hoverColor, Color hoverTextColor)
        {
            Color baseColor = Color.FromArgb(18, 20, 37);
            Color baseText = Color.FromArgb(244, 247, 255);
            button.MouseEnter += delegate
            {
                button.BackColor = hoverColor;
                button.ForeColor = hoverTextColor;
            };
            button.MouseLeave += delegate
            {
                button.BackColor = baseColor;
                button.ForeColor = baseText;
            };
            button.MouseDown += delegate
            {
                button.BackColor = ControlPaint.Dark(hoverColor, 0.08f);
                button.ForeColor = hoverTextColor;
            };
            button.MouseUp += delegate
            {
                button.BackColor = hoverColor;
                button.ForeColor = hoverTextColor;
            };
            button.BackColor = baseColor;
            button.ForeColor = baseText;
        }

        private void TitleButtonPaint(object sender, PaintEventArgs e)
        {
            Button button = sender as Button;
            if (button == null)
            {
                return;
            }

            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (Pen pen = new Pen(button.ForeColor, 1.25f))
            {
                pen.Alignment = System.Drawing.Drawing2D.PenAlignment.Center;

                if (button == minimizeButton)
                {
                    int y = (button.Height / 2) + 4;
                    e.Graphics.DrawLine(pen, 18, y, button.Width - 18, y);
                    return;
                }

                if (button == maximizeButton)
                {
                    Rectangle rect = new Rectangle((button.Width - 10) / 2, (button.Height - 10) / 2 + 1, 10, 10);
                    e.Graphics.DrawRectangle(pen, rect);
                    return;
                }

                if (button == closeButton)
                {
                    int left = (button.Width - 10) / 2;
                    int top = (button.Height - 10) / 2 + 1;
                    e.Graphics.DrawLine(pen, left, top, left + 10, top + 10);
                    e.Graphics.DrawLine(pen, left + 10, top, left, top + 10);
                }
            }
        }

        private void OpenConsoleWindow()
        {
            try
            {
                Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--console")
                {
                    UseShellExecute = true,
                    WorkingDirectory = AppPaths.GetAppRoot()
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Console launch failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ClearRuntimeLog()
        {
            if (currentGame == null)
            {
                return;
            }

            try
            {
                string runtimeLog = Path.Combine(currentGame.GameRoot, "Aestik_Data", "ModLoader", "logs", "runtime.log");
                if (File.Exists(runtimeLog))
                {
                    File.Delete(runtimeLog);
                }
                AddActivity("Runtime log cleared.");
            }
            catch (Exception ex)
            {
                AddActivity("Failed to clear runtime log: " + ex.Message);
            }

            PushState();
        }

        private void AddActivity(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            activity.Add(DateTime.Now.ToString("HH:mm:ss") + " " + text);
            while (activity.Count > 60)
            {
                activity.RemoveAt(0);
            }
        }

        private static string GetString(Dictionary<string, object> payload, string key)
        {
            object value;
            if (payload != null && payload.TryGetValue(key, out value) && value != null)
            {
                return Convert.ToString(value);
            }

            return null;
        }

        private static void NormalizeManifestPresentation(ModManifest manifest)
        {
            if (manifest == null)
            {
                return;
            }

            manifest.TestingStatus = string.Empty;
            if (IsAdhFirstPartyManifest(manifest))
            {
                manifest.TrustLevel = "Official";
                manifest.SourceUrl = string.Empty;
            }
            else if (string.IsNullOrWhiteSpace(manifest.TrustLevel))
            {
                manifest.TrustLevel = "Unofficial";
            }
        }

        private static bool IsAdhFirstPartyManifest(ModManifest manifest)
        {
            string author = manifest.Author ?? string.Empty;
            string id = manifest.Id ?? string.Empty;
            string name = manifest.Name ?? string.Empty;
            return author.IndexOf("ADH", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   author.IndexOf("Tom Creations", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   id.IndexOf("adh", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("ADH", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void SyncBackgroundAsset()
        {
            string sourcePath = GetBackgroundImagePath();
            if (string.IsNullOrEmpty(sourcePath))
            {
                return;
            }

            string targetPath = Path.Combine(AppPaths.GetAppRoot(), BackgroundAssetName);
            try
            {
                File.Copy(sourcePath, targetPath, true);
            }
            catch
            {
            }
        }

        private static string GetBackgroundImagePath()
        {
            string downloadsRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            string renamedPath = Path.Combine(downloadsRoot, "ADH-Background.png");
            if (File.Exists(renamedPath))
            {
                return renamedPath;
            }

            string legacyPath = Path.Combine(downloadsRoot, "ADH Background.png");
            return File.Exists(legacyPath) ? legacyPath : string.Empty;
        }

        private static Size GetBackgroundImageSize()
        {
            string path = GetBackgroundImagePath();
            if (string.IsNullOrEmpty(path))
            {
                return Size.Empty;
            }

            try
            {
                using (Image image = Image.FromFile(path))
                {
                    return image.Size;
                }
            }
            catch
            {
                return Size.Empty;
            }
        }

        private void ApplyBrandIcon()
        {
            string iconPath = Path.Combine(AppPaths.GetAppRoot(), LogoAssetName);
            if (!File.Exists(iconPath))
            {
                return;
            }

            try
            {
                using (Icon icon = new Icon(iconPath))
                {
                    Icon = (Icon)icon.Clone();
                }
            }
            catch
            {
            }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);

        private static bool GetBool(Dictionary<string, object> payload, string key)
        {
            object value;
            if (payload != null && payload.TryGetValue(key, out value) && value != null)
            {
                if (value is bool)
                {
                    return (bool)value;
                }

                bool parsed;
                if (bool.TryParse(Convert.ToString(value), out parsed))
                {
                    return parsed;
                }
            }

            return false;
        }

        private sealed class LauncherStateDto
        {
            public string title { get; set; }
            public string status { get; set; }
            public bool gameDetected { get; set; }
            public string gameRoot { get; set; }
            public string steamState { get; set; }
            public bool loaderInstalled { get; set; }
            public int modsCount { get; set; }
            public int catalogCount { get; set; }
            public int packsCount { get; set; }
            public bool autoSearch { get; set; }
            public bool rememberWindow { get; set; }
            public string selectedKey { get; set; }
            public List<string> activity { get; set; }
            public List<LauncherModDto> mods { get; set; }
            public List<LauncherModDto> packs { get; set; }
        }

        private sealed class LauncherModDto
        {
            public string key { get; set; }
            public string id { get; set; }
            public string name { get; set; }
            public string version { get; set; }
            public string author { get; set; }
            public string description { get; set; }
            public string kind { get; set; }
            public string category { get; set; }
            public bool enabled { get; set; }
            public bool entryExists { get; set; }
            public string status { get; set; }
            public bool hasUpdate { get; set; }
            public string loaderVersion { get; set; }
            public string entryPath { get; set; }
            public string lastModifiedUtc { get; set; }
            public string sourceUrl { get; set; }
            public string downloadUrl { get; set; }
            public string trustLevel { get; set; }
            public string testingStatus { get; set; }
            public bool installed { get; set; }
            public bool installing { get; set; }
            public int installProgressPercent { get; set; }
            public string installStatusText { get; set; }
            public List<string> depends { get; set; }
            public List<string> after { get; set; }
            public List<string> before { get; set; }
        }

        private sealed class UploadPortalForm : Form
        {
            private readonly string url;
            private readonly WebView2 browser;

            public UploadPortalForm(string url)
            {
                this.url = url;
                Text = "ADH Mod Upload";
                StartPosition = FormStartPosition.CenterParent;
                Size = new Size(980, 760);
                MinimumSize = new Size(840, 620);
                BackColor = Color.FromArgb(10, 14, 22);
                browser = new WebView2();
                browser.Dock = DockStyle.Fill;
                browser.DefaultBackgroundColor = Color.FromArgb(10, 14, 22);
                Controls.Add(browser);
                Load += UploadPortalForm_Load;
            }

            private async void UploadPortalForm_Load(object sender, EventArgs e)
            {
                string environmentFolder = Path.Combine(AppPaths.GetUserDataRoot(), "webview2-upload");
                Directory.CreateDirectory(environmentFolder);
                CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, environmentFolder);
                await browser.EnsureCoreWebView2Async(env);
                browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                browser.CoreWebView2.Settings.IsStatusBarEnabled = false;
                browser.CoreWebView2.Navigate(url);
            }
        }
    }
}
