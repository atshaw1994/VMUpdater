using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using VMUpdater.Models;
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

        private void CollectionViewSource_VmWareFilter(object sender, FilterEventArgs e)
        {
            if (e.Item is VirtualMachineViewModel vmViewModel)
                e.Accepted = vmViewModel.Model.Hypervisor == HypervisorType.VMWare;
            else
                e.Accepted = false;
        }

        private void CollectionViewSource_VirtualBoxFilter(object sender, FilterEventArgs e)
        {
            if (e.Item is VirtualMachineViewModel vmViewModel)
                e.Accepted = vmViewModel.Model.Hypervisor == HypervisorType.VirtualBox;
            else
                e.Accepted = false;
        }

        private void CollectionViewSource_QemuFilter(object sender, FilterEventArgs e)
        {
            if (e.Item is VirtualMachineViewModel vmViewModel)
                e.Accepted = vmViewModel.Model.Hypervisor == HypervisorType.QEMU;
            else
                e.Accepted = false;
        }

        private void LogTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox logTextBox) logTextBox.ScrollToEnd();
        }

        private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox logTextBox) logTextBox.ScrollToEnd();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }
    }
}
