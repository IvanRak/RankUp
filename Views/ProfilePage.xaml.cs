using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.Maui.Controls.Shapes;
using RankUp.Models;
using RankUp.Services;

namespace RankUp.Views;

public partial class ProfilePage : ContentPage
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly DatabaseService _db;

    public ObservableCollection<DotaMatch> AllMatches { get; } = new();
    public ObservableCollection<DotaMatch> Visible { get; } = new();

    private long _accountId;
    private int _apiOffset = 0;
    private bool _isLoadingMore = false;
    private bool _hasMoreFromApi = true;
    private int _activeFilter = 0;

    private const string FavMatchesKey = "fav_matches";
    private const string FavAlliesKey = "fav_allies";

    private readonly Dictionary<long, Teammate> _teammates = new();

    private List<HeroPickerItem> _allHeroItems = new();
    private int _selectedHeroId = 0;

    public ProfilePage(DatabaseService db)
    {
        InitializeComponent();
        _db = db;
        MatchesList.ItemsSource = Visible;
        MatchesList.RemainingItemsThreshold = 5;
        MatchesList.RemainingItemsThresholdReached += OnNearBottom;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_accountId != 0) return;
        await InitialLoadAsync();
    }

    private async Task InitialLoadAsync()
    {
        var steamId = Preferences.Default.Get("steam_id", "");
        var accountIdStr = Preferences.Default.Get("account_id", "");

        if (string.IsNullOrEmpty(steamId) || string.IsNullOrEmpty(accountIdStr))
        {
            await Shell.Current.GoToAsync("//login");
            return;
        }

        _accountId = long.Parse(accountIdStr);
        NameLabel.Text = Preferences.Default.Get("persona_name", "Player");
        SteamIdLabel.Text = steamId;

        var avatar = Preferences.Default.Get("avatar_url", "");
        if (!string.IsNullOrEmpty(avatar))
        {
            AvatarImage.Source = new UriImageSource
            {
                Uri = new Uri(avatar),
                CachingEnabled = true,
                CacheValidity = TimeSpan.FromDays(7)
            };
        }

        Loader.IsRunning = true;

        try
        {
            var favs = LoadFavorites();
            var cached = await _db.GetMatchesAsync(_accountId);
            foreach (var m in cached)
            {
                m.IsFavorite = favs.Contains(m.MatchId);
                AllMatches.Add(m);

                foreach (var ally in m.Allies)
                {
                    if (!_teammates.ContainsKey(ally.AccountId))
                    {
                        _teammates[ally.AccountId] = new Teammate
                        {
                            AccountId = ally.AccountId,
                            Name = ally.Name,
                            Avatar = ally.Avatar,
                            GamesTogether = 0
                        };
                    }
                    _teammates[ally.AccountId].GamesTogether++;
                }
            }

            FillHeroPicker();
            SetActiveFilter(0);
            UpdateFilterCounts();
            UpdateStreakAndToday();
            ShowLocalStatsFallback();

            await LoadFirstBatchAsync();
            _ = LoadProfileAsync();
            _ = LoadAllTimeStatsAsync();
        }
        catch { }
        finally
        {
            Loader.IsRunning = false;
        }
    }

    private static long GetLong(JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var v))
            return v;
        return 0;
    }

    private static int GetInt(JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var v))
            return v;
        return 0;
    }

    private static bool GetBool(JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var p))
        {
            if (p.ValueKind == JsonValueKind.True) return true;
            if (p.ValueKind == JsonValueKind.False) return false;
        }
        return false;
    }

    private static string GetString(JsonElement el, string name, string fallback = "")
    {
        if (el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String)
            return p.GetString() ?? fallback;
        return fallback;
    }

    // ═══════════════════════════════════════════
    // СТАТИСТИКА
    // ═══════════════════════════════════════════
    private async Task LoadAllTimeStatsAsync()
    {
        try
        {
            var cachedWins = Preferences.Default.Get("alltime_wins", -1);
            var cachedLosses = Preferences.Default.Get("alltime_losses", -1);
            var cachedTs = Preferences.Default.Get("alltime_ts", 0L);

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            bool hasCache = cachedWins > 0 || cachedLosses > 0;
            bool fresh = (now - cachedTs) < 6 * 3600;

            if (hasCache)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                    ShowAllTimeStats(cachedWins, cachedLosses));
            }

            if (hasCache && fresh) return;

            var json = await _http.GetStringAsync(
                $"https://api.opendota.com/api/players/{_accountId}/wl");

            var root = JsonDocument.Parse(json).RootElement;
            if (root.TryGetProperty("error", out _)) return;

            int wins = GetInt(root, "win");
            int losses = GetInt(root, "lose");

            if (wins == 0 && losses == 0) return;

            Preferences.Default.Set("alltime_wins", wins);
            Preferences.Default.Set("alltime_losses", losses);
            Preferences.Default.Set("alltime_ts", now);

            MainThread.BeginInvokeOnMainThread(() =>
                ShowAllTimeStats(wins, losses));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AllTimeStats] {ex.Message}");
        }
    }

    private void ShowAllTimeStats(int wins, int losses)
    {
        int total = wins + losses;
        double wr = total == 0 ? 0 : (double)wins / total * 100;

        WinsLabel.Text = wins.ToString("N0");
        LossesLabel.Text = losses.ToString("N0");
        WinRateLabel.Text = $"{wr:F1}%";

        WinRateLabel.TextColor = wr >= 50
            ? Color.FromArgb("#4ade80")
            : Color.FromArgb("#f87171");
    }

    private void ShowLocalStatsFallback()
    {
        int wins = AllMatches.Count(m => m.IsWin);
        int losses = AllMatches.Count(m => !m.IsWin);
        int total = wins + losses;
        double wr = total == 0 ? 0 : (double)wins / total * 100;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            WinsLabel.Text = wins.ToString();
            LossesLabel.Text = losses.ToString();
            WinRateLabel.Text = $"{wr:F1}%";
            WinRateLabel.TextColor = wr >= 50
                ? Color.FromArgb("#4ade80")
                : Color.FromArgb("#f87171");
        });
    }

    // ═══════════════════════════════════════════
    // РАНГ
    // ═══════════════════════════════════════════
    private async Task LoadProfileAsync()
    {
        try
        {
            var cachedRank = Preferences.Default.Get("cached_rank_tier", 0);
            var cachedLeaderboard = Preferences.Default.Get("cached_leaderboard", 0);
            var cachedTs = Preferences.Default.Get("cached_profile_ts", 0L);

            if (cachedRank > 0)
                MainThread.BeginInvokeOnMainThread(() => ShowRankInUI(cachedRank, cachedLeaderboard));

            var age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - cachedTs;
            if (age < 6 * 3600 && cachedRank > 0) return;

            var json = await _http.GetStringAsync($"https://api.opendota.com/api/players/{_accountId}");
            var p = JsonDocument.Parse(json).RootElement;

            int rankTier = GetInt(p, "rank_tier");
            int leaderboard = GetInt(p, "leaderboard_rank");

            Preferences.Default.Set("cached_rank_tier", rankTier);
            Preferences.Default.Set("cached_leaderboard", leaderboard);
            Preferences.Default.Set("cached_profile_ts", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            MainThread.BeginInvokeOnMainThread(() => ShowRankInUI(rankTier, leaderboard));
        }
        catch { }
    }

    private void ShowRankInUI(int rankTier, int leaderboard)
    {
        var stats = new PlayerStats
        {
            Wins = 0,
            Losses = 0,
            RankTier = rankTier,
            LeaderboardRank = leaderboard
        };

        RankLabel.Text = stats.RankFullLabel;
        RankLabel.TextColor = Color.FromArgb(stats.RankColor);

        var medalFile = rankTier switch
        {
            >= 80 => "rank_immortal.png",
            >= 70 => "rank_divine.png",
            >= 60 => "rank_ancient.png",
            >= 50 => "rank_legend.png",
            >= 40 => "rank_archon.png",
            >= 30 => "rank_crusader.png",
            >= 20 => "rank_guardian.png",
            >= 10 => "rank_herald.png",
            _ => ""
        };

        if (!string.IsNullOrEmpty(medalFile))
            RankMedal.Source = medalFile;

        RankProgressLabel.Text = GetRankProgressText(rankTier);
    }

    // ═══════════════════════════════════════════
    // МАТЧИ
    // ═══════════════════════════════════════════
    private async Task LoadFirstBatchAsync()
    {
        try
        {
            var json = await _http.GetStringAsync(
                $"https://api.opendota.com/api/players/{_accountId}/recentMatches");

            var arr = JsonDocument.Parse(json).RootElement;
            var fresh = new List<DotaMatch>();
            var favs = LoadFavorites();

            foreach (var m in arr.EnumerateArray())
            {
                var matchId = GetLong(m, "match_id");
                if (matchId == 0) continue;

                var slot = GetLong(m, "player_slot");
                var radiantWin = GetBool(m, "radiant_win");

                fresh.Add(new DotaMatch
                {
                    MatchId = matchId,
                    HeroId = GetInt(m, "hero_id"),
                    Kills = GetInt(m, "kills"),
                    Deaths = GetInt(m, "deaths"),
                    Assists = GetInt(m, "assists"),
                    Duration = GetInt(m, "duration"),
                    StartTime = GetLong(m, "start_time"),
                    IsWin = (slot < 128) == radiantWin,
                    IsFavorite = favs.Contains(matchId)
                });
            }

            await _db.SaveMatchesAsync(_accountId, fresh);

            foreach (var m in fresh)
            {
                var old = AllMatches.FirstOrDefault(x => x.MatchId == m.MatchId);
                if (old is null) AllMatches.Insert(0, m);
                else
                {
                    old.Kills = m.Kills;
                    old.Deaths = m.Deaths;
                    old.Assists = m.Assists;
                    old.IsFavorite = m.IsFavorite;
                }
            }

            _apiOffset = fresh.Count;
            FillHeroPicker();
            ApplyFilter();
            UpdateFilterCounts();
            UpdateStreakAndToday();
            ShowLocalStatsFallback();
        }
        catch { }
    }

    private async void OnNearBottom(object? sender, EventArgs e)
    {
        if (_isLoadingMore || !_hasMoreFromApi) return;
        _isLoadingMore = true;

        try
        {
            var json = await _http.GetStringAsync(
                $"https://api.opendota.com/api/players/{_accountId}/matches?limit=20&offset={_apiOffset}");

            var arr = JsonDocument.Parse(json).RootElement;
            if (arr.GetArrayLength() == 0)
            {
                _hasMoreFromApi = false;
                return;
            }

            var batch = new List<DotaMatch>();
            var favs = LoadFavorites();

            foreach (var m in arr.EnumerateArray())
            {
                var matchId = GetLong(m, "match_id");
                if (matchId == 0) continue;

                var slot = GetLong(m, "player_slot");
                var radiantWin = GetBool(m, "radiant_win");

                batch.Add(new DotaMatch
                {
                    MatchId = matchId,
                    HeroId = GetInt(m, "hero_id"),
                    Kills = 0, Deaths = 0, Assists = 0,
                    Duration = GetInt(m, "duration"),
                    StartTime = GetLong(m, "start_time"),
                    IsWin = (slot < 128) == radiantWin,
                    IsFavorite = favs.Contains(matchId)
                });
            }

            await _db.SaveMatchesAsync(_accountId, batch);

            foreach (var m in batch)
                AllMatches.Add(m);

            _apiOffset += batch.Count;
            ApplyFilter();
            UpdateFilterCounts();
            UpdateStreakAndToday();
            ShowLocalStatsFallback();
        }
        catch { }
        finally
        {
            _isLoadingMore = false;
        }
    }

    // ═══════════════════════════════════════════
    // ИЗБРАННОЕ
    // ═══════════════════════════════════════════
    private HashSet<long> LoadFavorites()
    {
        var raw = Preferences.Default.Get(FavMatchesKey, "");
        if (string.IsNullOrEmpty(raw)) return new HashSet<long>();

        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
                  .Where(s => long.TryParse(s, out _))
                  .Select(long.Parse)
                  .ToHashSet();
    }

    private void SaveFavorites(HashSet<long> favs)
    {
        Preferences.Default.Set(FavMatchesKey, string.Join(",", favs));
    }

    private void OnToggleFavorite(object sender, EventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.BindingContext is not DotaMatch match) return;

        var favs = LoadFavorites();
        if (favs.Contains(match.MatchId)) favs.Remove(match.MatchId);
        else favs.Add(match.MatchId);

        SaveFavorites(favs);
        match.IsFavorite = favs.Contains(match.MatchId);

        ApplyFilter();
        UpdateFilterCounts();
        UpdateStreakAndToday();
    }

    private async void OnMatchTapped(object sender, TappedEventArgs e)
    {
        if (sender is not Border border) return;
        if (border.BindingContext is not DotaMatch match) return;

        var page = new MatchDetailsPage(match, _accountId);
        await Navigation.PushModalAsync(page);
    }

    // ═══════════════════════════════════════════
    // ДРУЗЬЯ
    // ═══════════════════════════════════════════
    private HashSet<long> LoadFavAllies()
    {
        var raw = Preferences.Default.Get(FavAlliesKey, "");
        if (string.IsNullOrEmpty(raw)) return new HashSet<long>();

        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
                  .Where(s => long.TryParse(s, out _))
                  .Select(long.Parse)
                  .ToHashSet();
    }

    private void SaveFavAllies(HashSet<long> favs)
    {
        Preferences.Default.Set(FavAlliesKey, string.Join(",", favs));
    }

    private void ToggleFavAlly(long accountId)
    {
        var favs = LoadFavAllies();
        if (favs.Contains(accountId)) favs.Remove(accountId);
        else favs.Add(accountId);
        SaveFavAllies(favs);
        RefreshTeammates();
    }

    private void UpdateFilterCounts()
    {
        int all = AllMatches.Count;
        int wins = AllMatches.Count(m => m.IsWin);
        int losses = AllMatches.Count(m => !m.IsWin);
        int favs = AllMatches.Count(m => m.IsFavorite);

        if (_activeFilter == 0)
            FilterAllText.Text = $"ВСЕ · {all}";
        else
            FilterAllText.Text = "ВСЕ";

        FilterWinText.Text = wins.ToString();
        FilterLossText.Text = losses.ToString();
        FilterFavText.Text = favs.ToString();
    }

    private string GetRankProgressText(int rankTier)
    {
        if (rankTier < 10) return "Нет ранга";

        int level = rankTier / 10;
        int stars = rankTier % 10;
        if (level >= 8) return "Максимальный ранг";

        int nextLevel = level;
        int nextStars = stars + 1;
        if (nextStars > 5) { nextLevel++; nextStars = 1; }

        var names = new[] { "", "Herald", "Guardian", "Crusader", "Archon", "Legend", "Ancient", "Divine", "Immortal" };
        var nextName = nextLevel <= 7 ? names[nextLevel] : "Immortal";
        return $"До {nextName} {nextStars}";
    }

    private void UpdateStreakAndToday()
    {
        var sorted = AllMatches.OrderByDescending(m => m.StartTime).ToList();

        int streak = 0;
        bool? streakWin = null;

        foreach (var m in sorted)
        {
            if (streakWin is null) { streakWin = m.IsWin; streak = 1; continue; }
            if (m.IsWin == streakWin) streak++;
            else break;
        }

        if (streakWin == true)
        {
            StreakLabel.Text = $"×{streak}";
            StreakLabel.TextColor = Color.FromArgb("#4ade80");
        }
        else if (streakWin == false)
        {
            StreakLabel.Text = $"×{streak}";
            StreakLabel.TextColor = Color.FromArgb("#f87171");
        }
        else
        {
            StreakLabel.Text = "—";
        }

        var todayStart = DateTime.Today.ToUniversalTime();
        long todayUnix = new DateTimeOffset(todayStart).ToUnixTimeSeconds();

        var todayMatches = AllMatches.Where(m => m.StartTime >= todayUnix).ToList();
        int todayWins = todayMatches.Count(m => m.IsWin);
        int todayTotal = todayMatches.Count;
        double todayWr = todayTotal == 0 ? 0 : (double)todayWins / todayTotal * 100;

        TodayLabel.Text = todayTotal.ToString();

        if (todayTotal == 0)
        {
            TodayWrLabel.Text = "—";
            TodayWrLabel.TextColor = Color.FromArgb("#8899aa");
        }
        else
        {
            TodayWrLabel.Text = $"{todayWr:F0}%";
            TodayWrLabel.TextColor = todayWr >= 50
                ? Color.FromArgb("#4ade80")
                : Color.FromArgb("#f87171");
        }
    }

    // ═══════════════════════════════════════════
    // ГЕРОЙ-ПИКЕР
    // ═══════════════════════════════════════════
    private void FillHeroPicker()
    {
        var heroes = AllMatches
            .Select(m => m.HeroId)
            .Where(h => h > 0)
            .Distinct()
            .ToList();

        _allHeroItems = new List<HeroPickerItem>
        {
            new HeroPickerItem { HeroId = 0, Name = "Все герои", IconUrl = "" }
        };

        foreach (var h in heroes)
        {
            _allHeroItems.Add(new HeroPickerItem
            {
                HeroId = h,
                Name = HeroDatabase.GetDisplayName(h),
                IconUrl = HeroDatabase.GetIconUrl(h)
            });
        }

        var rest = _allHeroItems.Skip(1).OrderBy(x => x.Name).ToList();
        var first = _allHeroItems[0];
        _allHeroItems = new List<HeroPickerItem> { first };
        _allHeroItems.AddRange(rest);

        HeroList.ItemsSource = _allHeroItems;
    }

    private void OnHeroPickerClicked(object sender, EventArgs e)
    {
        HeroOverlay.IsVisible = true;
        HeroSearchEntry.Text = "";
        HeroList.ItemsSource = _allHeroItems;
    }

    private void OnHeroOverlayTapped(object sender, EventArgs e)
    {
        HeroOverlay.IsVisible = false;
    }

    private void OnHeroSearchChanged(object sender, TextChangedEventArgs e)
    {
        var q = (e.NewTextValue ?? "").Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(q))
        {
            HeroList.ItemsSource = _allHeroItems;
            return;
        }

        var filtered = _allHeroItems
            .Where(h => h.HeroId == 0 || h.Name.ToLowerInvariant().Contains(q))
            .ToList();

        HeroList.ItemsSource = filtered;
    }

    private void OnHeroSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not HeroPickerItem item) return;

        _selectedHeroId = item.HeroId;
        HeroPickerLabel.Text = item.HeroId == 0 ? "Все герои" : item.Name;

        HeroList.SelectedItem = null;
        HeroOverlay.IsVisible = false;
        ApplyFilter();
    }

    // ═══════════════════════════════════════════
    // ФИЛЬТРЫ
    // ═══════════════════════════════════════════
    private void ApplyFilter()
    {
        if (AllMatches is null) return;

        int heroId = _selectedHeroId;
        var favorites = LoadFavorites();
        var query = AllMatches.AsEnumerable();

        if (_activeFilter == 1) query = query.Where(m => m.IsWin);
        if (_activeFilter == 2) query = query.Where(m => !m.IsWin);
        if (_activeFilter == 3) query = query.Where(m => favorites.Contains(m.MatchId));

        if (heroId > 0)
            query = query.Where(m => m.HeroId == heroId);

        query = query.OrderByDescending(m => m.StartTime);

        Visible.Clear();
        foreach (var m in query) Visible.Add(m);
    }

    private void SetActiveFilter(int filter)
    {
        _activeFilter = filter;

        FilterAllChip.BackgroundColor = Colors.Transparent;
        FilterAllChip.Stroke = Color.FromArgb("#f5df67");
        FilterAllText.TextColor = Color.FromArgb("#f5df67");

        FilterWinChip.BackgroundColor = Colors.Transparent;
        FilterWinChip.Stroke = Color.FromArgb("#4ade80");
        FilterWinText.TextColor = Color.FromArgb("#4ade80");

        FilterLossChip.BackgroundColor = Colors.Transparent;
        FilterLossChip.Stroke = Color.FromArgb("#f87171");
        FilterLossText.TextColor = Color.FromArgb("#f87171");

        FilterFavChip.BackgroundColor = Colors.Transparent;
        FilterFavChip.Stroke = Color.FromArgb("#f5df67");
        FilterFavText.TextColor = Color.FromArgb("#f5df67");

        switch (filter)
        {
            case 0:
                FilterAllChip.BackgroundColor = Color.FromArgb("#f5df67");
                FilterAllText.TextColor = Color.FromArgb("#0a0e1a");
                break;
            case 1:
                FilterWinChip.BackgroundColor = Color.FromArgb("#4ade80");
                FilterWinText.TextColor = Color.FromArgb("#0a0e1a");
                break;
            case 2:
                FilterLossChip.BackgroundColor = Color.FromArgb("#f87171");
                FilterLossText.TextColor = Color.FromArgb("#0a0e1a");
                break;
            case 3:
                FilterFavChip.BackgroundColor = Color.FromArgb("#f5df67");
                FilterFavText.TextColor = Color.FromArgb("#0a0e1a");
                break;
        }

        UpdateFilterCounts();
        ApplyFilter();
    }

    private void OnFilterAll(object sender, EventArgs e) => SetActiveFilter(0);
    private void OnFilterWin(object sender, EventArgs e) => SetActiveFilter(1);
    private void OnFilterLoss(object sender, EventArgs e) => SetActiveFilter(2);
    private void OnFilterFav(object sender, EventArgs e) => SetActiveFilter(3);

    // ═══════════════════════════════════════════
    // ВКЛАДКИ
    // ═══════════════════════════════════════════
    private void OnTabMatches(object sender, EventArgs e)
    {
        TabMatchesChip.BackgroundColor = Color.FromArgb("#f5df67");
        TabMatchesChip.Stroke = Color.FromArgb("#f5df67");
        TabMatchesText.TextColor = Color.FromArgb("#0a0e1a");

        TabFriendsChip.BackgroundColor = Colors.Transparent;
        TabFriendsChip.Stroke = Color.FromArgb("#c084fc");
        TabFriendsText.TextColor = Color.FromArgb("#c084fc");

        MatchesList.IsVisible = true;
        FriendsPanel.IsVisible = false;
    }

    private void OnTabFriends(object sender, EventArgs e)
    {
        TabMatchesChip.BackgroundColor = Colors.Transparent;
        TabMatchesChip.Stroke = Color.FromArgb("#f5df67");
        TabMatchesText.TextColor = Color.FromArgb("#f5df67");

        TabFriendsChip.BackgroundColor = Color.FromArgb("#c084fc");
        TabFriendsChip.Stroke = Color.FromArgb("#c084fc");
        TabFriendsText.TextColor = Color.FromArgb("#0a0e1a");

        MatchesList.IsVisible = false;
        FriendsPanel.IsVisible = true;

        RefreshTeammates();
    }

    // ═══════════════════════════════════════════
    // ДРУЗЬЯ (UI)
    // ═══════════════════════════════════════════
    private void RefreshTeammates()
    {
        TeammatesListVertical.Children.Clear();

        var favAllies = LoadFavAllies();

        var all = _teammates.Values
            .OrderByDescending(t => favAllies.Contains(t.AccountId) ? 1 : 0)
            .ThenByDescending(t => t.GamesTogether)
            .Take(50)
            .ToList();

        if (all.Count == 0)
        {
            NoTeammatesView.IsVisible = true;
            return;
        }

        NoTeammatesView.IsVisible = false;

        int rankCounter = 0;

        foreach (var t in all)
        {
            var isFav = favAllies.Contains(t.AccountId);
            int? topRank = null;

            if (!isFav)
            {
                rankCounter++;
                if (rankCounter <= 3) topRank = rankCounter;
            }

            var games = AllMatches.Where(m => m.Allies.Any(a => a.AccountId == t.AccountId)).ToList();
            int total = games.Count;
            int wins = games.Count(m => m.IsWin);
            double wr = total == 0 ? 0 : (double)wins / total * 100;

            var wrColor = wr >= 55 ? Color.FromArgb("#4ade80") :
                          wr >= 45 ? Color.FromArgb("#f5df67") :
                                     Color.FromArgb("#f87171");

            Color strokeColor;
            int strokeThickness;
            double cardScale = 1.0;

            if (isFav) { strokeColor = Color.FromArgb("#f5df67"); strokeThickness = 2; }
            else if (topRank == 1) { strokeColor = Color.FromArgb("#f5df67"); strokeThickness = 3; cardScale = 1.05; }
            else if (topRank == 2) { strokeColor = Color.FromArgb("#c0c0c0"); strokeThickness = 2; cardScale = 1.02; }
            else if (topRank == 3) { strokeColor = Color.FromArgb("#cd7f32"); strokeThickness = 2; }
            else { strokeColor = Color.FromArgb("#2a3748"); strokeThickness = 1; }

            var card = new Border
            {
                Stroke = strokeColor,
                StrokeThickness = strokeThickness,
                Padding = new Thickness(12, 10),
                Scale = cardScale
            };
            card.StrokeShape = new RoundRectangle { CornerRadius = 12 };

            var bg = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            bg.GradientStops.Add(new GradientStop { Color = Color.FromArgb("#161c2c"), Offset = 0.0f });
            bg.GradientStops.Add(new GradientStop { Color = Color.FromArgb("#0d121f"), Offset = 1.0f });
            card.Background = bg;

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(new GridLength(54)),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 12
            };

            var avatar = new Border { Stroke = strokeColor, StrokeThickness = 2, WidthRequest = 54, HeightRequest = 54 };
            avatar.StrokeShape = new RoundRectangle { CornerRadius = 27 };

            if (!string.IsNullOrEmpty(t.Avatar))
            {
                avatar.Content = new Image
                {
                    Source = new UriImageSource
                    {
                        Uri = new Uri(t.Avatar),
                        CachingEnabled = true,
                        CacheValidity = TimeSpan.FromDays(7)
                    },
                    Aspect = Aspect.AspectFill
                };
            }
            else
            {
                avatar.Background = Color.FromArgb("#1e2840");
                avatar.Content = new Label
                {
                    Text = string.IsNullOrEmpty(t.Name) ? "?" : t.Name.Substring(0, 1).ToUpper(),
                    FontSize = 22,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = isFav ? Color.FromArgb("#f5df67") : Colors.White,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                };
            }

            if (topRank.HasValue)
            {
                var medal = new Border
                {
                    Stroke = Colors.Transparent,
                    BackgroundColor = topRank.Value switch
                    {
                        1 => Color.FromArgb("#f5df67"),
                        2 => Color.FromArgb("#c0c0c0"),
                        3 => Color.FromArgb("#cd7f32"),
                        _ => Colors.Transparent
                    },
                    WidthRequest = 24, HeightRequest = 24, Padding = 0,
                    HorizontalOptions = LayoutOptions.Start,
                    VerticalOptions = LayoutOptions.Start,
                    ZIndex = 10,
                    TranslationX = -6, TranslationY = -6
                };
                medal.StrokeShape = new RoundRectangle { CornerRadius = 12 };
                medal.Content = new Label
                {
                    Text = topRank.Value.ToString(),
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#0a0e1a"),
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center
                };

                var avatarContainer = new Grid();
                avatarContainer.Children.Add(avatar);
                avatarContainer.Children.Add(medal);
                Grid.SetColumn(avatarContainer, 0);
                grid.Children.Add(avatarContainer);
            }
            else
            {
                Grid.SetColumn(avatar, 0);
                grid.Children.Add(avatar);
            }

            var infoStack = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };

            var nameLabel = new Label
            {
                Text = (isFav ? "★ " : "") + t.Name,
                FontSize = topRank == 1 ? 16 : 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = isFav ? Color.FromArgb("#f5df67") : Colors.White,
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 1
            };

            var statLabel = new Label
            {
                Text = $"{total} игр · {wr:F0}% побед",
                FontSize = 12,
                TextColor = wrColor
            };

            var progressBg = new Border
            {
                BackgroundColor = Color.FromArgb("#2a3748"),
                HeightRequest = 4,
                StrokeThickness = 0
            };
            progressBg.StrokeShape = new RoundRectangle { CornerRadius = 2 };

            var progressFill = new BoxView
            {
                Color = wrColor,
                HeightRequest = 4,
                WidthRequest = Math.Max(4, (wr / 100.0) * 200),
                HorizontalOptions = LayoutOptions.Start
            };

            var progressContainer = new Grid { HeightRequest = 4 };
            progressContainer.Children.Add(progressBg);
            progressContainer.Children.Add(progressFill);

            infoStack.Children.Add(nameLabel);
            infoStack.Children.Add(statLabel);
            infoStack.Children.Add(progressContainer);
            Grid.SetColumn(infoStack, 1);

            var arrow = new Label
            {
                Text = "›",
                FontSize = 26,
                TextColor = Color.FromArgb("#8899aa"),
                VerticalOptions = LayoutOptions.Center
            };
            Grid.SetColumn(arrow, 2);

            grid.Children.Add(infoStack);
            grid.Children.Add(arrow);

            card.Content = grid;

            var tap = new TapGestureRecognizer();
            var teammateCopy = t;
            tap.Tapped += async (s, e) =>
            {
                var page = new FriendMatchesPage(AllMatches.ToList(), teammateCopy);
                await Navigation.PushModalAsync(page);
            };
            card.GestureRecognizers.Add(tap);

            var tapDouble = new TapGestureRecognizer { NumberOfTapsRequired = 2 };
            var accountId = t.AccountId;
            tapDouble.Tapped += (s, e) => ToggleFavAlly(accountId);
            card.GestureRecognizers.Add(tapDouble);

            TeammatesListVertical.Children.Add(card);
        }
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        Preferences.Default.Remove("steam_id");
        Preferences.Default.Remove("account_id");
        Preferences.Default.Remove("persona_name");
        Preferences.Default.Remove("avatar_url");
        Preferences.Default.Remove("cached_rank_tier");
        Preferences.Default.Remove("cached_leaderboard");
        Preferences.Default.Remove("cached_profile_ts");
        Preferences.Default.Remove("alltime_wins");
        Preferences.Default.Remove("alltime_losses");
        Preferences.Default.Remove("alltime_ts");
        await _db.ClearAsync();
        await Shell.Current.GoToAsync("//login");
    }
}