using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace MusicBeePlugin
{
    /// <summary>
    /// Simple client for the Raspberry Pi iPod sync API.
    /// </summary>
    public class MbPiConnector : IDisposable
    {
        private readonly HttpClient _client;

        /// <summary>
        /// Create a connector with the given base url, e.g. "http://pi:8000".
        /// </summary>
        public MbPiConnector(string baseUrl)
        {
            if (string.IsNullOrEmpty(baseUrl))
                throw new ArgumentException("baseUrl is required", nameof(baseUrl));
            _client = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
        }

        public void Dispose()
        {
            _client.Dispose();
        }

        /// <summary>
        /// Upload a file to the Pi. Category can be "music" or "audiobook".
        /// </summary>
        public async Task<string> UploadFileAsync(string path, string category = null)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                throw new FileNotFoundException("File not found", path);

            using (var content = new MultipartFormDataContent())
            {
                var fileContent = new ByteArrayContent(File.ReadAllBytes(path));
                var fileName = Path.GetFileName(path);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                content.Add(fileContent, "file", fileName);
                var url = string.IsNullOrEmpty(category) ? "upload" : $"upload/{category}";
                var response = await _client.PostAsync(url, content).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Retrieve the server status string.
        /// </summary>
        public async Task<string> GetStatusAsync()
        {
            var response = await _client.GetAsync("status").ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }
    }
}
