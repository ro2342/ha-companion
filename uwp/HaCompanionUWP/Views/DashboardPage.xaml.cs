using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using HaCompanionUWP.Models;
using HaCompanionUWP.Services;
using Windows.Data.Json;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace HaCompanionUWP.Views
{
    // Carrega a config do dashboard configurado em Ajustes (via
    // HaWebSocketService, único lugar do app que usa WebSocket) + os
    // estados ao vivo (via HaApiService, REST), achata com DashboardParser
    // e mostra um tile por entidade — toque despacha pro serviço certo
    // conforme o domínio, igual ao padrão já usado em FavoritesPage.
    public sealed partial class DashboardPage : Page
    {
        private Dictionary<string, HaEntityState> _statesByEntityId = new Dictionary<string, HaEntityState>();

        public DashboardPage()
        {
            this.InitializeComponent();
            this.Loaded += DashboardPage_Loaded;
        }

        private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
        {
            DashboardTitleText.Text = CredentialStore.GetDashboardTitle() ?? "Dashboard";
            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            ErrorCard.Visibility = Visibility.Collapsed;
            EmptyText.Visibility = Visibility.Collapsed;
            LoadingRing.IsActive = true;

            try
            {
                JsonObject config = await HaWebSocketService.GetDashboardConfigAsync(CredentialStore.GetDashboardUrlPath());

                string title = DashboardParser.GetFirstViewTitle(config);
                if (!string.IsNullOrEmpty(title))
                {
                    CredentialStore.SaveDashboardTitle(title);
                    DashboardTitleText.Text = title;
                }

                List<HaEntityState> allStates = await HaApiService.GetStatesAsync();
                _statesByEntityId = allStates.ToDictionary(entity => entity.EntityId, entity => entity);

                List<DashboardTile> tiles = DashboardParser.Flatten(config, _statesByEntityId);
                TilesList.ItemsSource = tiles;
                EmptyText.Visibility = tiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (HaWebSocketException ex)
            {
                ErrorText.Text = ex.Message;
                ErrorCard.Visibility = Visibility.Visible;
            }
            catch (HaApiException ex)
            {
                ErrorText.Text = ex.Message;
                ErrorCard.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                // Rede de segurança: dashboards de terceiros têm esquema
                // livre (cards HACS variados) — se ainda assim sobrar
                // algum jeito de um card estourar uma exceção crua, isso
                // aparece aqui como mensagem amigável em vez de derrubar o
                // app inteiro pro diálogo genérico de erro não tratado.
                ErrorText.Text = "Não deu pra carregar o dashboard: " + ex.Message;
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

            HaEntityState entity;
            _statesByEntityId.TryGetValue(entityId, out entity);

            try
            {
                switch (domain)
                {
                    case "light":
                    case "switch":
                    case "fan":
                    case "humidifier":
                    case "input_boolean":
                    case "cover":
                        await HaApiService.CallServiceAsync(domain, "toggle", entityId);
                        break;
                    case "script":
                        await HaApiService.CallServiceAsync("script", "turn_on", entityId);
                        break;
                    case "media_player":
                        await HaApiService.CallServiceAsync("media_player", "media_play_pause", entityId);
                        break;
                    case "vacuum":
                        string vacuumService = entity != null && entity.State == "cleaning" ? "pause" : "start";
                        await HaApiService.CallServiceAsync("vacuum", vacuumService, entityId);
                        break;
                    case "update":
                        await HaApiService.CallServiceAsync("update", "install", entityId);
                        break;
                    case "number":
                        if (entity != null)
                        {
                            ShowNumberFlyout(element, entityId, entity);
                        }
                        return;
                    case "todo":
                        await OpenTodoListAsync(entityId);
                        return;
                    default:
                        // Domínios só leitura (sensor, weather etc.) — toque não faz nada.
                        return;
                }
                await LoadAsync();
            }
            catch (HaApiException ex)
            {
                ErrorText.Text = ex.Message;
                ErrorCard.Visibility = Visibility.Visible;
            }
        }

        private void ShowNumberFlyout(FrameworkElement anchor, string entityId, HaEntityState entity)
        {
            var input = new TextBox { Text = entity.State, Width = 160 };
            var setButton = new Button { Content = "Definir", Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = entity.FriendlyName, Margin = new Thickness(0, 0, 0, 8) });
            panel.Children.Add(input);
            panel.Children.Add(setButton);

            var flyout = new Flyout { Content = panel };
            setButton.Click += async (sender, e) =>
            {
                double value;
                if (double.TryParse(input.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                {
                    try
                    {
                        var extra = new JsonObject { ["value"] = JsonValue.CreateNumberValue(value) };
                        await HaApiService.CallServiceAsync("number", "set_value", entityId, extra);
                        flyout.Hide();
                        await LoadAsync();
                    }
                    catch (HaApiException ex)
                    {
                        ErrorText.Text = ex.Message;
                        ErrorCard.Visibility = Visibility.Visible;
                    }
                }
            };
            flyout.ShowAt(anchor);
        }

        private async Task OpenTodoListAsync(string entityId)
        {
            JsonArray items;
            try
            {
                items = await HaWebSocketService.GetTodoItemsAsync(entityId);
            }
            catch (HaWebSocketException ex)
            {
                ErrorText.Text = ex.Message;
                ErrorCard.Visibility = Visibility.Visible;
                return;
            }

            var panel = new StackPanel();
            foreach (IJsonValue itemValue in items)
            {
                if (itemValue.ValueType != JsonValueType.Object)
                {
                    continue;
                }
                JsonObject item = itemValue.GetObject();
                string uid = item.GetNamedString("uid", string.Empty);
                string summary = item.GetNamedString("summary", string.Empty);
                string status = item.GetNamedString("status", string.Empty);

                var checkBox = new CheckBox
                {
                    Content = summary,
                    IsChecked = status == "completed",
                    Margin = new Thickness(0, 2, 0, 2),
                };
                checkBox.Checked += async (sender, e) => await ToggleTodoItemAsync(entityId, uid, true);
                checkBox.Unchecked += async (sender, e) => await ToggleTodoItemAsync(entityId, uid, false);
                panel.Children.Add(checkBox);
            }

            var dialog = new ContentDialog
            {
                Title = "Lista",
                Content = new ScrollViewer { Content = panel, MaxHeight = 400 },
                CloseButtonText = "Fechar",
            };
            await dialog.ShowAsync();
        }

        private async Task ToggleTodoItemAsync(string entityId, string itemUid, bool completed)
        {
            try
            {
                await HaWebSocketService.MarkTodoItemAsync(entityId, itemUid, completed);
            }
            catch (HaWebSocketException ex)
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
