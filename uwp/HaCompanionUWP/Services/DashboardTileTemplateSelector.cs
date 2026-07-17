using HaCompanionUWP.Models;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace HaCompanionUWP.Services
{
    // Escolhe entre cabeçalho de seção, entidade solta (um card só) e
    // grupo (um vertical-stack/grid/entities do Lovelace original,
    // renderizado num card só com várias linhas compactas dentro — ver
    // DashboardParser.CollectGroup). Grupo com/sem legenda usa templates
    // diferentes em vez de um TextBlock condicional (evita precisar de
    // IValueConverter só pra esconder o título quando vazio).
    public sealed class DashboardTileTemplateSelector : DataTemplateSelector
    {
        public DataTemplate HeadingTemplate { get; set; }
        public DataTemplate EntityTemplate { get; set; }
        public DataTemplate GroupTemplate { get; set; }
        public DataTemplate GroupWithTitleTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            return SelectTemplateCore(item);
        }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            var tile = item as DashboardTile;
            if (tile == null)
            {
                return EntityTemplate;
            }
            if (tile.IsHeading)
            {
                return HeadingTemplate;
            }
            if (tile.IsGroup)
            {
                return string.IsNullOrEmpty(tile.GroupTitle) ? GroupTemplate : GroupWithTitleTemplate;
            }
            return EntityTemplate;
        }
    }
}
