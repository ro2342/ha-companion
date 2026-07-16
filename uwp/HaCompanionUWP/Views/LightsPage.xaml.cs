using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HaCompanionUWP.Models;
using HaCompanionUWP.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace HaCompanionUWP.Views
{
    // Todas as light.* e switch.* ao vivo — domínios que a doc trata do
    // mesmo jeito visualmente (card tocável; switch só não mostra brilho).
    public sealed partial class LightsPage : Page
    {
        public LightsPage()
        {
            this.InitializeComponent();
            this.Loaded += LightsPage_Loaded;
        }

        private async void LightsPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            ErrorCard.Visibility = Visibility.Collapsed;
            EmptyText.Visibility = Visibility.Collapsed;
            LoadingRing.IsActive = true;

            try
            {
                List<HaEntityState> allStates = await HaApiService.GetStatesAsync();
                List<HaEntityState> lights = allStates
                    .Where(entity => entity.Domain == "light" || entity.Domain == "switch")
                    .OrderBy(entity => entity.FriendlyName)
                    .ToList();

                EntitiesList.ItemsSource = lights;
                EmptyText.Visibility = lights.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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
                await HaApiService.CallServiceAsync(domain, "toggle", entityId);
                await LoadAsync();
            }
            catch (HaApiException ex)
            {
                ErrorText.Text = ex.Message;
                ErrorCard.Visibility = Visibility.Visible;
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadAsync();
        }
    }
}
