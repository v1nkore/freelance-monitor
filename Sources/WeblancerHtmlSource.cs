using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using FreelanceMonitor.Models;
using Microsoft.Extensions.Logging;

namespace FreelanceMonitor.Sources;

/// <summary>
/// Weblancer отдаёт список проектов серверным рендером (Tailwind-разметка) — берём
/// обычным HTTP + AngleSharp, без headless-браузера. Проект = якорь с href вида
/// /freelance/&lt;категория&gt;/&lt;слаг&gt;-&lt;id&gt;/, заголовок в .capitalize-first,
/// цена в .text-green-600.
/// </summary>
public sealed partial class WeblancerHtmlSource(
    IReadOnlyList<string> urls, IHttpClientFactory httpFactory, ILogger logger) : IProjectSource
{
    private const string Base = "https://www.weblancer.net";
    private static readonly HtmlParser Parser = new();

    public string Name => "Weblancer";

    public async Task<IReadOnlyList<FreelanceProject>> FetchAsync(CancellationToken ct)
    {
        var byId = new Dictionary<string, FreelanceProject>();

        foreach (var url in urls)
        {
            try
            {
                var client = httpFactory.CreateClient("scraper");
                var html = await client.GetStringAsync(url, ct);
                var doc = await Parser.ParseDocumentAsync(html, ct);

                foreach (var titleEl in doc.QuerySelectorAll(".capitalize-first"))
                {
                    // Разметка: <h2 class="...capitalize-first"><a href="/freelance/.../-id/">Заголовок</a></h2>
                    var anchor = titleEl.QuerySelector("a[href]");
                    var href = anchor?.GetAttribute("href");
                    if (href is null || !ProjectHref().IsMatch(href)) continue;

                    var title = TextUtils.Clean(titleEl.TextContent);
                    if (string.IsNullOrWhiteSpace(title)) continue;

                    var card = FindCard(titleEl);
                    var priceText = card?.QuerySelector(".text-green-600")?.TextContent ?? "";

                    var absUrl = Base + href;
                    byId[absUrl] = new FreelanceProject
                    {
                        Id = absUrl,
                        Source = Name,
                        Title = title,
                        Url = absUrl,
                        Description = string.Empty, // заголовки Weblancer информативны, фильтруем по ним
                        Budget = ParseBudget(priceText),
                    };
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                // Ловим и сетевые ошибки, и таймаут HttpClient — один сбойный URL не рушит цикл.
                logger.LogWarning(ex, "Weblancer: ошибка загрузки {Url}", url);
            }
        }

        logger.LogDebug("Weblancer: получено {Count}", byId.Count);
        return byId.Values.ToList();
    }

    /// <summary>Поднимаемся до контейнера карточки, где рядом лежит цена.</summary>
    private static IElement? FindCard(IElement titleEl) =>
        titleEl.Closest("div.space-y-3") ?? titleEl.ParentElement?.ParentElement;

    private static decimal? ParseBudget(string text)
    {
        var digits = new string(text.Where(char.IsDigit).ToArray());
        return decimal.TryParse(digits, out var v) && v > 0 ? v : null;
    }

    [GeneratedRegex(@"^/freelance/[^/]+/[^/]+-\d+/?$")]
    private static partial Regex ProjectHref();
}
