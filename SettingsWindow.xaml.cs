using System.Windows;

namespace PdfImageThresholdingTool
{
    public partial class SettingsWindow : Window
    {
        public int Threshold;
        public SettingsWindow(int initThreshold) {
            InitializeComponent();
            Threshold = initThreshold;
            threshold.Value = initThreshold;
        }

        private void Button_Click_OK(object sender, RoutedEventArgs e) {
            Threshold = (int)threshold.Value;
            Close();
        }

        private void Button_Click_Cancel(object sender, RoutedEventArgs e) {
            Close();
        }
    }
}
