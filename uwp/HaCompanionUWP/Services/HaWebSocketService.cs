using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
    // pra buscar config de dashboard (lovelace/config) e itens de todo,
    // recursos que não existem em REST simples (ver ha-companion-w10m.md:
    // o resto do app inteiro continua em HaApiService, puro REST). Conexão
    // nova por chamada (conecta, autentica, manda UM comando, fecha) —
    // sem estado de longa duração nem reconexão nesta leva, suficiente pra
    // um app de "olha e age" que não fica aberto o tempo todo.
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
            return await RunCommandAsync(command);
        }

        public static async Task<JsonArray> GetTodoItemsAsync(string entityId)
        {
            var command = new JsonObject
            {
                ["type"] = JsonValue.CreateStringValue("todo/item/list"),
                ["entity_id"] = JsonValue.CreateStringValue(entityId),
            };
            JsonObject result = await RunCommandAsync(command);
            return result.GetNamedArray("items", new JsonArray());
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

        private static async Task<JsonObject> RunCommandAsync(JsonObject command)
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

            var pending = new Dictionary<int, TaskCompletionSource<JsonObject>>();
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
                    TaskCompletionSource<JsonObject> tcs;
                    if (pending.TryGetValue(id, out tcs))
                    {
                        pending.Remove(id);
                        bool success = json.GetNamedBoolean("success", false);
                        if (success)
                        {
                            tcs.TrySetResult(json.GetNamedObject("result", new JsonObject()));
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
                foreach (TaskCompletionSource<JsonObject> tcs in pending.Values)
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
                var resultTcs = new TaskCompletionSource<JsonObject>();
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
