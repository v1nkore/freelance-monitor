using System.Net;
using System.Text;
using System.Text.Json;
using FreelanceMonitor.Models;
using FreelanceMonitor.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FreelanceMonitor.Notifications;

/// <summary>
/// Шлёт находки в личку через Telegram Bot API (метод sendMessage).
/// Никаких пакетов — обычный HTTP. Если бот не настроен, дублирует в лог.
/// </summary>
public sealed class TelegramNotifier(
    IOptions<TelegramOptions> options,
    IHttpClientFactory httpFactory,
    ILogger<TelegramNotifier> logger) : INotifier
{
    private readonly TelegramOptions _opt = options.Value;

    public async Task NotifyAsync(FreelanceProject project, CancellationToken ct)
    {
        if (!_opt.IsConfigured)
        {
            var budget = project.Budget is { } b0 ? $" | 💰 {b0:N0} ₽" : "";
            logger.LogInformation("🆕 (Telegram не настроен) [{Source}]{Budget} {Title} {Url}",
                project.Source, budget, project.Title, project.Url);
            return;
        }

        // Отклик, адаптированный под эту заявку (ссылка на заголовок + специализация по типу задачи).
        var reply = ReplyBuilder.Build(project, _opt.Contact);

        var text = BuildMessage(project, reply);
        var url = $"https://api.telegram.org/bot{_opt.BotToken}/sendMessage";

        // Кнопки: (1) copy_text кладёт готовый отклик в буфер по тапу,
        //         (2) url открывает страницу заказа, где жмёшь «Откликнуться» и вставляешь Ctrl+V.
        var buttons = new List<object[]>();
        buttons.Add([new { text = "📋 Скопировать отклик", copy_text = new { text = reply } }]);
        buttons.Add([new { text = "🔗 Открыть заказ / Откликнуться", url = project.Url }]);

        var payload = new
        {
            chat_id = _opt.ChatId,
            text,
            parse_mode = "HTML",
            disable_web_page_preview = true,
            reply_markup = new { inline_keyboard = buttons },
        };

        try
        {
            var client = httpFactory.CreateClient();
            using var content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var resp = await client.PostAsync(url, content, ct);

            if (resp.StatusCode == HttpStatusCode.TooManyRequests)
            {
                // Telegram просит подождать — пауза, следующий цикл дошлёт.
                logger.LogWarning("Telegram rate limit, пропускаю до следующего цикла");
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
                return;
            }

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                logger.LogWarning("Telegram ответил {Code}: {Body}", (int)resp.StatusCode, body);
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Не удалось отправить в Telegram: {Title}", project.Title);
        }
    }

    private static string BuildMessage(FreelanceProject p, string reply)
    {
        var sb = new StringBuilder();
        sb.Append("<b>").Append(Escape(p.Title)).Append("</b>\n");
        sb.Append("📌 ").Append(Escape(p.Source));
        if (p.Budget is { } b) sb.Append(" | 💰 ").Append(b.ToString("N0")).Append(" ₽");
        sb.Append('\n');

        if (!string.IsNullOrWhiteSpace(p.Description))
        {
            var desc = p.Description.Length > 400 ? p.Description[..400] + "…" : p.Description;
            sb.Append(Escape(desc)).Append('\n');
        }

        // Готовый отклик под эту заявку — видно сразу, копируется кнопкой ниже.
        sb.Append("\n💬 <b>Отклик</b> (кнопка ниже копирует):\n");
        sb.Append("<blockquote>").Append(Escape(reply)).Append("</blockquote>");

        // Ссылка на заказ — в кнопке под сообщением (reply_markup).
        return sb.ToString();
    }

    // Экранируем спецсимволы HTML parse_mode, чтобы «<», «>», «&» из текста не ломали разметку.
    private static string Escape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
