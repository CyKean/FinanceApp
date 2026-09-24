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

public class SavingsRateColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is decimal rate)
        {
            if (rate >= 20) return Colors.Green;
            if (rate >= 10) return Colors.Orange;
            if (rate >= 0) return Colors.Yellow;
            return Colors.Red;
        }
        return Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class PeriodButtonStyleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is int currentMonths && parameter is string paramMonths)
        {
            var param = int.Parse(paramMonths);
            return currentMonths == param ? "PrimaryButtonStyle" : "OutlineButtonStyle";
        }
        return "OutlineButtonStyle";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class PredictionConfidenceColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is FinanceApp.Domain.Enums.PredictionConfidence confidence)
        {
            return confidence switch
            {
                FinanceApp.Domain.Enums.PredictionConfidence.High => Colors.Green,
                FinanceApp.Domain.Enums.PredictionConfidence.Moderate => Colors.Orange,
                FinanceApp.Domain.Enums.PredictionConfidence.Low => Colors.Red,
                FinanceApp.Domain.Enums.PredictionConfidence.InsufficientData => Colors.Gray,
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

public class PredictionConfidenceToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is FinanceApp.Domain.Enums.PredictionConfidence confidence && parameter is string param)
        {
            var paramEnum = Enum.Parse<FinanceApp.Domain.Enums.PredictionConfidence>(param);
            return confidence == paramEnum;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class TrendToIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is FinanceApp.Domain.Enums.SpendingTrend trend)
        {
            return trend switch
            {
                FinanceApp.Domain.Enums.SpendingTrend.Increasing => "📈",
                FinanceApp.Domain.Enums.SpendingTrend.Decreasing => "📉",
                FinanceApp.Domain.Enums.SpendingTrend.Stable => "➡️",
                _ => "➡️"
            };
        }
        return "➡️";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class TrendToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is FinanceApp.Domain.Enums.SpendingTrend trend)
        {
            return trend switch
            {
                FinanceApp.Domain.Enums.SpendingTrend.Increasing => Colors.Red,
                FinanceApp.Domain.Enums.SpendingTrend.Decreasing => Colors.Green,
                FinanceApp.Domain.Enums.SpendingTrend.Stable => Colors.Blue,
                _ => Colors.Black
            };
        }
        return Colors.Black;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool boolValue && parameter is string param)
        {
            var parts = param.Split('|');
            if (parts.Length == 2)
            {
                return boolValue ? Color.FromArgb(parts[0]) : Color.FromArgb(parts[1]);
            }
        }
        return Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class InsightSeverityToIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is FinanceApp.Application.DTOs.InsightSeverity severity)
        {
            return severity switch
            {
                FinanceApp.Application.DTOs.InsightSeverity.Info => "ℹ️",
                FinanceApp.Application.DTOs.InsightSeverity.Warning => "⚠️",
                FinanceApp.Application.DTOs.InsightSeverity.Critical => "🔴",
                _ => "ℹ️"
            };
        }
        return "ℹ️";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class InsightSeverityToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is FinanceApp.Application.DTOs.InsightSeverity severity)
        {
            return severity switch
            {
                FinanceApp.Application.DTOs.InsightSeverity.Info => Colors.Blue,
                FinanceApp.Application.DTOs.InsightSeverity.Warning => Colors.Orange,
                FinanceApp.Application.DTOs.InsightSeverity.Critical => Colors.Red,
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