﻿using Frosty.Controls;
using FrostySdk.IO;
using Microsoft.CSharp;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FrostyModManager
{
    class ConfigElement
    {
        public object CurrentValue
        {
            get => currentValue;
            internal set => currentValue = value;
        }

        protected string id;
        protected string displayName;
        protected object currentValue;

        public ConfigElement(string inId, string inDisplayName)
        {
            id = inId;
            displayName = inDisplayName;
        }

        public override string ToString() => displayName;

        public virtual string DisplayText => currentValue?.ToString() ?? "";

        // culture-independent, for Reconfigure
        public virtual string SaveText
        {
            get
            {
                if (currentValue is float f)
                    return f.ToString("R", CultureInfo.InvariantCulture);
                if (currentValue is IFormattable formattable)
                    return formattable.ToString(null, CultureInfo.InvariantCulture);
                return currentValue?.ToString() ?? "";
            }
        }
    }

    // [DAI fix] one per <resource> entry of the .daimod, in file order
    class ScriptResourceEntry
    {
        public string Name;
        public int ResourceId;
        public bool Enabled = true;
    }

    class ConfigElement<T> : ConfigElement
    {
        T defValue;
        T minValue;
        T maxValue;

        public ConfigElement(string inId, string inDisplayName, T inDefValue, T inMinValue, T inMaxValue)
            : base(inId, inDisplayName)
        {
            defValue = inDefValue;
            minValue = inMinValue;
            maxValue = inMaxValue;
            currentValue = defValue;
        }
    }

    // the value is the dropdown position, as in DAI Mod Manager
    class EnumConfigElement : ConfigElement
    {
        string[] names;
        public EnumConfigElement(string inId, string inDisplayName, string[] inNames, int inDefValue)
            : base(inId, inDisplayName)
        {
            names = inNames;
            currentValue = inDefValue;
        }

        public override string DisplayText => (currentValue is int i && i >= 0 && i < names.Length) ? names[i] : base.DisplayText;

        public override string SaveText => DisplayText;
    }

    class StringConfigElement : ConfigElement
    {
        string defValue;
        public StringConfigElement(string inId, string inDisplayName, string inDefValue)
            : base(inId, inDisplayName)
        {
            defValue = inDefValue;
            CurrentValue = defValue;
        }
    }

    class FrostyConfigElementsList : DAIMod.ModConfigElementsList
    {
        public int NumElements => elements.Count;

        private ConfigWindow theWindow;
        private Dictionary<string, ConfigElement> elements = new Dictionary<string, ConfigElement>();

        // [DAI fix] applied before the script runs, so a value still being typed isn't lost
        private List<Action> commitActions = new List<Action>();

        // [DAI fix] settings chosen last time, by id
        private IDictionary<string, string> savedValues;

        // mods converted before settings were saved: "<name> <value>" lines at the end of the description
        private string[] legacyLines;

        public FrostyConfigElementsList(ConfigWindow inWindow, IDictionary<string, string> inSavedValues = null, string inLegacyDescription = null)
        {
            theWindow = inWindow;
            savedValues = inSavedValues;
            if (!string.IsNullOrEmpty(inLegacyDescription))
                legacyLines = inLegacyDescription.Replace("\r\n", "\n").Split('\n');
        }

        public IEnumerable<KeyValuePair<string, ConfigElement>> Elements => elements;

        private bool TryGetSaved(string id, string name, out string value) => TryGetSaved(id, name, out value, out bool _);

        private bool TryGetSaved(string id, string name, out string value, out bool fromDescription)
        {
            fromDescription = false;
            if (savedValues != null && savedValues.TryGetValue(id, out value) && value != null)
                return true;

            if (legacyLines != null && !string.IsNullOrEmpty(name))
            {
                string prefix = name + " ";
                for (int i = legacyLines.Length - 1; i >= 0; i--)
                {
                    if (legacyLines[i].StartsWith(prefix, StringComparison.Ordinal))
                    {
                        value = legacyLines[i].Substring(prefix.Length);
                        fromDescription = true;
                        return true;
                    }
                }
            }

            value = null;
            return false;
        }

        public void CommitAll()
        {
            foreach (Action commit in commitActions)
                commit();
        }

        private void AddElement(string id, ConfigElement ce)
        {
            // [DAI fix] a duplicate id used to throw; last one wins
            elements[id] = ce;
        }

        private static bool TryParseFloat(string text, out float value)
        {
            // [DAI fix] accept the user's locale and plain "1.5"
            return float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
                || float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public override void AddIntElement(string id, string name, int defValue, int minValue, int maxValue)
        {
            ConfigElement<int> ce = new ConfigElement<int>(id, name, defValue, minValue, maxValue);
            AddElement(id, ce);

            // [DAI fix] restore saved value
            int initial = defValue;
            if (TryGetSaved(id, name, out string saved) && int.TryParse(saved.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int savedInt))
                initial = Math.Max(minValue, Math.Min(maxValue, savedInt));
            ce.CurrentValue = initial;

            Label lbl = new Label {Content = name};

            TextBox tb = new TextBox
            {
                Text = initial.ToString(),
                Height = 24,
                Margin = new Thickness(1),
                Padding = new Thickness(2, 0, 0, 0),
                BorderThickness = new Thickness(1),
                VerticalContentAlignment = VerticalAlignment.Center
            };

            Action commit = () =>
            {
                if (int.TryParse(tb.Text, out int newValue))
                {
                    if (newValue < minValue)
                        newValue = minValue;
                    else if (newValue > maxValue)
                        newValue = maxValue;
                }
                else
                {
                    newValue = defValue;
                    FrostyMessageBox.Show("Invalid value entered for \"" + name + "\", using the default instead", "Frosty Mod Manager");
                }

                tb.Text = newValue.ToString();
                ce.CurrentValue = newValue;
            };
            commitActions.Add(commit);

            tb.LostFocus += (o, e) => commit();
            tb.KeyDown += (o, e) => { if (e.Key == Key.Enter) { tb.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)); } };

            theWindow.valuesStackPanel.Children.Add(tb);
            theWindow.namesStackPanel.Children.Add(lbl);
        }

        public override void AddFloatElement(string id, string name, float defValue, float minValue, float maxValue)
        {
            ConfigElement<float> ce = new ConfigElement<float>(id, name, defValue, minValue, maxValue);
            AddElement(id, ce);

            // [DAI fix] restore saved value
            float initial = defValue;
            if (TryGetSaved(id, name, out string saved) && TryParseFloat(saved.Trim(), out float savedFloat))
                initial = Math.Max(minValue, Math.Min(maxValue, savedFloat));
            ce.CurrentValue = initial;

            Label lbl = new Label {Content = name};

            TextBox tb = new TextBox
            {
                Text = initial.ToString("F3"),
                Height = 24,
                Margin = new Thickness(1),
                Padding = new Thickness(2, 0, 0, 0),
                BorderThickness = new Thickness(1),
                VerticalContentAlignment = VerticalAlignment.Center
            };

            Action commit = () =>
            {
                if (TryParseFloat(tb.Text, out float newValue))
                {
                    if (newValue < minValue)
                        newValue = minValue;
                    else if (newValue > maxValue)
                        newValue = maxValue;
                }
                else
                {
                    newValue = defValue;
                    FrostyMessageBox.Show("Invalid value entered for \"" + name + "\", using the default instead", "Frosty Mod Manager");
                }

                tb.Text = newValue.ToString("F3");
                ce.CurrentValue = newValue;
            };
            commitActions.Add(commit);

            tb.LostFocus += (o, e) => commit();
            tb.KeyDown += (o, e) => { if (e.Key == Key.Enter) { tb.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)); } };

            theWindow.valuesStackPanel.Children.Add(tb);
            theWindow.namesStackPanel.Children.Add(lbl);
        }

        public override void AddBoolElement(string id, string name, bool defValue)
        {
            ConfigElement<bool> ce = new ConfigElement<bool>(id, name, defValue, false, true);
            AddElement(id, ce);

            // [DAI fix] restore saved value
            bool initial = defValue;
            if (TryGetSaved(id, name, out string saved) && bool.TryParse(saved.Trim(), out bool savedBool))
                initial = savedBool;
            ce.CurrentValue = initial;

            Label lbl = new Label {Content = name};

            CheckBox cb = new CheckBox
            {
                IsChecked = initial,
                Height = 24,
                Margin = new Thickness(1),
                VerticalContentAlignment = VerticalAlignment.Center
            };

            cb.Checked += (o, e) => { ce.CurrentValue = true; };
            cb.Unchecked += (o, e) => { ce.CurrentValue = false; };

            theWindow.valuesStackPanel.Children.Add(cb);
            theWindow.namesStackPanel.Children.Add(lbl);
        }

        public override void AddEnumElement(string id, string name, Type enumType, int defValue)
        {
            // [DAI fix] the script gets the dropdown position and defValue is one, as in DAI Mod Manager (1.0.6.4-dai.1 to 1.7.5 passed the enum value)
            string[] names = Enum.GetNames(enumType);
            EnumConfigElement ce = new EnumConfigElement(id, name, names, defValue);
            AddElement(id, ce);

            Label lbl = new Label {Content = name};

            int selectedIndex = (defValue >= 0 && defValue < names.Length) ? defValue : -1;

            // saved as the name; settings files from before hold the enum value, old descriptions the position
            if (TryGetSaved(id, name, out string saved, out bool fromDescription))
            {
                string savedText = saved.Trim();
                int savedIndex = Array.IndexOf(names, savedText);
                if (savedIndex < 0 && int.TryParse(savedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int savedNumber))
                    savedIndex = fromDescription ? savedNumber : DaiEnumIndexOfValue(enumType, savedNumber);
                if (savedIndex >= 0 && savedIndex < names.Length)
                {
                    selectedIndex = savedIndex;
                    ce.CurrentValue = savedIndex;
                }
            }

            ComboBox cb = new ComboBox
            {
                ItemsSource = names,
                SelectedIndex = selectedIndex,
                Padding = new Thickness(4, 0, 0, 0),
                Margin = new Thickness(1),
                VerticalContentAlignment = VerticalAlignment.Center,
                Height = 24
            };

            cb.SelectionChanged += (o, e) =>
            {
                if (cb.SelectedIndex >= 0)
                    ce.CurrentValue = cb.SelectedIndex;
            };

            theWindow.valuesStackPanel.Children.Add(cb);
            theWindow.namesStackPanel.Children.Add(lbl);
        }

        private static int DaiEnumIndexOfValue(Type enumType, int value)
        {
            try
            {
                Array rawValues = Enum.GetValues(enumType);
                for (int i = 0; i < rawValues.Length; i++)
                {
                    if (Convert.ToInt64(rawValues.GetValue(i), CultureInfo.InvariantCulture) == value)
                        return i;
                }
            }
            catch (Exception)
            {
                // treat it as a position
            }
            return value;
        }

        private static bool TryParseColor(string text, out DAIMod.Color color)
        {
            color = null;
            try
            {
                System.Windows.Media.Color c = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(text.Trim());
                color = new DAIMod.Color(c.R / 255.0f, c.G / 255.0f, c.B / 255.0f, c.A / 255.0f);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static System.Windows.Media.Color ToMediaColor(DAIMod.Color color) => (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color.ToString());

        // [DAI fix] colour settings, which DAI Mod Manager has; typed as #RRGGBB or #AARRGGBB
        public override void AddColorElement(string id, string name, DAIMod.Color defValue)
        {
            DAIMod.Color def = defValue ?? new DAIMod.Color(1, 1, 1, 1);
            ConfigElement ce = new ConfigElement(id, name) { CurrentValue = def };
            AddElement(id, ce);

            DAIMod.Color initial = def;
            if (TryGetSaved(id, name, out string saved) && TryParseColor(saved, out DAIMod.Color savedColor))
                initial = savedColor;
            ce.CurrentValue = initial;

            Label lbl = new Label {Content = name};

            Border swatch = new Border
            {
                Width = 22,
                Margin = new Thickness(1),
                BorderThickness = new Thickness(1),
                BorderBrush = System.Windows.Media.Brushes.Gray,
                Background = new System.Windows.Media.SolidColorBrush(ToMediaColor(initial))
            };

            TextBox tb = new TextBox
            {
                Text = initial.ToString(),
                Height = 24,
                Margin = new Thickness(1),
                Padding = new Thickness(2, 0, 0, 0),
                BorderThickness = new Thickness(1),
                VerticalContentAlignment = VerticalAlignment.Center
            };

            DockPanel panel = new DockPanel { Height = 26 };
            DockPanel.SetDock(swatch, Dock.Left);
            panel.Children.Add(swatch);
            panel.Children.Add(tb);

            Action commit = () =>
            {
                if (!TryParseColor(tb.Text, out DAIMod.Color newValue))
                {
                    newValue = def;
                    FrostyMessageBox.Show("Invalid colour entered for \"" + name + "\", using the default instead", "Frosty Mod Manager");
                }

                tb.Text = newValue.ToString();
                swatch.Background = new System.Windows.Media.SolidColorBrush(ToMediaColor(newValue));
                ce.CurrentValue = newValue;
            };
            commitActions.Add(commit);

            tb.LostFocus += (o, e) => commit();
            tb.KeyDown += (o, e) => { if (e.Key == Key.Enter) { tb.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)); } };

            theWindow.valuesStackPanel.Children.Add(panel);
            theWindow.namesStackPanel.Children.Add(lbl);
        }

        public override void AddStringElement(string id, string name, string defValue)
        {
            StringConfigElement ce = new StringConfigElement(id, name, defValue ?? "");
            AddElement(id, ce);

            // [DAI fix] restore saved value
            string initial = defValue ?? "";
            if (TryGetSaved(id, name, out string saved))
                initial = saved;
            ce.CurrentValue = initial;

            Label lbl = new Label {Content = name};

            TextBox tb = new TextBox
            {
                Text = initial,
                Height = 24,
                Margin = new Thickness(1),
                Padding = new Thickness(2, 0, 0, 0),
                BorderThickness = new Thickness(1),
                VerticalContentAlignment = VerticalAlignment.Center
            };

            Action commit = () => { ce.CurrentValue = tb.Text; };
            commitActions.Add(commit);

            tb.LostFocus += (o, e) => commit();
            tb.KeyDown += (o, e) => { if (e.Key == Key.Enter) { tb.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)); } };

            theWindow.valuesStackPanel.Children.Add(tb);
            theWindow.namesStackPanel.Children.Add(lbl);
        }

        public ConfigElement GetElement(string name) => !elements.ContainsKey(name) ? null : elements[name];

        public ConfigElement GetElement(int index) => index >= elements.Count ? null : elements.Values.ElementAt(index);
    }

    class FrostyScripting : DAIMod.IScripting
    {
        public FrostyConfigElementsList config;
        public List<byte[]> resources;
        public List<ScriptResourceEntry> entries;
        public List<string> log = new List<string>();

        public FrostyScripting(FrostyConfigElementsList inConfig, List<byte[]> inResources, List<ScriptResourceEntry> inEntries)
        {
            config = inConfig;
            resources = inResources;
            entries = inEntries;
        }

        private ScriptResourceEntry FindEntry(string name)
        {
            ScriptResourceEntry entry = name == null ? null : entries.Find(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
                log.Add("Unable to find resource with name " + name);
            return entry;
        }

        public object GetConfigParam(string name)
        {
            // [DAI fix] 0 for an unknown name instead of crashing, like DAI's default script interface
            ConfigElement elem = config.GetElement(name);
            if (elem == null)
            {
                log.Add("Script asked for unknown setting \"" + name + "\", using 0");
                return 0;
            }
            return elem.CurrentValue;
        }

        // [DAI fix] script log lines used to be discarded
        public void LogLn(string line) => log.Add(line);

        public byte[] GetResourceData(int index)
        {
            if (index < 0 || index >= resources.Count)
                return null;

            using (CasReader reader = new CasReader(new MemoryStream(resources[index])))
                return reader.Read();
        }

        public void SetResourceData(int index, byte[] data)
        {
            if (index < 0 || index >= resources.Count || data == null)
            {
                log.Add("Script tried to replace resource " + index + ", which does not exist; ignored");
                return;
            }
            resources[index] = FrostySdk.Utils.CompressFile(data);
        }

        // [DAI fix] the index is the entry's position in the mod's resource list (was the data block, so removals couldn't be turned off)
        public void SetResourceEnabled(int index, bool enabled)
        {
            if (index < 0 || index >= entries.Count)
            {
                log.Add("Script tried to enable/disable resource " + index + ", which does not exist; ignored");
                return;
            }
            entries[index].Enabled = enabled;
        }

        public void SetResourceEnabled(string name, bool enabled)
        {
            ScriptResourceEntry entry = FindEntry(name);
            if (entry != null)
                entry.Enabled = enabled;
        }

        public bool ChangeResourceId(int index, int newResourceId)
        {
            if (index < 0 || index >= entries.Count)
            {
                log.Add("Script tried to change resource " + index + ", which does not exist; ignored");
                return false;
            }
            return ChangeResourceId(entries[index], newResourceId);
        }

        public bool ChangeResourceId(string name, int newResourceId)
        {
            ScriptResourceEntry entry = FindEntry(name);
            return entry != null && ChangeResourceId(entry, newResourceId);
        }

        private bool ChangeResourceId(ScriptResourceEntry entry, int newResourceId)
        {
            if (newResourceId < 0 || newResourceId >= resources.Count)
            {
                log.Add("Script tried to point " + entry.Name + " at data " + newResourceId + ", which does not exist; ignored");
                return false;
            }
            entry.ResourceId = newResourceId;
            return true;
        }
    }

    /// <summary>
    /// Interaction logic for ConfigWindow.xaml
    /// </summary>
    public partial class ConfigWindow : FrostyDockableWindow
    {
        // [DAI fix] closing without Save used to import the mod with the script never run
        public bool ScriptApplied { get; private set; }

        public string ScriptError { get; private set; }

        private DAIMod.ModScript modScript;
        private FrostyScripting scripting;

        // [DAI fix] savedValues / legacyDescription pre-fill the settings (Reconfigure)
        internal ConfigWindow(string code, List<byte[]> inResources, List<ScriptResourceEntry> inEntries, IDictionary<string, string> savedValues = null, string legacyDescription = null)
        {
            InitializeComponent();

            CSharpCodeProvider codeProvider = new CSharpCodeProvider();
            CompilerParameters parameters = new CompilerParameters
            {
                GenerateExecutable = false,
                GenerateInMemory = true
            };

            // [DAI fix] full paths so they resolve from any working directory; Linq is in System.Core
            HashSet<string> references = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                typeof(Uri).Assembly.Location,
                typeof(Enumerable).Assembly.Location,
                typeof(System.Xml.XmlDocument).Assembly.Location,
                typeof(System.Xml.Linq.XDocument).Assembly.Location,
                typeof(System.Data.DataTable).Assembly.Location,
                typeof(System.Drawing.Color).Assembly.Location,
                typeof(DAIMod.ModScript).Assembly.Location
            };
            foreach (string reference in references)
            {
                if (!string.IsNullOrEmpty(reference))
                    parameters.ReferencedAssemblies.Add(reference);
            }

            CompilerResults results = codeProvider.CompileAssemblyFromSource(parameters, code);

            // [DAI fix] compile errors used to crash the import
            if (results.Errors.HasErrors)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("The mod's configuration script failed to compile:");
                foreach (CompilerError err in results.Errors)
                {
                    if (!err.IsWarning)
                        sb.AppendLine(string.Format("  line {0}: {1} ({2})", err.Line, err.ErrorText, err.ErrorNumber));
                }
                throw new InvalidOperationException(sb.ToString());
            }

            foreach (Type type in results.CompiledAssembly.GetTypes())
            {
                if (typeof(DAIMod.ModScript).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
                {
                    modScript = (DAIMod.ModScript)Activator.CreateInstance(type);
                    break;
                }
            }

            if (modScript == null)
                throw new InvalidOperationException("The mod's configuration script does not contain a ModScript class.");

            scripting = new FrostyScripting(new FrostyConfigElementsList(this, savedValues, legacyDescription), inResources, inEntries);
            DAIMod.Scripting.SetScriptInterface(scripting);

            modScript.ConstructUI(scripting.config);
        }

        public string GetConfigValues()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < scripting.config.NumElements; i++)
            {
                ConfigElement elem = scripting.config.GetElement(i);
                sb.AppendLine(elem.ToString() + " " + elem.DisplayText);
            }

            return sb.ToString();
        }

        // [DAI fix] chosen settings by id, culture-independent, for Reconfigure
        public Dictionary<string, string> GetSettingValues()
        {
            Dictionary<string, string> values = new Dictionary<string, string>();
            foreach (KeyValuePair<string, ConfigElement> pair in scripting.config.Elements)
                values[pair.Key] = pair.Value.SaveText;
            return values;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // [DAI fix] commit a value still being typed, and catch script errors (they crashed the Mod Manager)
            scripting.config.CommitAll();

            try
            {
                modScript.RunScript();
                ScriptApplied = true;
            }
            catch (Exception ex)
            {
                ScriptError = "The mod's configuration script failed while running: " + ex.Message;
            }

            foreach (string line in scripting.log)
                App.Logger.Log("[DAI script] " + line);

            Close();
        }
    }
}
