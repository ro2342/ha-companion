using System.Collections.Generic;
using System.Linq;
using HaCompanionUWP.Models;
using HaCompanionUWP.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace HaCompanionUWP.Views
{
    // Tela de seleção de favoritos: lista TODAS as entidades ao vivo
    // (agrupadas por domínio) com um CheckBox cada, marcado conforme a lista
    // já salva no CredentialStore. Existe porque não há como cravar
    // entity_id no código sem saber os reais da conta do rod -- ver
    // ha-companion-w10m.md.
    public sealed partial class FavoritesPickerPage : Page
    {
        private sealed class FavoriteRow
        {
            public CheckBox CheckBox;
            public string EntityId;
        }

        private static readonly KeyValuePair<string, string>[] Sections =
        {
            new KeyValuePair<string, string>("light", "Luzes"),
            new KeyValuePair<string, string>("switch", "Interruptores"),
            new KeyValuePair<string, string>("sensor", "Sensores"),
            new KeyValuePair<string, string>("script", "Scripts"),
        };

        private readonly List<FavoriteRow> _rows = new List<FavoriteRow>();

        public FavoritesPickerPage()
        {
            this.InitializeComponent();
            this.Loaded += FavoritesPickerPage_Loaded;
        }

        private async void FavoritesPickerPage_Loaded(object sender, RoutedEventArgs e)
        {
            ErrorCard.Visibility = Visibility.Collapsed;
            LoadingRing.IsActive = true;
            GroupsPanel.Children.Clear();
            _rows.Clear();

            try
            {
                List<HaEntityState> allStates = await HaApiService.GetStatesAsync();
                var currentFavorites = new HashSet<string>(CredentialStore.GetFavorites());

                foreach (KeyValuePair<string, string> section in Sections)
                {
                    string domain = section.Key;
                    string title = section.Value;

                    List<HaEntityState> entities = allStates
                        .Where(entity => entity.Domain == domain)
                        .OrderBy(entity => entity.FriendlyName)
                        .ToList();

                    if (entities.Count == 0)
                    {
                        continue;
                    }

                    GroupsPanel.Children.Add(new TextBlock
                    {
                        Text = title,
                        Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
                        Margin = new Thickness(0, 16, 0, 4),
                    });

                    foreach (HaEntityState entity in entities)
                    {
                        var checkBox = new CheckBox
                        {
                            Content = entity.FriendlyName,
                            IsChecked = currentFavorites.Contains(entity.EntityId),
                            Margin = new Thickness(0, 2, 0, 2),
                        };
                        GroupsPanel.Children.Add(checkBox);
                        _rows.Add(new FavoriteRow { CheckBox = checkBox, EntityId = entity.EntityId });
                    }
                }
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

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            IEnumerable<string> selected = _rows
                .Where(row => row.CheckBox.IsChecked == true)
                .Select(row => row.EntityId);
            CredentialStore.SaveFavorites(selected);
            this.Frame.GoBack();
        }
    }
}
