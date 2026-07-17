using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Security.Credentials;
using Windows.Storage;

namespace HaCompanionUWP.Services
{
    // Guarda a URL base e a lista de favoritos em ApplicationData.LocalSettings
    // (não sensível) e o Long-Lived Access Token no PasswordVault (sensível) —
    // nunca em texto puro em disco fora do vault.
    public static class CredentialStore
    {
        private const string VaultResource = "HaCompanion";
        private const string VaultUserName = "token";
        private const string BaseUrlKey = "ha_base_url";
        private const string FavoritesKey = "ha_favorites";
        private const string DashboardUrlPathKey = "ha_dashboard_url_path";
        private const string DashboardTitleKey = "ha_dashboard_title";

        public static string GetBaseUrl()
        {
            return ApplicationData.Current.LocalSettings.Values[BaseUrlKey] as string;
        }

        public static void SaveBaseUrl(string baseUrl)
        {
            ApplicationData.Current.LocalSettings.Values[BaseUrlKey] = baseUrl?.TrimEnd('/');
        }

        public static string GetToken()
        {
            var vault = new PasswordVault();
            try
            {
                PasswordCredential credential = vault.Retrieve(VaultResource, VaultUserName);
                credential.RetrievePassword();
                return credential.Password;
            }
            catch (Exception)
            {
                // Retrieve lança quando não existe nenhuma credencial salva ainda
                // (primeiro uso) — tratado como "sem token", não como erro.
                return null;
            }
        }

        public static void SaveToken(string token)
        {
            ClearToken();
            if (!string.IsNullOrEmpty(token))
            {
                var vault = new PasswordVault();
                vault.Add(new PasswordCredential(VaultResource, VaultUserName, token));
            }
        }

        public static void ClearToken()
        {
            var vault = new PasswordVault();
            try
            {
                foreach (PasswordCredential credential in vault.FindAllByResource(VaultResource))
                {
                    vault.Remove(credential);
                }
            }
            catch (Exception)
            {
                // FindAllByResource lança quando não há nada salvo — nada a limpar.
            }
        }

        public static bool HasConnection()
        {
            return !string.IsNullOrEmpty(GetBaseUrl()) && !string.IsNullOrEmpty(GetToken());
        }

        public static void ClearAll()
        {
            ClearToken();
            ApplicationData.Current.LocalSettings.Values.Remove(BaseUrlKey);
            ApplicationData.Current.LocalSettings.Values.Remove(FavoritesKey);
            ApplicationData.Current.LocalSettings.Values.Remove(DashboardUrlPathKey);
            ApplicationData.Current.LocalSettings.Values.Remove(DashboardTitleKey);
        }

        // url_path do dashboard Lovelace a mostrar na aba Dashboard (vazio =
        // dashboard padrão da conta). Título é só cache: preenchido depois
        // do primeiro carregamento bem-sucedido, pra próxima vez já mostrar
        // o nome real sem precisar esperar a rede.
        public static string GetDashboardUrlPath()
        {
            return ApplicationData.Current.LocalSettings.Values[DashboardUrlPathKey] as string;
        }

        public static void SaveDashboardUrlPath(string urlPath)
        {
            ApplicationData.Current.LocalSettings.Values[DashboardUrlPathKey] = urlPath?.Trim();
        }

        public static string GetDashboardTitle()
        {
            return ApplicationData.Current.LocalSettings.Values[DashboardTitleKey] as string;
        }

        public static void SaveDashboardTitle(string title)
        {
            ApplicationData.Current.LocalSettings.Values[DashboardTitleKey] = title;
        }

        public static IReadOnlyList<string> GetFavorites()
        {
            string raw = ApplicationData.Current.LocalSettings.Values[FavoritesKey] as string;
            if (string.IsNullOrEmpty(raw))
            {
                return new List<string>();
            }
            return raw.Split('|').Where(id => !string.IsNullOrEmpty(id)).ToList();
        }

        public static void SaveFavorites(IEnumerable<string> entityIds)
        {
            ApplicationData.Current.LocalSettings.Values[FavoritesKey] =
                string.Join("|", entityIds ?? Enumerable.Empty<string>());
        }
    }
}
