namespace HaCompanionUWP.Models
{
    // Um item já achatado pronto pra exibir na DashboardPage — ou um
    // cabeçalho de seção (heading, do card type="heading" do Lovelace), ou
    // uma entidade (o mesmo card genérico reaproveitado dos outros
    // domínios do app).
    public sealed class DashboardTile
    {
        public bool IsHeading { get; private set; }
        public string HeadingText { get; private set; }
        public HaEntityState Entity { get; private set; }

        public static DashboardTile ForHeading(string text)
        {
            return new DashboardTile { IsHeading = true, HeadingText = text };
        }

        public static DashboardTile ForEntity(HaEntityState entity)
        {
            return new DashboardTile { IsHeading = false, Entity = entity };
        }
    }
}
