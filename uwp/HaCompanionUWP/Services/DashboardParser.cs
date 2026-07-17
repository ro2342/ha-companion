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

            // Contêineres de layout ("grid" aqui é o card de layout dentro
            // de uma view masonry, diferente do "grid" que aparece como
            // tipo de SEÇÃO em view type "sections", já tratado em Flatten
            // antes de chegar aqui) e "entities" — em vez de espalhar cada
            // entidade lá dentro num card cheio separado (perdendo a
            // relação entre elas, ex.: o vacuum e os botões de escolher
            // cômodo que estavam juntos, ou o status da impressora e os
            // níveis de tinta), agrupa tudo num só DashboardTile.ForGroup,
            // com o primeiro heading/title encontrado como legenda.
            if (type == "vertical-stack" || type == "horizontal-stack" || type == "grid" || type == "entities")
            {
                var collected = new List<HaEntityState>();
                string groupTitle = null;
                CollectGroup(card, statesByEntityId, collected, ref groupTitle);

                if (collected.Count > 1)
                {
                    tiles.Add(DashboardTile.ForGroup(groupTitle, collected));
                }
                else if (collected.Count == 1)
                {
                    tiles.Add(DashboardTile.ForEntity(collected[0]));
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
            AddResolvedEntity(cardEntityId, cardName, statesByEntityId, collectedInto: null, tiles: tiles);
        }

        // Resolve um único card (não-container) pra dentro de um grupo em
        // construção — heading/title do PRIMEIRO card com essa informação
        // vira a legenda do grupo inteiro; "vertical-stack"/"grid" aninhado
        // dentro de outro é achatado pro MESMO grupo (ex.: o grid de botões
        // de cômodo dentro do vertical-stack do vacuum).
        private static void CollectGroup(JsonObject container, IReadOnlyDictionary<string, HaEntityState> statesByEntityId, List<HaEntityState> collected, ref string groupTitle)
        {
            string containerType = GetString(container, "type", string.Empty);

            if (containerType == "entities")
            {
                if (groupTitle == null)
                {
                    string title = GetString(container, "title", string.Empty);
                    if (!string.IsNullOrEmpty(title))
                    {
                        groupTitle = title;
                    }
                }
                foreach (IJsonValue itemValue in GetArray(container, "entities"))
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
                    AddResolvedEntity(entityId, customName, statesByEntityId, collected, tiles: null);
                }
                return;
            }

            foreach (IJsonValue cardValue in GetArray(container, "cards"))
            {
                JsonObject card = AsObjectOrNull(cardValue);
                if (card == null)
                {
                    continue;
                }
                string type = GetString(card, "type", string.Empty);

                if (type == "heading")
                {
                    if (groupTitle == null)
                    {
                        string headingText = GetString(card, "heading", string.Empty);
                        if (!string.IsNullOrEmpty(headingText))
                        {
                            groupTitle = headingText;
                        }
                    }
                    continue;
                }

                if (type == "vertical-stack" || type == "horizontal-stack" || type == "grid" || type == "entities")
                {
                    CollectGroup(card, statesByEntityId, collected, ref groupTitle);
                    continue;
                }

                string cardEntityId = GetString(card, "entity", string.Empty);
                string cardName = GetString(card, "name", string.Empty);
                AddResolvedEntity(cardEntityId, cardName, statesByEntityId, collected, tiles: null);
            }
        }

        // Ponto único de resolução entity_id -> HaEntityState (aplicando o
        // nome customizado do card, se houver) — usado tanto pra um tile
        // solto (tiles != null) quanto pra dentro de um grupo em formação
        // (collectedInto != null).
        private static void AddResolvedEntity(string entityId, string customName, IReadOnlyDictionary<string, HaEntityState> statesByEntityId, List<HaEntityState> collectedInto, List<DashboardTile> tiles)
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

            if (collectedInto != null)
            {
                collectedInto.Add(entity);
            }
            else
            {
                tiles.Add(DashboardTile.ForEntity(entity));
            }
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
