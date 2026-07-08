using FreelanceMonitor.Filtering;
using FreelanceMonitor.Notifications;
using FreelanceMonitor.Options;
using FreelanceMonitor.Sources;
using FreelanceMonitor.State;
using Microsoft.Extensions.Options;

namespace FreelanceMonitor;

/// <summary>
/// Главный цикл: раз в PollIntervalSeconds обходит все источники, отбирает новые
/// подходящие заказы и шлёт их в Telegram. При самом первом запуске (пустое
/// хранилище) только «запоминает» текущие заказы, ничего не отправляя, — иначе
/// прилетел бы шквал старых объявлений.
/// </summary>
public sealed class Worker(
    IEnumerable<IProjectSource> sources,
    MatchFilter filter,
    SeenStore seen,
    INotifier notifier,
    IOptions<MonitorOptions> options,
    ILogger<Worker> logger) : BackgroundService
{
    private readonly MonitorOptions _opt = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var srcList = sources.ToList();
        logger.LogInformation(
            "FreelanceMonitor запущен. Источников: {Count}, интервал: {Interval}с, известно заказов: {Seen}",
            srcList.Count, _opt.PollIntervalSeconds, seen.Count);

        var firstRun = seen.Count == 0;
        if (firstRun)
            logger.LogInformation("Первый запуск: текущие заказы будут помечены без отправки, уведомления пойдут с новых.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var active = IsWithinActiveHours();
            // Уведомляем только внутри окна активности и не на первом (сеющем) запуске.
            // Ночью цикл идёт как обычно, но молча помечает заказы — иначе утром был бы шквал.
            var notify = active && !firstRun;

            try
            {
                await RunCycleAsync(srcList, notify, active, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break; // штатная остановка
            }
            catch (Exception ex)
            {
                // Ни одна ошибка цикла не должна ронять сервис — логируем и ждём следующего.
                logger.LogError(ex, "Ошибка в цикле опроса, продолжаю работу");
            }
            firstRun = false;

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_opt.PollIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RunCycleAsync(List<IProjectSource> sources, bool notify, bool active, CancellationToken ct)
    {
        var newMatches = 0;
        var scanned = 0;

        foreach (var source in sources)
        {
            var projects = await source.FetchAsync(ct);
            scanned += projects.Count;

            foreach (var project in projects)
            {
                if (!seen.IsNew(project.Id))
                    continue;

                seen.Add(project.Id);

                if (!filter.IsMatch(project))
                    continue;

                if (notify)
                {
                    await notifier.NotifyAsync(project, ct);
                    newMatches++;
                }
            }
        }

        seen.Save();
        var mode = active ? "" : " (тихий режим — вне окна активности, уведомления выключены)";
        logger.LogInformation("Цикл завершён: просмотрено {Scanned}, отправлено {New}{Mode}",
            scanned, newMatches, mode);
    }

    /// <summary>Внутри ли текущее время окна активности (по умолчанию 09:00–23:00 МСК).</summary>
    private bool IsWithinActiveHours()
    {
        var ah = _opt.ActiveHours;
        if (!ah.Enabled) return true;

        var hour = DateTime.UtcNow.AddHours(ah.UtcOffsetHours).Hour;
        return ah.StartHour <= ah.EndHour
            ? hour >= ah.StartHour && hour < ah.EndHour   // обычное окно в пределах суток
            : hour >= ah.StartHour || hour < ah.EndHour;  // окно, переходящее через полночь
    }
}
