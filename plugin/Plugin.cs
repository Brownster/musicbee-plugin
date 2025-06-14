using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace YourNamespace
{

    public sealed class LibraryEntryPoint
    {
        private static string DLLDirectory = "";
        private static List<string> DLLDirectoryExpected = new List<string>();
        private static bool isInitialized = false;
        private static readonly object initializationLock = new object();

        public static string libraryDir { get; private set; } = "";

        // This static constructor will be called when the DLL is loaded
        static LibraryEntryPoint()
        {
            // Use a lock to ensure thread safety
            lock (initializationLock)
            {
                if (!isInitialized)
                {

#if DEBUG
                    // Force exceptions to be in English
                    System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Threading.Thread.CurrentThread.CurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;
#endif


                    Assembly thisAssem = typeof(LibraryEntryPoint).Assembly;
#if DEBUG

                    Console.WriteLine($"Loaded {thisAssem.GetName().Name}");
#endif

                    // Setup DLL dependencies
                    libraryDir = Path.GetDirectoryName(thisAssem.Location);

                    string libDepFolder = Path.Combine(libraryDir, thisAssem.GetCustomAttribute<AssemblyTitleAttribute>().Title);

                    SetupDllDependencies(libDepFolder);

                }
            }

            isInitialized = true;
        }

        static public void SetupDllDependencies(string dependencyDirPath)
        {
            DLLDirectory = dependencyDirPath;

            if (Directory.Exists(DLLDirectory))
            {
                DLLDirectoryExpected = Directory.GetFiles(DLLDirectory, "*.dll").Select(f => Path.GetFileName(f)).ToList();
            }

            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += ResolveAssembly;
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
        }

        static private Assembly ResolveAssembly(Object sender, ResolveEventArgs e)
        {
            Assembly res = null;
            string dllName = $"{e.Name.Split(',')[0]}.dll";
            if (DLLDirectoryExpected.Count > 0 && !DLLDirectoryExpected.Contains(dllName))
            {
                return res;
            }

            string path = Path.Combine(DLLDirectory, dllName);
            try
            {
                res = System.Reflection.Assembly.LoadFile(path);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load {path}");
                Console.WriteLine(ex.ToString());
            }
            return res;
        }
    }

}

namespace MusicBeePlugin
{

    using System.Drawing;
    using System.Windows.Forms;

    using YourNamespace;

    public partial class Plugin
    {
        // Required so entrypoint can be called
        static private LibraryEntryPoint entryPoint = new LibraryEntryPoint();

        private MusicBeeApiInterface mbApiInterface;
        private PluginInfo about = new PluginInfo();
        private MbPiConnector connector;
        private FileUploadQueue uploadQueue;
        private PluginSettingsManager settingsManager;
        private TextBox endpointTextBox;
        private TextBox apiKeyTextBox;
        private int uploadTotal;
        private int uploadCompleted;

        /// <summary>
        /// Create the connector using the currently loaded settings.
        /// </summary>
        private void CreateConnector()
        {
            uploadQueue?.Dispose();
            connector?.Dispose();
            connector = new MbPiConnector(settingsManager.Settings.EndpointUrl,
                settingsManager.Settings.ApiKey);
            uploadQueue = new FileUploadQueue(connector);
            uploadQueue.UploadStarted += (s, path) =>
                mbApiInterface.MB_SetBackgroundTaskMessage($"Uploading {Path.GetFileName(path)} ({uploadCompleted + 1}/{uploadTotal})");
            uploadQueue.UploadCompleted += (s, e) =>
            {
                uploadCompleted++;
                mbApiInterface.MB_SetBackgroundTaskMessage($"Uploaded {uploadCompleted}/{uploadTotal}: {Path.GetFileName(e.FilePath)}");
                mbApiInterface.MB_SendNotification(CallbackType.FilesRetrievedChanged);
                if (uploadCompleted == uploadTotal)
                {
                    mbApiInterface.MB_SetBackgroundTaskMessage("Uploads complete");
                    uploadTotal = uploadCompleted = 0;
                }
                mbApiInterface.MB_Trace($"Uploaded {e.FilePath}: {e.Response}");
            };
            uploadQueue.UploadFailed += (s, e) =>
            {
                uploadCompleted++;
                mbApiInterface.MB_SetBackgroundTaskMessage($"Failed {uploadCompleted}/{uploadTotal}: {Path.GetFileName(e.FilePath)}");
                mbApiInterface.MB_SendNotification(CallbackType.FilesRetrievedFail);
                if (uploadCompleted == uploadTotal)
                {
                    mbApiInterface.MB_SetBackgroundTaskMessage("Uploads complete");
                    uploadTotal = uploadCompleted = 0;
                }
                mbApiInterface.MB_Trace($"Upload failed for {e.FilePath}: {e.Error.Message}");
            };
        }

        public PluginInfo Initialise(IntPtr apiInterfacePtr)
        {
            Assembly thisAssem = typeof(Plugin).Assembly;

            // Change these attributes in the .csproj
            string name = thisAssem.GetCustomAttribute<AssemblyTitleAttribute>().Title;
            Version ver = thisAssem.GetName().Version;
            string author = thisAssem.GetCustomAttribute<AssemblyCompanyAttribute>().Company;
            string description = thisAssem.GetCustomAttribute<AssemblyDescriptionAttribute>().Description;


            mbApiInterface = new MusicBeeApiInterface();
            mbApiInterface.Initialise(apiInterfacePtr);
            settingsManager = new PluginSettingsManager(mbApiInterface.Setting_GetPersistentStoragePath());
            CreateConnector();
            // add menu item under the playing track context menu
            mbApiInterface.MB_AddMenuItem("mnuContext/Send to iPod", null, OnSendToIpod);
            about.PluginInfoVersion = PluginInfoVersion;
            about.Name = name;
            about.Description = description;
            about.Author = author;
            about.TargetApplication = "";   //  the name of a Plugin Storage device or panel header for a dockable panel
            about.Type = PluginType.General;
            about.VersionMajor = (short)ver.Major;  // your plugin version
            about.VersionMinor = (short)ver.Minor;
            about.Revision = (short)ver.Revision;
            about.MinInterfaceVersion = MinInterfaceVersion;
            about.MinApiRevision = MinApiRevision;
            about.ReceiveNotifications = (ReceiveNotificationFlags.PlayerEvents | ReceiveNotificationFlags.TagEvents);
            about.ConfigurationPanelHeight = 0;   // height in pixels that musicbee should reserve in a panel for config settings. When set, a handle to an empty panel will be passed to the Configure function

            return about;
        }

        public bool Configure(IntPtr panelHandle)
        {
            if (settingsManager == null)
                settingsManager = new PluginSettingsManager(mbApiInterface.Setting_GetPersistentStoragePath());

            if (panelHandle != IntPtr.Zero)
            {
                Panel configPanel = (Panel)Panel.FromHandle(panelHandle);
                Label prompt = new Label { AutoSize = true, Location = new Point(0, 0), Text = "Endpoint URL:" };
                endpointTextBox = new TextBox { Bounds = new Rectangle(90, 0, 200, 20), Text = settingsManager.Settings.EndpointUrl };
                Label keyLabel = new Label { AutoSize = true, Location = new Point(0, 30), Text = "API Key:" };
                apiKeyTextBox = new TextBox { Bounds = new Rectangle(90, 30, 200, 20), Text = settingsManager.Settings.ApiKey };
                configPanel.Controls.AddRange(new Control[] { prompt, endpointTextBox, keyLabel, apiKeyTextBox });
            }
            else
            {
                using (Form dlg = new Form())
                {
                    dlg.Text = "Plugin Settings";
                    dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                    dlg.StartPosition = FormStartPosition.CenterParent;
                    dlg.Width = 350;
                    dlg.Height = 160;

                    Label prompt = new Label { AutoSize = true, Location = new Point(10, 10), Text = "Endpoint URL:" };
                    endpointTextBox = new TextBox { Location = new Point(110, 8), Width = 200, Text = settingsManager.Settings.EndpointUrl };
                    Label keyLabel = new Label { AutoSize = true, Location = new Point(10, 40), Text = "API Key:" };
                    apiKeyTextBox = new TextBox { Location = new Point(110, 38), Width = 200, Text = settingsManager.Settings.ApiKey };
                    Button ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(110, 70) };
                    Button cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(200, 70) };
                    dlg.Controls.AddRange(new Control[] { prompt, endpointTextBox, keyLabel, apiKeyTextBox, ok, cancel });
                    dlg.AcceptButton = ok;
                    dlg.CancelButton = cancel;

                    if (dlg.ShowDialog() == DialogResult.OK)
                    {
                        SaveSettings();
                    }
                }
            }
            return false;
        }

        // called by MusicBee when the user clicks Apply or Save in the MusicBee Preferences screen.
        // its up to you to figure out whether anything has changed and needs updating
        public void SaveSettings()
        {
            if (settingsManager == null)
                settingsManager = new PluginSettingsManager(mbApiInterface.Setting_GetPersistentStoragePath());
            if (endpointTextBox != null)
                settingsManager.Settings.EndpointUrl = endpointTextBox.Text;
            if (apiKeyTextBox != null)
                settingsManager.Settings.ApiKey = apiKeyTextBox.Text;

            settingsManager.Save();

            CreateConnector();
        }

        // MusicBee is closing the plugin (plugin is being disabled by user or MusicBee is shutting down)
        public void Close(PluginCloseReason reason)
        {
        }

        // uninstall this plugin - clean up any persisted files
        public void Uninstall()
        {
        }

        // receive event notifications from MusicBee
        // you need to set about.ReceiveNotificationFlags = PlayerEvents to receive all notifications, and not just the startup event
        public void ReceiveNotification(string sourceFileUrl, NotificationType type)
        {
            // perform some action depending on the notification type
            switch (type)
            {
                case NotificationType.PluginStartup:
                    // perform startup initialisation
                    switch (mbApiInterface.Player_GetPlayState())
                    {
                        case PlayState.Playing:
                        case PlayState.Paused:
                            // ...
                            break;
                    }
                    break;
                case NotificationType.TrackChanged:
                    string artist = mbApiInterface.NowPlaying_GetFileTag(MetaDataType.Artist);
                    // ...
                    break;
            }
        }

        // return an array of lyric or artwork provider names this plugin supports
        // the providers will be iterated through one by one and passed to the RetrieveLyrics/ RetrieveArtwork function in order set by the user in the MusicBee Tags(2) preferences screen until a match is found
        //public string[] GetProviders()
        //{
        //    return null;
        //}

        // return lyrics for the requested artist/title from the requested provider
        // only required if PluginType = LyricsRetrieval
        // return null if no lyrics are found
        //public string RetrieveLyrics(string sourceFileUrl, string artist, string trackTitle, string album, bool synchronisedPreferred, string provider)
        //{
        //    return null;
        //}

        // return Base64 string representation of the artwork binary data from the requested provider
        // only required if PluginType = ArtworkRetrieval
        // return null if no artwork is found
        //public string RetrieveArtwork(string sourceFileUrl, string albumArtist, string album, string provider)
        //{
        //    //Return Convert.ToBase64String(artworkBinaryData)
        //    return null;
        //}

        //  presence of this function indicates to MusicBee that this plugin has a dockable panel. MusicBee will create the control and pass it as the panel parameter
        //  you can add your own controls to the panel if needed
        //  you can control the scrollable area of the panel using the mbApiInterface.MB_SetPanelScrollableArea function
        //  to set a MusicBee header for the panel, set about.TargetApplication in the Initialise function above to the panel header text
        //public int OnDockablePanelCreated(Control panel)
        //{
        //  //    return the height of the panel and perform any initialisation here
        //  //    MusicBee will call panel.Dispose() when the user removes this panel from the layout configuration
        //  //    < 0 indicates to MusicBee this control is resizable and should be sized to fill the panel it is docked to in MusicBee
        //  //    = 0 indicates to MusicBee this control resizeable
        //  //    > 0 indicates to MusicBee the fixed height for the control.Note it is recommended you scale the height for high DPI screens(create a graphics object and get the DpiY value)
        //    float dpiScaling = 0;
        //    using (Graphics g = panel.CreateGraphics())
        //    {
        //        dpiScaling = g.DpiY / 96f;
        //    }
        //    panel.Paint += panel_Paint;
        //    return Convert.ToInt32(100 * dpiScaling);
        //}

        // presence of this function indicates to MusicBee that the dockable panel created above will show menu items when the panel header is clicked
        // return the list of ToolStripMenuItems that will be displayed
        //public List<ToolStripItem> GetHeaderMenuItems()
        //{
        //    List<ToolStripItem> list = new List<ToolStripItem>();
        //    list.Add(new ToolStripMenuItem("A menu item"));
        //    return list;
        //}

        //private void panel_Paint(object sender, PaintEventArgs e)
        //{
        //    e.Graphics.Clear(Color.Red);
        //    TextRenderer.DrawText(e.Graphics, "hello", SystemFonts.CaptionFont, new Point(10, 10), Color.Blue);
        //}

        private void OnSendToIpod(object sender, EventArgs e)
        {
            try
            {
                string[] files;
                if (mbApiInterface.Library_QueryFilesEx("domain=SelectedFiles", out files) && files.Length > 0)
                {
                    foreach (var f in files)
                    {
                        string kind = mbApiInterface.Library_GetFileProperty(f, FilePropertyType.Kind);
                        string category = null;
                        if (!string.IsNullOrEmpty(kind))
                        {
                            if (kind.Equals("Audiobook", StringComparison.OrdinalIgnoreCase))
                                category = "audiobook";
                            else if (kind.Equals("Podcast", StringComparison.OrdinalIgnoreCase))
                                category = "podcast";
                            else
                                category = "music";
                        }
                        uploadQueue.Enqueue(f, category);
                    }
                    uploadTotal += files.Length;
                    mbApiInterface.MB_SetBackgroundTaskMessage($"Queued {uploadTotal - uploadCompleted} file(s) for upload");
                }
                else
                {
                    string file = mbApiInterface.NowPlaying_GetFileUrl();
                    if (!string.IsNullOrEmpty(file))
                    {
                        string kind = mbApiInterface.NowPlaying_GetFileProperty(FilePropertyType.Kind);
                        string category = null;
                        if (!string.IsNullOrEmpty(kind))
                        {
                            if (kind.Equals("Audiobook", StringComparison.OrdinalIgnoreCase))
                                category = "audiobook";
                            else if (kind.Equals("Podcast", StringComparison.OrdinalIgnoreCase))
                                category = "podcast";
                            else
                                category = "music";
                        }
                        uploadQueue.Enqueue(file, category);
                        uploadTotal++;
                        mbApiInterface.MB_SetBackgroundTaskMessage($"Queued {uploadTotal - uploadCompleted} file(s) for upload");
                    }
                }
            }
            catch (Exception ex)
            {
                mbApiInterface.MB_Trace("Send to iPod failed: " + ex.Message);
            }
        }

     }
}

