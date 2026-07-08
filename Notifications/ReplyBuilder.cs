using FreelanceMonitor.Models;

namespace FreelanceMonitor.Notifications;

/// <summary>
/// Собирает отклик, адаптированный под конкретную заявку: ссылается на её заголовок
/// и подбирает специализацию по типу задачи (бот/парсер/интеграция/автоматизация/бэкенд),
/// распознанному из текста. Без внешних LLM — детерминированно и мгновенно.
/// Лимит 256 символов (ограничение Telegram-кнопки copy_text) держим за счёт усечения заголовка.
/// </summary>
public static class ReplyBuilder
{
    private const int Limit = 256;
    private const string DefaultSpecialty = "бэкенд и автоматизацию";

    // Порядок важен: первое совпадение выигрывает.
    private static readonly (string[] Triggers, string Specialty)[] Map =
    [
        (["бот", "робот", "telegram bot", "discord", "чат-бот"], "Telegram-ботов и автоматизацию"),
        (["парс", "скрап", "scrap", "сбор данных"], "парсеры и сбор данных"),
        (["интеграц", "webhook", "вебхук", "grpc", "api"], "интеграции по API"),
        (["автоматизац", "скрипт", "макрос", "vba"], "автоматизацию рутины и скрипты"),
        (["backend", "бэкенд", "микросервис", "postgres", "sql", "база данных"], "бэкенд-разработку"),
    ];

    public static string Build(FreelanceProject p, string contact)
    {
        var text = p.SearchText;
        var specialty = DefaultSpecialty;
        foreach (var (triggers, spec) in Map)
            if (triggers.Any(text.Contains)) { specialty = spec; break; }

        // Контакт по умолчанию не добавляем — общение держим на платформе.
        var tail = string.IsNullOrWhiteSpace(contact) ? "" : $" {contact}";

        const string prefix = "Здравствуйте! Увидел задачу «";
        var suffix = $"». Готов взять — делаю {specialty}. Опишите детали или пришлите ТЗ — оценю сроки и стоимость, готов обсудить здесь.{tail}";

        var room = Limit - prefix.Length - suffix.Length;
        var title = p.Title.Trim();

        if (room < 12) // экзотический случай (очень длинные specialty+contact) — без заголовка
            return $"Здравствуйте! Готов взять задачу — делаю {specialty}. Опишите детали или пришлите ТЗ — оценю сроки и стоимость, готов обсудить здесь.{tail}";

        if (title.Length > room)
            title = title[..(room - 1)].TrimEnd() + "…";

        return prefix + title + suffix;
    }
}
