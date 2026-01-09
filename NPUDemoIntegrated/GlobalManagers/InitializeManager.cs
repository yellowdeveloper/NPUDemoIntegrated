using System.IO;

namespace NPUDemoIntegrated.GlobalManagers
{
    class InitializeManager
    {
        public static void InitializeProgram()
        {
            InitializePaths();
            GlobalConfigManager.Instance.LoadConfig();
        }

        private static void InitializePaths()
        {
            string image_folder_path = GlobalConfigManager.Instance.GetImageFolderPath();
            string config_folder_path = GlobalConfigManager.Instance.GetConfigFolderPath();

            try
            {
                Directory.CreateDirectory(image_folder_path);
                GlobalLogManager.Instance.ConsoleLog("Image Folder Created");
                Directory.CreateDirectory(config_folder_path);
                GlobalLogManager.Instance.ConsoleLog("Config Folder Created");
            }
            catch
            {
                GlobalLogManager.Instance.ConsoleLog("ERROR!! Folder Create Error (Config, Image)");
            }
        }
    }
}
