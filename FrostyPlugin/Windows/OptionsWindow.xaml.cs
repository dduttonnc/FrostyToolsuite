using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Controls.Editors;
using Frosty.Core.Misc;
using FrostySdk.Attributes;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using FrostySdk;
using System.IO;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Media;

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

    [DisplayName("Editor Options")]
    public class EditorOptionsData : OptionsExtension
    {
        [Category("Localization")]
        [Description("Selects which localized language files to read from the game files.")]
        [EbxFieldMeta(EbxFieldType.Struct)]
        [Editor(typeof(FrostyLocalizationLanguageDataEditor))]
        public CustomComboData<string, string> Language { get; set; }

        [Category("Autosave")]
        [DisplayName("Enabled")]
        [Description("Enables autosaving for projects.")]
        [EbxFieldMeta(EbxFieldType.Boolean)]
        public bool AutosaveEnabled { get; set; } = true;
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

        [Category("Asset")]
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

        [Category("Update Checking")]
        [DisplayName("Check for Updates")]
        [Description("Check Github for Frosty updates on startup")]
        [EbxFieldMeta(EbxFieldType.Boolean)]
        public bool UpdateCheck { get; set; } = true;

        [Category("Update Checking")]
        [DisplayName("Check for Prerelease Updates")]
        [Description("Check Github for Frosty Alpha and Beta updates on startup")]
        [EbxFieldMeta(EbxFieldType.Boolean)]
#if FROSTY_ALPHA
        public bool UpdateCheckPrerelease { get; set; } = true;
#elif FROSTY_BETA
        public bool UpdateCheckPrerelease { get; set; } = true;
#else
        public bool UpdateCheckPrerelease { get; set; } = false;
#endif

        [Category("General")]
        [DisplayName("CAS Max File Size")]
        [Description("Change the maximum size of written cas files when applying mods.\r\n\r\nHigher Values decrease system stability but ensure mod compatibility.")]
        [EbxFieldMeta(EbxFieldType.Struct)]
        [Editor(typeof(FrostyLocalizationLanguageDataEditor))]
        public CustomComboData<string, string> MaxCasFileSize { get; set; }

        [Category("General")]
        [DisplayName("Command Line Arguments")]
        [Description("Command line arguments to run on launch.")]
        [EbxFieldMeta(EbxFieldType.Boolean)]
        public string CommandLineArgs { get; set; } = "";

        public override void Load()
        {
            List<string> langs = GetLocalizedLanguages();
            Language = new CustomComboData<string, string>(langs, langs) {SelectedIndex = langs.IndexOf(Config.Get<string>("Language", "English", ConfigScope.Game))};
            
            AutosaveEnabled = Config.Get<bool>("AutosaveEnabled", true);
            AutosavePeriod = Config.Get<int>("AutosavePeriod", 5);
            AutosaveMaxSaves = Config.Get<int>("AutosaveMaxCount", 10);

            TextEditorTabSize = Config.Get<int>("TextEditorTabSize", 4);
            TextEditorIndentOnEnter = Config.Get<bool>("TextEditorIndentOnEnter", false);

            DiscordEnabled = Config.Get<bool>("DiscordRPCEnabled", false);
            ModSettingsAuthor = Config.Get<string>("ModAuthor", "");

            AssetDisplayModuleInId = Config.Get<bool>("DisplayModuleInId", false);
            RememberChoice = Config.Get<bool>("UseDefaultProfile", false);

            UpdateCheck = Config.Get<bool>("UpdateCheck", true);
            UpdateCheckPrerelease = Config.Get<bool>("UpdateCheckPrerelease", false);

            List<string> sizes = new List<string>() { "1GB", "512MB", "256MB" };
            MaxCasFileSize = new CustomComboData<string, string>(sizes, sizes);
            MaxCasFileSize.SelectedIndex = sizes.IndexOf(Config.Get<string>("MaxCasFileSize", "1GB"));

            CommandLineArgs = Config.Get<string>("CommandLineArgs", "", ConfigScope.Game);

            DefaultInstallation = CheckFileAssociation();

            //Language = new CustomComboData<string, string>(langs, langs) { SelectedIndex = langs.IndexOf(Config.Get<string>("Init", "Language", "English")) };

            //AutosaveEnabled = Config.Get<bool>("Autosave", "Enabled", true);
            //AutosavePeriod = Config.Get<int>("Autosave", "Period", 5);
            //AutosaveMaxSaves = Config.Get<int>("Autosave", "MaxCount", 10);

            //TextEditorTabSize = Config.Get<int>("TextEditor", "TabSize", 4);
            //TextEditorIndentOnEnter = Config.Get<bool>("TextEditor", "IndentOnEnter", false);

            //DiscordEnabled = Config.Get<bool>("DiscordRPC", "Enabled", false);
            //ModSettingsAuthor = Config.Get<string>("ModSettings", "Author", "");

            //AssetDisplayModuleInId = Config.Get<bool>("Asset", "DisplayModuleInId", true);
            //RememberChoice = Config.Get<bool>("Init", "RememberChoice", false);
        }

        public override void Save()
        {
            Config.Add("AutosaveEnabled", AutosaveEnabled);
            Config.Add("AutosavePeriod", AutosavePeriod);
            Config.Add("AutosaveMaxCount", AutosaveMaxSaves);

            Config.Add("TextEditorTabSize", TextEditorTabSize);
            Config.Add("TextEditorIndentOnEnter", TextEditorIndentOnEnter);

            Config.Add("DiscordRPCEnabled", DiscordEnabled);
            Config.Add("ModAuthor", ModSettingsAuthor);
            Config.Add("DisplayModuleInId", AssetDisplayModuleInId);
            Config.Add("UseDefaultProfile", RememberChoice);

            Config.Add("UpdateCheck", UpdateCheck);
            Config.Add("UpdateCheckPrerelease", UpdateCheckPrerelease);

            Config.Add("MaxCasFileSize", MaxCasFileSize.SelectedName);

            Config.Add("CommandLineArgs", CommandLineArgs, ConfigScope.Game);

            if (RememberChoice)
                Config.Add("DefaultProfile", ProfilesLibrary.ProfileName);
            else
                Config.Remove("DefaultProfile");

            Config.Add("Language", Language.SelectedName, ConfigScope.Game);

            Config.Save();

            LocalizedStringDatabase.Current.Initialize();

            // Create file association if enabled and doesnt already exist
            if (DefaultInstallation && !CheckFileAssociation())
            {
                CreateFileAssociation();
            }

            //Config.Add("Autosave", "Enabled", AutosaveEnabled);
            //Config.Add("Autosave", "Period", AutosavePeriod);
            //Config.Add("Autosave", "MaxCount", AutosaveMaxSaves);

            //Config.Add("TextEditor", "TabSize", TextEditorTabSize);
            //Config.Add("TextEditor", "IndentOnEnter", TextEditorIndentOnEnter);

            //Config.Add("DiscordRPC", "Enabled", DiscordEnabled);
            //Config.Add("ModSettings", "Author", ModSettingsAuthor);
            //Config.Add("Asset", "DisplayModuleInId", AssetDisplayModuleInId);
            //Config.Add("Init", "RememberChoice", RememberChoice);
            //Config.Add("Init", "Language", Language.SelectedName);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        private void CreateFileAssociation()
        {
            string Extension = ".fbproject";
            string KeyName = "frostyproject";
            string OpenWith = Assembly.GetEntryAssembly().Location;
            string FileDescription = "Frosty Project";
            string FileIcon = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Icons", "fbproject.ico");

            try
            {
                RegistryKey BaseKey = Registry.CurrentUser.CreateSubKey($"Software\\Classes\\{Extension}");
                BaseKey.SetValue("", KeyName);

                RegistryKey OpenMethod = Registry.CurrentUser.CreateSubKey($"Software\\Classes\\{KeyName}");
                OpenMethod.SetValue("", FileDescription);
                OpenMethod.CreateSubKey("DefaultIcon").SetValue("", $"\"{FileIcon}\"");

                RegistryKey Shell = OpenMethod.CreateSubKey("shell");
                Shell.CreateSubKey("edit").CreateSubKey("command").SetValue("", $"\"{OpenWith}\" \"%1\"");
                Shell.CreateSubKey("open").CreateSubKey("command").SetValue("", $"\"{OpenWith}\" \"%1\"");
                BaseKey.Close();
                OpenMethod.Close();
                Shell.Close();

                RegistryKey CurrentUser = Registry.CurrentUser.OpenSubKey($"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\FileExts\\{Extension}", true);
                CurrentUser.DeleteSubKey("UserChoice", false);
                CurrentUser.Close();

                // Tell explorer the file association has been changed
                SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                SystemSounds.Hand.Play();
                App.Logger.LogError($"Unable to Set File Association: {ex.Message}");
            }
        }

        private bool CheckFileAssociation()
        {
            // Checks the registry for the current association against current frosty installation
            string KeyName = "frostyproject";
            string OpenWith = Assembly.GetEntryAssembly().Location;

            try
            {
                string openCommand = Registry.CurrentUser.OpenSubKey("Software\\Classes\\" + KeyName).OpenSubKey("shell").OpenSubKey("open").OpenSubKey("command").GetValue("").ToString();
                return openCommand.Contains(OpenWith);
            }
            catch
            {
                return false;
            }
        }

        public override bool Validate()
        {
            return true;
        }

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
    public class ModManagerOptionsData : OptionsExtension
    {
        [Category("Manager")]
        [DisplayName("Remember Profile Choice")]
        [Description("If true, the last chosen profile will be automatically loaded when starting the Mod Manager.")]
        [EbxFieldMeta(EbxFieldType.Boolean)]
        public bool RememberChoice { get; set; } = false;

        [Category("Manager")]
        [DisplayName("Command Line Arguments")]
        [Description("Command line arguments to run on launch.")]
        [EbxFieldMeta(EbxFieldType.Boolean)]
        public string CommandLineArgs { get; set; } = "";

        [Category("Manager")]
        [DisplayName("Delete Mods with Collection")]
        [Description("If true, deleting a collection will also delete the mods inside of it.")]
        [EbxFieldMeta(EbxFieldType.Boolean)]
        public bool DeleteCollectionMods { get; set; } = true;

        [Category("Manager")]
        [DisplayName("Custom Mods Directory")]
        [Description("Select directory to load mods from upon startup.")]
        [EbxFieldMeta(EbxFieldType.String)]
        [DependsOn("UseCustomModsDirectory")]
        public string CustomModsDirectory { get; set; }

        [Category("Update Checking")]
        [DisplayName("Check for Updates")]
        [Description("Check Github for Frosty updates on startup")]
        [EbxFieldMeta(EbxFieldType.Boolean)]
        public bool UpdateCheck { get; set; } = true;

        [Category("Update Checking")]
        [DisplayName("Check for Prerelease Updates")]
        [Description("Check Github for Frosty Alpha and Beta updates on startup")]
        [EbxFieldMeta(EbxFieldType.Boolean)]
#if FROSTY_ALPHA
        public bool UpdateCheckPrerelease { get; set; } = true;
#elif FROSTY_BETA
        public bool UpdateCheckPrerelease { get; set; } = true;
#else
        public bool UpdateCheckPrerelease { get; set; } = false;
#endif

        [Category("General")]
        [DisplayName("CAS Max File Size")]
        [Description("Change the maximum size of written cas files when applying mods.\r\n\r\nHigher Values decrease system stability but ensure mod compatibility.")]
        [EbxFieldMeta(EbxFieldType.Struct)]
        [Editor(typeof(FrostyLocalizationLanguageDataEditor))]
        public CustomComboData<string, string> MaxCasFileSize { get; set; }

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

        //[Category("Mod View")]
        //[DisplayName("Collapse categories by default")]
        //[Description("Automatically collapse mod categories in the Available Mods list on startup.")]
        //[EbxFieldMeta(EbxFieldType.Boolean)]
        //public bool CollapseCategories { get; set; } = false;

        //[Category("Mod View")]
        //[DisplayName("Applied Mod Icons")]
        //[Description("Hide the applied mod icons in the Applied Mod list.")]
        //[EbxFieldMeta(EbxFieldType.Boolean)]
        //public bool AppliedModIcons { get; set; } = true;

        public override void Load()
        {
            RememberChoice = Config.Get<bool>("UseDefaultProfile", false);
            CommandLineArgs = Config.Get<string>("CommandLineArgs", "", ConfigScope.Game);
            DeleteCollectionMods = Config.Get<bool>("DeleteCollectionMods", true);

            CustomModsDirectory = Config.Get<string>("CustomModsDirectory", "");

            UpdateCheck = Config.Get<bool>("UpdateCheck", true);

            UpdateCheckPrerelease = Config.Get<bool>("UpdateCheckPrerelease", false);

            List<string> sizes = new List<string>() { "1GB", "512MB", "256MB" };
            MaxCasFileSize = new CustomComboData<string, string>(sizes, sizes);
            MaxCasFileSize.SelectedIndex = sizes.IndexOf(Config.Get<string>("MaxCasFileSize", "1GB"));

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

            //CollapseCategories = Config.Get("CollapseCategories", false);
            //AppliedModIcons = Config.Get("AppliedModIcons", true);
        }

        public override void Save()
        {
            Config.Add("UseDefaultProfile", RememberChoice);
            Config.Add("CommandLineArgs", CommandLineArgs, ConfigScope.Game);
            Config.Add("DeleteCollectionMods", DeleteCollectionMods);

            if (Directory.Exists(CustomModsDirectory))
            {
                Config.Add("CustomModsDirectory", CustomModsDirectory);
            }

            Config.Add("UpdateCheck", UpdateCheck);
            Config.Add("UpdateCheckPrerelease", UpdateCheckPrerelease);

            Config.Add("MaxCasFileSize", MaxCasFileSize.SelectedName);

            Config.Add("WorkerThreads", WorkerThreads.SelectedValue);
            Config.Add("WorkerPriority", WorkerPriority.SelectedValue);
            ApplyPerformanceSettings();

            //Config.Add("CollapseCategories", CollapseCategories);
            //Config.Add("AppliedModIcons", AppliedModIcons);

            if (RememberChoice)
                Config.Add("DefaultProfile", ProfilesLibrary.ProfileName);
            else
                Config.Remove("DefaultProfile");

            Config.Save();
        }

        public override bool Validate()
        {
            return true;
        }
    }

    /// <summary>
    /// Interaction logic for OptionsWindow.xaml
    /// </summary>
    public partial class OptionsWindow : FrostyDockableWindow
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
                optionDataList.Add(new ModManagerOptionsData());

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
                //Config.Save(App.configFilename);

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
