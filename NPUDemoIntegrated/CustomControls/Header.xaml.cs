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
                parentWindow.Close();
                // Application.Current.Shutdown();
            }
        }
    }
}
