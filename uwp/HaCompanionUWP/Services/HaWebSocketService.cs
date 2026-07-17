using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HaCompanionUWP.Models;
using Windows.Data.Json;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace HaCompanionUWP.Services
{
    // Erro amigável equivalente ao HaApiException, mas pro canal WebSocket
    // (autenticação, comando recusado, conexão caindo no meio do caminho).
    public sealed class HaWebSocketException : Exception
    {
        public HaWebSocketException(string message) : base(message)
        {
        }
    }

    // Cliente fino do WebSocket do Home Assistant (/api/websocket) — só
    // pra buscar config/lista de dashboard e itens de todo, recursos que
    // não existem em REST simples (ver ha-companion-w10m.md: o resto do
    // app inteiro continua em HaApiService, puro REST). Conexão nova por
    // chamada (conecta, autentica, manda UM comando, fecha) — sem estado de
    // longa duração nem reconexão nesta leva, suficiente pra um app de
    // "olha e age" que não fica aberto o tempo todo.
    public static class HaWebSocketService
    {
        public static async Task<JsonObject> GetDashboardConfigAsync(string urlPath)
        {
            var command = new JsonObject
            {
                ["type"] = JsonValue.CreateStringValue("lovelace/config"),
            };
            if (!string.IsNullOrEmpty(urlPath))
            {
                command["url_path"] = JsonValue.CreateStringValue(urlPath);
            }
            IJsonValue result = await RunCommandAsync(command);
            return result.GetObject();
        }

        // Lista os dashboards cadastrados na conta (mesmo dado que
        // aparece na barra lateral do HA) — usado pro ComboBox de Ajustes
        // em vez do usuário ter que descobrir/digitar o url_path na mão.
        public static async Task<List<DashboardInfo>> GetDashboardsAsync()
        {
            var command = new JsonObject
            {
                ["type"] = JsonValue.CreateStringValue("lovelace/dashboards/list"),
            };
            IJsonValue result = await RunCommandAsync(command);

            var dashboards = new List<DashboardInfo>
            {
                new DashboardInfo { Title = "Overview (padrão)", UrlPath = string.Empty },
            };

            foreach (IJsonValue itemValue in result.GetArray())
            {
                if (itemValue.ValueType != JsonValueType.Object)
                {
                    continue;
                }
                JsonObject item = itemValue.GetObject();
                string urlPath = item.GetNamedString("url_path", string.Empty);
                string title = item.GetNamedString("title", urlPath);
                if (!string.IsNullOrEmpty(urlPath))
                {
                    dashboards.Add(new DashboardInfo { Title = title, UrlPath = urlPath });
                }
            }

            return dashboards;
        }

        public static async Task<JsonArray> GetTodoItemsAsync(string entityId)
        {
            var command = new JsonObject
            {
                ["type"] = JsonValue.CreateStringValue("todo/item/list"),
                ["entity_id"] = JsonValue.CreateStringValue(entityId),
            };
            IJsonValue result = await RunCommandAsync(command);
            return result.GetObject().GetNamedArray("items", new JsonArray());
        }

        public static async Task MarkTodoItemAsync(string entityId, string itemId, bool completed)
        {
            var command = new JsonObject
            {
                ["type"] = JsonValue.CreateStringValue("todo/item/update"),
                ["entity_id"] = JsonValue.CreateStringValue(entityId),
                ["item"] = JsonValue.CreateStringValue(itemId),
                ["status"] = JsonValue.CreateStringValue(completed ? "completed" : "needs_action"),
            };
            await RunCommandAsync(command);
        }

        // Devolve o "result" cru (IJsonValue) — alguns comandos respondem
        // com objeto (lovelace/config), outros com array na raiz
        // (lovelace/dashboards/list); cada chamador converte pro tipo
        // certo, em vez do método genérico assumir só um dos dois.
        private static async Task<IJsonValue> RunCommandAsync(JsonObject command)
        {
            string baseUrl = CredentialStore.GetBaseUrl();
            string token = CredentialStore.GetToken();
            if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(token))
            {
                throw new HaWebSocketException("Configure a URL do Home Assistant e o token em Ajustes primeiro.");
            }

            Uri wsUri = ToWebSocketUri(baseUrl);
            var socket = new MessageWebSocket();
            socket.Control.MessageType = SocketMessageType.Utf8;

            var pending = new Dictionary<int, TaskCompletionSource<IJsonValue>>();
            var authTcs = new TaskCompletionSource<bool>();
            DataWriter writer = null;

            socket.MessageReceived += (sender, args) =>
            {
                string message;
                using (DataReader reader = args.GetDataReader())
                {
                    reader.UnicodeEncoding = UnicodeEncoding.Utf8;
                    message = reader.ReadString(reader.UnconsumedBufferLength);
                }

                JsonObject json;
                if (!JsonObject.TryParse(message, out json))
                {
                    return;
                }

                string type = json.GetNamedString("type", string.Empty);
                if (type == "auth_ok")
                {
                    authTcs.TrySetResult(true);
                }
                else if (type == "auth_invalid")
                {
                    authTcs.TrySetException(new HaWebSocketException("Token inválido ou expirado — gere um novo em Ajustes."));
                }
                else if (type == "result")
                {
                    int id = (int)json.GetNamedNumber("id", 0);
                    TaskCompletionSource<IJsonValue> tcs;
                    if (pending.TryGetValue(id, out tcs))
                    {
                        pending.Remove(id);
                        bool success = json.GetNamedBoolean("success", false);
                        if (success)
                        {
                            tcs.TrySetResult(json.GetNamedValue("result"));
                        }
                        else
                        {
                            JsonObject error = json.GetNamedObject("error", new JsonObject());
                            tcs.TrySetException(new HaWebSocketException(error.GetNamedString("message", "Erro desconhecido do Home Assistant.")));
                        }
                    }
                }
            };

            socket.Closed += (sender, args) =>
            {
                authTcs.TrySetException(new HaWebSocketException("Conexão fechada antes de autenticar."));
                foreach (TaskCompletionSource<IJsonValue> tcs in pending.Values)
                {
                    tcs.TrySetException(new HaWebSocketException("Conexão fechada antes de receber resposta."));
                }
            };

            try
            {
                await socket.ConnectAsync(wsUri);
                writer = new DataWriter(socket.OutputStream);

                await SendAsync(writer, new JsonObject
                {
                    ["type"] = JsonValue.CreateStringValue("auth"),
                    ["access_token"] = JsonValue.CreateStringValue(token),
                });
                await authTcs.Task;

                const int commandId = 1;
                command["id"] = JsonValue.CreateNumberValue(commandId);
                var resultTcs = new TaskCompletionSource<IJsonValue>();
                pending[commandId] = resultTcs;
                await SendAsync(writer, command);

                return await resultTcs.Task;
            }
            catch (HaWebSocketException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new HaWebSocketException("Não foi possível conectar ao Home Assistant: " + ex.Message);
            }
            finally
            {
                if (writer != null)
                {
                    writer.DetachStream();
                    writer.Dispose();
                }
                socket.Dispose();
            }
        }

        private static async Task SendAsync(DataWriter writer, JsonObject payload)
        {
            writer.WriteString(payload.Stringify());
            await writer.StoreAsync();
        }

        private static Uri ToWebSocketUri(string baseUrl)
        {
            string wsBase = baseUrl.Replace("https://", "wss://").Replace("http://", "ws://");
            return new Uri(wsBase.TrimEnd('/') + "/api/websocket");
        }
    }
}
