--[[
  Borderless Window - VLC extension (VLC 3.x on Windows)

  Removes the title bar from VLC's main window - handy with "Minimal Interface" + "Always on Top", so the video
  floats without a "VLC media player" title bar above it.

  VLC's Lua API cannot touch window styles, so this extension only starts / stops a small helper program,
  VlcBorderless.exe, which does the actual work (and lets you drag the video to move the window).

    View > Borderless Window   (tick)   -> title bar removed
    View > Borderless Window   (unticked) -> title bar restored
  In Minimal Interface the menu bar is hidden: right-click the video > View > Borderless Window.

  Install with install.ps1 (copies this file to %APPDATA%\vlc\lua\extensions and the helper to %APPDATA%\vlc\borderless).

  Keep this file small and defensive: if activate() ever raises a Lua error, VLC reports "Could not activate extension",
  deactivates the extension straight away and the next click on the menu item ACTIVATES it again - the item then never
  behaves like a checkbox (deactivate() is never called, so the title bar cannot be brought back). Everything that can
  fail below runs inside pcall for that reason. (vlc.misc, for example, does not exist inside extensions.)
]]

local HELPER = "VlcBorderless.exe"
local dlg = nil

function descriptor()
  return {
    title        = "Borderless Window",
    version      = "26.11.24",
    author       = "skitch",
    shortdesc    = "Borderless Window",
    description  = "Removes VLC's title bar and window border  with Minimal Interface and Always on Top "
                .. "Drag the video to move the window; resize from its edges - the window keeps the video's aspect ratio.",
    capabilities = {}
  }
end

local function log(text)
  pcall(vlc.msg.info, "[borderless] " .. text)
end

-- Helper lookup: the user's VLC data folder first, then the VLC install folder.
local function find_helper()
  local dirs = {}
  local ok1, ud = pcall(vlc.config.userdatadir)
  if ok1 and ud then dirs[#dirs + 1] = ud end
  local ok2, dd = pcall(vlc.config.datadir)
  if ok2 and dd then dirs[#dirs + 1] = dd end
  for _, d in ipairs(dirs) do
    local path = (d .. "\\borderless\\" .. HELPER):gsub("/", "\\")
    local f = io.open(path, "rb")
    if f then f:close(); return path end
  end
  return nil
end

local function run_helper(exe, args)
  -- "start" so VLC does not wait for the helper; the empty "" is start's window-title argument.
  local cmd = 'start "" "' .. exe .. '"' .. (args and (" " .. args) or "")
  local ok, res = pcall(os.execute, cmd)
  log(cmd .. " -> " .. tostring(ok) .. " " .. tostring(res))
  return ok
end

local function show_problem(text)
  local ok = pcall(function()
    dlg = vlc.dialog("Borderless Window")
    dlg:add_label(text, 1, 1, 4, 1)
    dlg:add_button("Close", function() vlc.deactivate() end, 1, 2, 1, 1)
    dlg:show()
  end)
  if not ok then log("problem: " .. text) end
end

function activate()
  log("activate")
  local exe = find_helper()
  if not exe then
    show_problem("VlcBorderless.exe was not found in %APPDATA%\\vlc\\borderless. Run install.ps1 again.")
    return
  end
  if not run_helper(exe, nil) then
    show_problem("Could not start " .. exe .. " (VLC's Lua is not allowed to run programs here).")
    return
  end
end

function deactivate()
  log("deactivate")
  local exe = find_helper()
  if exe then run_helper(exe, "--stop") end
end

function close()
  vlc.deactivate()
end
