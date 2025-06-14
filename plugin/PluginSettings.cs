using System.Runtime.Serialization;

namespace MusicBeePlugin
{
    /// <summary>
    /// Represents user configurable plugin settings.
    /// Currently the endpoint URL and API key are stored but
    /// more fields may be added in future versions.
    /// </summary>
    [DataContract]
    public class PluginSettings
    {
        /// <summary>
        /// URL of the Raspberry Pi endpoint used by <see cref="MbPiConnector"/>.
        /// </summary>
        [DataMember(Name = "endpointUrl")]
        public string EndpointUrl { get; set; } = "http://localhost:8000";

        /// <summary>
        /// Optional API key sent with each request.
        /// </summary>
        [DataMember(Name = "apiKey")]
        public string ApiKey { get; set; } = "";
    }
}
