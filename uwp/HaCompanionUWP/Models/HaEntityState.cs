using System;
using System.Globalization;
using Windows.Data.Json;

namespace HaCompanionUWP.Models
{
    // Espelha um item de GET /api/states. Usa Windows.Data.Json (não
    // System.Text.Json) porque o TargetPlatformMinVersion deste projeto
    // (10.0.14393.0, a base do Lumia 830) é anterior ao pacote System.Text.Json.
    public sealed class HaEntityState
    {
        public string EntityId { get; set; }
        public string State { get; set; }
        public JsonObject Attributes { get; set; }
        public DateTimeOffset? LastChanged { get; set; }

        public string Domain
        {
            get
            {
                if (string.IsNullOrEmpty(EntityId))
                {
                    return string.Empty;
                }
                int dot = EntityId.IndexOf('.');
                return dot >= 0 ? EntityId.Substring(0, dot) : string.Empty;
            }
        }

        public string FriendlyName
        {
            get
            {
                string name = GetStringAttribute("friendly_name");
                return string.IsNullOrEmpty(name) ? EntityId : name;
            }
        }

        public string UnitOfMeasurement => GetStringAttribute("unit_of_measurement");

        public string DeviceClass => GetStringAttribute("device_class");

        public bool IsOn => string.Equals(State, "on", StringComparison.OrdinalIgnoreCase);

        // Brilho do HA vai de 0 a 255 -- convertido pra percentual (0-100),
        // que é o que faz sentido mostrar na linha de contexto do card.
        public int? BrightnessPercent
        {
            get
            {
                if (Attributes != null && Attributes.ContainsKey("brightness"))
                {
                    IJsonValue value = Attributes["brightness"];
                    if (value.ValueType == JsonValueType.Number)
                    {
                        return (int)Math.Round(value.GetNumber() / 255.0 * 100.0);
                    }
                }
                return null;
            }
        }

        // Valor grande em destaque no card -- "ligada"/"desligada" pra
        // light/switch, "pronto" pra script (o estado bruto do HA aqui é só
        // "on"/"off" de disponibilidade, não é útil mostrar cru), e o valor
        // com unidade pra qualquer outro domínio (sensor etc.).
        public string DisplayValue
        {
            get
            {
                switch (Domain)
                {
                    case "light":
                    case "switch":
                        return IsOn ? "Ligada" : "Desligada";
                    case "script":
                        return "Pronto";
                    default:
                        return string.IsNullOrEmpty(UnitOfMeasurement) ? State : $"{State} {UnitOfMeasurement}";
                }
            }
        }

        // Linha de contexto pequena embaixo do valor -- brilho ou dica de
        // toque pra light/switch, hora da última atualização pra sensor,
        // dica de toque pra script.
        public string ContextLine
        {
            get
            {
                switch (Domain)
                {
                    case "light":
                    case "switch":
                        return BrightnessPercent.HasValue
                            ? $"Brilho {BrightnessPercent.Value}% · toque para alternar"
                            : "Toque para ligar/desligar";
                    case "script":
                        return "Toque para rodar";
                    default:
                        return LastChanged.HasValue
                            ? $"Sensor · atualizado às {LastChanged.Value.ToLocalTime():HH:mm}"
                            : "Sensor";
                }
            }
        }

        private string GetStringAttribute(string key)
        {
            if (Attributes == null || !Attributes.ContainsKey(key))
            {
                return null;
            }
            IJsonValue value = Attributes[key];
            switch (value.ValueType)
            {
                case JsonValueType.String:
                    return value.GetString();
                case JsonValueType.Number:
                    return value.GetNumber().ToString(CultureInfo.InvariantCulture);
                default:
                    return null;
            }
        }

        public static HaEntityState FromJson(JsonObject json)
        {
            var entity = new HaEntityState
            {
                EntityId = json.GetNamedString("entity_id", string.Empty),
                State = json.GetNamedString("state", string.Empty),
                Attributes = json.GetNamedObject("attributes", new JsonObject()),
            };

            string rawLastChanged = json.GetNamedString("last_changed", string.Empty);
            DateTimeOffset parsed;
            if (!string.IsNullOrEmpty(rawLastChanged) &&
                DateTimeOffset.TryParse(rawLastChanged, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out parsed))
            {
                entity.LastChanged = parsed;
            }

            return entity;
        }
    }
}
