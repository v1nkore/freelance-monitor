using System.Net;
using FreelanceMonitor;
using FreelanceMonitor.Filtering;
using FreelanceMonitor.Notifications;
using FreelanceMonitor.Options;
using FreelanceMonitor.Sources;
using FreelanceMonitor.State;
using Microsoft.Extensions.Options;

// ContentRoot жёстко = папка exe: иначе при запуске из планировщика/службы рабочая
// папка — System32 и appsettings.json (с токеном и фильтром) не находится.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

// Позволяет запускать как Windows-службу (sc create ...) и как обычный процесс.
builder.Services.AddWindowsService(o => o.ServiceName = "FreelanceMonitor");

builder.Services.Configure<MonitorOptions>(builder.Configuration.GetSection(MonitorOptions.SectionName));
builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection(TelegramOptions.SectionName));

// HttpClient с браузерным UA и распаковкой gzip — иначе часть бирж отдаёт 403/пустоту.
builder.Services.AddHttpClient("scraper", c =>
    {
        c.Timeout = TimeSpan.FromSeconds(30);
        c.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36");
        c.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ru-RU,ru;q=0.9");
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
    });

builder.Services.AddHttpClient(); // безымянный — для Telegram

// Фильтр и хранилище — синглтоны.
builder.Services.AddSingleton(sp =>
    new MatchFilter(sp.GetRequiredService<IOptions<MonitorOptions>>().Value.Filter));
builder.Services.AddSingleton<SeenStore>();

// Уведомления: Telegram (сам падает в лог, если бот не настроен).
builder.Services.AddSingleton<INotifier, TelegramNotifier>();

// Источники: по одному RssProjectSource на каждый включённый фид из конфига.
builder.Services.AddSingleton<IEnumerable<IProjectSource>>(sp =>
{
    var opt = sp.GetRequiredService<IOptions<MonitorOptions>>().Value;
    var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

    var sources = new List<IProjectSource>();

    // RSS-биржи из конфига
    sources.AddRange(opt.Feeds
        .Where(f => f.Enabled && !string.IsNullOrWhiteSpace(f.Url))
        .Select(IProjectSource (f) => new RssProjectSource(
            f, httpFactory, loggerFactory.CreateLogger<RssProjectSource>())));

    // Публичные Telegram-каналы через t.me/s/
    sources.AddRange(opt.TelegramChannels
        .Where(c => !string.IsNullOrWhiteSpace(c))
        .Select(IProjectSource (c) => new TelegramChannelSource(
            c.Trim().TrimStart('@'), httpFactory, loggerFactory.CreateLogger<TelegramChannelSource>())));

    // Weblancer — HTML-парсер (серверный рендер)
    if (opt.Weblancer.Enabled && opt.Weblancer.Urls.Count > 0)
        sources.Add(new WeblancerHtmlSource(
            opt.Weblancer.Urls, httpFactory, loggerFactory.CreateLogger<WeblancerHtmlSource>()));

    return sources;
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
