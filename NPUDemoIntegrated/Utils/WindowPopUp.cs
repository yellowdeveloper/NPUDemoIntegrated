using NPUDemoIntegrated.Windows;
using System.Windows;

namespace NPUDemoIntegrated.Utils
{
    internal class WindowPopUp
    {
        public static void ErrorWindowPopUp(string errMsg)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ErrorWindow errorWindow = new ErrorWindow(errMsg);
                errorWindow.Owner = Application.Current.MainWindow;
                errorWindow.ShowDialog();
            });
        }
    }
}
