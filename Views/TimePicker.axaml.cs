using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace VMUpdater.Views
{
    /// <summary>
    /// Interaction logic for TimePicker.axaml
    /// </summary>
    public partial class TimePicker : UserControl
    {
        private bool _isSynchronizing;

        // 1. Define the Avalonia Styled Properties
        public static readonly StyledProperty<DateTime> SelectedTimeProperty =
            AvaloniaProperty.Register<TimePicker, DateTime>(
                nameof(SelectedTime),
                defaultValue: DateTime.Now,
                defaultBindingMode: BindingMode.TwoWay);

        public DateTime SelectedTime
        {
            get => GetValue(SelectedTimeProperty);
            set => SetValue(SelectedTimeProperty, value);
        }

        public static readonly StyledProperty<int> SelectedHourProperty =
            AvaloniaProperty.Register<TimePicker, int>(
                nameof(SelectedHour),
                defaultValue: 12);

        public int SelectedHour
        {
            get => GetValue(SelectedHourProperty);
            set => SetValue(SelectedHourProperty, value);
        }

        public static readonly StyledProperty<int> SelectedMinuteProperty =
            AvaloniaProperty.Register<TimePicker, int>(
                nameof(SelectedMinute),
                defaultValue: 0);

        public int SelectedMinute
        {
            get => GetValue(SelectedMinuteProperty);
            set => SetValue(SelectedMinuteProperty, value);
        }

        public static readonly StyledProperty<string> SelectedMeridianProperty =
            AvaloniaProperty.Register<TimePicker, string>(
                nameof(SelectedMeridian),
                defaultValue: "AM");

        public string SelectedMeridian
        {
            get => GetValue(SelectedMeridianProperty);
            set => SetValue(SelectedMeridianProperty, value);
        }

        // Lists for the ComboBox dropdown items
        public List<int> Hours { get; } = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];
        public List<int> Minutes { get; } = CreateMinuteList();
        public List<string> Meridians { get; } = ["AM", "PM"];

        static TimePicker()
        {
            // Register property change notifications statically via AvaloniaProperty.Changed
            SelectedTimeProperty.Changed.AddClassHandler<TimePicker>((x, e) => x.OnSelectedTimeChanged(e));
            SelectedHourProperty.Changed.AddClassHandler<TimePicker>((x, e) => x.OnInternalPropertyChanged(e));
            SelectedMinuteProperty.Changed.AddClassHandler<TimePicker>((x, e) => x.OnInternalPropertyChanged(e));
            SelectedMeridianProperty.Changed.AddClassHandler<TimePicker>((x, e) => x.OnInternalPropertyChanged(e));
        }

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

        // Property changed handlers using AvaloniaPropertyChangedEventArgs
        private void OnSelectedTimeChanged(AvaloniaPropertyChangedEventArgs e)
        {
            SyncWrappersFromSelectedTime();
        }

        private void OnInternalPropertyChanged(AvaloniaPropertyChangedEventArgs e)
        {
            SyncSelectedTimeFromWrappers();
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