using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace FreelanceMonitor.Sources;

/// <summary>Общие помощники для источников: очистка HTML и извлечение бюджета.</summary>
public static partial class TextUtils
{
    /// <summary>Убирает теги (переводя &lt;br&gt;/&lt;/p&gt; в переносы), декодирует сущности, схлопывает пробелы.</summary>
    public static string Clean(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var withBreaks = BreakRegex().Replace(raw, "\n");
        var noTags = TagRegex().Replace(withBreaks, " ");
        var decoded = WebUtility.HtmlDecode(noTags);
        // Схлопываем пробелы, но сохраняем переносы строк.
        decoded = InlineSpaceRegex().Replace(decoded, " ");
        decoded = MultiNewlineRegex().Replace(decoded, "\n");
        return decoded.Trim();
    }

    /// <summary>Первая непустая строка текста — используем как заголовок для сообщений без title.</summary>
    public static string FirstLine(string text, int maxLen = 120)
    {
        var line = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? text;
        return line.Length > maxLen ? line[..maxLen] + "…" : line;
    }

    /// <summary>Первое денежное значение вида "15000 руб", "15 000 ₽", "от 120000". null — не найдено.</summary>
    public static decimal? ExtractBudget(string text)
    {
        var m = BudgetRegex().Match(text);
        if (!m.Success) return null;
        var digits = m.Groups["num"].Value.Replace(" ", "").Replace(" ", "").Replace(" ", "");
        return decimal.TryParse(digits, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    [GeneratedRegex(@"<br\s*/?>|</p>|</div>", RegexOptions.IgnoreCase)]
    private static partial Regex BreakRegex();

    [GeneratedRegex("<.*?>", RegexOptions.Singleline)]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"[^\S\n]+")]
    private static partial Regex InlineSpaceRegex();

    [GeneratedRegex(@"\n{2,}")]
    private static partial Regex MultiNewlineRegex();

    [GeneratedRegex(@"(?<num>\d[\d\s  ]{2,})\s*(?:руб|₽|р\.|rub)", RegexOptions.IgnoreCase)]
    private static partial Regex BudgetRegex();
}
