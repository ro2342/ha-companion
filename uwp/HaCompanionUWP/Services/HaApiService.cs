using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using HaCompanionUWP.Models;
using Windows.Data.Json;

namespace HaCompanionUWP.Services
{
    // Erro amigável pra mostrar direto na UI (offline, token inválido,
    // timeout) em vez de deixar a exceção crua de HttpClient estourar.
    public sealed class HaApiException : Exception
    {
        public HaApiException(string message) : base(message)
        {
        }
    }

    // Cliente HTTP fino pra API REST do Home Assistant -- só os endpoints que
    // a doc pede: /api/states e /api/services/<domain>/<service>. Sem
    // WebSocket, sem SDK externo, autenticação via header Bearer com o
    // Long-Lived Access Token guardado no CredentialStore.
    public static class HaApiService
    {
        private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };

        public static async Task<List<HaEntityState>> GetStatesAsync()
        {
            string body = await SendAsync(HttpMethod.Get, "/api/states", null);

            JsonArray array;
            if (!JsonArray.TryParse(body, out array))
            {
                throw new HaApiException("Resposta inesperada do Home Assistant ao listar entidades.");
            }

            var result = new List<HaEntityState>();
            foreach (IJsonValue value in array)
            {
                if (value.ValueType == JsonValueType.Object)
                {
                    result.Add(HaEntityState.FromJson(value.GetObject()));
                }
            }
            return result;
        }

        public static async Task CallServiceAsync(string domain, string service, string entityId)
        {
            var payload = new JsonObject
            {
                ["entity_id"] = JsonValue.CreateStringValue(entityId),
            };
            await SendAsync(HttpMethod.Post, $"/api/services/{domain}/{service}", payload.Stringify());
        }

        private static async Task<string> SendAsync(HttpMethod method, string path, string jsonBody)
        {
            string baseUrl = CredentialStore.GetBaseUrl();
            string token = CredentialStore.GetToken();
            if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(token))
            {
                throw new HaApiException("Configure a URL do Home Assistant e o token em Ajustes primeiro.");
            }

            var request = new HttpRequestMessage(method, baseUrl + path);
            request.Headers.Add("Authorization", "Bearer " + token);
            if (jsonBody != null)
            {
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            }

            HttpResponseMessage response;
            try
            {
                response = await Client.SendAsync(request);
            }
            catch (TaskCanceledException)
            {
                throw new HaApiException("Sem resposta do Home Assistant -- confira se o celular está na mesma rede.");
            }
            catch (Exception ex)
            {
                throw new HaApiException("Não foi possível conectar ao Home Assistant: " + ex.Message);
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                throw new HaApiException("Token inválido ou expirado -- gere um novo em Ajustes.");
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new HaApiException($"Home Assistant respondeu {(int)response.StatusCode}.");
            }

            return await response.Content.ReadAsStringAsync();
        }
    }
}
