using System.Text.Json;
using RankUp.Models;

namespace RankUp.Views;

public partial class LoginPage : ContentPage
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public LoginPage()
    {
        InitializeComponent();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        var input = SteamIdEntry.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(input))
        {
            ErrorLabel.Text = "Введи Steam ID или ник";
            return;
        }

        Loader.IsRunning = true;
        ErrorLabel.Text = "";

        try
        {
            long accountId;

            if (input.Length == 17 && long.TryParse(input, out var id64))
            {
                accountId = id64 - 76561197960265728L;
            }
            else
            {
                var (found, apiOk) = await SearchByNicknameAsync(input);

                // 👇 ИСПРАВЛЕНО: различаем ошибки API и «ник не найден»
                if (!apiOk)
                {
                    ErrorLabel.Text = "OpenDota недоступна. Проверь интернет и попробуй позже";
                    return;
                }

                if (found.Count == 0)
                {
                    ErrorLabel.Text = "Ник не найден. Попробуй Steam ID";
                    return;
                }

                if (found.Count == 1)
                {
                    accountId = found[0].AccountId;
                }
                else
                {
                    var picker = new ProfilePickerPage(found);
                    await Navigation.PushModalAsync(picker);
                    var chosen = await picker.Result;

                    if (!chosen.HasValue) return;
                    accountId = chosen.Value;
                }
            }

            var profile = await LoadProfileAsync(accountId);
            if (profile is null)
            {
                ErrorLabel.Text = "Профиль не найден или скрыт";
                return;
            }

            var steamId64 = accountId + 76561197960265728L;

            Preferences.Default.Set("steam_id", steamId64.ToString());
            Preferences.Default.Set("account_id", accountId.ToString());
            Preferences.Default.Set("persona_name", profile.Value.Name);
            Preferences.Default.Set("avatar_url", profile.Value.Avatar);

            await Shell.Current.GoToAsync("//profile");
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = $"Ошибка: {ex.Message}";
        }
        finally
        {
            Loader.IsRunning = false;
        }
    }

    // 👇 ИСПРАВЛЕНО: возвращаем tuple (results, success)
    private async Task<(List<ProfileSearchResult> Results, bool Success)> SearchByNicknameAsync(string query)
    {
        var results = new List<ProfileSearchResult>();

        try
        {
            var url = $"https://api.opendota.com/api/search?q={Uri.EscapeDataString(query)}";
            var json = await _http.GetStringAsync(url);
            var arr = JsonDocument.Parse(json).RootElement;

            foreach (var item in arr.EnumerateArray())
            {
                if (!item.TryGetProperty("account_id", out var accId)) continue;
                if (accId.ValueKind == JsonValueKind.Null) continue;

                var accountId = accId.GetInt64();

                var name = item.TryGetProperty("personaname", out var n)
                    ? n.GetString() ?? "?"
                    : "?";

                var avatar = item.TryGetProperty("avatarfull", out var a)
                    ? a.GetString() ?? ""
                    : "";

                results.Add(new ProfileSearchResult
                {
                    AccountId = accountId,
                    SteamId = accountId + 76561197960265728L,
                    Name = name,
                    Avatar = avatar
                });

                if (results.Count >= 20) break;
            }

            return (results, true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Search] {ex.Message}");
            return (results, false);
        }
    }

    private async Task<(string Name, string Avatar)?> LoadProfileAsync(long accountId)
    {
        try
        {
            var json = await _http.GetStringAsync($"https://api.opendota.com/api/players/{accountId}");
            var root = JsonDocument.Parse(json).RootElement;

            if (!root.TryGetProperty("profile", out var p)) return null;

            var name = p.TryGetProperty("personaname", out var n) ? n.GetString() ?? "Player" : "Player";
            var avatar = p.TryGetProperty("avatarfull", out var a) ? a.GetString() ?? "" : "";

            return (name, avatar);
        }
        catch
        {
            return null;
        }
    }
}