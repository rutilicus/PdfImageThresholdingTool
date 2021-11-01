using System.Windows;

namespace PdfImageThresholdingTool
{
    public partial class SettingsWindow : Window
    {
        public int Threshold;
        public int BoldDistance;
        public SettingsWindow(int initThreshold, int initBoldDistance) {
            InitializeComponent();
            Threshold = initThreshold;
            threshold.Value = initThreshold;
            BoldDistance = initBoldDistance;
            boldDistance.Value = initBoldDistance;
        }

        private void Button_Click_OK(object sender, RoutedEventArgs e) {
            Threshold = (int)threshold.Value;
            BoldDistance = (int)boldDistance.Value;
            Close();
        }

        private void Button_Click_Cancel(object sender, RoutedEventArgs e) {
            Close();
        }
    }
}
