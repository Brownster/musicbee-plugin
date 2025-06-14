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
        private readonly Queue<string> _queue = new Queue<string>();
        private bool _processing;

        public event EventHandler<UploadCompletedEventArgs> UploadCompleted;
        public event EventHandler<UploadFailedEventArgs> UploadFailed;

        public FileUploadQueue(MbPiConnector connector)
        {
            _connector = connector ?? throw new ArgumentNullException(nameof(connector));
            _connector.UploadCompleted += (s, e) => UploadCompleted?.Invoke(this, e);
            _connector.UploadFailed += (s, e) => UploadFailed?.Invoke(this, e);
        }

        public void Enqueue(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is required", nameof(path));

            lock (_queue)
            {
                _queue.Enqueue(path);
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
                string next;
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
                    await _connector.UploadFileAsync(next).ConfigureAwait(false);
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
