using System;
using System.Threading.Tasks;
using HaCompanionUWP.Models;
using Windows.Data.Json;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace HaCompanionUWP.Services
{
    // Painel de controle de uma luz (ligar/desligar, brilho, cor) num
    // Flyout, reaproveitado por qualquer página que mostre um card de
    // light (Favoritos/Luzes/Dashboard). De propósito SEM
    // ColorPicker/ColorSpectrum (só existem em SDKs mais novos que o
    // TargetPlatformMinVersion deste projeto — mesma classe de risco que
    // já pegou o SymbolIcon Play antes) e sem roda de cor desenhada na
    // mão (mais código, mais risco pra testar só no aparelho de verdade).
    // Slider (controle básico, existe desde a v1 do UWP) pro brilho, e uma
    // grade de botões com cores sólidas pré-definidas em vez de uma roda —
    // não é qualquer tom exato, mas cobre o pedido de ajustar cor sem
    // introduzir nada arriscado.
    public static class LightControlFlyout
    {
        private static readonly byte[][] Palette =
        {
            new byte[] { 255, 0, 0 },
            new byte[] { 255, 140, 0 },
            new byte[] { 255, 215, 0 },
            new byte[] { 0, 200, 0 },
            new byte[] { 0, 180, 180 },
            new byte[] { 30, 100, 255 },
            new byte[] { 150, 50, 220 },
            new byte[] { 255, 80, 180 },
            new byte[] { 255, 214, 170 },
            new byte[] { 255, 255, 255 },
        };

        public static void Show(FrameworkElement anchor, HaEntityState entity, Func<Task> onToggleAsync, Action<string> onError)
        {
            var flyout = new Flyout();
            var panel = new StackPanel { Width = 260 };
            panel.Children.Add(new TextBlock { Text = entity.FriendlyName, Margin = new Thickness(0, 0, 0, 8) });

            string entityId = entity.EntityId;
            bool wasOn = entity.IsOn;

            var toggleButton = new Button
            {
                Content = wasOn ? "Desligar" : "Ligar",
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            toggleButton.Click += async (sender, e) =>
            {
                try
                {
                    await HaApiService.CallServiceAsync("light", wasOn ? "turn_off" : "turn_on", entityId);
                    flyout.Hide();
                    await onToggleAsync();
                }
                catch (HaApiException ex)
                {
                    onError(ex.Message);
                }
            };
            panel.Children.Add(toggleButton);

            panel.Children.Add(new TextBlock { Text = "Brilho", Margin = new Thickness(0, 12, 0, 0) });
            var brightnessSlider = new Slider
            {
                Minimum = 1,
                Maximum = 100,
                Value = entity.BrightnessPercent ?? 100,
            };
            panel.Children.Add(brightnessSlider);

            var setBrightnessButton = new Button
            {
                Content = "Definir brilho",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 8, 0, 0),
            };
            setBrightnessButton.Click += async (sender, e) =>
            {
                try
                {
                    var extra = new JsonObject { ["brightness_pct"] = JsonValue.CreateNumberValue(brightnessSlider.Value) };
                    await HaApiService.CallServiceAsync("light", "turn_on", entityId, extra);
                }
                catch (HaApiException ex)
                {
                    onError(ex.Message);
                }
            };
            panel.Children.Add(setBrightnessButton);

            panel.Children.Add(new TextBlock { Text = "Cor", Margin = new Thickness(0, 12, 0, 4) });
            var colorGrid = new Grid();
            for (int i = 0; i < 5; i++)
            {
                colorGrid.ColumnDefinitions.Add(new ColumnDefinition());
            }
            colorGrid.RowDefinitions.Add(new RowDefinition());
            colorGrid.RowDefinitions.Add(new RowDefinition());

            for (int i = 0; i < Palette.Length; i++)
            {
                byte r = Palette[i][0];
                byte g = Palette[i][1];
                byte b = Palette[i][2];

                var swatch = new Button
                {
                    Background = new SolidColorBrush(Color.FromArgb(255, r, g, b)),
                    Width = 40,
                    Height = 40,
                    Margin = new Thickness(2),
                    BorderThickness = new Thickness(1),
                };
                Grid.SetColumn(swatch, i % 5);
                Grid.SetRow(swatch, i / 5);
                swatch.Click += async (sender, e) =>
                {
                    try
                    {
                        var rgb = new JsonArray();
                        rgb.Add(JsonValue.CreateNumberValue(r));
                        rgb.Add(JsonValue.CreateNumberValue(g));
                        rgb.Add(JsonValue.CreateNumberValue(b));
                        var extra = new JsonObject { ["rgb_color"] = rgb };
                        await HaApiService.CallServiceAsync("light", "turn_on", entityId, extra);
                    }
                    catch (HaApiException ex)
                    {
                        onError(ex.Message);
                    }
                };
                colorGrid.Children.Add(swatch);
            }
            panel.Children.Add(colorGrid);

            flyout.Content = panel;
            flyout.ShowAt(anchor);
        }
    }
}
