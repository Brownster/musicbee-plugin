using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MusicBeePlugin;
using Xunit;

namespace PluginTests
{
    class FakeConnector : MbPiConnector
    {
        private readonly Queue<string> _log;
        public FakeConnector(Queue<string> log) : base("http://localhost")
        {
            _log = log;
        }
        public override async Task<string> UploadFileAsync(string path, string category = null)
        {
            await Task.Yield();
            _log.Enqueue(path);
            UploadCompleted?.Invoke(this, new UploadCompletedEventArgs(path, "ok"));
            return "ok";
        }
    }

    public class QueueTests
    {
        [Fact]
        public async Task ProcessesFilesSequentially()
        {
            var log = new Queue<string>();
            var connector = new FakeConnector(log);
            var queue = new FileUploadQueue(connector);

            var t1 = new TaskCompletionSource<bool>();
            var t2 = new TaskCompletionSource<bool>();
            int count = 0;
            queue.UploadCompleted += (s, e) =>
            {
                count++;
                if (count == 1) t1.SetResult(true);
                if (count == 2) t2.SetResult(true);
            };

            queue.Enqueue("a");
            queue.Enqueue("b");

            await Task.WhenAll(t1.Task, t2.Task);

            Assert.Equal(new[] { "a", "b" }, log.ToArray());
        }
    }
}
