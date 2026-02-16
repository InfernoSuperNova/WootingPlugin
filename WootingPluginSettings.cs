using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using Sandbox;
using VRage.FileSystem;
using VRage.Input;
using VRage.Utils;
using VRageMath;


namespace WootingPlugin
{
    public class WootingPluginSettings
    {
        private const string fileName = "WootingPluginSettings.xml";
        private static string FilePath => Path.Combine(MyFileSystem.UserDataPath, "Storage", fileName);
        
        
        public static WootingPluginSettings I { get; private set; }        
        public float PitchSensitivityMultiplier { get; set; } = 2f;
        public float YawSensitivityMultiplier { get; set; } = 2f;
        public float RollSensitivityMultiplier { get; set; } = 3f;
        
        public WootingPluginSettings()
        {
            I = this;
        }
        
        
        
        public static void Load()
        {
            string file = FilePath;
            if (File.Exists(file))
            {
                try
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(WootingPluginSettings));
                    using (XmlReader xml = XmlReader.Create(file))
                    {
                        I = (WootingPluginSettings)serializer.Deserialize(xml);
                    }
                }
                catch (Exception e)
                {
                    MyLogExtensions.Error(MySandboxGame.Log, $"Failed to load WootingPluginSettings from {file}: {e}");
                }
            }
            if (I == null) I = new WootingPluginSettings();
            MyLogExtensions.Info(MySandboxGame.Log, $"WootingPluginSettings loaded from {FilePath}");
        }

        public void Save()
        {
            try
            {
                string file = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                XmlSerializer serializer = new XmlSerializer(typeof(WootingPluginSettings));
                using (StreamWriter stream = File.CreateText(file))
                {
                    serializer.Serialize(stream, this);
                }
            }
            catch (Exception e)
            {
                MyLogExtensions.Error(MySandboxGame.Log, $"Failed to save WootingPluginSettings to {FilePath}: {e}");
            }
            MyLogExtensions.Info(MySandboxGame.Log, $"WootingPluginSettings saved to {FilePath}");
        }
    }
}