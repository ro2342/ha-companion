using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HaCompanionUWP.Models;
using HaCompanionUWP.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace HaCompanionUWP.Views
{
    // Tela inicial: cards das entidades marcadas como favoritas em Ajustes >
    // Escolher favoritos. Sem entity_id cravado no código -- a lista vem do
    // CredentialStore, escolhida na FavoritesPickerPage a partir do que a
    // conta do HA realmente tem.
    public sealed partial class FavoritesPage : Page
    {
        public FavoritesPage()
        {
            this.InitializeComponent();
            this.Loaded += FavoritesPage_Loaded;
        }

        private async void FavoritesPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            ErrorCard.Visibility = Visibility.Collapsed;
            EmptyStateCard.Visibility = Visibility.Collapsed;
            LoadingRing.IsActive = true;

            try
            {
                IReadOnlyList<string> favoriteIds = CredentialStore.GetFavorites();
                if (favoriteIds.Count == 0)
                {
                    EntitiesList.ItemsSource = null;
                    EmptyStateCard.Visibility = Visibility.Visible;
                    return;
                }

                List<HaEntityState> allStates = await HaApiService.GetStatesAsync();
                Dictionary<string, HaEntityState> byId = allStates.ToDictionary(entity => entity.EntityId, entity => entity);

                var favorites = new List<HaEntityState>();
                foreach (string id in favoriteIds)
                {
                    HaEntityState entity;
                    if (byId.TryGetValue(id, out entity))
                    {
                        favorites.Add(entity);
                    }
                }

                EntitiesList.ItemsSource = favorites;
            }
            catch (HaApiException ex)
            {
                ErrorText.Text = ex.Message;
                ErrorCard.Visibility = Visibility.Visible;
            }
            finally
            {
                LoadingRing.IsActive = false;
            }
        }

        private async void EntityCard_Click(object sender, RoutedEventArgs e)
        {
            string entityId = (string)((FrameworkElement)sender).Tag;
            string domain = entityId.Contains(".") ? entityId.Substring(0, entityId.IndexOf('.')) : string.Empty;

            try
            {
                switch (domain)
                {
                    case "light":
                    case "switch":
                        await HaApiService.CallServiceAsync(domain, "toggle", entityId);
                        break;
                    case "script":
                        await HaApiService.CallServiceAsync("script", "turn_on", entityId);
                        break;
                }
                await LoadAsync();
            }
            catch (HaApiException ex)
            {
                ErrorText.Text = ex.Message;
                ErrorCard.Visibility = Visibility.Visible;
            }
        }

        private void ChooseFavorites_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(FavoritesPickerPage));
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadAsync();
        }
    }
}
