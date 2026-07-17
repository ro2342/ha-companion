using HaCompanionUWP.Models;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace HaCompanionUWP.Services
{
    // Escolhe entre o template de cabeçalho de seção e o template de
    // entidade genérico (o mesmo pra qualquer domínio) ao renderizar a
    // lista já achatada pelo DashboardParser.
    public sealed class DashboardTileTemplateSelector : DataTemplateSelector
    {
        public DataTemplate HeadingTemplate { get; set; }
        public DataTemplate EntityTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            return SelectTemplateCore(item);
        }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            var tile = item as DashboardTile;
            if (tile != null && tile.IsHeading)
            {
                return HeadingTemplate;
            }
            return EntityTemplate;
        }
    }
}
