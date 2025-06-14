using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MusicBeePlugin
{
    /// <summary>
    /// Simple sequential upload queue for <see cref="MbPiConnector"/>.
    /// </summary>
    public class FileUploadQueue : IDisposable
    {
        private readonly MbPiConnector _connector;
        private readonly Queue<UploadItem> _queue = new Queue<UploadItem>();
        private bool _processing;

        private struct UploadItem
        {
            public string Path { get; }
            public string? Category { get; }

            public UploadItem(string path, string? category)
            {
                Path = path;
                Category = category;
            }
        }

        public event EventHandler<string> UploadStarted;
        public event EventHandler<UploadCompletedEventArgs> UploadCompleted;
        public event EventHandler<UploadFailedEventArgs> UploadFailed;

        public FileUploadQueue(MbPiConnector connector)
        {
            _connector = connector ?? throw new ArgumentNullException(nameof(connector));
            _connector.UploadCompleted += (s, e) => UploadCompleted?.Invoke(this, e);
            _connector.UploadFailed += (s, e) => UploadFailed?.Invoke(this, e);
        }

        public void Enqueue(string path, string? category = null)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is required", nameof(path));

            lock (_queue)
            {
                _queue.Enqueue(new UploadItem(path, category));
                if (!_processing)
                {
                    _processing = true;
                    _ = ProcessQueue();
                }
            }
        }

        private async Task ProcessQueue()
        {
            while (true)
            {
                UploadItem next;
                lock (_queue)
                {
                    if (_queue.Count == 0)
                    {
                        _processing = false;
                        return;
                    }
                    next = _queue.Dequeue();
                }

                try
                {
                    UploadStarted?.Invoke(this, next.Path);
                    await _connector.UploadFileAsync(next.Path, next.Category).ConfigureAwait(false);
                }
                catch
                {
                    // exceptions are surfaced via UploadFailed event
                }
            }
        }

        public void Dispose()
        {
            _connector.Dispose();
        }
    }
}
