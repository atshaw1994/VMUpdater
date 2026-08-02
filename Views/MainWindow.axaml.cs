using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Diagnostics;
using VMUpdater.ViewModels;

namespace VMUpdater.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
        }

        // Accept the application's shared ViewModel via dependency injection
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
            TransparencyLevelHint = [WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur];
            FindTextBox.PropertyChanged += (s, e) =>
            {
                if (e.Property == IsVisibleProperty && (bool)e.NewValue!)
                {
                    _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () => 
                    {
                        // Small delay matching the transition duration
                        await Task.Delay(200);
                        FindTextBox.Focus(); 
                    });
                }
            };
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
