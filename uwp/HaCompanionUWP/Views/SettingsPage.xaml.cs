using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HaCompanionUWP.Models;
using HaCompanionUWP.Services;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace HaCompanionUWP.Views
{
    // URL base + Long-Lived Access Token (primeiro uso, ou reconfiguração).
    // "Testar conexão" e "Salvar" gravam os campos atuais no CredentialStore
    // antes de agir — não existe um caminho paralelo "não salvo" no
    // HaApiService, que sempre lê do CredentialStore.
    public sealed partial class SettingsPage : Page
    {
        private StorageFile _downloadedUpdateFile;
        private string _savedDashboardUrlPath;

        public SettingsPage()
        {
            this.InitializeComponent();
            this.Loaded += SettingsPage_Loaded;
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            BaseUrlBox.Text = CredentialStore.GetBaseUrl() ?? string.Empty;
            if (!string.IsNullOrEmpty(CredentialStore.GetToken()))
            {
                TokenBox.PlaceholderText = "Token salvo — deixe em branco para manter";
            }
            _savedDashboardUrlPath = CredentialStore.GetDashboardUrlPath() ?? string.Empty;
            CurrentVersionText.Text = $"Versão instalada: {UpdateCheckService.CurrentVersion}";
        }

        // Se o campo de token estiver vazio, mantém o token já salvo (evita
        // apagar sem querer só por reabrir a tela e apertar Salvar de novo).
        private void SaveCurrentInput()
        {
            CredentialStore.SaveBaseUrl(BaseUrlBox.Text?.Trim());
            if (!string.IsNullOrEmpty(TokenBox.Password))
            {
                CredentialStore.SaveToken(TokenBox.Password);
            }
            // Só grava se o usuário chegou a carregar/escolher algo nesta
            // visita à tela — sem isso, abrir Ajustes só pra trocar o
            // token e apertar Salvar apagaria o dashboard já configurado.
            var selected = DashboardPicker.SelectedValue as string;
            if (selected != null)
            {
                CredentialStore.SaveDashboardUrlPath(selected);
            }
        }

        private async void LoadDashboards_Click(object sender, RoutedEventArgs e)
        {
            LoadDashboardsButton.IsEnabled = false;
            StatusText.Visibility = Visibility.Visible;
            StatusText.Text = "Carregando dashboards...";

            try
            {
                List<DashboardInfo> dashboards = await HaWebSocketService.GetDashboardsAsync();
                DashboardPicker.ItemsSource = dashboards;

                int savedIndex = dashboards.FindIndex(d => d.UrlPath == _savedDashboardUrlPath);
                DashboardPicker.SelectedIndex = savedIndex >= 0 ? savedIndex : 0;

                StatusText.Text = $"{dashboards.Count} dashboard(s) encontrado(s).";
            }
            catch (HaWebSocketException ex)
            {
                StatusText.Text = ex.Message;
            }
            catch (Exception ex)
            {
                StatusText.Text = "Não deu pra carregar os dashboards: " + ex.Message;
            }
            finally
            {
                LoadDashboardsButton.IsEnabled = true;
            }
        }

        private async void Test_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentInput();
            StatusText.Visibility = Visibility.Visible;
            StatusText.Text = "Testando...";

            try
            {
                var states = await HaApiService.GetStatesAsync();
                StatusText.Text = $"Conectado! {states.Count} entidades encontradas.";
            }
            catch (HaApiException ex)
            {
                StatusText.Text = ex.Message;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentInput();
            if (CredentialStore.HasConnection())
            {
                MainPage.Current.NavigateToTab(typeof(FavoritesPage));
            }
        }

        private void ChooseFavorites_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(FavoritesPickerPage));
        }

        private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            CheckUpdateButton.IsEnabled = false;
            InstallUpdateButton.Visibility = Visibility.Collapsed;
            OpenDownloadPageButton.Visibility = Visibility.Collapsed;
            UpdateStatusText.Visibility = Visibility.Visible;
            UpdateStatusText.Text = "Verificando...";

            try
            {
                UpdateCheckResult result = await UpdateCheckService.CheckAsync();
                if (result.HasUpdate)
                {
                    // Baixa direto, sem precisar de um segundo toque — a
                    // única ação manual que sobra é escolher a pasta de
                    // download (uma vez só, via FolderPicker) e depois
                    // tocar em "Instalar agora".
                    UpdateStatusText.Text = $"Versão nova disponível: {result.LatestVersion}. Baixando...";
                    await DownloadUpdateAsync();
                }
                else
                {
                    UpdateStatusText.Text = "Você já está na versão mais recente.";
                }
            }
            catch (Exception)
            {
                UpdateStatusText.Text = "Não foi possível verificar agora — confira sua conexão com a internet.";
                OpenDownloadPageButton.Visibility = Visibility.Visible;
            }
            finally
            {
                CheckUpdateButton.IsEnabled = true;
            }
        }

        private async Task DownloadUpdateAsync()
        {
            UpdateProgressBar.Value = 0;
            UpdateProgressBar.Visibility = Visibility.Visible;

            var progress = new Progress<double>(p => UpdateProgressBar.Value = p);
            StorageFile file;
            try
            {
                file = await UpdateCheckService.DownloadUpdateAsync(progress);
            }
            catch (Exception ex)
            {
                UpdateProgressBar.Visibility = Visibility.Collapsed;
                UpdateStatusText.Text = "Falha ao baixar a atualização: " + ex.Message;
                OpenDownloadPageButton.Visibility = Visibility.Visible;
                return;
            }

            UpdateProgressBar.Visibility = Visibility.Collapsed;

            if (file == null)
            {
                UpdateStatusText.Text = "Escolha uma pasta de download pra continuar.";
                return;
            }

            _downloadedUpdateFile = file;
            InstallUpdateButton.Visibility = Visibility.Visible;
            UpdateStatusText.Text = "Atualização baixada — toque em \"Instalar agora\".";
        }

        private async void InstallUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (_downloadedUpdateFile == null)
            {
                return;
            }
            await Windows.System.Launcher.LaunchFileAsync(_downloadedUpdateFile);
        }

        private async void OpenDownloadPage_Click(object sender, RoutedEventArgs e)
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri(UpdateCheckService.DownloadPageUrl));
        }

        private async void ClearCredentials_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Windows.UI.Popups.MessageDialog(
                "Isso apaga a URL, o token e a lista de favoritos salvos neste aparelho.",
                "Limpar credenciais?");
            dialog.Commands.Add(new Windows.UI.Popups.UICommand("Limpar"));
            dialog.Commands.Add(new Windows.UI.Popups.UICommand("Cancelar"));
            dialog.CancelCommandIndex = 1;

            Windows.UI.Popups.IUICommand result = await dialog.ShowAsync();
            if (result.Label == "Limpar")
            {
                CredentialStore.ClearAll();
                BaseUrlBox.Text = string.Empty;
                TokenBox.Password = string.Empty;
                TokenBox.PlaceholderText = "Gerado no seu perfil do Home Assistant";
                DashboardPicker.ItemsSource = null;
                _savedDashboardUrlPath = string.Empty;
                StatusText.Visibility = Visibility.Collapsed;
            }
        }
    }
}
