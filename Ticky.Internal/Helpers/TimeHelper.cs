namespace Ticky.Internal.Helpers;

public static class TimeHelper
{
    public static string ToElapsedString(this DateTime dateTime)
    {
        var difference = DateTime.Now - dateTime;

        if (difference.TotalMinutes < 1)
            return "now";
        else if (difference.TotalMinutes < 2)
            return $"{Math.Round(difference.TotalMinutes)} min ago";
        else if (difference.TotalHours < 1)
            return $"{Math.Round(difference.TotalMinutes)} mins ago";
        else if (difference.TotalHours < 2)
            return $"{Math.Round(difference.TotalHours)} hr ago";
        else if (difference.TotalDays < 1)
            return $"{Math.Round(difference.TotalHours)} hrs ago";
        else if (difference.TotalDays <= 2)
            return $"yesterday, {dateTime.ToString("HH:mm")}";
        else if (difference.TotalDays < 7)
            return $"{Math.Round(difference.TotalDays)} days ago";
        else if (dateTime.Year == DateTime.Now.Year)
            return dateTime.ToString("dd. MMM");

        return dateTime.ToString("dd. MMM yyyy");
    }

    public static string ToElapsedString(this TimeSpan timeSpan, bool cutOffSeconds = false)
    {
        string result = string.Empty;

        var hoursValue = Math.Floor(timeSpan.TotalHours);
        if (hoursValue != 0)
            result += $" {hoursValue}h";

        if (timeSpan.Minutes != 0)
            result += $" {timeSpan.Minutes}m";

        if (!cutOffSeconds)
            result += $" {timeSpan.Seconds}s";

        return result.Trim();
    }

    public static string ToShortString(this DateTime dateTime) =>
        dateTime.Date == DateTime.Now.Date
            ? dateTime.ToString("HH:mm")
            : dateTime.ToString("MMM d");
}
