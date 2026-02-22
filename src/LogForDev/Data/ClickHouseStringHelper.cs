namespace LogForDev.Data;

internal static class ClickHouseStringHelper
{
    /// <summary>
    /// Escapes a string value for safe use in ClickHouse SQL single-quoted literals.
    /// ClickHouse uses backslash escaping inside single-quoted strings.
    /// </summary>
    public static string Escape(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        return input
            .Replace("\\", "\\\\")   // backslash first
            .Replace("'", "\\'")     // single quote
            .Replace("\0", "")       // strip null bytes
            .Replace("\n", "\\n")    // newline
            .Replace("\r", "\\r")    // carriage return
            .Replace("\t", "\\t");   // tab
    }

    /// <summary>
    /// Wraps an escaped string in single quotes for use in SQL.
    /// </summary>
    public static string Quote(string input)
        => $"'{Escape(input)}'";

    /// <summary>
    /// Validates and returns a safe integer value, or throws if invalid.
    /// Prevents SQL injection through numeric parameters.
    /// </summary>
    public static int SafeInt(int value, int min = 0, int max = int.MaxValue)
    {
        if (value < min || value > max)
            throw new ArgumentOutOfRangeException(nameof(value), $"Value {value} is out of allowed range [{min}, {max}]");
        return value;
    }

    /// <summary>
    /// Validates a GUID string to prevent injection through ID fields.
    /// </summary>
    public static string SafeGuid(Guid value) => value.ToString();

    public static string SafeGuid(string value)
    {
        if (!Guid.TryParse(value, out var guid))
            throw new ArgumentException("Invalid GUID format", nameof(value));
        return guid.ToString();
    }

    /// <summary>
    /// Validates a value against an allowed whitelist. Used for enums and fixed values.
    /// </summary>
    public static string SafeWhitelist(string value, IReadOnlySet<string> allowed)
    {
        if (!allowed.Contains(value))
            throw new ArgumentException($"Value '{value}' is not in the allowed set", nameof(value));
        return value;
    }
}
