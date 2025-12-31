using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NPUDemoIntegrated.GlobalManagers
{
    class GlobalLogManager
    {
        private static readonly GlobalLogManager _instance = new GlobalLogManager();
        public static GlobalLogManager Instance => _instance;

        private string _log_folder_path = @".\";
        private string _log_file_name = "_log.txt";

        public void AddLogToFile(
            string type,
            string comment,
            string note = "-",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string memberName = "")
        {
            /*
            try {
                string now_date = DateTime.Now.ToString("yyyy-MM-dd");
                string now_time = DateTime.Now.ToString("HH:mm:ss");
                string log_file_name = type + note + now_date + _log_file_name;
                string logFilePath = Path.Combine(_log_folder_path, log_file_name);

                string fileName = Path.GetFileName(filePath);

                // 소스 정보
                string source_line = $"Time: {now_time}: {fileName}: {memberName}(): Line {lineNumber}";

                string logMessage = source_line + note + comment + "\n\n";

                File.AppendAllText(logFilePath, logMessage);
            }
            catch (Exception ex) {
                Console.WriteLine($"Failed to write log: {ex.Message}");
            }
            */
        }

        public void ConsoleLog(
            string comment,
            string note = " - ",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string memberName = "")
        {
            string now_time = DateTime.Now.ToString("HH:mm:ss");
            string fileName = Path.GetFileName(filePath);
            string source_line = $"[{now_time}]:{fileName}:{memberName}(): Line {lineNumber}";
            string logMessage = source_line + note + comment;

            if (comment.StartsWith("ERROR!!"))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                //Console.WriteLine(logMessage);
            }
            else if (comment.StartsWith("WARN.."))
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
            }
            else if (comment.StartsWith("OK.."))
            {
                Console.ForegroundColor = ConsoleColor.Green;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.White;
            }

            Console.WriteLine(logMessage);
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;
        private bool _is_console_visible = true;

        public bool is_console_visible
        {
            get { return _is_console_visible; }
            set { _is_console_visible = value; }
        }

        private static IntPtr GetConsoleHandle()
        {
            return GetConsoleWindow();
        }

        public void ConsoleShow()
        {
            IntPtr handle = GetConsoleHandle();
            if (handle != IntPtr.Zero)
            {
                _instance._is_console_visible = true;
                ShowWindow(handle, SW_SHOW);
            }
        }

        public void ConsoleHide()
        {
            IntPtr handle = GetConsoleHandle();
            if (handle != IntPtr.Zero)
            {
                _instance._is_console_visible = false;
                ShowWindow(handle, SW_HIDE);
            }
        }
    }
}
