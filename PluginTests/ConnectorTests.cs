using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MusicBeePlugin;
using Xunit;

namespace PluginTests
{
    class StubHandler : HttpMessageHandler
    {
        public HttpResponseMessage Response { get; set; } = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
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

        class ThrowHandler : HttpMessageHandler
        {
            public Exception ExceptionToThrow { get; set; } = new HttpRequestException();
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromException<HttpResponseMessage>(ExceptionToThrow);
            }
        }

        [Fact]
        public async Task UploadFileAsync_RequestException_RaisesFailedEvent()
        {
            var handler = new ThrowHandler { ExceptionToThrow = new HttpRequestException("fail") };
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
            Assert.IsType<HttpRequestException>(args!.Error);
        }

        [Fact]
        public async Task UploadFileAsync_MissingFile_Throws()
        {
            var connector = new MbPiConnector("http://localhost");
            bool raised = false;
            connector.UploadFailed += (s, e) => raised = true;
            await Assert.ThrowsAsync<FileNotFoundException>(() => connector.UploadFileAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())));
            Assert.False(raised);
        }

        [Fact]
        public async Task ApiKeyHeaderIsAddedWhenProvided()
        {
            var handler = new StubHandler();
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            var connector = new MbPiConnector("http://localhost", "secret");
            typeof(MbPiConnector).GetField("_client", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(connector, client);

            var temp = Path.GetTempFileName();
            File.WriteAllText(temp, "data");
            await connector.UploadFileAsync(temp);

            Assert.NotNull(handler.LastRequest);
            Assert.True(handler.LastRequest!.Headers.Contains("X-Api-Key"));
            Assert.Equal("secret", handler.LastRequest!.Headers.GetValues("X-Api-Key").First());
        }

        [Fact]
        public async Task CategoryAppendedToUrl()
        {
            var handler = new StubHandler();
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            var connector = new MbPiConnector("http://localhost");
            typeof(MbPiConnector).GetField("_client", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(connector, client);

            var temp = Path.GetTempFileName();
            File.WriteAllText(temp, "data");
            await connector.UploadFileAsync(temp, "podcast");

            Assert.NotNull(handler.LastRequest);
            Assert.EndsWith("/upload/podcast", handler.LastRequest!.RequestUri!.AbsoluteUri);
        }
    }
}
