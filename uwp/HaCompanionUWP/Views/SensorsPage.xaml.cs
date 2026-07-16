using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HaCompanionUWP.Models;
using HaCompanionUWP.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace HaCompanionUWP.Views
{
    // Todas as sensor.* ao vivo — só leitura, sem ação de toque.
    public sealed partial class SensorsPage : Page
    {
        public SensorsPage()
        {
            this.InitializeComponent();
            this.Loaded += SensorsPage_Loaded;
        }

        private async void SensorsPage_Loaded(object sender, RoutedEventArgs e)
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
                List<HaEntityState> sensors = allStates
                    .Where(entity => entity.Domain == "sensor")
                    .OrderBy(entity => entity.FriendlyName)
                    .ToList();

                EntitiesList.ItemsSource = sensors;
                EmptyText.Visibility = sensors.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadAsync();
        }
    }
}
