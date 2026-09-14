namespace FinanceApp.Mobile.Converters;

using Microsoft.Maui.Controls;

public class BoolToStringConverter : IValueConverter
{
    public string TrueValue { get; set; } = "Yes";
    public string FalseValue { get; set; } = "No";

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool boolValue && parameter is string param)
        {
            var parts = param.Split('|');
            if (parts.Length == 2)
                return boolValue ? parts[0] : parts[1];
        }
        if (value is bool boolValue2)
            return boolValue2 ? TrueValue : FalseValue;
        return FalseValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is string strValue)
            return strValue == TrueValue;
        return false;
    }
}

public class InverseBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;
        return true;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;
        return false;
    }
}

public class TransactionTypeToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is FinanceApp.Domain.Enums.TransactionType type)
        {
            return type == FinanceApp.Domain.Enums.TransactionType.Income ? Colors.Green : Colors.Red;
        }
        return Colors.Black;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class TransactionTypeToButtonStyleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is FinanceApp.Domain.Enums.TransactionType currentType && parameter is string paramType)
        {
            var param = Enum.Parse<FinanceApp.Domain.Enums.TransactionType>(paramType);
            return currentType == param ? "PrimaryButtonStyle" : "OutlineButtonStyle";
        }
        return "OutlineButtonStyle";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class CategoryTabStyleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is FinanceApp.Domain.Enums.CategoryType currentTab && parameter is string paramTab)
        {
            var param = Enum.Parse<FinanceApp.Domain.Enums.CategoryType>(paramTab);
            return currentTab == param ? "PrimaryButtonStyle" : "OutlineButtonStyle";
        }
        return "OutlineButtonStyle";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class CategoryTabVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is FinanceApp.Domain.Enums.CategoryType currentTab && parameter is string paramTab)
        {
            var param = Enum.Parse<FinanceApp.Domain.Enums.CategoryType>(paramTab);
            return currentTab == param;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class SyncStatusToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is FinanceApp.Domain.Enums.SyncStatus status)
        {
            return status switch
            {
                FinanceApp.Domain.Enums.SyncStatus.Synced => Colors.Green,
                FinanceApp.Domain.Enums.SyncStatus.PendingCreate => Colors.Orange,
                FinanceApp.Domain.Enums.SyncStatus.PendingUpdate => Colors.Orange,
                FinanceApp.Domain.Enums.SyncStatus.PendingDelete => Colors.Orange,
                FinanceApp.Domain.Enums.SyncStatus.Failed => Colors.Red,
                _ => Colors.Gray
            };
        }
        return Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class SyncStatusToIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is FinanceApp.Domain.Enums.SyncStatus status)
        {
            return status switch
            {
                FinanceApp.Domain.Enums.SyncStatus.Synced => "✓",
                FinanceApp.Domain.Enums.SyncStatus.PendingCreate => "↑",
                FinanceApp.Domain.Enums.SyncStatus.PendingUpdate => "↻",
                FinanceApp.Domain.Enums.SyncStatus.PendingDelete => "⌫",
                FinanceApp.Domain.Enums.SyncStatus.Failed => "✗",
                _ => "?"
            };
        }
        return "?";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class MoneyToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is FinanceApp.Domain.ValueObjects.Money money)
            return money.ToString();
        return "₱0.00";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class PercentageToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is decimal percentage)
        {
            if (percentage >= 100) return Colors.Red;
            if (percentage >= 80) return Colors.Orange;
            if (percentage >= 50) return Colors.Yellow;
            return Colors.Green;
        }
        return Colors.Green;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class NullToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is System.Collections.ICollection collection)
            return collection.Count > 0;
        return value != null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class ColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is string hexColor && !string.IsNullOrEmpty(hexColor))
        {
            try
            {
                return Color.FromArgb(hexColor);
            }
            catch
            {
                return Colors.Gray;
            }
        }
        return Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class PercentageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is decimal percentage)
            return (double)percentage / 100.0;
        if (value is double d)
            return d / 100.0;
        return 0.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class GoalStatusToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is FinanceApp.Domain.Enums.GoalStatus status)
        {
            return status switch
            {
                FinanceApp.Domain.Enums.GoalStatus.Active => "Active",
                FinanceApp.Domain.Enums.GoalStatus.Completed => "Completed",
                FinanceApp.Domain.Enums.GoalStatus.Paused => "Paused",
                FinanceApp.Domain.Enums.GoalStatus.Cancelled => "Cancelled",
                _ => "Unknown"
            };
        }
        return "Unknown";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class GoalStatusVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is FinanceApp.Domain.Enums.GoalStatus status && parameter is string paramStatus)
        {
            var param = Enum.Parse<FinanceApp.Domain.Enums.GoalStatus>(paramStatus);
            return status == param;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}