using FreelanceMonitor.Models;

namespace FreelanceMonitor.Notifications;

public interface INotifier
{
    Task NotifyAsync(FreelanceProject project, CancellationToken ct);
}
