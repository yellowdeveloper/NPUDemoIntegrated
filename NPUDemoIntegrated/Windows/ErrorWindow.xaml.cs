using NPUDemoIntegrated.GlobalManagers;
using NPUDemoIntegrated.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace NPUDemoIntegrated.Windows
{
    /// <summary>
    /// ErrorWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ErrorWindow : Window
    {
        public ErrorWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Window MainWindow = Application.Current.MainWindow;
            

            if (MainWindow != null)
            {
                if (MainWindow?.DataContext is ViewModels.MainViewModel vm)
                {
                    GlobalLogManager.Instance.ConsoleLog("Closing ... Dispose all Instances");
                    vm.OBJVM.DeactivateModule(EModuleType.OBJ);
                    vm.IRVM.DeactivateModule(EModuleType.OBJ);

                    vm.OBJVM.Dispose();
                    vm.IRVM.Dispose();
                }

                MainWindow.Close();
                // Application.Current.Shutdown();
            }
        }
    }
}
