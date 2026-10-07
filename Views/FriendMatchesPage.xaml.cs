using System.Collections.ObjectModel;
using RankUp.Models;

namespace RankUp.Views;

public partial class FriendMatchesPage : ContentPage
{
    public ObservableCollection<FriendMatchView> Items { get; } = new();

    public FriendMatchesPage(List<DotaMatch> myMatches, Teammate friend)
    {
        InitializeComponent();
        MatchesList.ItemsSource = Items;

        FriendName.Text = friend.Name;

        if (!string.IsNullOrEmpty(friend.Avatar))
            FriendAvatar.Source = ImageSource.FromUri(new Uri(friend.Avatar));

        var games = myMatches
            .Where(m => m.Allies.Any(a => a.AccountId == friend.AccountId))
            .OrderByDescending(m => m.StartTime)
            .ToList();

        foreach (var m in games)
        {
            var ally = m.Allies.FirstOrDefault(a => a.AccountId == friend.AccountId);
            Items.Add(new FriendMatchView
            {
                Match = m,
                FriendHeroId = ally?.HeroId ?? 0,
                FriendKills = ally?.Kills ?? 0,
                FriendDeaths = ally?.Deaths ?? 0,
                FriendAssists = ally?.Assists ?? 0
            });
        }

        int total = games.Count;
        int wins = games.Count(m => m.IsWin);
        double wr = total == 0 ? 0 : (double)wins / total * 100;

        GamesTogetherLabel.Text = total.ToString();
        WinsTogetherLabel.Text = wins.ToString();
        WinRateTogetherLabel.Text = $"{wr:F0}%";

        FriendStats.Text = $"Steam ID: {friend.SteamId}";

        if (total == 0)
            EmptyLabel.IsVisible = true;
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}