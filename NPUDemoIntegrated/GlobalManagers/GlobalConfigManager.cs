using NPUDemoIntegrated.Models;
using NPUDemoIntegrated.Models.IRModule;
using NPUDemoIntegrated.Models.OBJModule;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NPUDemoIntegrated.GlobalManagers
{
    class GlobalConfigManager
    {
        private static readonly GlobalConfigManager _instance = new GlobalConfigManager();
        public static GlobalConfigManager Instance => _instance;

        private GlobalConfigManager() { }

        public OBJConfig objConfig { get; set; } = new OBJConfig();
        public IRConfig irConfig { get; set; } = new IRConfig();

        private string _configFolderPath = @"Config\";
        private string _configFileName = "init_config.ini";

        private string _imageFolderPath = @"Image\";
        private string _imageFileName = $"Processed.png";

        public string GetConfigFolderPath()
        {
            return _configFolderPath ?? string.Empty; ;
        }

        public string GetConfigFileName()
        {
            return _configFileName ?? string.Empty; ;
        }

        public string GetImageFolderPath()
        {
            return _imageFolderPath ?? string.Empty; ;
        }

        public string GetNowImageFileName()
        {
            string now_time = DateTime.Now.ToString("[yyyy_MM_dd_HH_mm_ss]");
            string now_image_file_name = now_time + _imageFileName;
            return now_image_file_name;
        }

        private void MapControlToIni(string path, string section, object configObj)
        {
            PropertyInfo[] properties = configObj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                if (!prop.CanWrite) continue;
                object val = prop.GetValue(configObj);
                WritePrivateProfileString(section, prop.Name, val?.ToString() ?? "", path);
            }
        }
        private void MapIniToControl(string path, string section, object configObj)
        {
            PropertyInfo[] properties = configObj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            StringBuilder temp = new StringBuilder(255);

            foreach (var prop in properties)
            {
                if (!prop.CanWrite) continue;

                GetPrivateProfileString(section, prop.Name, "", temp, 255, path);
                string value = temp.ToString();
                if (string.IsNullOrEmpty(value)) continue;

                try
                {
                    object convertedVal;
                    if (prop.PropertyType.IsEnum)
                        convertedVal = Enum.Parse(prop.PropertyType, value);
                    else
                        convertedVal = Convert.ChangeType(value, prop.PropertyType);

                    prop.SetValue(configObj, convertedVal);
                }
                catch { /* Default Setting */ }
            }
        }

        public void LoadConfig()
        {
            string configFilePath = Path.Combine(_configFolderPath, _configFileName);
            if (!File.Exists(configFilePath))
            {
                SaveConfig();
                GlobalLogManager.Instance.ConsoleLog("No Config File Found!! Creating Config File ... ");
                return;
            }
            MapIniToControl(configFilePath, "OBJConfigInfo", objConfig);
            MapIniToControl(configFilePath, "IRConfigInfo", irConfig);
        }

        public void SaveConfig()
        {
            if (!Directory.Exists(_configFolderPath)) Directory.CreateDirectory(_configFolderPath);
            string configFilePath = Path.Combine(_configFolderPath, _configFileName);

            MapControlToIni(configFilePath, "OBJConfigInfo", objConfig);
            MapControlToIni(configFilePath, "IRConfigInfo", irConfig);
        }

        [DllImport("Kernel32")]
        private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);
        [DllImport("Kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);
    }

}
