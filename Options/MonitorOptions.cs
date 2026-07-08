namespace FreelanceMonitor.Options;

/// <summary>Корневая секция конфигурации "Monitor" из appsettings.json.</summary>
public sealed class MonitorOptions
{
    public const string SectionName = "Monitor";

    /// <summary>Как часто опрашивать биржи, секунды.</summary>
    public int PollIntervalSeconds { get; set; } = 300;

    public List<FeedOptions> Feeds { get; set; } = [];

    /// <summary>Публичные Telegram-каналы (username без @), читаются через t.me/s/.</summary>
    public List<string> TelegramChannels { get; set; } = [];

    /// <summary>Weblancer — HTML-парсер (серверный рендер), список страниц-листингов.</summary>
    public WeblancerOptions Weblancer { get; set; } = new();

    /// <summary>Окно активности: вне него заказы помечаются молча, без уведомлений.</summary>
    public ActiveHoursOptions ActiveHours { get; set; } = new();

    public FilterOptions Filter { get; set; } = new();
}

/// <summary>Описание одного RSS-источника.</summary>
public sealed class FeedOptions
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

public sealed class ActiveHoursOptions
{
    public bool Enabled { get; set; } = true;
    public int StartHour { get; set; } = 9;   // включительно
    public int EndHour { get; set; } = 23;     // не включительно
    public int UtcOffsetHours { get; set; } = 3; // МСК = UTC+3, без перехода на летнее время
}

public sealed class WeblancerOptions
{
    public bool Enabled { get; set; }
    public List<string> Urls { get; set; } = [];
}

public sealed class FilterOptions
{
    /// <summary>Проект проходит, если содержит хотя бы одно из этих слов.</summary>
    public List<string> IncludeKeywords { get; set; } = [];

    /// <summary>Проект отбрасывается, если содержит хотя бы одно из этих слов.</summary>
    public List<string> ExcludeKeywords { get; set; } = [];

    /// <summary>Минимальный бюджет (руб). 0 — не фильтровать по бюджету.
    /// Проекты без распознанного бюджета не отсекаются.</summary>
    public decimal MinBudget { get; set; }

    /// <summary>Максимальный бюджет (руб). Отсекает крупные/долгие проекты, оставляя
    /// короткие задачи «сделал и забыл». 0 — не фильтровать.
    /// Проекты без распознанного бюджета не отсекаются.</summary>
    public decimal MaxBudget { get; set; }
}

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = "";
    public string ChatId { get; set; } = "";

    /// <summary>Контакт в подписи отклика. Пусто — не добавлять (общение на платформе).</summary>
    public string Contact { get; set; } = "";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BotToken) && !string.IsNullOrWhiteSpace(ChatId);
}
