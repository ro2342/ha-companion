using System.Collections.Generic;

namespace HaCompanionUWP.Models
{
    // Um item já achatado pronto pra exibir na DashboardPage — cabeçalho de
    // seção (heading), entidade solta (um card só), ou grupo (um
    // vertical-stack/grid/entities do Lovelace original, renderizado como
    // UM card com várias linhas compactas dentro — em vez de espalhar em
    // vários cards soltos e idênticos, perdendo a relação entre eles, ex.:
    // o vacuum e os botões de escolher cômodo que estavam juntos no
    // dashboard original).
    public sealed class DashboardTile
    {
        public bool IsHeading { get; private set; }
        public string HeadingText { get; private set; }
        public HaEntityState Entity { get; private set; }
        public string GroupTitle { get; private set; }
        public List<HaEntityState> GroupEntities { get; private set; }

        public bool IsGroup => GroupEntities != null;

        public static DashboardTile ForHeading(string text)
        {
            return new DashboardTile { IsHeading = true, HeadingText = text };
        }

        public static DashboardTile ForEntity(HaEntityState entity)
        {
            return new DashboardTile { IsHeading = false, Entity = entity };
        }

        public static DashboardTile ForGroup(string title, List<HaEntityState> entities)
        {
            return new DashboardTile { IsHeading = false, GroupTitle = title, GroupEntities = entities };
        }
    }
}
