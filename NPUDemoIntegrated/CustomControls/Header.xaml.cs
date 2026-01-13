using NPUDemoIntegrated.GlobalManagers;
using System.Windows;
using System.Windows.Controls;


namespace NPUDemoIntegrated.CustomControls
{
    /// <summary>
    /// Header.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class Header : UserControl
    {
        public Header()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Window parentWindow = Window.GetWindow(this);

            if (parentWindow != null)
            {
                if (parentWindow?.DataContext is ViewModels.MainViewModel vm)
                {
                    GlobalLogManager.Instance.ConsoleLog("Closing ... Dispose all Instances");
                    vm.OBJVM.Dispose();
                    vm.IRVM.Dispose();
                }

                parentWindow.Close();
                // Application.Current.Shutdown();
            }
        }
    }
}
