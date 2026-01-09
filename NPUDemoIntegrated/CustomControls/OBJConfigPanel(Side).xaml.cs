using NPUDemoIntegrated.GlobalManagers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NPUDemoIntegrated.CustomControls
{
    /// <summary>
    /// ConfigPanel_Side_.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class OBJConfigPanel_Side_ : UserControl
    {
        public OBJConfigPanel_Side_()
        {
            InitializeComponent();
        }
        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var text_box = sender as TextBox;
            text_box.Text = "";
            text_box.Foreground = Brushes.Black;
        }

        private void PNTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var text_box = sender as TextBox;
            if (string.IsNullOrWhiteSpace(text_box.Text))
            {
                text_box.Text = GlobalConfigManager.Instance.objConfig.portName;
                text_box.Foreground = Brushes.Gray;
            }
            else
            {
                GlobalConfigManager.Instance.objConfig.portName = text_box.Text;
                text_box.Foreground = Brushes.Gray;
            }
        }
        private void BaudTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var text_box = sender as TextBox;
            if (string.IsNullOrWhiteSpace(text_box.Text))
            {
                text_box.Text = GlobalConfigManager.Instance.objConfig.baudRate.ToString();
                text_box.Foreground = Brushes.Gray;
            }
            else
            {
                GlobalConfigManager.Instance.objConfig.baudRate = Convert.ToInt32(text_box.Text);
                text_box.Foreground = Brushes.Gray;
            }
        }

        private void DatTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var text_box = sender as TextBox;
            if (string.IsNullOrWhiteSpace(text_box.Text))
            {
                text_box.Text = GlobalConfigManager.Instance.objConfig.dataBits.ToString();
                text_box.Foreground = Brushes.Gray;
            }
            else
            {
                GlobalConfigManager.Instance.objConfig.dataBits = Convert.ToInt32(text_box.Text);
                text_box.Foreground = Brushes.Gray;
            }
        }

        private void PacketTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var text_box = sender as TextBox;
            if (string.IsNullOrWhiteSpace(text_box.Text))
            {
                text_box.Text = GlobalConfigManager.Instance.objConfig.chunk_size.ToString();
                text_box.Foreground = Brushes.Gray;
            }
            else
            {
                GlobalConfigManager.Instance.objConfig.chunk_size = Convert.ToInt32(text_box.Text);
                text_box.Foreground = Brushes.Gray;
            }
        }

        private void ProbTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var text_box = sender as TextBox;
            if (string.IsNullOrWhiteSpace(text_box.Text))
            {
                text_box.Text = GlobalConfigManager.Instance.objConfig.prob_thres.ToString();
                text_box.Foreground = Brushes.Gray;
            }
            else
            {
                GlobalConfigManager.Instance.objConfig.prob_thres = Convert.ToInt32(text_box.Text);
                text_box.Foreground = Brushes.Gray;
            }
        }
    }
}
