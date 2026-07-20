using System.Windows;
using System.Windows.Controls;

namespace VMUpdater.Views
{
    /// <summary>
    /// Interaction logic for TimePicker.xaml
    /// </summary>
    public partial class TimePicker : UserControl
    {
        private bool _isSynchronizing;

        // 1. Define the Dependency Property for the parent to bind to
        public static readonly DependencyProperty SelectedTimeProperty =
            DependencyProperty.Register(
                nameof(SelectedTime),
                typeof(DateTime),
                typeof(TimePicker),
                new FrameworkPropertyMetadata(DateTime.Now, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedTimeChanged));

        public DateTime SelectedTime
        {
            get => (DateTime)GetValue(SelectedTimeProperty);
            set => SetValue(SelectedTimeProperty, value);
        }

        // Internal dependency properties for the ComboBoxes to bind to
        public static readonly DependencyProperty SelectedHourProperty =
            DependencyProperty.Register(nameof(SelectedHour), typeof(int), typeof(TimePicker), new PropertyMetadata(12, OnInternalPropertyChanged));

        public int SelectedHour
        {
            get => (int)GetValue(SelectedHourProperty);
            set => SetValue(SelectedHourProperty, value);
        }

        public static readonly DependencyProperty SelectedMinuteProperty =
            DependencyProperty.Register(nameof(SelectedMinute), typeof(int), typeof(TimePicker), new PropertyMetadata(0, OnInternalPropertyChanged));

        public int SelectedMinute
        {
            get => (int)GetValue(SelectedMinuteProperty);
            set => SetValue(SelectedMinuteProperty, value);
        }

        public static readonly DependencyProperty SelectedMeridianProperty =
            DependencyProperty.Register(nameof(SelectedMeridian), typeof(string), typeof(TimePicker), new PropertyMetadata("AM", OnInternalPropertyChanged));

        public string SelectedMeridian
        {
            get => (string)GetValue(SelectedMeridianProperty);
            set => SetValue(SelectedMeridianProperty, value);
        }

        // Lists for the ComboBox dropdown items
        public List<int> Hours { get; } = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];
        public List<int> Minutes { get; } = CreateMinuteList();
        public List<string> Meridians { get; } = ["AM", "PM"];

        public TimePicker()
        {
            InitializeComponent();
            SyncWrappersFromSelectedTime();
        }

        private static List<int> CreateMinuteList()
        {
            var list = new List<int>();
            for (int i = 0; i < 60; i++) list.Add(i);
            return list;
        }

        // Triggers when the parent ViewModel updates "SelectedTime"
        private static void OnSelectedTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TimePicker control)
            {
                control.SyncWrappersFromSelectedTime();
            }
        }

        // Triggers when the user changes one of the three ComboBoxes
        private static void OnInternalPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TimePicker control)
            {
                control.SyncSelectedTimeFromWrappers();
            }
        }

        private void SyncWrappersFromSelectedTime()
        {
            if (_isSynchronizing) return;
            _isSynchronizing = true;

            try
            {
                int hour24 = SelectedTime.Hour;
                SelectedMinute = SelectedTime.Minute;

                if (hour24 == 0)
                {
                    SelectedHour = 12;
                    SelectedMeridian = "AM";
                }
                else if (hour24 == 12)
                {
                    SelectedHour = 12;
                    SelectedMeridian = "PM";
                }
                else if (hour24 > 12)
                {
                    SelectedHour = hour24 - 12;
                    SelectedMeridian = "PM";
                }
                else
                {
                    SelectedHour = hour24;
                    SelectedMeridian = "AM";
                }
            }
            finally
            {
                _isSynchronizing = false;
            }
        }

        private void SyncSelectedTimeFromWrappers()
        {
            if (_isSynchronizing) return;
            _isSynchronizing = true;

            try
            {
                int hour12 = SelectedHour == 0 ? 12 : SelectedHour;
                string amPm = string.IsNullOrEmpty(SelectedMeridian) ? "AM" : SelectedMeridian;

                int hour24 = hour12;
                if (amPm == "PM" && hour12 < 12) hour24 += 12;
                else if (amPm == "AM" && hour12 == 12) hour24 = 0;

                SelectedTime = new DateTime(
                    SelectedTime.Year,
                    SelectedTime.Month,
                    SelectedTime.Day,
                    hour24,
                    SelectedMinute,
                    0,
                    SelectedTime.Kind
                );
            }
            finally
            {
                _isSynchronizing = false;
            }
        }
    }
}
