using NPUDemoIntegrated.GlobalManagers;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace NPUDemoIntegrated.CustomControls
{
    /// <summary>
    /// IRConfigPanel_Side_.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class IRConfigPanel_Side_ : UserControl
    {
        public IRConfigPanel_Side_()
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
                text_box.Text = GlobalConfigManager.Instance.irConfig.portName;
                text_box.Foreground = Brushes.Gray;
            }
            else
            {
                GlobalConfigManager.Instance.irConfig.portName = text_box.Text;
                text_box.Foreground = Brushes.Gray;
            }
        }
        private void BaudTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var text_box = sender as TextBox;
            if (string.IsNullOrWhiteSpace(text_box.Text))
            {
                text_box.Text = GlobalConfigManager.Instance.irConfig.baudRate.ToString();
                text_box.Foreground = Brushes.Gray;
            }
            else
            {
                GlobalConfigManager.Instance.irConfig.baudRate = Convert.ToInt32(text_box.Text);
                text_box.Foreground = Brushes.Gray;
            }
        }

        private void DatTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var text_box = sender as TextBox;
            if (string.IsNullOrWhiteSpace(text_box.Text))
            {
                text_box.Text = GlobalConfigManager.Instance.irConfig.dataBits.ToString();
                text_box.Foreground = Brushes.Gray;
            }
            else
            {
                GlobalConfigManager.Instance.irConfig.dataBits = Convert.ToInt32(text_box.Text);
                text_box.Foreground = Brushes.Gray;
            }
        }
        private void NumOfDatTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var text_box = sender as TextBox;
            if (string.IsNullOrWhiteSpace(text_box.Text))
            {
                text_box.Text = GlobalConfigManager.Instance.irConfig.numOfData.ToString();
                text_box.Foreground = Brushes.Gray;
            }
            else
            {
                GlobalConfigManager.Instance.irConfig.numOfData = Convert.ToInt32(text_box.Text);
                text_box.Foreground = Brushes.Gray;
            }
        }

    }
}
