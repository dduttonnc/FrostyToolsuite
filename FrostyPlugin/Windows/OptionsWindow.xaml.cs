using Frosty.Controls;
using Frosty.Core.Controls;
using Frosty.Core.Controls.Editors;
using Frosty.Core.Misc;
using FrostySdk.Attributes;
using FrostySdk.Ebx;
using FrostySdk.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using FrostySdk;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using FrostySdk.Managers.Entries;

namespace Frosty.Core.Windows
{
    public class OptionsDisplayNameToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Type valueType = value.GetType();
            var attr = valueType.GetCustomAttribute<DisplayNameAttribute>();
            if (attr != null)
                return attr.Name;
            return valueType.Name;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class FrostyLocalizationLanguageDataEditor : FrostyCustomComboDataEditor<string, string>
    {
    }

    public class FrostyCustomPathEditor : FrostyFolderPathEditor
    {
    }

    public abstract class BaseOptionsData : OptionsExtension
    {
        [Category("Update Checking")]
        [DisplayName("Check for Updates")]
        [Description("Check GitHub for Frosty updates on startup")]
        [EbxFieldMeta(EbxFieldType.Boolean)]
        public bool UpdateCheck { get; set; } = true;

        //[Category("Update Checking")]
        //[DisplayName("Check for Prerelease Updates")]
        //[Description("Check GitHub for Frosty Alpha and Beta updates on startup")]
        //[EbxFieldMeta(EbxFieldType.Boolean)]
        //public bool UpdateCheckPrerelease { get; set; } = false;

        [Category("General")]
        [DisplayName("CAS Max File Size")]
        [Description("Change the maximum size of written cas files when applying mods.\r\n\r\nHigher Values decrease system stability but ensure mod compatibility.")]
        [EbxFieldMeta(EbxFieldType.Struct)]
        [Editor(typeof(FrostyLocalizationLanguageDataEditor))]
        public CustomComboData<string, string> MaxCasFileSize { get; set; }

        [Category("General")]
        [DisplayName("Disable Launch Process Check")]
        [Description("Disable the functionality to check if a process is already running when trying to launch")]
        [EbxFieldMeta(EbxFieldType.Boolean)]
        public bool DisableLaunchProcessCheck { get; set; } = false;

        [Category("General")]
        [DisplayName("Disable Shader Compilation At Launch")]
        [Description("Disables the shader compilation/verification done by the game at launch")]
        [EbxFieldMeta(EbxFieldType.Boolean)]
        public bool DisableShaderCacheSymLink { get; set; } = false;

        [Category("General")]
        [DisplayName("Launch Game Using Steam Protocol")]
        [Description("An alternate launch method that uses the Steam Protocol for launching the game with mods")]
        [EbxFieldMeta(EbxFieldType.Boolean)]
        public bool UseSteamProtocol { get; set; } = false;

        [Category("General")]
        [DisplayName("ModData Build Log")]
        [Description("Each time ModData is built, saves a file to the Logs folder next to the exe (ModDataBuild_ with the date and time) listing the superbundles, bundles and chunks the mods added or changed, by name and ID.\r\n\r\nMeant for mod authors. Diagnostic Logging covers troubleshooting. Automatic Log Cleanup deletes old files.")]
        [EbxFieldMeta(EbxFieldType.Boolean)]
        public bool PrintLog { get; set; } = false;
        
        [Category("General")]
        [DisplayName("Alternate SymLink Method")]
        [Description("An alternate method of creating Symbolic Links for ModData")]
        [EbxFieldMeta(EbxFieldType.Boolean)]
        public bool UseAltSymLink { get; set; } = false;

        public override void Load()
        {
            base.Load();
            
            UpdateCheck = Config.Get<bool>("UpdateCheck", true);
            //UpdateCheckPrerelease = Config.Get<bool>("UpdateCheckPrerelease", false);

            List<string> sizes = new List<string>() { "1GB", "512MB", "256MB" };
            MaxCasFileSize = new CustomComboData<string, string>(sizes, sizes);
            MaxCasFileSize.SelectedIndex = sizes.IndexOf(Config.Get<string>("MaxCasFileSize", "1GB"));

            DisableLaunchProcessCheck = Config.Get<bool>("DisableLaunchProcessCheck", false);
            DisableShaderCacheSymLink = Config.Get<bool>("DisableShaderCacheSymLink", false);
            UseSteamProtocol = Config.Get<bool>("UseSteamProtocol", false);
            PrintLog = Config.Get<bool>("PrintLog", false);
            UseAltSymLink = Config.Get<bool>("UseAltSymLink", false);
        }

        public override void Save()
        {
            base.Save();
            
            Config.Add("UpdateCheck", UpdateCheck);
            //Config.Add("UpdateCheckPrerelease", UpdateCheckPrerelease);
            
            // [DAI fix] applying mods uses the Performance options; drop the old separate setting
            Config.Remove("ApplyingThreadCount");
            Config.Add("MaxCasFileSize", MaxCasFileSize.SelectedName);

            Config.Add("DisableLaunchProcessCheck", DisableLaunchProcessCheck);
            Config.Add("DisableShaderCacheSymLink", DisableShaderCacheSymLink);
            Config.Add("UseSteamProtocol", UseSteamProtocol);
            Config.Add("PrintLog", PrintLog);
            Config.Add("UseAltSymLink", UseAltSymLink);
        }
    }
    
    [DisplayName("Editor Options")]
    public class EditorOptionsData : BaseOptionsData
    {
        [Category("General")]
        [Description("Selects which localized language files to read from the game files.")]
        [EbxFieldMeta(EbxFieldType.Struct)]
        [Editor(typeof(FrostyLocalizationLanguageDataEditor))]
        public CustomComboData<string, string> Language { get; set; }

        [Category("Autosave")]
        [DisplayName("Custom Autosave Directory")]
        [Description("Select directory where projects will be autosaved.")]
        [Editor(typeof(FrostyCustomPathEditor))]
        [DependsOn("AutosaveEnabled")]
        public string CustomAutosaveDirectory { get; set; } = "";

        [Category("Autosave")]
        [DisplayName("Period")]
        [Description("How often to autosave the project. Value is defined in minutes.")]
        [EbxFieldMeta(EbxFieldType.Int32)]
        [DependsOn("AutosaveEnabled")]
        public int AutosavePeriod { get; set; } = 5;

        [Category("Autosave")]
        [DisplayName("Max Saves")]
        [Description("Maximum number of autosave files to generate per project.")]
        [EbxFieldMeta(EbxFieldType.Int32)]
        [DependsOn("AutosaveEnabled")]
        public int AutosaveMaxSaves { get; set; } = 10;

        [Category("Autosave")]
        [DisplayName("Enabled")]
        [Description("Enables autosaving for projects.")]
        [EbxFieldMeta(EbxFieldType.Boolean)]
        public bool AutosaveEnabled { get; set; } = true;
        
        [Category("Text Editor")]
        [DisplayName("Tab Size")]
        [Description("Size of opened tabs in the Editor.")]
        [EbxFieldMeta(EbxFieldType.Int32)]
        public int TextEditorTabSize { get; set; } = 4;

        [Category("Text Editor")]
        [DisplayName("Indent on Enter")]
        [Description("Indents text upon pressing the Enter key.")]
        [EbxFieldMeta(EbxFieldType.Boolean)]
        public bool TextEditorIndentOnEnter { get; set; } = false;

        [Category("Export to XML")]
        [DisplayName("Tab Size")]
        [Description("Tab size indentation to use for XML or YAML export and related plugins.")]
        [EbxFieldMeta(EbxFieldType.Int32)]
        public int ExportTabSize { get; set; } = 2;

        [Category("Export to XML")]
        [DisplayName("Export debug offsets")]
        [Description("Adds hexadecimal offset tags to the exported XML or YAML.")]
        [EbxFieldMeta(EbxFieldType.Boolean)]
        public bool ExportWithOffsets { get; set; } = false;

        [Category("Discord RPC")]
        [DisplayName("Hide Project Name")]
        [Description("Hides project name when rich presence for Discord is turned on.")]
        [EbxFieldMeta(EbxFieldType.Boolean)]
        [DependsOn("DiscordEnabled")]
        public bool DiscordHideProject { get; set; } = false;

        [Category("Discord RPC")]
        [DisplayName("Hide Opened Asset")]
        [Description("Hides opened asset name when rich presence for Discord is turned on.")]
        [EbxFieldMeta(EbxFieldType.Boolean)]
        [DependsOn("DiscordEnabled")]
        public bool DiscordHideAsset { get; set; } = false;

        [Category("Discord RPC")]
        [DisplayName("Enabled")]
        [Description("Turns on rich presence for Discord.")]
        [EbxFieldMeta(EbxFieldType.Boolean)]
        public bool DiscordEnabled { get; set; } = false;

        [Category("Mod Settings")]
        [DisplayName("Default Author")]
        [Description("Sets the default author for a mod.")]
        [EbxFieldMeta(EbxFieldType.String)]
        public string ModSettingsAuthor { get; set; } = "";

        [Category("Editor")]
        [DisplayName("Display Module in Class Id")]
        [Description("Determines whether a class's default Id, when viewed in the property grid, is prepended with the module name of that class.\r\n\r\nTrue: Entity.MathEntityData\r\nFalse: MathEntityData")]
        [EbxFieldMeta(EbxFieldType.Boolean)]
        public bool AssetDisplayModuleInId { get; set; } = true;

        [Category("Editor")]
        [DisplayName("Remember Profile Choice")]
        [Description("If true, the last chosen profile will be automatically loaded when starting the Editor.")]
        [EbxFieldMeta(EbxFieldType.Boolean)]
        public bool RememberChoice { get; set; } = false;

        [Category("Editor")]
        [DisplayName("Set as Default Installation")]
        [Description("Use this installation for .fbproject files.")]
        [EbxFieldMeta(EbxFieldType.Boolean)]
        public bool DefaultInstallation { get; set; } = false;

        public override void Load()
        {
            base.Load();
            
            if (ProfilesLibrary.HasLoadedProfile)
            {
                List<string> langs = GetLocalizedLanguages();
                Language = new CustomComboData<string, string>(langs, langs) { SelectedIndex = langs.IndexOf(Config.Get<string>("Language", "English", ConfigScope.Game)) };
            }
            else
            {
                List<string> emptyLangs = new List<string>();
                Language = new CustomComboData<string, string>(emptyLangs, emptyLangs);
            }

            AutosaveEnabled = Config.Get<bool>("AutosaveEnabled", true);
            AutosavePeriod = Config.Get<int>("AutosavePeriod", 5);
            AutosaveMaxSaves = Config.Get<int>("AutosaveMaxCount", 10);
            CustomAutosaveDirectory = Config.Get<string>("CustomAutosaveDirectory", "");

            TextEditorTabSize = Config.Get<int>("TextEditorTabSize", 4);
            TextEditorIndentOnEnter = Config.Get<bool>("TextEditorIndentOnEnter", false);

            DiscordEnabled = Config.Get<bool>("DiscordRPCEnabled", false);
            DiscordHideProject = Config.Get<bool>("DiscordRPCHideProject", false);
            DiscordHideAsset = Config.Get<bool>("DiscordRPCHideAsset", false);
            ModSettingsAuthor = Config.Get<string>("ModAuthor", "");

            ExportTabSize = Config.Get<int>("ExportTabSize", 2);
            ExportWithOffsets = Config.Get<bool>("ExportWithOffsets", false);

            AssetDisplayModuleInId = Config.Get<bool>("DisplayModuleInId", false);
            RememberChoice = Config.Get<bool>("UseDefaultProfile2", false);

            //Checks the registry for the current association instead of loading from config
            string KeyName = "frostyproject";
            string OpenWith = Assembly.GetEntryAssembly().Location;

            if (Registry.CurrentUser.OpenSubKey("Software\\Classes\\" + KeyName) != null) {
                string openCommand = (string)Registry.CurrentUser.OpenSubKey("Software\\Classes\\" + KeyName).OpenSubKey("shell").OpenSubKey("open").OpenSubKey("command").GetValue("");
                if (openCommand.Contains(OpenWith)) DefaultInstallation = true;
            }
        }

        public override void Save()
        {
            base.Save();
            
            Config.Add("AutosaveEnabled", AutosaveEnabled);
            Config.Add("AutosavePeriod", AutosavePeriod);
            Config.Add("AutosaveMaxCount", AutosaveMaxSaves);
            Config.Add("CustomAutosaveDirectory", CustomAutosaveDirectory);

            Config.Add("TextEditorTabSize", TextEditorTabSize);
            Config.Add("TextEditorIndentOnEnter", TextEditorIndentOnEnter);

            Config.Add("DiscordRPCEnabled", DiscordEnabled);
            Config.Add("DiscordRPCHideProject", DiscordHideProject);
            Config.Add("DiscordRPCHideAsset", DiscordHideAsset);
            Config.Add("ModAuthor", ModSettingsAuthor);

            Config.Add("ExportTabSize", ExportTabSize);
            Config.Add("ExportWithOffsets", ExportWithOffsets);

            Config.Add("DisplayModuleInId", AssetDisplayModuleInId);
            Config.Add("UseDefaultProfile2", RememberChoice);

            if (RememberChoice)
                Config.Add("DefaultProfile2", ProfilesLibrary.ProfileName);
            else
                Config.Remove("DefaultProfile2");

            Config.Add("Language", Language.SelectedName, ConfigScope.Game);

            Config.Save();

            LocalizedStringDatabase.Current.Initialize();

            //Create file association if enabled
            if (DefaultInstallation) {
                string Extension = ".fbproject";
                string KeyName = "frostyproject";
                string OpenWith = Assembly.GetEntryAssembly().Location;
                string FileDescription = "Frosty Project";

                RegistryKey BaseKey;
                RegistryKey OpenMethod;
                RegistryKey Shell;
                RegistryKey CurrentUser;

                BaseKey = Registry.CurrentUser.CreateSubKey("Software\\Classes\\" + Extension);
                BaseKey.SetValue("", KeyName);

                OpenMethod = Registry.CurrentUser.CreateSubKey("Software\\Classes\\" + KeyName);
                OpenMethod.SetValue("", FileDescription);
                OpenMethod.CreateSubKey("DefaultIcon").SetValue("", "\"" + OpenWith + "\",0");
                Shell = OpenMethod.CreateSubKey("shell");
                Shell.CreateSubKey("edit").CreateSubKey("command").SetValue("", "\"" + OpenWith + "\"" + " \"%1\"");
                Shell.CreateSubKey("open").CreateSubKey("command").SetValue("", "\"" + OpenWith + "\"" + " \"%1\"");
                BaseKey.Close();
                OpenMethod.Close();
                Shell.Close();

                CurrentUser = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\FileExts\\" + Extension, true);
                CurrentUser.DeleteSubKey("UserChoice", false);
                CurrentUser.Close();

                // Tell explorer the file association has been changed
                SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
            }
        }

        public override bool Validate()
        {
            return true;
        }
        
        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        private List<string> GetLocalizedLanguages()
        {
            List<string> languages = new List<string>();
            foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx("LocalizationAsset"))
            {
                // read master localization asset
                dynamic localizationAsset = App.AssetManager.GetEbx(entry).RootObject;

                // iterate through localized texts
                foreach (PointerRef pointer in localizationAsset.LocalizedTexts)
                {
                    EbxAssetEntry textEntry = App.AssetManager.GetEbxEntry(pointer.External.FileGuid);
                    if (textEntry == null)
                        continue;

                    // read localized text asset
                    dynamic localizedText = App.AssetManager.GetEbx(textEntry).RootObject;

                    string lang = localizedText.Language.ToString();
                    lang = lang.Replace("LanguageFormat_", "");

                    languages.Add(lang);
                }
            }

            if (languages.Count == 0)
                languages.Add("English");

            return languages;
        }
    }

    [DisplayName("Mod Manager Options")]
    public class ModManagerOptionsData : BaseOptionsData, IPropertyGridLayout
    {
        // [DAI fix] groups and order shown on this page; settings keep their config keys
        private static readonly string[][] categoryLayout =
        {
            new[] { "Launch", nameof(UseSteamProtocol), nameof(DisableShaderCacheSymLink), nameof(DisableLaunchProcessCheck), nameof(TerminateStuckGame), nameof(CommandLineArgs) },
            new[] { "Mods & ModData", nameof(UseCustomModsDirectory), nameof(CustomModsDirectory), nameof(MaxCasFileSize), nameof(UseAltSymLink) },
            new[] { "Logging", nameof(PrintLog), nameof(DiagnosticLogging), nameof(AutoLogCleanup), nameof(LogCleanupDays) },
            new[] { "Interface", nameof(Theme), nameof(RememberChoice), nameof(ProgressView) },
            new[] { "Performance", nameof(WorkerPriority), nameof(WorkerThreads) },
            new[] { "Updates", nameof(UpdateCheck) },
        };

        public string[][] GetCategoryLayout() => categoryLayout;

        [Category("Interface")]
        [DisplayName("Remember Profile Choice")]
        [Description("If true, the last chosen profile will be automatically loaded when starting the Mod Manager.")]
        [EbxFieldMeta(EbxFieldType.Boolean)]
        public bool RememberChoice { get; set; } = false;

        [Category("Launch")]
        [DisplayName("Command Line Arguments")]
        [Description("Command line arguments to run on launch.")]
        [EbxFieldMeta(EbxFieldType.Boolean)]
        public string CommandLineArgs { get; set; } = "";

        // [DAI fix] off gives the old behaviour: refuse to launch while the game is running
        [Category("Launch")]
        [DisplayName("Terminate Stuck Game")]
        [Description("When the game is still running in the background as you launch (for example its window closed but it never fully shut down), Frosty asks whether to end it. On Yes it ends the game, losing anything not saved in it, asking for admin rights through the Windows prompt only if needed, and launches once the old game has fully closed. If it can't be ended, Frosty explains how to end it in Task Manager.\r\n\r\nWhen off, Frosty only says the game is already running and doesn't launch. Not used while Disable Launch Process Check is on.")]
        [EbxFieldMeta(EbxFieldType.Boolean)]
        public bool TerminateStuckGame { get; set; } = true;

        [Category("Mods & ModData")]
        [DisplayName("Enable Custom Mods Directory")]
        [Description("Enables custom directory to load mods from.")]
        [EbxFieldMeta(EbxFieldType.Boolean)]
        public bool UseCustomModsDirectory { get; set; } = false;

        [Category("Mods & ModData")]
        [DisplayName("Custom Mods Directory")]
        [Description("Select directory to load mods from upon startup.")]
        [Editor(typeof(FrostyCustomPathEditor))]
        [DependsOn("UseCustomModsDirectory")]
        public string CustomModsDirectory { get; set; } = "";

        // [DAI fix] one setting for the cache window and the apply window
        [Category("Interface")]
        [DisplayName("Progress View")]
        [Description("How much the progress windows show while Frosty builds its cache and applies mods.\r\n\r\nDetailed (the default) shows a taller window with each step, how long it took, and what each CPU thread is working on. Simple shows only the current step on one line.")]
        [EbxFieldMeta(EbxFieldType.Struct)]
        [Editor(typeof(FrostyLocalizationLanguageDataEditor))]
        public CustomComboData<string, string> ProgressView { get; set; }

        private static readonly List<string> progressViewNames = new List<string>() { "Detailed", "Simple" };

        // a method, since the options page would list a static property as a setting
        public static bool IsDetailedProgressView() => Config.Get<string>("ProgressView", "Detailed") != "Simple";

        // [DAI fix] color theme for the Mod Manager windows, applied as soon as the options are saved
        [Category("Interface")]
        [DisplayName("Theme")]
        [Description("Colors of the Mod Manager's windows: Dark (the default), Light, Frosty, Dark Frosty or Original (the classic Frosty greys). The new theme shows as soon as the options are saved.")]
        [EbxFieldMeta(EbxFieldType.Struct)]
        [Editor(typeof(FrostyLocalizationLanguageDataEditor))]
        public CustomComboData<string, string> Theme { get; set; }

        public static void ApplyTheme() => FrostyThemeManager.Apply(Config.Get<string>("Theme", FrostyThemeManager.DefaultTheme));

        // [DAI fix] for troubleshooting someone else's PC; off by default so normal logs stay the same
        [Category("Logging")]
        [DisplayName("Diagnostic Logging")]
        [Description("Adds extra detail to the Log tab for troubleshooting: the game's version and platform, paths, cache, plugins and settings, each mod being applied and where its data is written, how the game is launched, and how it exits.\r\n\r\nThe whole log is also saved to a file in the Logs folder next to FrostyModManager.exe so it can be sent to whoever is helping. Leave it off normally, or turn on Automatic Log Cleanup below so old files are deleted.")]
        [EbxFieldMeta(EbxFieldType.Boolean)]
        public bool DiagnosticLogging { get; set; } = false;

        [Category("Logging")]
        [DisplayName("Automatic Log Cleanup")]
        [Description("Deletes Diagnostic Logging and ModData Build Log files in the Logs folder that are older than the age chosen below, each time the Mod Manager starts and when these options are saved. Only those Frosty files are deleted.\r\n\r\nWhen off, every file is kept.")]
        [EbxFieldMeta(EbxFieldType.Boolean)]
        public bool AutoLogCleanup { get; set; } = false;

        [Category("Logging")]
        [DisplayName("Delete Logs Older Than")]
        [Description("How old a Diagnostic Logging or ModData Build Log file must be before Automatic Log Cleanup deletes it.")]
        [EbxFieldMeta(EbxFieldType.Struct)]
        [Editor(typeof(FrostyLocalizationLanguageDataEditor))]
        [DependsOn("AutoLogCleanup")]
        public CustomComboData<string, string> LogCleanupDays { get; set; }

        private static readonly List<string> logCleanupDayValues = new List<string>() { "14", "30", "60", "90" };
        private const string DefaultLogCleanupDays = "30";

        public static void ApplyLogCleanup()
        {
            if (!Config.Get<bool>("AutoLogCleanup", false))
                return;

            string days = Config.Get<string>("LogCleanupDays", DefaultLogCleanupDays);
            if (!logCleanupDayValues.Contains(days))
                days = DefaultLogCleanupDays;

            int deleted = DiagnosticLog.DeleteOldFiles(DiagnosticLog.LogsFolder, int.Parse(days));
            if (deleted > 0)
                App.Logger?.Log("Automatic Log Cleanup deleted {0} log file(s) older than {1} days.", deleted, days);
        }

        public static void ApplyDiagnosticSetting()
        {
            if (!Config.Get<bool>("DiagnosticLogging", false))
            {
                DiagnosticLog.Stop();
                return;
            }
            if (DiagnosticLog.Enabled)
                return;

            DiagnosticLog.Start(App.Logger, DiagnosticLog.LogsFolder, (App.Logger as FrostyCore.FrostyLogger)?.LogText);
            DiagnosticReport.LogProfile();
        }

        // [DAI fix] limits for Frosty's worker threads (cache build and applying mods)
        [Category("Performance")]
        [DisplayName("CPU Threads")]
        [Description("How many CPU threads (logical processors in Task Manager) Frosty uses when it builds its cache and applies mods.\r\n\r\nThe default leaves two free so the PC stays responsive. Lower it if your PC gets unstable under full load.")]
        [EbxFieldMeta(EbxFieldType.Struct)]
        [Editor(typeof(FrostyLocalizationLanguageDataEditor))]
        public CustomComboData<string, string> WorkerThreads { get; set; }

        [Category("Performance")]
        [DisplayName("CPU Priority")]
        [Description("Priority of that work compared with other programs. Low (the default) lets everything else go first, so the PC stays usable; the game launched afterwards is not affected.")]
        [EbxFieldMeta(EbxFieldType.Struct)]
        [Editor(typeof(FrostyLocalizationLanguageDataEditor))]
        public CustomComboData<string, string> WorkerPriority { get; set; }

        private static readonly List<string> priorityNames = new List<string>() { "Low", "Below Normal", "Normal", "Above Normal", "High" };
        private static readonly System.Threading.ThreadPriority[] priorityValues =
        {
            System.Threading.ThreadPriority.Lowest, System.Threading.ThreadPriority.BelowNormal, System.Threading.ThreadPriority.Normal,
            System.Threading.ThreadPriority.AboveNormal, System.Threading.ThreadPriority.Highest
        };

        public static void ApplyPerformanceSettings()
        {
            if (!int.TryParse(Config.Get<string>("WorkerThreads", ""), out int threads))
                threads = WorkerPool.DefaultThreads;

            int priority = priorityNames.IndexOf(Config.Get<string>("WorkerPriority", "Low"));
            WorkerPool.Configure(threads, priorityValues[(priority < 0) ? 0 : priority]);
        }

        public override void Load()
        {
            base.Load();
            
            RememberChoice = Config.Get<bool>("UseDefaultProfile2", false);
            CommandLineArgs = Config.Get<string>("CommandLineArgs", "", ConfigScope.Game);
            TerminateStuckGame = Config.Get<bool>("TerminateStuckGame", true);
            UseCustomModsDirectory = Config.Get<bool>("UseCustomModsDirectory", false);
            CustomModsDirectory = Config.Get<string>("CustomModsDirectory", "");
            DiagnosticLogging = Config.Get<bool>("DiagnosticLogging", false);
            AutoLogCleanup = Config.Get<bool>("AutoLogCleanup", false);

            List<string> threadValues = new List<string>();
            List<string> threadNames = new List<string>();
            for (int i = 1; i <= Environment.ProcessorCount; i++)
            {
                threadValues.Add(i.ToString());
                threadNames.Add((i == WorkerPool.DefaultThreads) ? i + " (default)" : i.ToString());
            }
            WorkerThreads = new CustomComboData<string, string>(threadValues, threadNames);
            WorkerThreads.SelectedIndex = Math.Max(0, Math.Min(WorkerPool.MaxThreads, threadValues.Count) - 1);

            WorkerPriority = new CustomComboData<string, string>(priorityNames, priorityNames);
            WorkerPriority.SelectedIndex = Math.Max(0, priorityNames.IndexOf(Config.Get<string>("WorkerPriority", "Low")));

            ProgressView = new CustomComboData<string, string>(progressViewNames, progressViewNames);
            ProgressView.SelectedIndex = IsDetailedProgressView() ? 0 : 1;

            List<string> themeNames = FrostyThemeManager.ThemeNames.ToList();
            Theme = new CustomComboData<string, string>(themeNames, themeNames.Select((t, i) => FrostyThemeManager.ThemeDisplayNames[i] + ((t == FrostyThemeManager.DefaultTheme) ? " (default)" : "")).ToList());
            Theme.SelectedIndex = Math.Max(0, themeNames.IndexOf(Config.Get<string>("Theme", FrostyThemeManager.DefaultTheme)));

            LogCleanupDays = new CustomComboData<string, string>(logCleanupDayValues, logCleanupDayValues.Select(d => (d == DefaultLogCleanupDays) ? d + " days (default)" : d + " days").ToList());
            int dayIndex = logCleanupDayValues.IndexOf(Config.Get<string>("LogCleanupDays", DefaultLogCleanupDays));
            LogCleanupDays.SelectedIndex = (dayIndex < 0) ? logCleanupDayValues.IndexOf(DefaultLogCleanupDays) : dayIndex;
        }

        public override void Save()
        {
            base.Save();
            
            Config.Add("UseDefaultProfile2", RememberChoice);
            Config.Add("CommandLineArgs", CommandLineArgs, ConfigScope.Game);
            Config.Add("TerminateStuckGame", TerminateStuckGame);
            
            Config.Add("UseCustomModsDirectory", UseCustomModsDirectory);
            Config.Add("CustomModsDirectory", CustomModsDirectory);

            Config.Add("WorkerThreads", WorkerThreads.SelectedValue);
            Config.Add("WorkerPriority", WorkerPriority.SelectedValue);
            Config.Add("ProgressView", ProgressView.SelectedValue);
            Config.Add("Theme", Theme.SelectedValue);
            Config.Add("DiagnosticLogging", DiagnosticLogging);
            Config.Add("AutoLogCleanup", AutoLogCleanup);
            Config.Add("LogCleanupDays", LogCleanupDays.SelectedValue);
            ApplyPerformanceSettings();
            ApplyLogCleanup();
            ApplyDiagnosticSetting();
            ApplyTheme();

            if (RememberChoice)
                Config.Add("DefaultProfile2", ProfilesLibrary.ProfileName);
            else
                Config.Remove("DefaultProfile2");

            Config.Save();
        }

        public override bool Validate()
        {
            // Custom Mods Directory
            if (UseCustomModsDirectory)
            {
                string[] folders = CustomModsDirectory.Split('\\');

                foreach (var folder in folders)
                {
                    if (ProfilesLibrary.HasProfile(folder))
                    {
                        FrostyMessageBox.Show($"Path cannot contain '{folder}'", "Custom Mods Directory");
                        return false;
                    }
                }
            }
            
            return true;
        }
    }

    // [DAI fix] picks the patch version ModData reports, so saves made with DAI Mod Manager load
    [DisplayName("Dragon Age Inquisition")]
    public class DaiPatchVersionOptionsData : OptionsExtension
    {
        [Category("Saves")]
        [DisplayName("Patch Version Reported to the Game")]
        [Description("Saves made with DAI Mod Manager record a patch one higher than the game's real patch, and won't load at the real patch. Pick the patch those saves need.\r\n\r\nSaves made while a higher patch is picked only load when the game is launched through Frosty. Takes effect on the next launch.")]
        [EbxFieldMeta(EbxFieldType.Struct)]
        [Editor(typeof(FrostyLocalizationLanguageDataEditor))]
        public CustomComboData<string, string> PatchVersion { get; set; }

        [Category("Saves")]
        [DisplayName("Ask When Saves Need a Higher Patch")]
        [Description("When launching, ask whether to report a higher patch if some saves need one.")]
        [EbxFieldMeta(EbxFieldType.Boolean)]
        public bool AskForHigherPatch { get; set; } = true;

        private int gamePatch;
        private int loadedVersion;

        public override void Load()
        {
            base.Load();

            gamePatch = DaiPatchVersion.GetGamePatchVersion(Config.Get<string>("GamePath", "", ConfigScope.Game));
            loadedVersion = DaiPatchVersion.GetChosenVersion(gamePatch);
            SortedDictionary<int, int> saves = DaiPatchVersion.ScanSaves();

            List<string> values = new List<string>() { "0" };
            List<string> names = new List<string>() { (gamePatch < 0) ? "Game's real patch" : $"Patch {gamePatch} (game's real patch)" };
            if (gamePatch >= 0)
            {
                // a few extra numbers for saves copied from another PC
                SortedSet<int> versions = new SortedSet<int>(saves.Keys.Where(v => v > gamePatch)) { gamePatch + 1, gamePatch + 2, gamePatch + 3, loadedVersion };
                versions.Remove(gamePatch);
                foreach (int version in versions)
                {
                    saves.TryGetValue(version, out int count);
                    values.Add(version.ToString());
                    if (count > 0)
                        names.Add($"Patch {version} ({count} {((count == 1) ? "save needs" : "saves need")} this)");
                    else
                        names.Add((version > gamePatch + 1) ? $"Patch {version} (experimental)" : $"Patch {version}");
                }
            }

            PatchVersion = new CustomComboData<string, string>(values, names);
            PatchVersion.SelectedIndex = Math.Max(0, values.IndexOf((loadedVersion > gamePatch) ? loadedVersion.ToString() : "0"));
            AskForHigherPatch = Config.Get<bool>(DaiPatchVersion.AskKey, true, ConfigScope.Game);
        }

        public override bool Validate()
        {
            int picked = Math.Max(gamePatch, int.Parse(PatchVersion.SelectedValue));
            if (gamePatch < 0 || picked >= loadedVersion)
                return true;

            return FrostyMessageBox.Show($"Saves made while patch {loadedVersion} was reported won't load at patch {picked}. While 'Ask When Saves Need a Higher Patch' is ticked, Frosty offers the higher patch again at launch.\r\n\r\nChange it anyway?", "Dragon Age Inquisition", MessageBoxButton.YesNo) == MessageBoxResult.Yes;
        }

        public override void Save()
        {
            base.Save();

            Config.Add(DaiPatchVersion.VersionKey, int.Parse(PatchVersion.SelectedValue), ConfigScope.Game);
            Config.Add(DaiPatchVersion.AskKey, AskForHigherPatch, ConfigScope.Game);
        }
    }

    /// <summary>
    /// Interaction logic for OptionsWindow.xaml
    /// </summary>
    public partial class OptionsWindow
    {
        private List<OptionsExtension> optionDataList = new List<OptionsExtension>();

        public OptionsWindow()
        {
            InitializeComponent();
        }

        private void OptionsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Add default settings
            if (App.PluginManager.IsManagerType(PluginManagerType.Editor))
                optionDataList.Add(new EditorOptionsData());
            else
            {
                optionDataList.Add(new ModManagerOptionsData());
                if (ProfilesLibrary.IsLoaded(ProfileVersion.DragonAgeInquisition))
                    optionDataList.Add(new DaiPatchVersionOptionsData());
            }

            // Add plugin settings
            foreach (var optionExtType in App.PluginManager.OptionsExtensions)
                optionDataList.Add((OptionsExtension)Activator.CreateInstance(optionExtType));

            foreach (var optionData in optionDataList)
            {
                optionData.Load();

                FrostyTabItem ti = new FrostyTabItem
                {
                    Header = new OptionsDisplayNameToStringConverter().Convert(optionData, null, null, null),
                    Content = new FrostyPropertyGrid() {Object = optionData}
                };
                optionsTabControl.Items.Add(ti);
            }

            optionsTabControl.SelectedIndex = 0;
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void saveButton_Click(object sender, RoutedEventArgs e)
        {
            if (Validate())
            {
                foreach (var optionData in optionDataList)
                    optionData.Save();

                Config.Save();

                Close();
            }
        }

        private bool Validate()
        {
            foreach (var optionData in optionDataList)
            {
                if (!optionData.Validate())
                    return false;
            }
            return true;
        }
    }
}
