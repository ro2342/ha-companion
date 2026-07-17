using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Data.Json;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;

namespace HaCompanionUWP.Services
{
    public sealed class UpdateCheckResult
    {
        public bool HasUpdate { get; set; }
        public string CurrentVersion { get; set; }
        public string LatestVersion { get; set; }
    }

    // Compara a versão instalada (Package.Current.Id.Version) com a
    // publicada em app/version.json (gerado pelo workflow 02 a cada build
    // verde, ver .github/workflows/02-build-appx.yml). Um app sideloaded
    // (sem associação com a Store) não consegue se instalar sozinho — é uma
    // barreira de segurança do próprio Windows. O máximo que dá pra
    // automatizar é isto: baixar o pacote com barra de progresso pra uma
    // pasta escolhida uma vez (o token de acesso persiste via
    // FutureAccessList) e, ao terminar, abrir o instalador nativo do
    // Windows com um toque (LaunchFileAsync) — mesma tela que já aparece ao
    // baixar manualmente pelo navegador. Mesmo esquema do
    // artistsway/uwp/ArtistWayUWP/Services/UpdateCheckService.cs.
    public static class UpdateCheckService
    {
        public const string DownloadPageUrl = "https://ro2342.github.io/ha-companion/";
        private const string VersionJsonUrl = "https://ro2342.github.io/ha-companion/version.json";
        private const string DownloadFileUrl = "https://ro2342.github.io/ha-companion/app.appxbundle";

        private const string DownloadFolderTokenKey = "UpdateDownloadFolder";
        private const string DownloadFileName = "HaCompanionUWP.appxbundle";

        private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        public static string CurrentVersion
        {
            get
            {
                PackageVersion v = Package.Current.Id.Version;
                return $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
            }
        }

        // A primeira requisição HTTPS pra um host novo (DNS + handshake TLS
        // do zero) pode levar mais tempo que uma reaproveitando conexão já
        // aberta — reportado pelo rod: a primeira verificação às vezes
        // falhava e só funcionava no segundo toque. Em vez de deixar o
        // usuário ter que tentar de novo na mão, tenta uma segunda vez
        // sozinho antes de desistir.
        public static async Task<UpdateCheckResult> CheckAsync()
        {
            Exception lastError = null;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    string body = await Client.GetStringAsync(new Uri(VersionJsonUrl));
                    JsonObject json = JsonObject.Parse(body);
                    string latest = json.GetNamedString("version", string.Empty);
                    string current = CurrentVersion;

                    return new UpdateCheckResult
                    {
                        CurrentVersion = current,
                        LatestVersion = latest,
                        HasUpdate = !string.IsNullOrEmpty(latest) && CompareVersions(latest, current) > 0,
                    };
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    if (attempt == 0)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1));
                    }
                }
            }
            throw lastError;
        }

        public static bool HasDownloadFolder()
        {
            return StorageApplicationPermissions.FutureAccessList.ContainsItem(DownloadFolderTokenKey);
        }

        public static async Task<StorageFolder> PickDownloadFolderAsync()
        {
            var picker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.Downloads,
            };
            picker.FileTypeFilter.Add("*");

            StorageFolder folder = await picker.PickSingleFolderAsync();
            if (folder == null)
            {
                return null;
            }

            StorageApplicationPermissions.FutureAccessList.AddOrReplace(DownloadFolderTokenKey, folder);
            return folder;
        }

        public static async Task<StorageFolder> GetOrPickDownloadFolderAsync()
        {
            if (HasDownloadFolder())
            {
                return await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(DownloadFolderTokenKey);
            }
            return await PickDownloadFolderAsync();
        }

        public static async Task<StorageFile> DownloadUpdateAsync(IProgress<double> progress)
        {
            StorageFolder folder = await GetOrPickDownloadFolderAsync();
            if (folder == null)
            {
                return null;
            }

            StorageFile file = await folder.CreateFileAsync(DownloadFileName, CreationCollisionOption.ReplaceExisting);

            using (var client = new HttpClient())
            using (HttpResponseMessage response = await client.GetAsync(new Uri(DownloadFileUrl), HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                long? totalBytes = response.Content.Headers.ContentLength;

                using (Stream networkStream = await response.Content.ReadAsStreamAsync())
                using (Stream fileStream = await file.OpenStreamForWriteAsync())
                {
                    byte[] buffer = new byte[81920];
                    long totalRead = 0;
                    int read;
                    while ((read = await networkStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, read);
                        totalRead += read;
                        if (totalBytes.HasValue && totalBytes.Value > 0)
                        {
                            progress?.Report((double)totalRead / totalBytes.Value * 100.0);
                        }
                    }
                }
            }

            return file;
        }

        private static int CompareVersions(string a, string b)
        {
            int[] partsA = ParseVersion(a);
            int[] partsB = ParseVersion(b);
            for (int i = 0; i < 4; i++)
            {
                if (partsA[i] != partsB[i])
                {
                    return partsA[i].CompareTo(partsB[i]);
                }
            }
            return 0;
        }

        private static int[] ParseVersion(string value)
        {
            int[] result = { 0, 0, 0, 0 };
            string[] segments = (value ?? string.Empty).Split('.');
            for (int i = 0; i < result.Length && i < segments.Length; i++)
            {
                int parsed;
                if (int.TryParse(segments[i], out parsed))
                {
                    result[i] = parsed;
                }
            }
            return result;
        }
    }
}
