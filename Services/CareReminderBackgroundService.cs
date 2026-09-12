namespace HomePlant.Services;

public sealed class CareReminderBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CareReminderBackgroundService> _logger;

    public CareReminderBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<CareReminderBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var minutes = int.TryParse(
            _configuration["Notifications:ScanIntervalMinutes"],
            out var configuredMinutes)
            ? Math.Clamp(configuredMinutes, 1, 1440)
            : 5;

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(minutes));
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var reminders = scope.ServiceProvider.GetRequiredService<CareReminderService>();
                await reminders.SyncAll(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Care reminder scan failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
