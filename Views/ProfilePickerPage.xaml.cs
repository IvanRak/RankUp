// === RANKUP-FEATURE: login-search-picker ===
using RankUp.Models;

namespace RankUp.Views;

public partial class ProfilePickerPage : ContentPage
{
    private readonly TaskCompletionSource<long?> _tcs = new();

    public Task<long?> Result => _tcs.Task;

    public ProfilePickerPage(List<ProfileSearchResult> profiles)
    {
        InitializeComponent();
        ProfilesList.ItemsSource = profiles;
    }

    private async void OnProfileSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ProfileSearchResult selected) return;

        _tcs.TrySetResult(selected.AccountId);
        await Navigation.PopModalAsync();
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        _tcs.TrySetResult(null);
        await Navigation.PopModalAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _tcs.TrySetResult(null);
        return base.OnBackButtonPressed();
    }
}
// === END RANKUP-FEATURE: login-search-picker ===
