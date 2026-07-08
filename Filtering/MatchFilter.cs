using System.Text.RegularExpressions;
using FreelanceMonitor.Models;
using FreelanceMonitor.Options;

namespace FreelanceMonitor.Filtering;

/// <summary>
/// Решает, подходит ли заказ под профиль. Логика:
///   1) есть хотя бы одно include-слово (питон, api, бот, парсер...);
///   2) нет ни одного exclude-слова (вакансия, дизайн, карточка товара...);
///   3) если бюджет распознан и заданы Min/MaxBudget — бюджет в диапазоне.
///
/// Ключевое: матч по ГРАНИЦЕ СЛОВА, а не по подстроке. Иначе «бот» ловит
/// «работа/робот/разработка», «rest» ловит «pinterest», «ml» ловит «html».
/// Латиница — целое слово (обе границы), кириллица — по началу слова
/// (допускаем суффиксы: «парс» → «парсер/парсинг»).
/// </summary>
public sealed class MatchFilter
{
    private readonly Regex[] _include;
    private readonly Regex[] _exclude;
    private readonly decimal _minBudget;
    private readonly decimal _maxBudget;

    public MatchFilter(FilterOptions options)
    {
        _include = options.IncludeKeywords.Select(BuildMatcher).ToArray();
        _exclude = options.ExcludeKeywords.Select(BuildMatcher).ToArray();
        _minBudget = options.MinBudget;
        _maxBudget = options.MaxBudget;
    }

    public bool IsMatch(FreelanceProject project)
    {
        var text = project.SearchText;

        if (_exclude.Any(r => r.IsMatch(text)))
            return false;

        if (_include.Length > 0 && !_include.Any(r => r.IsMatch(text)))
            return false;

        if (_minBudget > 0 && project.Budget is { } b && b < _minBudget)
            return false;

        if (_maxBudget > 0 && project.Budget is { } bmax && bmax > _maxBudget)
            return false;

        return true;
    }

    private static Regex BuildMatcher(string keyword)
    {
        var kw = keyword.Trim().ToLowerInvariant();
        var isAscii = kw.All(c => c < 128);
        var esc = Regex.Escape(kw);

        // Слева всегда требуем границу: перед ключом не буква/цифра.
        // Это убирает «работа»→«бот», «pinterest»→«rest», «html»→«ml».
        const string left = @"(?<![\p{L}\p{Nd}])";
        // Латиница — целое слово (правая граница тоже). Кириллица — префикс (суффиксы ок).
        var right = isAscii ? @"(?![\p{L}\p{Nd}])" : "";

        return new Regex(left + esc + right,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }
}
