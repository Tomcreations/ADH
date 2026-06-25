using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using AestikModLoader.Common;

namespace AestikModLoader.App
{
    public enum ModFilterMode
    {
        All,
        Installed,
        Enabled,
        OutOfDate,
        WhatsNew
    }

    public sealed class MainForm : Form
    {
        private const int ModsPageSize = 24;
        private readonly LauncherSettings settings;
        private GameInstallInfo currentGame;
        private DiscoveryResult lastDiscovery;

        private readonly BufferedPanel bannerPanel;
        private readonly BufferedPanel tabPanel;
        private readonly BufferedPanel contentPanel;
        private readonly BufferedPanel infoPage;
        private readonly BufferedPanel modsPage;
        private readonly BufferedPanel packsPage;
        private readonly BufferedPanel settingsPage;
        private BufferedPanel modsDetailPanel;
        private BufferedFlowLayoutPanel modsList;
        private BufferedFlowLayoutPanel packsList;
        private TextBox searchBox;
        private Label statusLabel;
        private Label titleLabel;
        private Label pathValueLabel;
        private Label installStateLabel;
        private Label modsCountLabel;
        private Label packsCountLabel;
        private Label steamStateLabel;
        private ToggleSwitch autoSearchSwitch;
        private ToggleSwitch rememberWindowSwitch;
        private Button detectButton;
        private Button activeSearchButton;
        private Button installButton;
        private Button launchModdedButton;
        private Button launchSteamButton;
        private Button importModButton;
        private Button refreshModsButton;
        private Button openModsFolderButton;
        private Button openPacksFolderButton;
        private Button modsPrevButton;
        private Button modsNextButton;
        private Label modsPageLabel;
        private Label modsCountValueLabel;
        private Label modsFilteredCountLabel;
        private MenuStrip menuStrip;
        private ModFilterMode activeModFilter;

        private readonly Button infoTab;
        private readonly Button modsTab;
        private readonly Button packsTab;
        private readonly Button settingsTab;

        private List<ModEntry> loadedMods;
        private List<ModEntry> loadedPacks;
        private List<ModEntry> displayedMods;
        private ModEntry selectedMod;
        private int discoveryToken;
        private int modsPageIndex;
        private FileSystemWatcher modsWatcher;
        private FileSystemWatcher packsWatcher;
        private System.Windows.Forms.Timer refreshTimer;

        public MainForm()
        {
            settings = LauncherSettings.Load();
            loadedMods = new List<ModEntry>();
            loadedPacks = new List<ModEntry>();

            BackColor = UiStyle.Background;
            ForeColor = UiStyle.Text;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(Math.Max(1160, settings.Width), Math.Max(760, settings.Height));
            Text = "ADH";
            MinimumSize = new Size(1100, 720);

            bannerPanel = new BufferedPanel();
            bannerPanel.Dock = DockStyle.Top;
            bannerPanel.Height = 110;
            bannerPanel.Paint += BannerPanel_Paint;
            Controls.Add(bannerPanel);

            titleLabel = new Label();
            titleLabel.Text = "ADH";
            titleLabel.Font = new Font("Segoe UI Semibold", 21f, FontStyle.Bold);
            titleLabel.AutoSize = true;
            titleLabel.Location = new Point(26, 20);
            titleLabel.ForeColor = UiStyle.Text;
            bannerPanel.Controls.Add(titleLabel);

            Label subtitleLabel = new Label();
            subtitleLabel.Text = "Lumafly-style mod manager for the Steam install of Aestik";
            subtitleLabel.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
            subtitleLabel.AutoSize = true;
            subtitleLabel.Location = new Point(28, 56);
            subtitleLabel.ForeColor = UiStyle.SubText;
            bannerPanel.Controls.Add(subtitleLabel);

            statusLabel = new Label();
            statusLabel.AutoSize = false;
            statusLabel.TextAlign = ContentAlignment.MiddleRight;
            statusLabel.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
            statusLabel.ForeColor = UiStyle.SubText;
            statusLabel.Location = new Point(760, 20);
            statusLabel.Size = new Size(360, 24);
            statusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            statusLabel.Text = "Detecting game...";
            bannerPanel.Controls.Add(statusLabel);

            Label bannerHint = new Label();
            bannerHint.Text = "Import mods, manage packs, and launch straight from a single launcher.";
            bannerHint.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            bannerHint.AutoSize = true;
            bannerHint.Location = new Point(760, 53);
            bannerHint.ForeColor = UiStyle.SubText;
            bannerHint.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            bannerPanel.Controls.Add(bannerHint);

            menuStrip = new MenuStrip();
            menuStrip.Dock = DockStyle.Top;
            menuStrip.BackColor = UiStyle.Surface;
            menuStrip.ForeColor = UiStyle.Text;
            menuStrip.RenderMode = ToolStripRenderMode.System;
            ToolStripMenuItem fileMenu = new ToolStripMenuItem("File");
            ToolStripMenuItem openConsoleMenu = new ToolStripMenuItem("Open Console");
            ToolStripMenuItem launchMenu = new ToolStripMenuItem("Launch Game");
            ToolStripMenuItem exitMenu = new ToolStripMenuItem("Exit");
            openConsoleMenu.Click += delegate { OpenConsoleWindow(); };
            launchMenu.Click += delegate { LaunchGame(false); };
            exitMenu.Click += delegate { Close(); };
            fileMenu.DropDownItems.Add(openConsoleMenu);
            fileMenu.DropDownItems.Add(launchMenu);
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(exitMenu);
            ToolStripMenuItem toolsMenu = new ToolStripMenuItem("Tools");
            ToolStripMenuItem detectMenu = new ToolStripMenuItem("Redetect Game");
            ToolStripMenuItem importMenu = new ToolStripMenuItem("Import Mod");
            detectMenu.Click += delegate { BeginDiscovery(true); };
            importMenu.Click += delegate { ImportMod(); };
            toolsMenu.DropDownItems.Add(detectMenu);
            toolsMenu.DropDownItems.Add(importMenu);
            menuStrip.Items.Add(fileMenu);
            menuStrip.Items.Add(toolsMenu);
            Controls.Add(menuStrip);
            MainMenuStrip = menuStrip;

            tabPanel = new BufferedPanel();
            tabPanel.Dock = DockStyle.Top;
            tabPanel.Height = 52;
            tabPanel.Padding = new Padding(22, 10, 22, 10);
            tabPanel.BackColor = UiStyle.Background;
            Controls.Add(tabPanel);

            FlowLayoutPanel tabFlow = new FlowLayoutPanel();
            tabFlow.Dock = DockStyle.Fill;
            tabFlow.FlowDirection = FlowDirection.LeftToRight;
            tabFlow.WrapContents = false;
            tabFlow.BackColor = Color.Transparent;
            tabFlow.Padding = new Padding(0);
            tabPanel.Controls.Add(tabFlow);

            infoTab = UiFactory.CreateTabButton("Info");
            modsTab = UiFactory.CreateTabButton("Mods");
            packsTab = UiFactory.CreateTabButton("Packs");
            settingsTab = UiFactory.CreateTabButton("Settings");
            tabFlow.Controls.Add(infoTab);
            tabFlow.Controls.Add(modsTab);
            tabFlow.Controls.Add(packsTab);
            tabFlow.Controls.Add(settingsTab);
            infoTab.Click += delegate { ShowPage(infoPage); };
            modsTab.Click += delegate { ShowPage(modsPage); };
            packsTab.Click += delegate { ShowPage(packsPage); };
            settingsTab.Click += delegate { ShowPage(settingsPage); };

            contentPanel = new BufferedPanel();
            contentPanel.Dock = DockStyle.Fill;
            contentPanel.Padding = new Padding(22, 0, 22, 22);
            Controls.Add(contentPanel);

            infoPage = BuildInfoPage();
            modsPage = BuildModsPage();
            packsPage = BuildPacksPage();
            settingsPage = BuildSettingsPage();

            contentPanel.Controls.Add(infoPage);
            contentPanel.Controls.Add(modsPage);
            contentPanel.Controls.Add(packsPage);
            contentPanel.Controls.Add(settingsPage);

            infoPage.Visible = true;
            modsPage.Visible = false;
            packsPage.Visible = false;
            settingsPage.Visible = false;

            refreshTimer = new System.Windows.Forms.Timer();
            refreshTimer.Interval = 450;
            refreshTimer.Tick += delegate
            {
                refreshTimer.Stop();
                RefreshMods();
            };

            Load += MainForm_Load;
            FormClosing += MainForm_FormClosing;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            autoSearchSwitch.Checked = settings.AutoSearch;
            rememberWindowSwitch.Checked = settings.RememberWindow;

            if (settings.RememberWindow)
            {
                if (settings.Width > 0 && settings.Height > 0)
                {
                    Size = new Size(Math.Max(1100, settings.Width), Math.Max(720, settings.Height));
                }
            }

            BeginDiscovery(false);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            StopWatching();
            settings.AutoSearch = autoSearchSwitch.Checked;
            settings.RememberWindow = rememberWindowSwitch.Checked;
            settings.Width = Width;
            settings.Height = Height;
            if (currentGame != null)
            {
                settings.LastGameRoot = currentGame.GameRoot;
            }
            settings.Save();
        }

        private void BannerPanel_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
            Rectangle rect = bannerPanel.ClientRectangle;
            using (LinearGradientBrush brush = new LinearGradientBrush(rect, Color.FromArgb(12, 20, 40), Color.FromArgb(6, 10, 20), 0f))
            {
                e.Graphics.FillRectangle(brush, rect);
            }

            using (SolidBrush glow = new SolidBrush(Color.FromArgb(42, 103, 156, 255)))
            {
                e.Graphics.FillEllipse(glow, -60, -55, 220, 220);
            }

            using (Pen pen = new Pen(Color.FromArgb(45, UiStyle.Border)))
            {
                e.Graphics.DrawLine(pen, 0, rect.Bottom - 1, rect.Right, rect.Bottom - 1);
            }
        }

        private void InfoHero_Paint(object sender, PaintEventArgs e)
        {
            Control control = sender as Control;
            if (control == null)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
            Rectangle rect = control.ClientRectangle;
            using (LinearGradientBrush brush = new LinearGradientBrush(rect, Color.FromArgb(15, 21, 38), Color.FromArgb(8, 11, 19), 0f))
            {
                e.Graphics.FillRectangle(brush, rect);
            }

            using (SolidBrush glow = new SolidBrush(Color.FromArgb(34, UiStyle.Accent.R, UiStyle.Accent.G, UiStyle.Accent.B)))
            {
                e.Graphics.FillEllipse(glow, rect.Width - 290, -60, 240, 240);
            }

            using (SolidBrush glow2 = new SolidBrush(Color.FromArgb(18, 122, 180, 255)))
            {
                e.Graphics.FillEllipse(glow2, rect.Width - 490, 108, 180, 180);
            }

            using (Pen pen = new Pen(UiStyle.Border))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, rect.Width - 1, rect.Height - 1);
            }
        }

        private BufferedPanel BuildInfoPage()
        {
            BufferedPanel page = CreatePage();

            BufferedPanel heroCard = CreateCardPanel();
            heroCard.Location = new Point(0, 0);
            heroCard.Size = new Size(1180, 258);
            heroCard.Paint += InfoHero_Paint;
            page.Controls.Add(heroCard);

            Label heroTitle = new Label();
            heroTitle.Text = "ADH";
            heroTitle.ForeColor = UiStyle.Text;
            heroTitle.Font = new Font("Segoe UI Semibold", 26f, FontStyle.Bold);
            heroTitle.AutoSize = true;
            heroTitle.Location = new Point(24, 18);
            heroCard.Controls.Add(heroTitle);

            Label heroVersion = UiFactory.CreateCaption("Lumafly-style launcher for Aestik");
            heroVersion.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
            heroVersion.Location = new Point(26, 56);
            heroCard.Controls.Add(heroVersion);

            Label heroBlurb = new Label();
            heroBlurb.Text = "Import, install, enable, and launch large mod collections with dependency-aware support and Steam-aware discovery.";
            heroBlurb.ForeColor = UiStyle.Text;
            heroBlurb.Font = new Font("Segoe UI", 10.25f, FontStyle.Regular);
            heroBlurb.Location = new Point(24, 88);
            heroBlurb.Size = new Size(560, 52);
            heroCard.Controls.Add(heroBlurb);

            installStateLabel = new Label();
            installStateLabel.Text = "Waiting for detection";
            installStateLabel.ForeColor = UiStyle.SubText;
            installStateLabel.Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold);
            installStateLabel.Location = new Point(24, 146);
            installStateLabel.AutoSize = true;
            heroCard.Controls.Add(installStateLabel);

            pathValueLabel = new Label();
            pathValueLabel.Text = "Aestik not detected yet";
            pathValueLabel.ForeColor = UiStyle.SubText;
            pathValueLabel.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            pathValueLabel.Location = new Point(24, 174);
            pathValueLabel.Size = new Size(550, 40);
            pathValueLabel.AutoEllipsis = true;
            heroCard.Controls.Add(pathValueLabel);

            detectButton = UiFactory.CreateActionButton("Detect Game", false);
            activeSearchButton = UiFactory.CreateActionButton("Active Search", false);
            installButton = UiFactory.CreateActionButton("Install Loader", true);
            launchModdedButton = UiFactory.CreateActionButton("Launch Modded", true);
            launchSteamButton = UiFactory.CreateActionButton("Launch via Steam", false);
            detectButton.Location = new Point(24, 218);
            activeSearchButton.Location = new Point(146, 218);
            installButton.Location = new Point(270, 218);
            launchModdedButton.Location = new Point(402, 218);
            launchSteamButton.Location = new Point(544, 218);
            heroCard.Controls.Add(detectButton);
            heroCard.Controls.Add(activeSearchButton);
            heroCard.Controls.Add(installButton);
            heroCard.Controls.Add(launchModdedButton);
            heroCard.Controls.Add(launchSteamButton);

            detectButton.Click += delegate { BeginDiscovery(false); };
            activeSearchButton.Click += delegate { BeginDiscovery(true); };
            installButton.Click += delegate { InstallLoader(); };
            launchModdedButton.Click += delegate { LaunchGame(false); };
            launchSteamButton.Click += delegate { LaunchGame(true); };

            BufferedPanel leftCard = CreateCardPanel();
            leftCard.Location = new Point(0, 278);
            leftCard.Size = new Size(560, 214);
            page.Controls.Add(leftCard);

            Label leftTitle = CreateSectionTitle("Install");
            leftTitle.Location = new Point(22, 18);
            leftCard.Controls.Add(leftTitle);

            Label leftCaption = UiFactory.CreateCaption("Steam-detected game location and loader state.");
            leftCaption.Location = new Point(22, 48);
            leftCard.Controls.Add(leftCaption);

            Label gameLabel = UiFactory.CreateCaption("Game root");
            gameLabel.Location = new Point(22, 82);
            leftCard.Controls.Add(gameLabel);

            Label stateLabel = UiFactory.CreateCaption("Loader status");
            stateLabel.Location = new Point(22, 142);
            leftCard.Controls.Add(stateLabel);

            steamStateLabel = new Label();
            steamStateLabel.Text = "Not detected";
            steamStateLabel.ForeColor = UiStyle.Text;
            steamStateLabel.Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold);
            steamStateLabel.AutoSize = true;
            steamStateLabel.Location = new Point(22, 164);
            leftCard.Controls.Add(steamStateLabel);

            BufferedPanel statsCard = CreateCardPanel();
            statsCard.Location = new Point(580, 278);
            statsCard.Size = new Size(600, 214);
            page.Controls.Add(statsCard);

            Label statsTitle = CreateSectionTitle("Support");
            statsTitle.Location = new Point(22, 18);
            statsCard.Controls.Add(statsTitle);

            Label statsCaption = UiFactory.CreateCaption("Compatibility helpers and live mod counts.");
            statsCaption.Location = new Point(22, 48);
            statsCard.Controls.Add(statsCaption);

            modsCountLabel = CreateMetricCard(statsCard, "Mods", "0", 22, 84);
            packsCountLabel = CreateMetricCard(statsCard, "Packs", "0", 194, 84);
            CreateMetricCard(statsCard, "Steam", "Ready", 366, 84);

            Label supportText = new Label();
            supportText.Text = "Large mod lists, dependency order, pack discovery, shared DLL resolution, and importer previews are all handled in the same loader path.";
            supportText.ForeColor = UiStyle.Text;
            supportText.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            supportText.Location = new Point(22, 188);
            supportText.Size = new Size(548, 44);
            statsCard.Controls.Add(supportText);

            return page;
        }

        private BufferedPanel BuildModsPage()
        {
            BufferedPanel page = CreatePage();

            BufferedPanel toolbar = CreateCardPanel();
            toolbar.Location = new Point(0, 0);
            toolbar.Size = new Size(1180, 126);
            page.Controls.Add(toolbar);

            Label header = CreateSectionTitle("Mods");
            header.Location = new Point(22, 16);
            toolbar.Controls.Add(header);

            Label headerHint = UiFactory.CreateCaption("Search, filter, and manage large mod libraries without losing responsiveness.");
            headerHint.Location = new Point(22, 46);
            toolbar.Controls.Add(headerHint);

            searchBox = UiFactory.CreateSearchBox();
            searchBox.Location = new Point(22, 72);
            searchBox.Width = 300;
            searchBox.TextChanged += delegate { RenderMods(); };
            toolbar.Controls.Add(searchBox);

            importModButton = UiFactory.CreateActionButton("Import Mod", true);
            refreshModsButton = UiFactory.CreateActionButton("Refresh", false);
            openModsFolderButton = UiFactory.CreateActionButton("Open Mods Folder", false);
            importModButton.Location = new Point(344, 70);
            refreshModsButton.Location = new Point(474, 70);
            openModsFolderButton.Location = new Point(572, 70);
            toolbar.Controls.Add(importModButton);
            toolbar.Controls.Add(refreshModsButton);
            toolbar.Controls.Add(openModsFolderButton);

            importModButton.Click += delegate { ImportMod(); };
            refreshModsButton.Click += delegate { RefreshMods(); };
            openModsFolderButton.Click += delegate { OpenModsFolder(); };

            FlowLayoutPanel filterBar = new FlowLayoutPanel();
            filterBar.Location = new Point(22, 102);
            filterBar.Size = new Size(740, 24);
            filterBar.FlowDirection = FlowDirection.LeftToRight;
            filterBar.WrapContents = false;
            filterBar.BackColor = Color.Transparent;
            toolbar.Controls.Add(filterBar);

            Button allFilter = CreateFilterButton("All", ModFilterMode.All);
            Button installedFilter = CreateFilterButton("Installed", ModFilterMode.Installed);
            Button enabledFilter = CreateFilterButton("Enabled", ModFilterMode.Enabled);
            Button outOfDateFilter = CreateFilterButton("Out of date", ModFilterMode.OutOfDate);
            Button whatsNewFilter = CreateFilterButton("What's New", ModFilterMode.WhatsNew);
            filterBar.Controls.Add(allFilter);
            filterBar.Controls.Add(installedFilter);
            filterBar.Controls.Add(enabledFilter);
            filterBar.Controls.Add(outOfDateFilter);
            filterBar.Controls.Add(whatsNewFilter);

            modsCountValueLabel = UiFactory.CreateCaption("0 mods");
            modsCountValueLabel.Location = new Point(940, 20);
            modsCountValueLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            toolbar.Controls.Add(modsCountValueLabel);

            modsFilteredCountLabel = UiFactory.CreateCaption("0 shown");
            modsFilteredCountLabel.Location = new Point(940, 42);
            modsFilteredCountLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            toolbar.Controls.Add(modsFilteredCountLabel);

            modsPageLabel = UiFactory.CreateCaption("Page 1");
            modsPageLabel.Location = new Point(940, 64);
            modsPageLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            toolbar.Controls.Add(modsPageLabel);

            modsPrevButton = UiFactory.CreateActionButton("Prev", false);
            modsNextButton = UiFactory.CreateActionButton("Next", false);
            modsPrevButton.Size = new Size(70, 30);
            modsNextButton.Size = new Size(70, 30);
            modsPrevButton.Location = new Point(1032, 66);
            modsNextButton.Location = new Point(1110, 66);
            toolbar.Controls.Add(modsPrevButton);
            toolbar.Controls.Add(modsNextButton);
            modsPrevButton.Click += delegate
            {
                if (modsPageIndex > 0)
                {
                    modsPageIndex--;
                    RenderMods();
                }
            };
            modsNextButton.Click += delegate
            {
                if (displayedMods != null && (modsPageIndex + 1) * ModsPageSize < displayedMods.Count)
                {
                    modsPageIndex++;
                    RenderMods();
                }
            };

            allFilter.Click += delegate { activeModFilter = ModFilterMode.All; modsPageIndex = 0; UpdateFilterButtons(filterBar, allFilter); RenderMods(); };
            installedFilter.Click += delegate { activeModFilter = ModFilterMode.Installed; modsPageIndex = 0; UpdateFilterButtons(filterBar, installedFilter); RenderMods(); };
            enabledFilter.Click += delegate { activeModFilter = ModFilterMode.Enabled; modsPageIndex = 0; UpdateFilterButtons(filterBar, enabledFilter); RenderMods(); };
            outOfDateFilter.Click += delegate { activeModFilter = ModFilterMode.OutOfDate; modsPageIndex = 0; UpdateFilterButtons(filterBar, outOfDateFilter); RenderMods(); };
            whatsNewFilter.Click += delegate { activeModFilter = ModFilterMode.WhatsNew; modsPageIndex = 0; UpdateFilterButtons(filterBar, whatsNewFilter); RenderMods(); };
            activeModFilter = ModFilterMode.All;
            UpdateFilterButtons(filterBar, allFilter);

            modsDetailPanel = CreateCardPanel();
            modsDetailPanel.Location = new Point(808, 152);
            modsDetailPanel.Size = new Size(372, 448);
            modsDetailPanel.Anchor = AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom;
            page.Controls.Add(modsDetailPanel);

            modsList = new BufferedFlowLayoutPanel();
            modsList.Location = new Point(0, 152);
            modsList.Size = new Size(792, 448);
            modsList.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom;
            modsList.AutoScroll = true;
            modsList.WrapContents = false;
            modsList.FlowDirection = FlowDirection.TopDown;
            modsList.BackColor = Color.Transparent;
            modsList.Padding = new Padding(0, 0, 0, 20);
            page.Controls.Add(modsList);

            return page;
        }

        private BufferedPanel BuildPacksPage()
        {
            BufferedPanel page = CreatePage();

            BufferedPanel toolbar = CreateCardPanel();
            toolbar.Location = new Point(0, 0);
            toolbar.Size = new Size(1180, 84);
            page.Controls.Add(toolbar);

            Label header = CreateSectionTitle("Packs");
            header.Location = new Point(22, 16);
            toolbar.Controls.Add(header);

            Label caption = UiFactory.CreateCaption("Optional content bundles and asset packs.");
            caption.Location = new Point(22, 46);
            toolbar.Controls.Add(caption);

            openPacksFolderButton = UiFactory.CreateActionButton("Open Packs Folder", false);
            openPacksFolderButton.Location = new Point(430, 42);
            openPacksFolderButton.Click += delegate { OpenPacksFolder(); };
            toolbar.Controls.Add(openPacksFolderButton);

            packsList = new BufferedFlowLayoutPanel();
            packsList.Location = new Point(0, 102);
            packsList.Size = new Size(1180, 498);
            packsList.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom;
            packsList.AutoScroll = true;
            packsList.WrapContents = false;
            packsList.FlowDirection = FlowDirection.TopDown;
            packsList.BackColor = Color.Transparent;
            page.Controls.Add(packsList);

            return page;
        }

        private BufferedPanel BuildSettingsPage()
        {
            BufferedPanel page = CreatePage();

            BufferedPanel card = CreateCardPanel();
            card.Location = new Point(0, 0);
            card.Size = new Size(600, 280);
            page.Controls.Add(card);

            Label header = CreateSectionTitle("Settings");
            header.Location = new Point(22, 18);
            card.Controls.Add(header);

            Label caption = UiFactory.CreateCaption("Launcher preferences and quality-of-life options.");
            caption.Location = new Point(22, 48);
            card.Controls.Add(caption);

            Label autoLabel = new Label();
            autoLabel.Text = "Auto search on startup";
            autoLabel.ForeColor = UiStyle.Text;
            autoLabel.Location = new Point(22, 98);
            autoLabel.AutoSize = true;
            card.Controls.Add(autoLabel);

            autoSearchSwitch = new ToggleSwitch();
            autoSearchSwitch.Location = new Point(380, 94);
            card.Controls.Add(autoSearchSwitch);

            Label rememberLabel = new Label();
            rememberLabel.Text = "Remember window size";
            rememberLabel.ForeColor = UiStyle.Text;
            rememberLabel.Location = new Point(22, 146);
            rememberLabel.AutoSize = true;
            card.Controls.Add(rememberLabel);

            rememberWindowSwitch = new ToggleSwitch();
            rememberWindowSwitch.Location = new Point(380, 142);
            card.Controls.Add(rememberWindowSwitch);

            Button exportLogButton = UiFactory.CreateActionButton("Open App Data", false);
            exportLogButton.Location = new Point(22, 198);
            exportLogButton.Click += delegate
            {
                string root = AppPaths.GetUserDataRoot();
                if (!Directory.Exists(root))
                {
                    Directory.CreateDirectory(root);
                }
                Process.Start("explorer.exe", root);
            };
            card.Controls.Add(exportLogButton);

            Button clearLogButton = UiFactory.CreateActionButton("Clear Log", false);
            clearLogButton.Location = new Point(160, 198);
            clearLogButton.Click += delegate
            {
                try
                {
                    if (currentGame != null)
                    {
                        string runtimeLog = Path.Combine(currentGame.GameRoot, "Aestik_Data", "ModLoader", "logs", "runtime.log");
                        if (File.Exists(runtimeLog))
                        {
                            File.Delete(runtimeLog);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Clear log failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            card.Controls.Add(clearLogButton);

            BufferedPanel noteCard = CreateCardPanel();
            noteCard.Location = new Point(620, 0);
            noteCard.Size = new Size(560, 280);
            page.Controls.Add(noteCard);

            Label noteTitle = CreateSectionTitle("About");
            noteTitle.Location = new Point(22, 18);
            noteCard.Controls.Add(noteTitle);

            Label noteText = new Label();
            noteText.Text = "ADH installs a managed bootstrap DLL into Aestik_Data\\Managed so Unity loads the mod system at startup. Mods are imported into Aestik_Data\\ModLoader\\Mods and can be toggled without hard-coding a single install path.";
            noteText.ForeColor = UiStyle.Text;
            noteText.Location = new Point(22, 52);
            noteText.Size = new Size(500, 150);
            noteText.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
            noteCard.Controls.Add(noteText);

            return page;
        }

        private BufferedPanel CreatePage()
        {
            BufferedPanel page = new BufferedPanel();
            page.Dock = DockStyle.Fill;
            page.BackColor = UiStyle.Background;
            page.Visible = false;
            page.AutoScroll = true;
            page.Padding = new Padding(0);
            return page;
        }

        private BufferedPanel CreateCardPanel()
        {
            BufferedPanel panel = new BufferedPanel();
            panel.BackColor = UiStyle.Surface;
            panel.BorderStyle = BorderStyle.FixedSingle;
            panel.Padding = new Padding(0);
            return panel;
        }

        private Label CreateSectionTitle(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.ForeColor = UiStyle.Text;
            label.Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold);
            label.AutoSize = true;
            return label;
        }

        private Label CreateMetricCard(Panel parent, string title, string value, int x, int y)
        {
            BufferedPanel metric = new BufferedPanel();
            metric.BackColor = UiStyle.SurfaceAlt;
            metric.Size = new Size(150, 110);
            metric.Location = new Point(x, y);
            parent.Controls.Add(metric);

            Label titleLabel = UiFactory.CreateCaption(title);
            titleLabel.Location = new Point(14, 12);
            metric.Controls.Add(titleLabel);

            Label valueLabel = new Label();
            valueLabel.Text = value;
            valueLabel.ForeColor = UiStyle.Text;
            valueLabel.Font = new Font("Segoe UI Semibold", 19f, FontStyle.Bold);
            valueLabel.Location = new Point(14, 44);
            valueLabel.AutoSize = true;
            metric.Controls.Add(valueLabel);

            return valueLabel;
        }

        private Button CreateFilterButton(string text, ModFilterMode mode)
        {
            Button button = UiFactory.CreateActionButton(text, false);
            button.Tag = mode;
            button.Height = 28;
            button.Padding = new Padding(10, 0, 10, 0);
            button.Font = new Font("Segoe UI Semibold", 8.75f, FontStyle.Bold);
            return button;
        }

        private void UpdateFilterButtons(FlowLayoutPanel parent, Button selected)
        {
            foreach (Control control in parent.Controls)
            {
                Button button = control as Button;
                if (button == null)
                {
                    continue;
                }

                button.BackColor = button == selected ? UiStyle.AccentSoft : UiStyle.SurfaceAlt;
            }
        }

        private void StartWatching()
        {
            StopWatching();
            if (currentGame == null)
            {
                return;
            }

            string modsRoot = AppPaths.GetDefaultModsRoot(currentGame);
            string packsRoot = AppPaths.GetDefaultPacksRoot(currentGame);
            if (!string.IsNullOrEmpty(modsRoot) && Directory.Exists(modsRoot))
            {
                modsWatcher = CreateWatcher(modsRoot);
            }

            if (!string.IsNullOrEmpty(packsRoot) && Directory.Exists(packsRoot))
            {
                packsWatcher = CreateWatcher(packsRoot);
            }
        }

        private void StopWatching()
        {
            if (modsWatcher != null)
            {
                modsWatcher.EnableRaisingEvents = false;
                modsWatcher.Dispose();
                modsWatcher = null;
            }

            if (packsWatcher != null)
            {
                packsWatcher.EnableRaisingEvents = false;
                packsWatcher.Dispose();
                packsWatcher = null;
            }
        }

        private FileSystemWatcher CreateWatcher(string path)
        {
            FileSystemWatcher watcher = new FileSystemWatcher(path);
            watcher.IncludeSubdirectories = true;
            watcher.NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size;
            watcher.Changed += OnModTreeChanged;
            watcher.Created += OnModTreeChanged;
            watcher.Deleted += OnModTreeChanged;
            watcher.Renamed += OnModTreeChanged;
            watcher.EnableRaisingEvents = true;
            return watcher;
        }

        private void OnModTreeChanged(object sender, FileSystemEventArgs e)
        {
            if (IsDisposed)
            {
                return;
            }

            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    refreshTimer.Stop();
                    refreshTimer.Start();
                });
            }
            catch
            {
            }
        }

        private bool MatchesFilter(ModEntry mod, ModFilterMode filter)
        {
            if (mod == null)
            {
                return false;
            }

            switch (filter)
            {
                case ModFilterMode.Installed:
                    return mod.EntryExists;
                case ModFilterMode.Enabled:
                    return mod.Manifest != null && mod.Manifest.IsEnabled;
                case ModFilterMode.OutOfDate:
                    return mod.HasUpdate || !mod.EntryExists;
                case ModFilterMode.WhatsNew:
                    return true;
                default:
                    return true;
            }
        }

        private void UpdateModDetails(ModEntry mod)
        {
            if (modsDetailPanel == null)
            {
                return;
            }

            modsDetailPanel.SuspendLayout();
            modsDetailPanel.Controls.Clear();

            Label title = CreateSectionTitle("Mod Details");
            title.Location = new Point(18, 16);
            modsDetailPanel.Controls.Add(title);

            if (mod == null || mod.Manifest == null)
            {
                Label empty = UiFactory.CreateCaption("Select a mod to view details.");
                empty.Location = new Point(18, 50);
                modsDetailPanel.Controls.Add(empty);
                modsDetailPanel.ResumeLayout();
                return;
            }

            Label name = new Label();
            name.Text = mod.Manifest.Name;
            name.ForeColor = UiStyle.Text;
            name.Font = new Font("Segoe UI Semibold", 15f, FontStyle.Bold);
            name.AutoSize = true;
            name.Location = new Point(18, 50);
            modsDetailPanel.Controls.Add(name);

            Label version = UiFactory.CreateCaption("Version " + mod.Manifest.Version + "  |  " + (mod.Manifest.Category ?? "General"));
            version.Location = new Point(18, 80);
            modsDetailPanel.Controls.Add(version);

            Label desc = new Label();
            desc.Text = string.IsNullOrEmpty(mod.Manifest.Description) ? "No description provided." : mod.Manifest.Description;
            desc.ForeColor = UiStyle.Text;
            desc.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            desc.Location = new Point(18, 112);
            desc.Size = new Size(332, 120);
            desc.AutoEllipsis = true;
            modsDetailPanel.Controls.Add(desc);

            Label status = UiFactory.CreateCaption("Status: " + mod.Status + (mod.HasUpdate ? " | Update recommended" : ""));
            status.Location = new Point(18, 236);
            modsDetailPanel.Controls.Add(status);

            ToggleSwitch enabledSwitch = new ToggleSwitch();
            enabledSwitch.Checked = mod.Manifest.IsEnabled;
            enabledSwitch.Location = new Point(18, 274);
            enabledSwitch.CheckedChanged += delegate
            {
                mod.Manifest.IsEnabled = enabledSwitch.Checked;
                ModRepository.SetEnabled(mod, enabledSwitch.Checked);
                RefreshMods();
            };
            modsDetailPanel.Controls.Add(enabledSwitch);

            Label enabledLabel = new Label();
            enabledLabel.Text = "Enabled";
            enabledLabel.ForeColor = UiStyle.Text;
            enabledLabel.Location = new Point(82, 280);
            enabledLabel.AutoSize = true;
            modsDetailPanel.Controls.Add(enabledLabel);

            Button openButton = UiFactory.CreateActionButton("Open Folder", false);
            openButton.Location = new Point(18, 322);
            openButton.Click += delegate { OpenFolder(mod.FolderPath); };
            modsDetailPanel.Controls.Add(openButton);

            Button removeButton = UiFactory.CreateActionButton("Remove", false);
            removeButton.BackColor = UiStyle.Danger;
            removeButton.Location = new Point(136, 322);
            removeButton.Click += delegate { RemoveMod(mod); };
            modsDetailPanel.Controls.Add(removeButton);

            Button installButton = UiFactory.CreateActionButton("Reinstall Loader", false);
            installButton.Location = new Point(18, 370);
            installButton.Click += delegate { InstallLoader(); };
            modsDetailPanel.Controls.Add(installButton);

            Label extra = UiFactory.CreateCaption("Load order uses priority, dependency, and file date sorting.");
            extra.Location = new Point(18, 416);
            modsDetailPanel.Controls.Add(extra);

            modsDetailPanel.ResumeLayout();
        }

        private void ShowPage(Panel page)
        {
            infoPage.Visible = page == infoPage;
            modsPage.Visible = page == modsPage;
            packsPage.Visible = page == packsPage;
            settingsPage.Visible = page == settingsPage;

            infoTab.BackColor = page == infoPage ? UiStyle.SurfaceAlt : UiStyle.Background;
            modsTab.BackColor = page == modsPage ? UiStyle.SurfaceAlt : UiStyle.Background;
            packsTab.BackColor = page == packsPage ? UiStyle.SurfaceAlt : UiStyle.Background;
            settingsTab.BackColor = page == settingsPage ? UiStyle.SurfaceAlt : UiStyle.Background;

            infoTab.ForeColor = page == infoPage ? UiStyle.Text : UiStyle.SubText;
            modsTab.ForeColor = page == modsPage ? UiStyle.Text : UiStyle.SubText;
            packsTab.ForeColor = page == packsPage ? UiStyle.Text : UiStyle.SubText;
            settingsTab.ForeColor = page == settingsPage ? UiStyle.Text : UiStyle.SubText;
        }

        private void BeginDiscovery(bool activeSearch)
        {
            int token = Interlocked.Increment(ref discoveryToken);
            SetStatus(activeSearch ? "Searching Steam libraries..." : "Checking cached Steam path...");
            LogLine("Starting " + (activeSearch ? "active" : "quick") + " discovery...");

            ThreadPool.QueueUserWorkItem(delegate
            {
                DiscoveryResult result = SteamLocator.DiscoverGame(activeSearch || autoSearchSwitch.Checked);
                if (token != discoveryToken)
                {
                    return;
                }

                BeginInvoke((MethodInvoker)delegate
                {
                    ApplyDiscoveryResult(result);
                });
            });
        }

        private void ApplyDiscoveryResult(DiscoveryResult result)
        {
            lastDiscovery = result;
            currentGame = result.Install;

            if (currentGame == null)
            {
                StopWatching();
                pathValueLabel.Text = "Aestik was not found in Steam libraries.";
                installStateLabel.Text = "Not detected";
                installStateLabel.ForeColor = UiStyle.Danger;
                steamStateLabel.Text = "Not found";
                SetStatus("Game not found");
                loadedMods = new List<ModEntry>();
                loadedPacks = new List<ModEntry>();
                RenderMods();
                RenderPacks();
                UpdateSummary();
                return;
            }

            pathValueLabel.Text = currentGame.GameRoot;
            bool installed = ModRepository.IsLoaderInstalled(currentGame);
            installStateLabel.Text = installed ? "Loader installed" : "Loader not installed";
            installStateLabel.ForeColor = installed ? UiStyle.Success : UiStyle.Danger;
            steamStateLabel.Text = currentGame.FoundViaSteamManifest ? "Steam manifest" : "Active scan";
            SetStatus(installed ? "Ready" : "Detected, not installed");
            StartWatching();

            loadedMods = ModRepository.LoadMods(currentGame);
            loadedPacks = LoadPackFolders(currentGame);
            RenderMods();
            RenderPacks();
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            if (modsCountLabel != null)
            {
                modsCountLabel.Text = loadedMods != null ? loadedMods.Count.ToString() : "0";
            }

            if (packsCountLabel != null)
            {
                packsCountLabel.Text = loadedPacks != null ? loadedPacks.Count.ToString() : "0";
            }
        }

        private void RenderMods()
        {
            if (modsList == null)
            {
                return;
            }

            string filter = searchBox != null ? searchBox.Text.Trim().ToLowerInvariant() : "";
            IEnumerable<ModEntry> source = loadedMods ?? new List<ModEntry>();
            List<ModEntry> filtered = new List<ModEntry>();
            foreach (ModEntry mod in source)
            {
                if (mod == null || mod.Manifest == null)
                {
                    continue;
                }

                string hay = (mod.Manifest.Name + " " + mod.Manifest.Author + " " + mod.Manifest.Description + " " + mod.Manifest.Version + " " + mod.Manifest.Category).ToLowerInvariant();
                if (filter.Length > 0 && hay.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (!MatchesFilter(mod, activeModFilter))
                {
                    continue;
                }

                filtered.Add(mod);
            }

            if (activeModFilter == ModFilterMode.WhatsNew)
            {
                filtered = filtered.OrderByDescending(delegate(ModEntry mod) { return mod.LastModifiedUtc; }).ToList();
            }

            displayedMods = filtered;
            if (modsCountValueLabel != null)
            {
                modsCountValueLabel.Text = (loadedMods != null ? loadedMods.Count : 0).ToString() + " mods";
            }
            if (modsFilteredCountLabel != null)
            {
                modsFilteredCountLabel.Text = filtered.Count.ToString() + " shown";
            }

            int pageCount = filtered.Count == 0 ? 1 : ((filtered.Count - 1) / ModsPageSize) + 1;
            if (modsPageIndex >= pageCount)
            {
                modsPageIndex = Math.Max(0, pageCount - 1);
            }

            int start = modsPageIndex * ModsPageSize;
            int end = Math.Min(start + ModsPageSize, filtered.Count);

            modsPageLabel.Text = "Page " + (modsPageIndex + 1).ToString() + " / " + pageCount.ToString();
            modsPrevButton.Enabled = modsPageIndex > 0;
            modsNextButton.Enabled = end < filtered.Count;

            modsList.SuspendLayout();
            modsList.Controls.Clear();

            if (filtered.Count == 0)
            {
                Label empty = new Label();
                empty.Text = "No mods match the current filter.";
                empty.ForeColor = UiStyle.SubText;
                empty.Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold);
                empty.AutoSize = true;
                empty.Margin = new Padding(10, 20, 10, 20);
                modsList.Controls.Add(empty);
                selectedMod = null;
                UpdateModDetails(null);
            }
            else
            {
                for (int i = start; i < end; i++)
                {
                    ModEntry mod = filtered[i];
                    modsList.Controls.Add(new ModCardControl(mod, delegate(ModEntry m)
                    {
                        selectedMod = m;
                        UpdateModDetails(m);
                    }, delegate(ModEntry m)
                    {
                        ModRepository.SetEnabled(m, m.Manifest.IsEnabled);
                        RefreshMods();
                    }, delegate(ModEntry m)
                    {
                        OpenFolder(m.FolderPath);
                    }, delegate(ModEntry m)
                    {
                        RemoveMod(m);
                    }));
                }

                if (selectedMod == null || !filtered.Contains(selectedMod))
                {
                    selectedMod = filtered[0];
                }
                UpdateModDetails(selectedMod);
            }

            modsList.ResumeLayout();
            UpdateSummary();
        }

        private void RenderPacks()
        {
            if (packsList == null)
            {
                return;
            }

            packsList.SuspendLayout();
            packsList.Controls.Clear();
            List<ModEntry> source = loadedPacks ?? new List<ModEntry>();
            if (source.Count == 0)
            {
                Label empty = new Label();
                empty.Text = "No content packs have been imported yet.";
                empty.ForeColor = UiStyle.SubText;
                empty.Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold);
                empty.AutoSize = true;
                empty.Margin = new Padding(10, 20, 10, 20);
                packsList.Controls.Add(empty);
            }
            else
            {
                for (int i = 0; i < source.Count; i++)
                {
                    packsList.Controls.Add(new ModCardControl(source[i], delegate(ModEntry mod)
                    {
                        selectedMod = mod;
                        UpdateModDetails(mod);
                    }, delegate(ModEntry mod)
                    {
                        ModRepository.SetEnabled(mod, mod.Manifest.IsEnabled);
                        RenderPacks();
                    }, delegate(ModEntry mod)
                    {
                        OpenFolder(mod.FolderPath);
                    }, delegate(ModEntry mod)
                    {
                        RemoveMod(mod);
                    }));
                }
            }

            packsList.ResumeLayout();
            UpdateSummary();
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

        private void RefreshMods()
        {
            if (currentGame == null)
            {
                BeginDiscovery(false);
                return;
            }

            loadedMods = ModRepository.LoadMods(currentGame);
            loadedPacks = LoadPackFolders(currentGame);
            RenderMods();
            RenderPacks();
            UpdateSummary();
            SetStatus("Refreshed.");
        }

        private void ImportMod()
        {
            if (currentGame == null)
            {
                MessageBox.Show(this, "Detect the game first so the loader knows where to place the mod.", "Aestik Mod Loader", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "Import mod";
            dialog.Filter = "Mod packages (*.dll;*.zip)|*.dll;*.zip|All files (*.*)|*.*";
            dialog.Multiselect = false;
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                string selectedManifest = null;
                string[] manifestOptions = ModRepository.FindManifestOptions(dialog.FileName);
                if (string.Equals(Path.GetExtension(dialog.FileName), ".zip", StringComparison.OrdinalIgnoreCase) && manifestOptions.Length > 1)
                {
                    MessageBox.Show(this, "This zip contains multiple manifest files. Please import it through the HTML launcher so you can choose which manifest to use.", "Choose manifest", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (manifestOptions.Length == 1)
                {
                    selectedManifest = manifestOptions[0];
                }

                ImportPreviewInfo preview = ModRepository.AnalyzeImportSource(currentGame, dialog.FileName, loadedMods, selectedManifest);
                string previewText = BuildImportSummary(preview);
                DialogResult confirm = MessageBox.Show(this, previewText, "Import preview", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (confirm != DialogResult.Yes)
                {
                    return;
                }

                ModRepository.ImportModFile(currentGame, dialog.FileName, selectedManifest);
                RefreshMods();
                SetStatus("Imported " + Path.GetFileName(dialog.FileName));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            builder.AppendLine("Kind: " + preview.Entry.Manifest.Kind);

            if (preview.DeclaredDependencies.Count > 0)
            {
                builder.AppendLine("Dependencies: " + string.Join(", ", preview.DeclaredDependencies.ToArray()));
            }
            else
            {
                builder.AppendLine("Dependencies: None declared");
            }

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

        private void RemoveMod(ModEntry mod)
        {
            if (mod == null)
            {
                return;
            }

            if (MessageBox.Show(this, "Remove " + mod.Manifest.Name + "?", "Confirm removal", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                ModRepository.RemoveMod(mod);
                RefreshMods();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Removal failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InstallLoader()
        {
            if (currentGame == null)
            {
                MessageBox.Show(this, "Detect the game first.", "Aestik Mod Loader", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                ModRepository.EnsureLoaderInstalled(currentGame);
                ApplyDiscoveryResult(lastDiscovery ?? new DiscoveryResult { Install = currentGame });
                SetStatus("Loader installed.");
                MessageBox.Show(this, "The loader runtime was installed and Aestik bootstrap was patched.", "Installed", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Install failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LaunchGame(bool throughSteam)
        {
            if (currentGame == null)
            {
                MessageBox.Show(this, "Detect the game first.", "Aestik Mod Loader", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                ModRepository.EnsureLoaderInstalled(currentGame);
                if (throughSteam)
                {
                    Process.Start(new ProcessStartInfo("steam://rungameid/" + currentGame.AppId)
                    {
                        UseShellExecute = true
                    });
                    SetStatus("Launching through Steam.");
                }
                else
                {
                    string exe = currentGame.ExecutablePath;
                    if (!File.Exists(exe))
                    {
                        MessageBox.Show(this, "Game executable not found.", "Launch failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    Process.Start(new ProcessStartInfo(exe)
                    {
                        WorkingDirectory = currentGame.GameRoot,
                        UseShellExecute = true
                    });
                    SetStatus("Game launched.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Launch failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenModsFolder()
        {
            if (currentGame == null)
            {
                return;
            }

            string root = AppPaths.GetDefaultModsRoot(currentGame);
            if (!Directory.Exists(root))
            {
                Directory.CreateDirectory(root);
            }

            Process.Start("explorer.exe", root);
        }

        private void OpenPacksFolder()
        {
            if (currentGame == null)
            {
                return;
            }

            string root = AppPaths.GetDefaultPacksRoot(currentGame);
            if (!Directory.Exists(root))
            {
                Directory.CreateDirectory(root);
            }

            Process.Start("explorer.exe", root);
        }

        private void OpenFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                return;
            }

            Process.Start("explorer.exe", path);
        }

        private void SetStatus(string text)
        {
            if (statusLabel != null)
            {
                statusLabel.Text = text;
            }
        }

        private void LogLine(string text)
        {
            ConsoleMode.WriteLine(text);
        }

        private sealed class ModCardControl : BufferedPanel
        {
            private readonly ModEntry mod;
            private readonly Action<ModEntry> selectAction;
            private readonly Action<ModEntry> toggleAction;
            private readonly Action<ModEntry> openAction;
            private readonly Action<ModEntry> removeAction;
            private readonly ToggleSwitch toggle;
            private readonly Button primaryAction;

            public ModCardControl(ModEntry mod, Action<ModEntry> selectAction, Action<ModEntry> toggleAction, Action<ModEntry> openAction, Action<ModEntry> removeAction)
            {
                this.mod = mod;
                this.selectAction = selectAction;
                this.toggleAction = toggleAction;
                this.openAction = openAction;
                this.removeAction = removeAction;

                BackColor = UiStyle.SurfaceAlt;
                Size = new Size(770, 72);
                Margin = new Padding(0, 0, 0, 10);
                Padding = new Padding(0);

                Label title = new Label();
                title.Text = mod.Manifest.Name;
                title.ForeColor = UiStyle.Text;
                title.Font = new Font("Segoe UI Semibold", 11.5f, FontStyle.Bold);
                title.AutoSize = true;
                title.Location = new Point(18, 14);
                Controls.Add(title);

                Label meta = new Label();
                meta.Text = string.Format("{0}  |  {1}", mod.Manifest.Author ?? "Unknown author", mod.Manifest.Version ?? "1.0.0");
                meta.ForeColor = UiStyle.SubText;
                meta.Font = new Font("Segoe UI", 8.75f, FontStyle.Regular);
                meta.AutoSize = true;
                meta.Location = new Point(18, 36);
                Controls.Add(meta);

                Label status = new Label();
                status.Text = mod.HasUpdate ? "Out of date" : mod.Status;
                status.ForeColor = mod.HasUpdate ? UiStyle.Accent : (mod.EntryExists ? UiStyle.Success : UiStyle.Danger);
                status.Font = new Font("Segoe UI Semibold", 8.75f, FontStyle.Bold);
                status.AutoSize = true;
                status.Location = new Point(18, 52);
                Controls.Add(status);

                toggle = new ToggleSwitch();
                toggle.Checked = mod.Manifest.IsEnabled;
                toggle.Location = new Point(388, 22);
                toggle.CheckedChanged += delegate
                {
                    mod.Manifest.IsEnabled = toggle.Checked;
                    if (toggleAction != null)
                    {
                        toggleAction(mod);
                    }
                };
                Controls.Add(toggle);

                Label enabledLabel = new Label();
                enabledLabel.Text = "Enabled";
                enabledLabel.ForeColor = UiStyle.SubText;
                enabledLabel.AutoSize = true;
                enabledLabel.Location = new Point(446, 26);
                Controls.Add(enabledLabel);

                primaryAction = UiFactory.CreateActionButton(mod.EntryExists ? "Open" : "Install", mod.EntryExists);
                primaryAction.Size = new Size(96, 32);
                primaryAction.Location = new Point(534, 20);
                primaryAction.Click += delegate
                {
                    if (openAction != null)
                    {
                        openAction(mod);
                    }
                };
                Controls.Add(primaryAction);

                Button detailsButton = UiFactory.CreateActionButton(">", false);
                detailsButton.Size = new Size(34, 32);
                detailsButton.Location = new Point(640, 20);
                detailsButton.Click += delegate
                {
                    if (selectAction != null)
                    {
                        selectAction(mod);
                    }
                };
                Controls.Add(detailsButton);

                Button removeButton = UiFactory.CreateActionButton("Remove", false);
                removeButton.BackColor = UiStyle.Danger;
                removeButton.Size = new Size(86, 32);
                removeButton.Location = new Point(680, 20);
                removeButton.Click += delegate
                {
                    if (removeAction != null)
                    {
                        removeAction(mod);
                    }
                };
                Controls.Add(removeButton);

                Click += delegate { if (selectAction != null) selectAction(mod); };
                title.Click += delegate { if (selectAction != null) selectAction(mod); };
                meta.Click += delegate { if (selectAction != null) selectAction(mod); };
                status.Click += delegate { if (selectAction != null) selectAction(mod); };
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                using (Pen pen = new Pen(UiStyle.Border))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                }
            }
        }
    }
}
