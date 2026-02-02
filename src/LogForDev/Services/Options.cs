namespace LogForDev.Services;

public class LogForDevOptions
{
    public string ApiKey { get; set; } = "change-me";
    public int RetentionDays { get; set; } = 30;
}

public class ClickHouseOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 8123;
    public string Database { get; set; } = "logfordev";
    public string? Username { get; set; }
    public string? Password { get; set; }

    public string ConnectionString =>
        string.IsNullOrEmpty(Username)
            ? $"Host={Host};Port={Port};Database={Database}"
            : $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password}";

    public string DefaultConnectionString =>
        string.IsNullOrEmpty(Username)
            ? $"Host={Host};Port={Port};Database=default"
            : $"Host={Host};Port={Port};Database=default;Username={Username};Password={Password}";
}
