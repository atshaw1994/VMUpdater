using Avalonia.Controls;
using Avalonia.Interactivity;

namespace VMUpdater.Views
{
    /// <summary>
    /// Interaction logic for AboutDialog.xaml
    /// </summary>
    public partial class AboutDialog : Window
    {
        public AboutDialog() => InitializeComponent();

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
