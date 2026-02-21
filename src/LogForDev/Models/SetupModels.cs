namespace LogForDev.Models;

public class ConnectionTestRequest
{
    public string? Host { get; set; }
    public int Port { get; set; }
    public string? Database { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public class TestLogRequest
{
    public string? ApiKey { get; set; }
}

public class SetupCompleteRequest
{
    public string? ClickHouseHost { get; set; }
    public int ClickHousePort { get; set; }
    public string? ClickHouseDatabase { get; set; }
    public string? ClickHouseUsername { get; set; }
    public string? ClickHousePassword { get; set; }
    public string? ProjectName { get; set; }
    public string? ApiKey { get; set; }
    public int KeyExpiryDays { get; set; }
    public int RetentionDays { get; set; }
    public string? AdminEmail { get; set; }
    public string? AdminPassword { get; set; }
}
