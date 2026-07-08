using System.Globalization;
using System.Xml.Linq;
using FreelanceMonitor.Models;
using FreelanceMonitor.Options;
using Microsoft.Extensions.Logging;

namespace FreelanceMonitor.Sources;

/// <summary>
/// Универсальный источник для любого RSS 2.0 фида (FL.ru, Freelancehunt, career.habr.com).
/// Парсит &lt;item&gt; вручную через LINQ to XML — без сторонних зависимостей.
/// </summary>
public sealed class RssProjectSource(FeedOptions feed, IHttpClientFactory httpFactory, ILogger logger)
    : IProjectSource
{
    public string Name => feed.Name;

    public async Task<IReadOnlyList<FreelanceProject>> FetchAsync(CancellationToken ct)
    {
        try
        {
            var client = httpFactory.CreateClient("scraper");
            var xml = await client.GetStringAsync(feed.Url, ct);
            var doc = XDocument.Parse(xml);

            var items = doc.Descendants("item")
                .Select(ParseItem)
                .Where(p => p is not null)
                .Select(p => p!)
                .ToList();

            logger.LogDebug("{Source}: получено {Count} элементов", Name, items.Count);
            return items;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Ловим и сетевые ошибки, и таймаут HttpClient (TaskCanceledException без отмены токена).
            logger.LogWarning(ex, "{Source}: ошибка загрузки фида {Url}", Name, feed.Url);
            return [];
        }
    }

    private FreelanceProject? ParseItem(XElement item)
    {
        var title = TextUtils.Clean((string?)item.Element("title"));
        var link = ((string?)item.Element("link"))?.Trim();
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link))
            return null;

        var description = TextUtils.Clean((string?)item.Element("description"));
        var guid = ((string?)item.Element("guid"))?.Trim();
        var id = !string.IsNullOrWhiteSpace(guid) ? guid : link;

        return new FreelanceProject
        {
            Id = id,
            Source = Name,
            Title = title,
            Url = link,
            Description = description,
            Budget = TextUtils.ExtractBudget($"{title} {description}"),
            Published = ParseDate((string?)item.Element("pubDate")),
        };
    }

    private static DateTimeOffset? ParseDate(string? raw) =>
        DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
}
