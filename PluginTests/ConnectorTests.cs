using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MusicBeePlugin;
using Xunit;

namespace PluginTests
{
    class StubHandler : HttpMessageHandler
    {
        public HttpResponseMessage Response { get; set; } = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Response);
        }
    }

    public class ConnectorTests
    {
        [Fact]
        public async Task UploadFileAsync_RaisesCompletedEvent()
        {
            var handler = new StubHandler();
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            var connector = new MbPiConnector("http://localhost");

            // replace internal client via reflection for test
            typeof(MbPiConnector).GetField("_client", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(connector, client);

            var temp = Path.GetTempFileName();
            File.WriteAllText(temp, "data");

            UploadCompletedEventArgs? args = null;
            connector.UploadCompleted += (s, e) => args = e;
            await connector.UploadFileAsync(temp);

            Assert.NotNull(args);
            Assert.Equal(temp, args!.FilePath);
            Assert.Equal("ok", args.Response);
        }

        [Fact]
        public async Task UploadFileAsync_RaisesFailedEvent()
        {
            var handler = new StubHandler { Response = new HttpResponseMessage(HttpStatusCode.InternalServerError) };
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            var connector = new MbPiConnector("http://localhost");
            typeof(MbPiConnector).GetField("_client", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(connector, client);
            var temp = Path.GetTempFileName();
            File.WriteAllText(temp, "data");

            UploadFailedEventArgs? args = null;
            connector.UploadFailed += (s, e) => args = e;
            await Assert.ThrowsAsync<HttpRequestException>(() => connector.UploadFileAsync(temp));

            Assert.NotNull(args);
            Assert.Equal(temp, args!.FilePath);
        }
    }
}
