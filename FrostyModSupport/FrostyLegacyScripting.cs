using System;

namespace DAIMod
{
    // [DAI fix] DAI Mod Manager's colour setting type, components 0 to 1
    public class Color
    {
        public float R;
        public float G;
        public float B;
        public float A;

        public Color()
        {
        }

        public Color(float r, float g, float b, float a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        private static int ToByte(float value) => (int)Math.Round(Math.Max(0.0f, Math.Min(1.0f, value)) * 255.0f);

        public override string ToString() => string.Format("#{0:X2}{1:X2}{2:X2}{3:X2}", ToByte(A), ToByte(R), ToByte(G), ToByte(B));
    }

    public class ModConfigElementsList
    {
        public virtual void AddIntElement(string id, string name, int defValue, int minValue, int maxValue) { throw new NotImplementedException(); }
        public virtual void AddFloatElement(string id, string name, float defValue, float minValue, float maxValue) { throw new NotImplementedException(); }
        public virtual void AddBoolElement(string id, string name, bool defValue) { throw new NotImplementedException(); }
        public virtual void AddEnumElement(string id, string name, Type enumType, int defValue) { throw new NotImplementedException(); }
        public virtual void AddStringElement(string id, string name, string defValue) { throw new NotImplementedException(); }
        public virtual void AddColorElement(string id, string name, Color defValue) { throw new NotImplementedException(); }
    }

    public interface ModScript
    {
        void ConstructUI(ModConfigElementsList ConfigElementsList);
        void RunScript();
    }

    public interface IScripting
    {
        object GetConfigParam(string name);
        void LogLn(string line);
        byte[] GetResourceData(int index);
        void SetResourceData(int index, byte[] data);
        void SetResourceEnabled(int index, bool enabled);
        void SetResourceEnabled(string name, bool enabled);
        bool ChangeResourceId(int index, int newResourceId);
        bool ChangeResourceId(string name, int newResourceId);
    }

    public class Scripting
    {
        static IScripting scriptInterface;

        public static object GetConfigParam(string name) => scriptInterface != null ? scriptInterface.GetConfigParam(name) : 0;

        public static void LogLn(string line) => scriptInterface?.LogLn(line);

        public static byte[] GetResourceData(int index) => scriptInterface?.GetResourceData(index);

        public static void SetResourceData(int index, byte[] data) => scriptInterface?.SetResourceData(index, data);

        public static void SetResourceEnabled(int index, bool enabled) => scriptInterface?.SetResourceEnabled(index, enabled);

        public static void SetResourceEnabled(string name, bool enabled) => scriptInterface?.SetResourceEnabled(name, enabled);

        public static bool ChangeResourceId(int index, int newResourceId) => scriptInterface != null && scriptInterface.ChangeResourceId(index, newResourceId);

        public static bool ChangeResourceId(string name, int newResourceId) => scriptInterface != null && scriptInterface.ChangeResourceId(name, newResourceId);

        public static void SetScriptInterface(IScripting inScriptInterface) => scriptInterface = inScriptInterface;
    }
}
