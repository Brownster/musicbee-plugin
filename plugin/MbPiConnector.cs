using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace MusicBeePlugin
{
    /// <summary>
    /// Simple client for the Raspberry Pi iPod sync API.
    /// </summary>
    /// <summary>
    /// Event args for a successful upload.
    /// </summary>
    public class UploadCompletedEventArgs : EventArgs
    {
        public string FilePath { get; }
        public string Response { get; }

        public UploadCompletedEventArgs(string filePath, string response)
        {
            FilePath = filePath;
            Response = response;
        }
    }

    /// <summary>
    /// Event args for a failed upload.
    /// </summary>
    public class UploadFailedEventArgs : EventArgs
    {
        public string FilePath { get; }
        public Exception Error { get; }

        public UploadFailedEventArgs(string filePath, Exception error)
        {
            FilePath = filePath;
            Error = error;
        }
    }

    public class MbPiConnector : IDisposable
    {
        private readonly HttpClient _client;
        private readonly string _apiKey;

        public event EventHandler<UploadCompletedEventArgs> UploadCompleted;
        public event EventHandler<UploadFailedEventArgs> UploadFailed;

        /// <summary>
        /// Create a connector with the given base url, e.g. "http://pi:8000".
        /// An optional API key will be sent with each request.
        /// </summary>
        public MbPiConnector(string baseUrl, string apiKey = null)
        {
            if (string.IsNullOrEmpty(baseUrl))
                throw new ArgumentException("baseUrl is required", nameof(baseUrl));
            _client = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
            _apiKey = apiKey ?? string.Empty;
            if (!string.IsNullOrEmpty(_apiKey))
            {
                _client.DefaultRequestHeaders.Add("X-Api-Key", _apiKey);
            }
        }

        public void Dispose()
        {
            _client.Dispose();
        }

        /// <summary>
        /// Upload a file to the Pi. Category can be "music" or "audiobook".
        /// </summary>
        public virtual async Task<string> UploadFileAsync(string path, string category = null)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                throw new FileNotFoundException("File not found", path);

            try
            {
                using (var content = new MultipartFormDataContent())
                {
                    var fileContent = new ByteArrayContent(File.ReadAllBytes(path));
                    var fileName = Path.GetFileName(path);
                    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                    content.Add(fileContent, "file", fileName);
                    var url = string.IsNullOrEmpty(category) ? "upload" : $"upload/{category}";
                    var response = await _client.PostAsync(url, content).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    var res = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    UploadCompleted?.Invoke(this, new UploadCompletedEventArgs(path, res));
                    return res;
                }
            }
            catch (Exception ex)
            {
                UploadFailed?.Invoke(this, new UploadFailedEventArgs(path, ex));
                throw;
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
