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
    //
    // Tudo aqui lê JSON de configuração ESCRITA PELO USUÁRIO (cards de
    // terceiros via HACS, com esquema livre) — diferente de HaEntityState,
    // que lê a resposta de /api/states, controlada pelo próprio núcleo do
    // HA. Por isso todo acesso a campo usa os helpers Get*/GetString/GetArray
    // abaixo em vez de JsonObject.GetNamedString/GetNamedArray direto: esses
    // métodos do WinRT só usam o defaultValue quando a CHAVE não existe —
    // se a chave existir com um tipo diferente do esperado (ex.: um card
    // customizado guardando um objeto onde a gente espera string), eles
    // estouram "This is not a string value" em vez de cair no default,
    // travando o app inteiro por causa de um card que nem é renderizado.
    public static class DashboardParser
    {
        // Só a primeira view nesta leva — é o caso real de uso do rod (uma
        // view só, tipo "sections").
        public static List<DashboardTile> Flatten(JsonObject dashboardConfig, IReadOnlyDictionary<string, HaEntityState> statesByEntityId)
        {
            var tiles = new List<DashboardTile>();

            JsonArray views = GetArray(dashboardConfig, "views");
            if (views.Count == 0)
            {
                return tiles;
            }

            JsonObject firstView = AsObjectOrNull(views[0]);
            if (firstView == null)
            {
                return tiles;
            }

            string viewType = GetString(firstView, "type", "masonry");

            if (viewType == "sections")
            {
                foreach (IJsonValue sectionValue in GetArray(firstView, "sections"))
                {
                    JsonObject section = AsObjectOrNull(sectionValue);
                    if (section == null)
                    {
                        continue;
                    }
                    AppendCards(GetArray(section, "cards"), statesByEntityId, tiles);
                }
            }
            else
            {
                AppendCards(GetArray(firstView, "cards"), statesByEntityId, tiles);
            }

            return tiles;
        }

        public static string GetFirstViewTitle(JsonObject dashboardConfig)
        {
            JsonArray views = GetArray(dashboardConfig, "views");
            if (views.Count == 0)
            {
                return string.Empty;
            }
            JsonObject firstView = AsObjectOrNull(views[0]);
            return firstView == null ? string.Empty : GetString(firstView, "title", string.Empty);
        }

        private static void AppendCards(JsonArray cards, IReadOnlyDictionary<string, HaEntityState> statesByEntityId, List<DashboardTile> tiles)
        {
            foreach (IJsonValue cardValue in cards)
            {
                JsonObject card = AsObjectOrNull(cardValue);
                if (card == null)
                {
                    continue;
                }
                AppendCard(card, statesByEntityId, tiles);
            }
        }

        private static void AppendCard(JsonObject card, IReadOnlyDictionary<string, HaEntityState> statesByEntityId, List<DashboardTile> tiles)
        {
            string type = GetString(card, "type", string.Empty);

            if (type == "heading")
            {
                string headingText = GetString(card, "heading", string.Empty);
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
                AppendCards(GetArray(card, "cards"), statesByEntityId, tiles);
                return;
            }

            // "entities": lista de entidades, cada uma como string direta
            // ("light.sala") ou objeto ({entity: ..., name: ...}).
            if (type == "entities")
            {
                foreach (IJsonValue itemValue in GetArray(card, "entities"))
                {
                    string entityId = null;
                    string customName = null;
                    if (itemValue.ValueType == JsonValueType.String)
                    {
                        entityId = itemValue.GetString();
                    }
                    else
                    {
                        JsonObject itemObject = AsObjectOrNull(itemValue);
                        if (itemObject != null)
                        {
                            entityId = GetString(itemObject, "entity", string.Empty);
                            customName = GetString(itemObject, "name", string.Empty);
                        }
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
            string cardEntityId = GetString(card, "entity", string.Empty);
            string cardName = GetString(card, "name", string.Empty);
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

        // Só usa o defaultValue quando a chave existe mas NÃO é string —
        // diferente de JsonObject.GetNamedString, que só cobre o caso de
        // chave ausente e estoura em qualquer outro tipo (ver comentário no
        // topo do arquivo).
        private static string GetString(JsonObject obj, string key, string defaultValue)
        {
            IJsonValue value;
            if (obj == null || !obj.TryGetValue(key, out value) || value.ValueType != JsonValueType.String)
            {
                return defaultValue;
            }
            return value.GetString();
        }

        // Mesma ideia pra array: devolve vazio em vez de estourar se a
        // chave existir com outro tipo.
        private static JsonArray GetArray(JsonObject obj, string key)
        {
            IJsonValue value;
            if (obj == null || !obj.TryGetValue(key, out value) || value.ValueType != JsonValueType.Array)
            {
                return new JsonArray();
            }
            return value.GetArray();
        }

        private static JsonObject AsObjectOrNull(IJsonValue value)
        {
            return value != null && value.ValueType == JsonValueType.Object ? value.GetObject() : null;
        }
    }
}
