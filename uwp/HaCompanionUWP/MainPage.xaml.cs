using System;
using HaCompanionUWP.Services;
using HaCompanionUWP.Views;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace HaCompanionUWP
{
    // Shell de navegação nativo: um Frame pro conteúdo + um cabeçalho fixo no
    // topo (hambúrguer + título da seção atual) que abre um SplitView
    // deslizando por cima do conteúdo — mesmo padrão do
    // artistsway/uwp/ArtistWayUWP/MainPage.xaml.cs, com os itens de nav
    // trocados pros domínios do Home Assistant.
    public sealed partial class MainPage : Page
    {
        public static MainPage Current { get; private set; }

        private Type _currentTabPageType;
        private readonly UISettings _uiSettings = new UISettings();

        public MainPage()
        {
            try
            {
                this.InitializeComponent();
                Current = this;
                StyleMenuButton();
                this.Loaded += MainPage_Loaded;
                ContentFrame.Navigated += ContentFrame_Navigated;
                SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
                _uiSettings.ColorValuesChanged += UiSettings_ColorValuesChanged;
            }
            catch (Exception ex)
            {
                ShowFatalError("Erro ao iniciar a página: " + ex.Message);
            }
        }

        private async void UiSettings_ColorValuesChanged(UISettings sender, object args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                StyleMenuButton();
                if (_currentTabPageType != null)
                {
                    UpdateActiveTab(_currentTabPageType);
                }
            });
        }

        // Quadrado sólido na cor de destaque, igual ao botão de menu dos
        // apps nativos da Microsoft — precisa ser aplicado em código porque
        // a cor de destaque do sistema não muda com o tema.
        private void StyleMenuButton()
        {
            SolidColorBrush accent = ThemeHelper.AccentBrush();
            MenuButton.Background = accent;
            MenuButton.Foreground = new SolidColorBrush(Windows.UI.Colors.White);
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                HeaderBar.Visibility = Visibility.Visible;
                if (CredentialStore.HasConnection())
                {
                    NavigateToTab(typeof(FavoritesPage));
                }
                else
                {
                    NavigateToTab(typeof(SettingsPage));
                }
            }
            catch (Exception ex)
            {
                ShowFatalError("Erro ao carregar o app: " + ex.Message);
            }
        }

        // — painel de navegação —

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            SetPaneOpen(!NavSplitView.IsPaneOpen);
        }

        private void PaneDismissOverlay_Tapped(object sender, TappedRoutedEventArgs e)
        {
            SetPaneOpen(false);
        }

        private void SetPaneOpen(bool open)
        {
            NavSplitView.IsPaneOpen = open;
            PaneDismissOverlay.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        }

        private void NavItem_Click(object sender, RoutedEventArgs e)
        {
            string tag = (string)((FrameworkElement)sender).Tag;
            Type pageType;
            switch (tag)
            {
                case "Favorites":
                    pageType = typeof(FavoritesPage);
                    break;
                case "Lights":
                    pageType = typeof(LightsPage);
                    break;
                case "Sensors":
                    pageType = typeof(SensorsPage);
                    break;
                case "Scripts":
                    pageType = typeof(ScriptsPage);
                    break;
                case "Settings":
                    pageType = typeof(SettingsPage);
                    break;
                default:
                    return;
            }
            NavigateToTab(pageType);
            SetPaneOpen(false);
        }

        public void NavigateToTab(Type pageType, object parameter = null)
        {
            ContentFrame.Navigate(pageType, parameter);
            ContentFrame.BackStack.Clear();
            _currentTabPageType = pageType;
            UpdateActiveTab(pageType);
        }

        private void UpdateActiveTab(Type pageType)
        {
            SolidColorBrush accent = ThemeHelper.AccentBrush();

            bool isFavorites = pageType == typeof(FavoritesPage);
            bool isLights = pageType == typeof(LightsPage);
            bool isSensors = pageType == typeof(SensorsPage);
            bool isScripts = pageType == typeof(ScriptsPage);
            bool isSettings = pageType == typeof(SettingsPage);

            SetTabForeground(NavFavoritesLabel, NavFavoritesIcon, isFavorites, accent);
            SetTabForeground(NavLightsLabel, NavLightsIcon, isLights, accent);
            SetTabForeground(NavSensorsLabel, NavSensorsIcon, isSensors, accent);
            SetTabForeground(NavScriptsLabel, NavScriptsIcon, isScripts, accent);
            SetTabForeground(NavSettingsLabel, NavSettingsIcon, isSettings, accent);

            if (isFavorites) HeaderTitleText.Text = "Favoritos";
            else if (isLights) HeaderTitleText.Text = "Luzes";
            else if (isSensors) HeaderTitleText.Text = "Sensores";
            else if (isScripts) HeaderTitleText.Text = "Scripts";
            else if (isSettings) HeaderTitleText.Text = "Ajustes";
        }

        // IconElement (não SymbolIcon) porque alguns ícones de nav são
        // FontIcon (glifo cru, ver comentário no MainPage.xaml) — os dois
        // herdam de IconElement, que já tem Foreground usado aqui.
        private static void SetTabForeground(TextBlock label, IconElement icon, bool active, Brush accent)
        {
            if (active)
            {
                label.Foreground = accent;
                icon.Foreground = accent;
            }
            else
            {
                label.ClearValue(TextBlock.ForegroundProperty);
                icon.ClearValue(IconElement.ForegroundProperty);
            }
        }

        // — navegação/voltar —

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                ContentFrame.CanGoBack ? AppViewBackButtonVisibility.Visible : AppViewBackButtonVisibility.Collapsed;
        }

        private void OnBackRequested(object sender, BackRequestedEventArgs e)
        {
            if (NavSplitView.IsPaneOpen)
            {
                e.Handled = true;
                SetPaneOpen(false);
                return;
            }
            if (ContentFrame.CanGoBack)
            {
                e.Handled = true;
                ContentFrame.GoBack();
            }
        }

        // — erro fatal —

        // Se o InitializeComponent desta própria página falhar bem cedo
        // (ex.: um recurso mal formado em Page.Resources, que vem ANTES do
        // Grid/ErrorPanel no documento XAML), ErrorText/ErrorPanel podem
        // nunca ter sido conectados — mostrar o erro neles seria um no-op
        // silencioso (tela em branco, sem nenhum rastro do que aconteceu).
        // Nesse caso, cai pro Window.Current.Content direto, com um
        // TextBlock puro sem nenhum StaticResource desta página.
        private void ShowFatalError(string message)
        {
            if (ErrorText != null && ErrorPanel != null)
            {
                ErrorText.Text = message;
                ErrorPanel.Visibility = Visibility.Visible;
            }
            else
            {
                Window.Current.Content = new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(24),
                    VerticalAlignment = VerticalAlignment.Center,
                };
            }
        }
    }
}
