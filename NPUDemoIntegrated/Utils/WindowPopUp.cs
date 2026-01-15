using NPUDemoIntegrated.Windows;
using System.Windows;

namespace NPUDemoIntegrated.Utils
{
    internal class WindowPopUp
    {
        public static void ErrorWindowPopUp()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ErrorWindow errorWindow = new ErrorWindow();
                errorWindow.Owner = Application.Current.MainWindow;
                errorWindow.Show();
            });
        }
    }
}
