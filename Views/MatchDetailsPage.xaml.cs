using System.Text.Json;
using Microsoft.Maui.Controls.Shapes;
using RankUp.Models;

namespace RankUp.Views;

public partial class MatchDetailsPage : ContentPage
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly DotaMatch _match;
    private readonly long _myAccountId;

    private MatchDetails? _details;
    private bool _loaded = false;
    private bool _closing = false;

    public MatchDetailsPage(DotaMatch match, long myAccountId)
    {
        InitializeComponent();
        _match = match;
        _myAccountId = myAccountId;

        ShowBasicInfo();
        Loaded += async (_, _) => await LoadAsync();
    }

    private void ShowBasicInfo()
    {
        ResultLabel.Text = _match.IsWin ? "ПОБЕДА" : "ПОРАЖЕНИЕ";
        ResultLabel.TextColor = _match.IsWin ? Color.FromArgb("#4ade80") : Color.FromArgb("#f87171");

        var dur = TimeSpan.FromSeconds(_match.Duration).ToString(@"mm\:ss");
        var date = DateTimeOffset.FromUnixTimeSeconds(_match.StartTime).LocalDateTime.ToString("dd.MM.yyyy HH:mm");

        MetaLabel.Text = $"{date} · {dur}";
        DebugLabel.Text = "Загрузка...";

        RadiantLabel.Text = "RADIANT";
        DireLabel.Text = "DIRE";
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

    private async Task LoadAsync()
    {
        if (_loaded) return;
        _loaded = true;

        Loader.IsRunning = true;

        try
        {
            DebugLabel.Text = "Загрузка справочника...";

            // Ждём не более 5 сек, потом идём дальше
            var itemsTask = ItemDatabase.EnsureLoadedAsync();
            await Task.WhenAny(itemsTask, Task.Delay(5000));

            DebugLabel.Text = $"Предметов: {ItemDatabase.Count}. Запрос матча...";

            var matchJson = await _http.GetStringAsync(
                $"https://api.opendota.com/api/matches/{_match.MatchId}");

            DebugLabel.Text = $"Ответ ({matchJson.Length} симв.). Парсинг...";

            var root = JsonDocument.Parse(matchJson).RootElement;

            if (root.TryGetProperty("error", out var apiErr))
            {
                DebugLabel.Text = $"API error: {apiErr.GetString()}";
                Loader.IsRunning = false;
                return;
            }

            _details = new MatchDetails
            {
                MatchId = GetLong(root, "match_id"),
                RadiantWin = GetBool(root, "radiant_win"),
                Duration = GetInt(root, "duration"),
                GameMode = GetInt(root, "game_mode"),
                StartTime = GetLong(root, "start_time"),
                MyAccountId = _myAccountId
            };

            if (root.TryGetProperty("players", out var players))
            {
                foreach (var p in players.EnumerateArray())
                {
                    var itemIds = new List<int>();
                    for (int i = 0; i < 6; i++)
                    {
                        var key = $"item_{i}";
                        if (!p.TryGetProperty(key, out var itm)) continue;
                        if (itm.ValueKind != JsonValueKind.Number) continue;

                        var id = itm.GetInt32();
                        if (id > 0) itemIds.Add(id);
                    }

                    var player = new MatchPlayer
                    {
                        AccountId = GetLong(p, "account_id"),
                        PlayerSlot = GetInt(p, "player_slot"),
                        HeroId = GetInt(p, "hero_id"),
                        Name = GetString(p, "personaname", "?"),
                        Kills = GetInt(p, "kills"),
                        Deaths = GetInt(p, "deaths"),
                        Assists = GetInt(p, "assists"),
                        NetWorth = GetInt(p, "net_worth"),
                        LastHits = GetInt(p, "last_hits"),
                        HeroDamage = GetInt(p, "hero_damage"),
                        ItemIds = itemIds
                    };

                    if (player.IsRadiant) _details.Radiant.Add(player);
                    else _details.Dire.Add(player);
                }
            }

            if (_details.Radiant.Count == 0 && _details.Dire.Count == 0)
            {
                DebugLabel.Text = "OpenDota ещё не распарсила матч. Игроков нет.";
                Loader.IsRunning = false;
                return;
            }

            ResultLabel.Text = _details.IWin ? "ПОБЕДА" : "ПОРАЖЕНИЕ";
            ResultLabel.TextColor = _details.IWin ? Color.FromArgb("#4ade80") : Color.FromArgb("#f87171");
            MetaLabel.Text = $"{_details.ModeName} · {_details.DurationLabel} · {_details.DateLabel}";

            RadiantLabel.Text = _details.RadiantWin ? "RADIANT ✓" : "RADIANT";
            DireLabel.Text = !_details.RadiantWin ? "DIRE ✓" : "DIRE";

            BuildPlayerCards(RadiantList, _details.Radiant);
            BuildPlayerCards(DireList, _details.Dire);

            DebugLabel.Text = $"Готово. Игроков: {_details.Radiant.Count + _details.Dire.Count}, предметов: {ItemDatabase.Count}";
        }
        catch (TaskCanceledException)
        {
            DebugLabel.Text = "Таймаут запроса (>30 сек)";
        }
        catch (HttpRequestException hex)
        {
            DebugLabel.Text = $"Сеть: {hex.StatusCode} — матч {_match.MatchId}";
        }
        catch (Exception ex)
        {
            DebugLabel.Text = $"Ошибка: {ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            Loader.IsRunning = false;
        }
    }

    private void BuildPlayerCards(VerticalStackLayout container, List<MatchPlayer> players)
    {
        container.Children.Clear();

        foreach (var p in players)
        {
            var isMe = p.AccountId == _myAccountId;

            var card = new Border
            {
                Stroke = isMe ? Color.FromArgb("#f5df67") : Color.FromArgb("#2a3748"),
                StrokeThickness = isMe ? 2 : 1,
                Padding = new Thickness(10, 8)
            };
            card.StrokeShape = new RoundRectangle { CornerRadius = 10 };

            var bg = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            bg.GradientStops.Add(new GradientStop { Color = Color.FromArgb("#161c2c"), Offset = 0.0f });
            bg.GradientStops.Add(new GradientStop { Color = Color.FromArgb("#0d121f"), Offset = 1.0f });
            card.Background = bg;

            var stack = new VerticalStackLayout { Spacing = 5 };

            var nameLabel = new Label
            {
                Text = p.Name.Length > 14 ? p.Name.Substring(0, 14) + "…" : p.Name,
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = isMe ? Color.FromArgb("#f5df67") : Colors.White,
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 1
            };

            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(new GridLength(52)),
                    new ColumnDefinition(GridLength.Star)
                },
                ColumnSpacing = 6
            };

            var heroBorder = new Border
            {
                Stroke = Color.FromArgb("#4a5568"),
                StrokeThickness = 1,
                WidthRequest = 52,
                HeightRequest = 30
            };
            heroBorder.StrokeShape = new RoundRectangle { CornerRadius = 5 };
            heroBorder.Content = new Image
            {
                Source = new UriImageSource
                {
                    Uri = new Uri(p.HeroIcon),
                    CachingEnabled = true,
                    CacheValidity = TimeSpan.FromDays(30)
                },
                Aspect = Aspect.AspectFill
            };
            Grid.SetColumn(heroBorder, 0);

            var kdaLabel = new Label
            {
                Text = p.Kda,
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                VerticalOptions = LayoutOptions.Center
            };
            Grid.SetColumn(kdaLabel, 1);

            row.Children.Add(heroBorder);
            row.Children.Add(kdaLabel);

            stack.Children.Add(nameLabel);
            stack.Children.Add(row);

            // === ПРЕДМЕТЫ ===
            if (p.ItemIds.Count > 0)
            {
                var itemsGrid = new Grid
                {
                    RowDefinitions = new RowDefinitionCollection
                    {
                        new RowDefinition { Height = GridLength.Auto },
                        new RowDefinition { Height = GridLength.Auto }
                    },
                    ColumnDefinitions = new ColumnDefinitionCollection
                    {
                        new ColumnDefinition { Width = GridLength.Auto },
                        new ColumnDefinition { Width = GridLength.Auto },
                        new ColumnDefinition { Width = GridLength.Auto }
                    },
                    RowSpacing = 4,
                    ColumnSpacing = 4,
                    HorizontalOptions = LayoutOptions.Start
                };

                int idx = 0;
                foreach (var itemId in p.ItemIds.Take(6))
                {
                    var itemBorder = new Border
                    {
                        Stroke = Color.FromArgb("#2a3748"),
                        StrokeThickness = 1,
                        WidthRequest = 40,
                        HeightRequest = 40
                    };
                    itemBorder.StrokeShape = new RoundRectangle { CornerRadius = 6 };

                    var url = ItemDatabase.GetIconUrl(itemId);
                    if (!string.IsNullOrEmpty(url))
                    {
                        itemBorder.Content = new Image
                        {
                            Source = new UriImageSource
                            {
                                Uri = new Uri(url),
                                CachingEnabled = true,
                                CacheValidity = TimeSpan.FromDays(30)
                            },
                            Aspect = Aspect.AspectFit
                        };
                    }

                    Grid.SetRow(itemBorder, idx / 3);
                    Grid.SetColumn(itemBorder, idx % 3);
                    itemsGrid.Children.Add(itemBorder);

                    idx++;
                }

                stack.Children.Add(itemsGrid);
            }

            var infoLabel = new Label
            {
                Text = $"💰 {p.NetWorthLabel}  ·  LH {p.LastHits}",
                FontSize = 10,
                TextColor = Color.FromArgb("#8899aa")
            };
            stack.Children.Add(infoLabel);

            card.Content = stack;
            container.Children.Add(card);
        }
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        if (_closing) return;
        _closing = true;

        try
        {
            await Navigation.PopModalAsync();
        }
        catch { }
    }
}