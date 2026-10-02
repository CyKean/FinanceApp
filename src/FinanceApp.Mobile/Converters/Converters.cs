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

/// <summary>
/// Maps a <see cref="FinanceApp.Domain.Enums.TransactionType"/> onto the Paytin
/// palette: Income is lime, Expense is ink. ConverterParameter selects the tone:
/// "accent" (default) for the fill, "onAccent" for text/icon colour on that fill.
/// </summary>
public class TransactionTypeToPayColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        var isIncome = value is FinanceApp.Domain.Enums.TransactionType.Income;
        var onAccent = string.Equals(parameter as string, "onAccent", StringComparison.OrdinalIgnoreCase);

        var key = (isIncome, onAccent) switch
        {
            (true, false) => "PayLime",
            (true, true) => "PayInk",
            (false, false) => "PayInk",
            _ => "PayLime"
        };

        return ResolveColor(key);
    }

    private static Color ResolveColor(string key)
    {
        var resources = Application.Current?.Resources;
        if (resources is not null)
        {
            if (resources.TryGetValue(key, out var direct) && direct is Color directColor)
                return directColor;

            foreach (var dictionary in resources.MergedDictionaries)
            {
                if (dictionary.TryGetValue(key, out var merged) && merged is Color mergedColor)
                    return mergedColor;
            }
        }

        return key == "PayLime" ? Color.FromArgb("#CDF463") : Color.FromArgb("#161B16");
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
            return BoolToStyleConverter.Resolve(currentType == param);
        }
        return BoolToStyleConverter.Resolve(false);
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
            return BoolToStyleConverter.Resolve(currentTab == param);
        }
        return BoolToStyleConverter.Resolve(false);
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
        {
            if (parameter is string parameterText && parameterText.Equals("Plain", System.StringComparison.OrdinalIgnoreCase))
                return money.Amount.ToString("N2", culture);
            return money.ToString();
        }
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

public class CountToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is int count)
            return count > 0;
        if (value is System.Collections.ICollection collection)
            return collection.Count > 0;
        return value != null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Maps a <see cref="FinanceApp.Application.Notifications.NotificationSeverity"/>
/// onto a distinct accent so critical alerts do not read the same as info ones:
/// Info = blue, Success = green, Warning = amber, Critical = red.
/// </summary>
public class SeverityToAccentConverter : IValueConverter
{
    private const string Fallback = "#161B16";

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        var key = value switch
        {
            FinanceApp.Application.Notifications.NotificationSeverity.Info => "Info",
            FinanceApp.Application.Notifications.NotificationSeverity.Success => "Success",
            FinanceApp.Application.Notifications.NotificationSeverity.Warning => "Warning",
            FinanceApp.Application.Notifications.NotificationSeverity.Critical => "Error",
            _ => "PayInk"
        };

        var resources = Application.Current?.Resources;
        if (resources is not null)
        {
            if (resources.TryGetValue(key, out var direct) && direct is Color directColor)
                return directColor;

            foreach (var dictionary in resources.MergedDictionaries)
            {
                if (dictionary.TryGetValue(key, out var merged) && merged is Color mergedColor)
                    return mergedColor;
            }
        }

        return Color.FromArgb(Fallback);
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

public class BoolToStyleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        var selected = value is true;
        if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase))
            selected = !selected;

        return Resolve(selected);
    }

    public static Style? Resolve(bool selected)
    {
        var key = selected ? "PrimaryButtonStyle" : "OutlineButtonStyle";
        var resources = Application.Current?.Resources;
        if (resources == null)
            return null;

        if (resources.TryGetValue(key, out var direct) && direct is Style directStyle)
            return directStyle;

        foreach (var dictionary in resources.MergedDictionaries)
        {
            if (dictionary.TryGetValue(key, out var merged) && merged is Style mergedStyle)
                return mergedStyle;
        }

        return null;
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
            // Lucide keys rendered by PaytinIconView (no emoji).
            return severity switch
            {
                FinanceApp.Application.DTOs.InsightSeverity.Info => "bulb",
                FinanceApp.Application.DTOs.InsightSeverity.Warning => "alert",
                FinanceApp.Application.DTOs.InsightSeverity.Critical => "alertCircle",
                _ => "bulb"
            };
        }
        return "bulb";
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

/// <summary>
/// Displays UTC timestamps in Philippine time (Asia/Manila, fixed UTC+8, no DST).
/// </summary>
public class ManilaTimeConverter : IValueConverter
{
    private static readonly TimeZoneInfo ManilaZone = ResolveManilaZone();

    private static TimeZoneInfo ResolveManilaZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
        }
        catch
        {
            return TimeZoneInfo.CreateCustomTimeZone("Asia/Manila", TimeSpan.FromHours(8), "Manila", "Manila");
        }
    }

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is DateTime dateTime)
        {
            var utc = dateTime.Kind == DateTimeKind.Utc
                ? dateTime
                : DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, ManilaZone);
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Paytin-style badge: first letter of the category name (e.g. "S" for Shopping).
/// Used instead of raw emoji so list icons match the mockup's minimal badges.
/// </summary>
public class CategoryInitialConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is string name && !string.IsNullOrWhiteSpace(name))
        {
            foreach (var ch in name.Trim())
            {
                if (char.IsLetterOrDigit(ch))
                    return char.ToUpperInvariant(ch).ToString();
            }
        }
        return "?";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}