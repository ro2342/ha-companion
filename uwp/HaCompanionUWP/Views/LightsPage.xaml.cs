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
    // Toque numa light abre o LightControlFlyout (ligar/desligar, brilho,
    // cor); switch continua um toggle simples.
    public sealed partial class LightsPage : Page
    {
        private Dictionary<string, HaEntityState> _statesByEntityId = new Dictionary<string, HaEntityState>();

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
                _statesByEntityId = allStates.ToDictionary(entity => entity.EntityId, entity => entity);

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
            var element = (FrameworkElement)sender;
            string entityId = (string)element.Tag;
            string domain = entityId.Contains(".") ? entityId.Substring(0, entityId.IndexOf('.')) : string.Empty;

            if (domain == "light")
            {
                HaEntityState entity;
                if (_statesByEntityId.TryGetValue(entityId, out entity))
                {
                    LightControlFlyout.Show(element, entity, LoadAsync, ShowError);
                }
                return;
            }

            try
            {
                await HaApiService.CallServiceAsync(domain, "toggle", entityId);
                await LoadAsync();
            }
            catch (HaApiException ex)
            {
                ShowError(ex.Message);
            }
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorCard.Visibility = Visibility.Visible;
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadAsync();
        }
    }
}
