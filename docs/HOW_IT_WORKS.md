# How it works

Notes for anyone reading or changing the code. Everything in the helper is in [`src/VlcBorderless.cs`](../src/VlcBorderless.cs) (C# 5, one file, built with the compiler that ships with Windows: keep the syntax at C# 5).

## The two pieces

```
VLC  --(View > Borderless Window)-->  borderless.lua  --start "" VlcBorderless.exe [--stop]-->  helper
```

VLC's Lua API cannot touch window styles, so [`extension/borderless.lua`](../extension/borderless.lua) only starts and stops the helper (`os.execute('start "" "<exe>"')`). VLC calls `activate()` when the menu item is ticked and `deactivate()` when it is unticked; the extension has no dialog and no capabilities.

### Why the extension must stay tiny and defensive

If `activate()` raises a Lua error, VLC prints `Could not activate extension!`, deactivates the extension straight away, and the next click on the item activates it again. The item then never behaves like a checkbox and `deactivate()` is never called. (For example, `vlc.misc` does not exist inside the extension sandbox, so calling `vlc.misc.mwait` raises exactly such an error.) So: everything that can fail runs in `pcall`, `capabilities = {}`, and the extension logs `[borderless] activate / deactivate` through `vlc.msg.info`.

## Taking the frame off without moving the video

`TitleBarManager.Reshape` clears `WS_CAPTION | WS_THICKFRAME`, calls `SetWindowPos(... SWP_FRAMECHANGED)`, then moves and resizes the window so that the client area is on exactly the same screen pixels as before. The video does not jump or rescale; the frame just disappears. Restoring does the reverse.

Qt believes the window still has a frame. When VLC changes its window flags (**Always on top**, leaving fullscreen) it puts the caption and frame back **and** re-places the window (the picture jumps by about the height of the title bar). The helper's 10 Hz scan sees the caption bit return, strips it again within ~30-60 ms and puts the video back, using the client-area position it remembered for that window. If VLC hides the window for a moment while doing this, the remembered position is kept (a window is only forgotten once it is really gone).

VLC also saves its window geometry on exit - while the frame is off - and restores it **with** a frame next time, which would sink the video by one title-bar height every session. The helper persists the last stripped placement in `%LOCALAPPDATA%\VlcBorderless\last-placement.txt` and recognises a window that looks exactly like that restore (same top-left corner, same video size) and puts the video back.

Maximized and fullscreen windows are otherwise left alone.

## Moving: a watch-only mouse hook

With no title bar there is nothing to grab, so a press-and-drag on the video moves the window. Windows' own move loop cannot be started from outside here: VLC's video child window lives on a different thread from the Qt main window, and the loop checks the button state of the Qt thread's queue, which never saw the press. So the helper moves the window itself: after a press on the managed window, once the mouse has travelled a few pixels with the button held, it follows the cursor with `SetWindowPos`.

The `WH_MOUSE_LL` hook **only watches**; it always calls `CallNextHookEx`, and never swallows or injects input (an embedded VLC video ignores a plain press / drag on the picture, so nothing unwanted happens). Calling `SendInput` from inside the hook stalled the callback for ~300 ms, so it is deliberately not used. The callback does as little as possible; slow callbacks are logged.

## Resizing: an invisible ring, locked to the picture

The window is now exactly the video, so there is no border to grab. `ResizeRing` is a nearly-transparent, layered, no-activate tool window shaped (by region) like a 6 px frame, placed directly above VLC in the z-order. It answers `WM_NCHITTEST` with the resize codes, which gives the normal resize cursors for free, and the interior is click-through. Dragging it runs the helper's own resize (Windows' native resize cannot be aspect-locked from outside VLC). The ring follows the window when something else moves it.

### What is locked: the picture, not the window

In embedded mode VLC draws into child windows:

```
Qt5QWindowIcon   (VLC's main window; the helper handles this one)
  ...
  VLC video main      the video area. Everything else in the window (control bar, docked playlist) is fixed "chrome"
    VLC video output  the PICTURE: exactly the displayed size of the video (non-square pixels included),
                      centred in the video area
```

The "black bars" you see around a video in a stretched window are nothing but the **gap between `VLC video output` and `VLC video main`**. So the helper never has to guess an aspect ratio, measure pixels, or be told one by the extension:

- **Locked resizing** (`AspectMath.Resize`): the video *area* is resized with its shape locked, and the chrome (window minus area) keeps its size. If a gap of 12 px or more exists the lock uses the picture's shape (so a click on an edge snaps bars away); otherwise the area's current shape. A remembered ratio stops repeated resizes drifting by a pixel at a time.
- **Auto-fit** (`PictureFit.Compute`): the new window rectangle is the old one minus the gap on each side that has one, so the picture stays put and the chrome keeps its size.
  - **At the strip:** when you tick the option on a window that is already stretched, the helper measures the picture inside its area *just before* removing the frame (the window is calm), strips, and fits straight away using those measurements - they are still true because the strip keeps the client area on the same pixels. Title bar and bars go together (~0.35 s after the click, which is mostly the helper starting up).
  - *Later:* bars that appear afterwards (you stretched the window while it was ticked) and sit unchanged for ~0.8 s while you are not dragging are fitted too. It tries at most three times so it can never fight VLC.
  - Non-square-pixel video needs nothing special because the picture child already has the displayed shape.
- Fallbacks, only if the picture child cannot be found (a VLC laid out differently): black bars measured on a screen capture of the picture (`BarDetector`), then the area's current shape.

## The helper's lifetime

- **One instance**, via the named mutex `Local\VlcBorderless.Instance`. A second launch just signals the running helper (`Local\VlcBorderless.Add`) to also handle the VLC that is now on top; `--stop` signals `Local\VlcBorderless.Stop`.
- **Session-bound:** the helper handles the VLC process it was started for (the owner of the top-most VLC main window) and **exits ~100 ms after it closes**, so a quickly relaunched VLC starts with its normal frame and the menu item is unticked, as it really is. `--linger` instead handles every VLC for as long as it runs.
- The extension only knows tick / untick; everything else (which window, when to exit) is decided here.

## VLC's saved "fullscreen" state

VLC saves its main window with Qt's `saveGeometry` in `%APPDATA%\vlc\vlc-qt-interface.ini`, section `[General]`, key `geometry=@ByteArray(...)` (50 bytes here). Byte 45 is the fullscreen flag. VLC closed in fullscreen reopens fullscreen - no title bar, nothing to grab, which looks like "borderless can't be reverted". When the VLC the helper handled closes, the helper (once no VLC is left that could rewrite the file) clears that one byte, keeps a backup of the original, and leaves everything else in the file untouched. `--keep-saved-fullscreen` disables this; `--fix-vlc-start` does it once on demand.

## Notes

- VLC itself re-fits its window to the video each time playback (re)starts, e.g. at each loop of a very short clip.
- A window that overhangs the screen edge cannot be fitted or measured reliably; keep the video window on-screen.
