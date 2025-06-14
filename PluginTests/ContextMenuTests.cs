using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using MusicBeePlugin;
using Xunit;

namespace PluginTests
{
    public class ContextMenuTests
    {
        [Fact]
        public async Task SelectedFilesAreEnqueued()
        {
            // prepare fake connector to log uploads
            var log = new Queue<string>();
            var connector = new FakeConnector(log);
            var queue = new FileUploadQueue(connector);
            var plugin = new Plugin();

            // set private fields via reflection
            typeof(Plugin).GetField("uploadQueue", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(plugin, queue);

            var api = new Plugin.MusicBeeApiInterface();
            api.Library_QueryFilesEx = (string q, out string[] files) => { files = new[] { "a", "b" }; return true; };
            api.NowPlaying_GetFileUrl = () => null!;
            api.MB_SetBackgroundTaskMessage = _ => { };
            api.MB_SendNotification = _ => { };

            typeof(Plugin).GetField("mbApiInterface", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(plugin, api);

            var method = typeof(Plugin).GetMethod("OnSendToIpod", BindingFlags.NonPublic | BindingFlags.Instance)!;

            var t1 = new TaskCompletionSource<bool>();
            var t2 = new TaskCompletionSource<bool>();
            int count = 0;
            queue.UploadCompleted += (s, e) =>
            {
                count++;
                if (count == 1) t1.SetResult(true);
                if (count == 2) t2.SetResult(true);
            };

            method.Invoke(plugin, new object[] { null!, EventArgs.Empty });

            await Task.WhenAll(t1.Task, t2.Task);

            Assert.Equal(new[] { "a", "b" }, log.ToArray());
        }
    }
}
