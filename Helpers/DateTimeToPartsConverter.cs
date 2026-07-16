using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Data;

namespace VMUpdater.Helpers
{
    public class DateTimeToPartsConverter : IMultiValueConverter
    {
        // Splits DateTime -> [Hour, Minute, Meridian]
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] is DateTime dateTime)
            {
                int hour12 = dateTime.Hour % 12;
                if (hour12 == 0) hour12 = 12;

                string meridian = dateTime.Hour >= 12 ? "PM" : "AM";

                // Return the requested part based on the parameter passed ("Hour", "Minute", or "Meridian")
                string part = (string)parameter;
                if (part == "Hour") return hour12;
                if (part == "Minute") return dateTime.Minute;
                if (part == "Meridian") return meridian;
            }
            return Binding.DoNothing;
        }

        // Merges [Hour, Minute, Meridian, OriginalDateTime] -> New DateTime
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            // We only need to return the updated DateTime to the first binding (SelectedTime)
            // The rest of the bindings in the MultiBinding can return Binding.DoNothing
            var result = new object[targetTypes.Length];
            for (int i = 0; i < result.Length; i++) result[i] = Binding.DoNothing;

            return result;
        }
    }
}