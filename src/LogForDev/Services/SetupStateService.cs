using System.Text.Json;

namespace LogForDev.Services;

public interface ISetupStateService
{
    bool IsSetupComplete();
    void CompleteSetup();
}

public class SetupStateService : ISetupStateService
{
    private readonly string _stateFilePath;
    private readonly ILogger<SetupStateService> _logger;

    public SetupStateService(IWebHostEnvironment env, ILogger<SetupStateService> logger)
    {
        _stateFilePath = Path.Combine(env.ContentRootPath, "setup-state.json");
        _logger = logger;
    }

    public bool IsSetupComplete()
    {
        if (!File.Exists(_stateFilePath))
            return false;

        try
        {
            var json = File.ReadAllText(_stateFilePath);
            var state = JsonSerializer.Deserialize<SetupState>(json);
            return state?.SetupCompleted == true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read setup state file");
            return false;
        }
    }

    public void CompleteSetup()
    {
        var state = new SetupState
        {
            SetupCompleted = true,
            CompletedAt = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_stateFilePath, json);
        _logger.LogInformation("Setup completed and state saved");
    }

    private class SetupState
    {
        public bool SetupCompleted { get; set; }
        public DateTime CompletedAt { get; set; }
    }
}
