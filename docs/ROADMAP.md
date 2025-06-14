# Roadmap for MusicBee Raspberry Pi Plugin

This roadmap outlines the steps needed to transform this template into a plugin capable of sending tracks to a Raspberry Pi running an iPod Classic-like interface. The focus is on maintainable, well‑documented code and incremental development.

## 1. Initial Setup

1. Install the **.NET SDK** (version 4.8 is targeted) and a development environment (Visual Studio or VS Code).
2. Clone this repository and build the template project:
   ```bash
   dotnet build plugin/plugin.csproj
   ```
3. Verify MusicBee can load the resulting plugin DLL from `plugin/bin/Debug/net4.8-windows/`.

## 2. Define Plugin Structure

1. Create a **core class library** inside `plugin` (e.g., `MbPiConnector`) responsible solely for network communication with the Raspberry Pi API.
2. Keep `Plugin.cs` focused on **MusicBee callbacks** and UI interactions—delegate all transfer logic to the core library.
3. Add unit tests for the connector library (consider using xUnit).

## 3. Configuration UI

1. Expose settings for the Raspberry Pi endpoint URL and any authentication tokens in the plugin configuration panel.
2. Store these settings using `mbApiInterface.Setting_GetPersistentStoragePath()` so they persist between sessions.

## 4. Context Menu Integration

1. Use `mbApiInterface.MB_AddMenuItem` (via `MB_AddMenuItemDelegate`) to add a **"Send to iPod"** item in the track and album context menus.
2. When triggered, obtain the file paths of the selected tracks using the API methods in `MusicBeeInterface.cs`.
3. Pass the paths to the connector library for upload.

## 5. File Transfer Logic

1. Implement HTTP POST requests in `MbPiConnector` to send files to the Raspberry Pi API.
2. Add progress reporting and error handling (e.g., via events or callbacks) so the MusicBee UI can display status.
3. Provide a way to queue multiple tracks for transfer to avoid blocking the UI thread.

## 6. Feedback to the User

1. Display a status message or progress bar in MusicBee while transfers are active.
2. Consider using MusicBee's notification API to show success or failure messages.

## 7. Testing and Debugging

1. Use the **console** project to exercise the transfer code outside of MusicBee.
2. Add basic unit tests for `MbPiConnector` to validate API calls.
3. Test within MusicBee to ensure context menu items appear and transfers succeed.

## 8. Packaging and Release

1. Update `plugin/plugin.csproj` metadata (name, description, author).
2. Build a release version with:
   ```bash
   dotnet build plugin/plugin.csproj -c Release
   ```
3. Distribute the resulting `.zip` from the `build` directory or the compiled DLL as needed.

## 9. Future Enhancements

- Resume interrupted transfers.
- Support playlists or entire libraries.
- Add automatic syncing when new tracks are added in MusicBee.

---

This file should be kept up to date as development progresses. Each major step can be tracked as an issue in your project tracker to monitor progress.
