namespace LogForDev.Data;

internal static class ClickHouseStringHelper
{
    public static string Escape(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return input
            .Replace("\\", "\\\\")
            .Replace("'", "\\'");
    }
}
