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

        // Nome customizado vindo de um card do dashboard (ex.: "Kitchen In"
        // pra switch.kitchen_lights_switch_2) — sobrepõe o friendly_name
        // do próprio HA só pra essa exibição, sem mutar o objeto original
        // (ver WithDisplayName, usado pelo DashboardParser).
        public string DisplayNameOverride { get; set; }

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
                if (!string.IsNullOrEmpty(DisplayNameOverride))
                {
                    return DisplayNameOverride;
                }
                string name = GetStringAttribute("friendly_name");
                return string.IsNullOrEmpty(name) ? EntityId : name;
            }
        }

        public string UnitOfMeasurement => GetStringAttribute("unit_of_measurement");

        public string DeviceClass => GetStringAttribute("device_class");

        public bool IsOn => string.Equals(State, "on", StringComparison.OrdinalIgnoreCase);

        // Brilho do HA vai de 0 a 255 — convertido pra percentual (0-100),
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

        // Valor grande em destaque no card — "ligada"/"desligada" pra
        // light/switch/fan/humidifier/input_boolean, e um valor específico
        // por domínio pros demais (media_player, weather, vacuum, cover,
        // update, todo) — o resto (sensor, number etc.) cai no valor bruto
        // + unidade.
        public string DisplayValue
        {
            get
            {
                switch (Domain)
                {
                    case "light":
                    case "switch":
                    case "fan":
                    case "humidifier":
                    case "input_boolean":
                        return IsOn ? "Ligada" : "Desligada";
                    case "script":
                        return "Pronto";
                    case "cover":
                        return string.Equals(State, "open", StringComparison.OrdinalIgnoreCase) ? "Aberta" : "Fechada";
                    case "media_player":
                        return MediaPlayerDisplay();
                    case "weather":
                        return WeatherDisplay();
                    case "vacuum":
                        return VacuumDisplay();
                    case "update":
                        return IsOn ? "Atualização disponível" : "Atualizado";
                    case "todo":
                        return $"{State} pendente(s)";
                    default:
                        return string.IsNullOrEmpty(UnitOfMeasurement) ? State : $"{State} {UnitOfMeasurement}";
                }
            }
        }

        // Linha de contexto pequena embaixo do valor — dica de toque por
        // domínio.
        public string ContextLine
        {
            get
            {
                switch (Domain)
                {
                    case "light":
                    case "switch":
                        return LightContextDetails();
                    case "fan":
                    case "humidifier":
                    case "input_boolean":
                    case "cover":
                        return "Toque para alternar";
                    case "script":
                        return "Toque para rodar";
                    case "media_player":
                        return "Toque para tocar/pausar";
                    case "vacuum":
                        return "Toque para iniciar/pausar";
                    case "update":
                        return IsOn ? "Toque para instalar" : "Sem atualização pendente";
                    case "todo":
                        return "Toque para ver a lista";
                    default:
                        return LastChanged.HasValue
                            ? $"Sensor · atualizado às {LastChanged.Value.ToLocalTime():HH:mm}"
                            : "Sensor";
                }
            }
        }

        // Menciona brilho e/ou capacidade de cor (RGB, temperatura de cor)
        // na linha de contexto — sem isso, uma lâmpada RGB ficava
        // indistinguível de um interruptor comum, ambos só "toque para
        // ligar/desligar". Só troca cor de verdade via app oficial/HA, não
        // dá pra ajustar aqui ainda — a menção já ajuda a saber "essa eu
        // posso trocar de cor no app de verdade", ver ha-companion-w10m.md.
        private string LightContextDetails()
        {
            string details = null;
            if (BrightnessPercent.HasValue)
            {
                details = $"Brilho {BrightnessPercent.Value}%";
            }
            if (Domain == "light" && SupportsColor())
            {
                details = details == null ? "Cor ajustável" : details + " · Cor ajustável";
            }
            return details == null ? "Toque para ligar/desligar" : details + " · toque para alternar";
        }

        private bool SupportsColor()
        {
            return Attributes != null &&
                (Attributes.ContainsKey("rgb_color") ||
                 Attributes.ContainsKey("hs_color") ||
                 Attributes.ContainsKey("color_temp") ||
                 Attributes.ContainsKey("color_temp_kelvin"));
        }

        private string MediaPlayerDisplay()
        {
            string title = GetStringAttribute("media_title");
            if (!string.IsNullOrEmpty(title))
            {
                return title;
            }
            switch (State)
            {
                case "playing": return "Tocando";
                case "paused": return "Pausado";
                case "idle": return "Parado";
                case "off": return "Desligado";
                default: return State;
            }
        }

        private string WeatherDisplay()
        {
            string temperature = GetStringAttribute("temperature");
            return string.IsNullOrEmpty(temperature) ? State : $"{State} · {temperature}°";
        }

        private string VacuumDisplay()
        {
            switch (State)
            {
                case "cleaning": return "Limpando";
                case "docked": return "Na base";
                case "returning": return "Voltando à base";
                case "paused": return "Pausado";
                case "idle": return "Parado";
                case "error": return "Erro";
                default: return State;
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

        // Cópia rasa com o nome de exibição sobrescrito — usado quando um
        // card do dashboard renomeia a entidade (ex.: name: "Kitchen In"),
        // sem afetar outras referências à mesma entidade (ex.: a mesma
        // switch também estar em Favoritos com o nome padrão).
        public HaEntityState WithDisplayName(string displayName)
        {
            return new HaEntityState
            {
                EntityId = EntityId,
                State = State,
                Attributes = Attributes,
                LastChanged = LastChanged,
                DisplayNameOverride = displayName,
            };
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
