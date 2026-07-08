using FreelanceMonitor.Models;

namespace FreelanceMonitor.Sources;

/// <summary>
/// Источник заказов. Реализации: RSS (RssProjectSource) и позже
/// HTML-парсеры для бирж без фидов (Weblancer, Freelance.ru).
/// </summary>
public interface IProjectSource
{
    string Name { get; }

    /// <summary>Забрать текущий список проектов. Ошибки сети обрабатываются внутри —
    /// при сбое возвращается пустой список, чтобы не ронять весь цикл.</summary>
    Task<IReadOnlyList<FreelanceProject>> FetchAsync(CancellationToken ct);
}
