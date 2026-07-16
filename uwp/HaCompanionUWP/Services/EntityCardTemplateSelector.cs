using HaCompanionUWP.Models;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace HaCompanionUWP.Services
{
    // Escolhe o DataTemplate certo por domínio da entidade (ver tabela de
    // mapeamento em ha-companion-w10m.md): light/switch viram card tocável
    // com toggle, script vira card "toque para rodar", qualquer outro
    // domínio (sensor etc.) vira card só leitura. Os templates em si ficam
    // nos recursos de cada página que os usa, atribuídos aqui pelas
    // propriedades abaixo.
    public sealed class EntityCardTemplateSelector : DataTemplateSelector
    {
        public DataTemplate LightCardTemplate { get; set; }
        public DataTemplate SensorCardTemplate { get; set; }
        public DataTemplate ScriptCardTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            return SelectTemplateCore(item);
        }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            var entity = item as HaEntityState;
            if (entity != null)
            {
                switch (entity.Domain)
                {
                    case "light":
                    case "switch":
                        return LightCardTemplate;
                    case "script":
                        return ScriptCardTemplate;
                    default:
                        return SensorCardTemplate;
                }
            }
            return SensorCardTemplate;
        }
    }
}
