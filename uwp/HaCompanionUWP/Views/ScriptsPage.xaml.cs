using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HaCompanionUWP.Models;
using HaCompanionUWP.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace HaCompanionUWP.Views
{
    // Todas as script.* ao vivo -- card dispara script.turn_on.
    public sealed partial class ScriptsPage : Page
    {
        public ScriptsPage()
        {
            this.InitializeComponent();
            this.Loaded += ScriptsPage_Loaded;
        }

        private async void ScriptsPage_Loaded(object sender, RoutedEventArgs e)
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
                List<HaEntityState> scripts = allStates
                    .Where(entity => entity.Domain == "script")
                    .OrderBy(entity => entity.FriendlyName)
                    .ToList();

                EntitiesList.ItemsSource = scripts;
                EmptyText.Visibility = scripts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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

            try
            {
                await HaApiService.CallServiceAsync("script", "turn_on", entityId);
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
