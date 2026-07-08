using FreelanceMonitor.Models;
using Microsoft.Extensions.Logging;

namespace FreelanceMonitor.Notifications;

/// <summary>Резервный канал: пишет находки в лог. Работает без настройки Telegram.</summary>
public sealed class ConsoleNotifier(ILogger<ConsoleNotifier> logger) : INotifier
{
    public Task NotifyAsync(FreelanceProject project, CancellationToken ct)
    {
        var budget = project.Budget is { } b ? $" | 💰 {b:N0} ₽" : "";
        logger.LogInformation("🆕 [{Source}]{Budget} {Title}\n    {Url}",
            project.Source, budget, project.Title, project.Url);
        return Task.CompletedTask;
    }
}
