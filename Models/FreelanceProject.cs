namespace FreelanceMonitor.Models;

/// <summary>
/// Единый вид проекта/заказа с любой биржи. К этому виду приводят
/// все источники (RSS, HTML-парсер), чтобы фильтр и уведомления
/// не зависели от конкретной площадки.
/// </summary>
public sealed record FreelanceProject
{
    /// <summary>Стабильный идентификатор для дедупликации (обычно ссылка или guid).</summary>
    public required string Id { get; init; }

    public required string Source { get; init; }

    public required string Title { get; init; }

    public required string Url { get; init; }

    public string Description { get; init; } = string.Empty;

    /// <summary>Бюджет, если удалось распарсить (в рублях). null — не определён.</summary>
    public decimal? Budget { get; init; }

    public DateTimeOffset? Published { get; init; }

    /// <summary>Текст, по которому работает фильтр: заголовок + описание в нижнем регистре.</summary>
    public string SearchText => $"{Title}\n{Description}".ToLowerInvariant();
}
