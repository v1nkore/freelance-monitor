using AngleSharp.Html.Parser;
using FreelanceMonitor.Models;
using Microsoft.Extensions.Logging;

namespace FreelanceMonitor.Sources;

/// <summary>
/// Читает публичный канал через веб-превью https://t.me/s/&lt;channel&gt; — обычный HTML,
/// без доступа к аккаунту и без Bot API. Парсит последние сообщения AngleSharp'ом.
/// </summary>
public sealed class TelegramChannelSource(string channel, IHttpClientFactory httpFactory, ILogger logger)
    : IProjectSource
{
    private static readonly HtmlParser Parser = new();

    public string Name => $"tg:{channel}";

    public async Task<IReadOnlyList<FreelanceProject>> FetchAsync(CancellationToken ct)
    {
        try
        {
            var client = httpFactory.CreateClient("scraper");
            var html = await client.GetStringAsync($"https://t.me/s/{channel}", ct);
            var doc = await Parser.ParseDocumentAsync(html, ct);

            var result = new List<FreelanceProject>();
            foreach (var msg in doc.QuerySelectorAll(".tgme_widget_message"))
            {
                var textEl = msg.QuerySelector(".tgme_widget_message_text");
                if (textEl is null) continue;

                var text = TextUtils.Clean(textEl.InnerHtml);
                if (string.IsNullOrWhiteSpace(text)) continue;

                var post = msg.GetAttribute("data-post");            // "channel/2466"
                var link = msg.QuerySelector("a.tgme_widget_message_date")?.GetAttribute("href")
                           ?? (post is null ? null : $"https://t.me/{post}");
                if (link is null) continue;

                var time = msg.QuerySelector("time")?.GetAttribute("datetime");

                result.Add(new FreelanceProject
                {
                    Id = post ?? link,
                    Source = Name,
                    Title = TextUtils.FirstLine(text),
                    Url = link,
                    Description = text,
                    Budget = TextUtils.ExtractBudget(text),
                    Published = DateTimeOffset.TryParse(time, out var d) ? d : null,
                });
            }

            logger.LogDebug("{Source}: получено {Count} сообщений", Name, result.Count);
            return result;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "{Source}: ошибка чтения канала", Name);
            return [];
        }
    }
}
