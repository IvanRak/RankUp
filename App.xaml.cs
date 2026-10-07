namespace RankUp;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());

        // ═══════════════════════════════════════════
        // ФОНОВАЯ ПРЕДЗАГРУЗКА ИКОНОК ГЕРОЕВ И ПРЕДМЕТОВ
        // ═══════════════════════════════════════════
        _ = Task.Run(async () =>
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[Prefetch] Старт");

                await RankUp.Models.ItemDatabase.PrefetchAllAsync();
                await RankUp.Models.HeroDatabase.PrefetchAllAsync();

                System.Diagnostics.Debug.WriteLine("[Prefetch] Всё готово");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Prefetch] Ошибка: {ex.Message}");
            }
        });

        // ═══════════════════════════════════════════
        // ПЕРЕХОД НА НУЖНУЮ СТРАНИЦУ
        // ═══════════════════════════════════════════
        var steamId = Preferences.Default.Get("steam_id", "");
        var target = string.IsNullOrEmpty(steamId) ? "//login" : "//profile";

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(100);
            await Shell.Current.GoToAsync(target);
        });

        return window;
    }
}