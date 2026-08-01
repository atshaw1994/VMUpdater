using Avalonia.Controls;
using Avalonia.Interactivity;
using VMUpdater.ViewModels;

namespace VMUpdater.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        // Accept the application's shared ViewModel via dependency injection
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
        }

        private void LogTextBox_Loaded(object sender, RoutedEventArgs e) => ScrollTextBoxToEnd(sender);

        private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e) => ScrollTextBoxToEnd(sender);

        private static void ScrollTextBoxToEnd(object? sender)
        {
            if (sender is TextBox logTextBox)
                logTextBox.CaretIndex = logTextBox.Text?.Length ?? 0;
        }

        private void Window_Closing(object? sender, WindowClosingEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }
    }
}
