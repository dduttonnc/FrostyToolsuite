using Frosty.Core.Attributes;
using FrostySdk;
using FrostySdk.Interfaces;
using System;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Data;

namespace FrostyCore
{
    public class FrostyLogger : ILogger, INotifyPropertyChanged
    {
        public string LogText { get { lock (sb) return sb.ToString(); } }
        private StringBuilder sb = new StringBuilder();

        // [DAI fix] locked, since mods are applied on several threads; copied to the diagnostic log file when that's on
        private void Append(string line)
        {
            lock (sb)
                sb.AppendLine(line);
            DiagnosticLog.WriteLine(line);
            RaisePropertyChanged("LogText");
        }

        public void Log(string text, params object[] vars)
        {
            Assembly assembly = Assembly.GetCallingAssembly();
            string category = "[Core] ";
            var attr = assembly.GetCustomAttribute<PluginDisplayNameAttribute>();

            if (attr != null)
                category = "[" + attr.DisplayName + "] ";

            Append(string.Format("[" + DateTime.Now.ToLongTimeString() + "]: " + category + text, vars));
        }

        public void LogWarning(string text, params object[] vars)
        {
            Assembly assembly = Assembly.GetCallingAssembly();
            string category = "[Core] ";
            var attr = assembly.GetCustomAttribute<PluginDisplayNameAttribute>();

            if (attr != null)
                category = "[" + attr.DisplayName + "] ";

            Append(string.Format("[" + DateTime.Now.ToLongTimeString() + "]: " + category + "(WARNING) " + text, vars));
        }

        public void LogError(string text, params object[] vars)
        {
            Assembly assembly = Assembly.GetCallingAssembly();
            string category = "[Core] ";
            var attr = assembly.GetCustomAttribute<PluginDisplayNameAttribute>();

            if (attr != null)
                category = "[" + attr.DisplayName + "] ";

            Append(string.Format("[" + DateTime.Now.ToLongTimeString() + "]: " + category + "(ERROR) " + text, vars));
        }

        public void AddBinding(UIElement elementToBind, DependencyProperty propertyToBind)
        {
            Binding b = new Binding("LogText")
            {
                Source = this,
                Mode = BindingMode.OneWay
            };

            BindingOperations.SetBinding(elementToBind, propertyToBind, b);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
