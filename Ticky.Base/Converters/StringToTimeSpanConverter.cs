namespace Ticky.Base.Converters;

public static class StringToTimeSpanConverter
{
    public static TimeSpan ConvertToTimeSpan(this string str)
    {
        var time = new TimeSpan();

        foreach (var part in str.Split(' '))
        {
            if (part.EndsWith("h", StringComparison.OrdinalIgnoreCase))
                time = time.Add(TimeSpan.FromHours(int.Parse(part.TrimEnd('h'))));
            else if (part.EndsWith("m", StringComparison.OrdinalIgnoreCase))
                time = time.Add(TimeSpan.FromMinutes(int.Parse(part.TrimEnd('m'))));
            else if (part.EndsWith("s", StringComparison.OrdinalIgnoreCase))
                time = time.Add(TimeSpan.FromSeconds(int.Parse(part.TrimEnd('s'))));
            else
                throw new InvalidDataException();
        }

        return time;
    }

    public static string ConvertToString(this TimeSpan time)
    {
        string result = string.Empty;

        if (time.Hours > 0)
            result += $"{time.Hours}h ";

        if (time.Minutes > 0)
            result += $"{time.Minutes}m ";

        if (time.Seconds > 0 || string.IsNullOrWhiteSpace(result))
            result += $"{time.Seconds}s ";

        return result.Trim();
    }
}
