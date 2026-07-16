using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace HaCompanionUWP
{
    // Ponto de entrada do app. Sem conteúdo pra carregar de antemão (ao
    // contrário do artistsway) — o shell (MainPage) decide sozinho, no
    // Loaded, se manda pra Ajustes (primeiro uso, sem URL/token salvos) ou
    // pra Favoritos.
    sealed partial class App : Application
    {
        // Guardado em vez de deixar a exceção propagar: uma falha aqui
        // (ex.: recurso mal formado em App.xaml) acontece dentro do próprio
        // InitializeComponent, antes de qualquer handler poder ser
        // registrado — sem isso o processo simplesmente encerra sozinho,
        // mesmo antes da splash screen aparecer, sem deixar rastro nenhum
        // (não há como puxar log do aparelho daqui).
        private string _startupError;

        public App()
        {
            try
            {
                this.InitializeComponent();
            }
            catch (Exception ex)
            {
                _startupError = ex.Message;
            }
            this.Suspending += OnSuspending;
            this.UnhandledException += App_UnhandledException;
        }

        private async void App_UnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            try
            {
                var dialog = new Windows.UI.Popups.MessageDialog(e.Message ?? "Erro desconhecido", "Erro inesperado no app");
                await dialog.ShowAsync();
            }
            catch
            {
                // Se nem o diálogo conseguir abrir, não há mais nada a fazer aqui.
            }
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            if (_startupError != null)
            {
                // TextBlock puro, sem nenhum StaticResource — se o
                // problema foi um recurso de App.xaml mal formado, resolver
                // contra esses mesmos recursos aqui só trocaria um crash
                // silencioso por um crash com uma tela em branco.
                Window.Current.Content = new TextBlock
                {
                    Text = "Erro ao iniciar o app: " + _startupError,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(24),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                Window.Current.Activate();
                return;
            }

            Frame rootFrame = Window.Current.Content as Frame;

            if (rootFrame == null)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }
                Window.Current.Activate();
            }
        }

        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new System.Exception("Falha ao carregar a página: " + e.SourcePageType.FullName);
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();
        }
    }
}
