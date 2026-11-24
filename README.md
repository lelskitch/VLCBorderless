# VLC Borderless

Removes the title bar from VLC and the window border on Windows, so that with **Minimal Interface** and **Always on Top**, the video floats on your desktop with nothing surrounding it.

- Activate by right clicking an active video, checking **View > Borderless Window** to remove the borders, and tick it again to bring it back. 

- You can drag the video around in this mode, by doing a simple click and drag of the video window; no title bar required to move it. 

- Resizing is also possible, as the edges stay hot, allowing for the resize cursors to function regularly. 

- Once resized, the video is then **locked to the video aspect ratio** maintaining that it never (re)creates black bars, a.k.a. padding. 

- You must pre set *Minimal Interface* for this to work. *Always on Top* is optional but recommended in most cases.

- As with everything I do, there's no network access in my code, and nothing running unless you tick the checkbox to activate it. 

> Tested on **VLC 3.0.23 with Windows 11**. See [Compatibility](#compatibility) for what has and has not yet been tested.

## Quick Start

1. Download the vlc-borderless zip from the [Releases] page and extract it anywhere you like. 
2. Double click **`install.cmd`**.
3. Restart VLC if it was already open. 
4. Voila, tool working.

## Known bugs.. will fix
- Videos that loop will default back to full size upon restarting.
- New videos in playlist at different aspect ratios or resolutions will also default back to full size.
**Both of these are self-fixable by going to Tools -> preferences -> resize interface to video size, then restarting VLC; I may look at circumventing it purely for borderless in future.**

## How it works

VLC's Lua extension API cannot change window styles, so this project is two small parts. 
[`extension/borderless.lua`](extension/borderless.lua) is the VLC extension; it adds the menu entry and only starts and stops the helper. 
[`src/VlcBorderless.cs`](src/VlcBorderless.cs) builds `VlcBorderless.exe`, a single file C# tray program, about 40kb or so, built with the C# compiler that ships with windows, so no SDK needed. It removes the window frame while keeping the video on the same exact pixels, allows you to move the window with dragging, and draws and invisible resize ring around the edges, putting everything back when it's stopped.

## How it interacts with your system

One of my biggest peeves is that software these days rarely tells you what it's doing, and you have to dig through the code to actually figure it out. Agentic LLMs have made this a bit easier, but it's still not 100% reliable as checking it yourself. Ultimately, it's your choice whether or not to trust me or my work, but everything is open-source so you can check. 

- **Windows it touches:** VLC's main window (class  `Qt5QWindowIcon`, titled "... - VLC media player") of the VLC it was started for. It changes that window's styling (caption/border), size, and position, and restores them when stopped. 
- **Mouse hook:** while running, it installs a global low-level mouse hook  (`WH_MOUSE_LL`) to watch  **only** for a click-and-drag on the video, so it can be moved. The hook never swallows, changes, injects, or otherwise interacts with any mouse or keyboard input - every event is passed straight on. Part of this, is the curse that some antivirus products dislike any program that installs a mouse hook. To get around this, see [INSTALL.md](INSTALL.md#antivirus-and-smartscreen).
- **Network:**  None. This has no networking code, and never will. I am very pro-offline software. 
- **Files it writes:** `%LOCALAPPDATA%\VlcBorderless\` (`last-placement.txt` - where the window last was; `helper.log` only if you pass `--log`; `vlc-qt-interface.ini.bak`). It also edits **one flag** in VLC's `%APPDATA%\vlc\vlc-qt-interface.ini`: if the VLC instance it handled is closed while fullscreen, the saved "fullscreen" state is cleared so the next VLC does not start fullscreen with no title bar (making there be nothing to grab). A backup of the original file is kept first, and `--keep-saved-fullscreen` turns this off.
- **Registry, services, scheduled tasks, startup entries:** None at all. No need for it.
- **Uninstall:** `uninstall.cmd` removes the two installed files; nothing else is left behind except the small `%LOCALAPPDATA%\VlcBorderless` folder, which you can optionally delete. 

## Options

The extension starts the helper without flags. To choose a mode, put flags in `%LOCALAPPDATA%\VlcBorderless\options.txt` (spaces or lines; `#` starts a comment).

| Option | Meaning |
|---|---|
| `--linger` | handle **every** VLC that opens, for as long as the helper runs (no extension needed; put a shortcut in `shell:startup` to have it always on). Default: only the VLC it was started for, then exit |
| `--no-autofit` | do not fit the window to the picture when black bars show (resizing stays locked to the picture's shape) |
| `--no-resize` | frameless, no resize grips at all (the window can only be moved) |
| `--native-resize` | keep Windows' own resize border: free-form resizing, but a thin strip above the video |
| `--keep-saved-fullscreen` | do not clear VLC's saved "fullscreen" state when the handled VLC closes |
| `--fix-vlc-start` | clear a saved "fullscreen" in VLC's window state now (VLC must be closed), if VLC keeps starting fullscreen |
| `--stop` | ask a running helper to put the title bar back and exit |
| `--log` | write `%LOCALAPPDATA%\VlcBorderless\helper.log` |

## Building from source

```powershell
.\build.ps1      # -> dist\VlcBorderless.exe (needs only the .NET Framework compiler that ships with Windows)
```

## Compatibility

Developed on **Windows 11, VLC 3.0.23, 64 bit, embedded video**, with multiple monitors, audio sources, 

I have not checked if it works on other Windows operating systems (Windows 10, or below).  If anybody would like to test this and provide me with an update I will check. 
I have also not checked mixed-DPI multi-monitor setups, audio only files, other VLC versions... I use a primarily old version because it still plays well for me. Newer VLC versions have had issues on my machine. 

## Notes

I, and this project, have no affiliation with VideoLAN, VLC, or its products and services. This is a separate, homemade tool developed with desire for more customisable layouts. 

:)
