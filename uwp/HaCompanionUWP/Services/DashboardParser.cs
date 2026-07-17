using System.Collections.Generic;
using HaCompanionUWP.Models;
using Windows.Data.Json;

namespace HaCompanionUWP.Services
{
    // Achata a config de um dashboard Lovelace (vinda de
    // HaWebSocketService.GetDashboardConfigAsync) numa lista simples de
    // tiles pra exibir — não tenta replicar o layout visual de cards de
    // terceiros (mushroom-*, hue-like-light-card etc.), só extrai a
    // entidade que cada card controla e deixa o resto do app renderizar do
    // jeito nativo de sempre, por domínio. Cards sem entity utilizável
    // (ex.: custom:firemote-card sem entity real, custom:mushroom-template-card
    // que só tem texto de template) são ignorados silenciosamente.
    public static class DashboardParser
    {
        // Só a primeira view nesta leva — é o caso real de uso do rod (uma
        // view só, tipo "sections").
        public static List<DashboardTile> Flatten(JsonObject dashboardConfig, IReadOnlyDictionary<string, HaEntityState> statesByEntityId)
        {
            var tiles = new List<DashboardTile>();

            JsonArray views = dashboardConfig.GetNamedArray("views", new JsonArray());
            if (views.Count == 0)
            {
                return tiles;
            }

            JsonObject firstView = views[0].GetObject();
            string viewType = firstView.GetNamedString("type", "masonry");

            if (viewType == "sections")
            {
                foreach (IJsonValue sectionValue in firstView.GetNamedArray("sections", new JsonArray()))
                {
                    if (sectionValue.ValueType != JsonValueType.Object)
                    {
                        continue;
                    }
                    JsonObject section = sectionValue.GetObject();
                    AppendCards(section.GetNamedArray("cards", new JsonArray()), statesByEntityId, tiles);
                }
            }
            else
            {
                AppendCards(firstView.GetNamedArray("cards", new JsonArray()), statesByEntityId, tiles);
            }

            return tiles;
        }

        public static string GetFirstViewTitle(JsonObject dashboardConfig)
        {
            JsonArray views = dashboardConfig.GetNamedArray("views", new JsonArray());
            if (views.Count == 0)
            {
                return null;
            }
            // GetNamedString não aceita null como defaultValue — é um
            // parâmetro HSTRING (WinRT), que não representa null; passar
            // null aqui estourava ArgumentNullException/Null_HString em
            // tempo de execução (só aparece com .NET Native/Release, não
            // dá pra pegar sem testar no aparelho de verdade).
            return views[0].GetObject().GetNamedString("title", string.Empty);
        }

        private static void AppendCards(JsonArray cards, IReadOnlyDictionary<string, HaEntityState> statesByEntityId, List<DashboardTile> tiles)
        {
            foreach (IJsonValue cardValue in cards)
            {
                if (cardValue.ValueType != JsonValueType.Object)
                {
                    continue;
                }
                AppendCard(cardValue.GetObject(), statesByEntityId, tiles);
            }
        }

        private static void AppendCard(JsonObject card, IReadOnlyDictionary<string, HaEntityState> statesByEntityId, List<DashboardTile> tiles)
        {
            string type = card.GetNamedString("type", string.Empty);

            if (type == "heading")
            {
                string headingText = card.GetNamedString("heading", string.Empty);
                if (!string.IsNullOrEmpty(headingText))
                {
                    tiles.Add(DashboardTile.ForHeading(headingText));
                }
                return;
            }

            // Contêineres de layout — achata recursivamente. "grid" aqui é
            // o card de layout (dentro de uma view masonry), diferente do
            // "grid" que aparece como tipo de SEÇÃO em view type "sections"
            // (já tratado em Flatten antes de chegar aqui) — os dois só
            // têm um array "cards" por dentro, mesmo tratamento serve.
            if (type == "vertical-stack" || type == "horizontal-stack" || type == "grid")
            {
                AppendCards(card.GetNamedArray("cards", new JsonArray()), statesByEntityId, tiles);
                return;
            }

            // "entities": lista de entidades, cada uma como string direta
            // ("light.sala") ou objeto ({entity: ..., name: ...}).
            if (type == "entities")
            {
                foreach (IJsonValue itemValue in card.GetNamedArray("entities", new JsonArray()))
                {
                    string entityId = null;
                    string customName = null;
                    if (itemValue.ValueType == JsonValueType.String)
                    {
                        entityId = itemValue.GetString();
                    }
                    else if (itemValue.ValueType == JsonValueType.Object)
                    {
                        JsonObject itemObject = itemValue.GetObject();
                        entityId = itemObject.GetNamedString("entity", string.Empty);
                        customName = itemObject.GetNamedString("name", string.Empty);
                    }
                    AddEntityTile(entityId, customName, statesByEntityId, tiles);
                }
                return;
            }

            // Qualquer outro card com campo "entity" direto — cobre tile,
            // light, humidifier, sensor, entity, todo-list nativos, e todo
            // custom:mushroom-*/hue-like-light-card/mushroom-vacuum-card,
            // que sempre declaram "entity:". Sem esse campo (ou "none"),
            // não tem como saber o que o card controla — ignora.
            string cardEntityId = card.GetNamedString("entity", string.Empty);
            string cardName = card.GetNamedString("name", string.Empty);
            AddEntityTile(cardEntityId, cardName, statesByEntityId, tiles);
        }

        private static void AddEntityTile(string entityId, string customName, IReadOnlyDictionary<string, HaEntityState> statesByEntityId, List<DashboardTile> tiles)
        {
            if (string.IsNullOrEmpty(entityId) || entityId == "none")
            {
                return;
            }

            HaEntityState entity;
            if (!statesByEntityId.TryGetValue(entityId, out entity))
            {
                return;
            }

            if (!string.IsNullOrEmpty(customName))
            {
                entity = entity.WithDisplayName(customName);
            }

            tiles.Add(DashboardTile.ForEntity(entity));
        }
    }
}
