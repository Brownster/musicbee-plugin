# MusicBee Raspberry Pi Sync Plugin

This repository hosts a MusicBee plugin for sending tracks to a Raspberry Pi running  https://github.com/Brownster/ipod-dock
connected to an iPod Classic via usb. /Brownster/ipod-dock exposes api to upload and managed files the pi manages the syncing to the ipod
The code started as a small template and now includes a working uploader, simple configuration UI and unit tests.

See the [MusicBee API](https://getmusicbee.com/help/api/) for details about plugin development.

## Project layout

- **plugin** – main plugin code
  - `MbPiConnector` implements the HTTP API client
  - `FileUploadQueue` handles sequential uploads
  - `Plugin.cs` wires the plugin into MusicBee and exposes a "Send to iPod" context menu item
- **console** – minimal console app used for quick library tests
- **PluginTests** – xUnit test suite covering the connector, queue and settings manager

## Building

The solution file `plugin.sln` includes the plugin and test projects. Build just the plugin with:

```bash
dotnet build plugin/plugin.csproj
```

A zip containing the compiled DLL is placed in `build/`.

To run the console sample:

```bash
dotnet run --project console/console.csproj
```

## Running tests

Execute the unit tests with:

```bash
dotnet test
```

This requires a .NET SDK with Windows desktop support because the plugin targets `net4.8-windows`.

## Configuration

When loaded in MusicBee the plugin exposes a settings panel where the Raspberry Pi endpoint URL and optional API key can be set.
Uploads are queued and performed sequentially. Multiple selected tracks can be sent in a single action and a status message within MusicBee shows progress.
The plugin maps the MusicBee **Kind** tag to API categories (music, podcast or audiobook) so files are automatically organised on the Pi.
Errors and completion notices are logged and also shown via notifications.

## Roadmap

Development tasks and future ideas are tracked in [`docs/ROADMAP.md`](docs/ROADMAP.md).
Contributions and bug reports are welcome.
