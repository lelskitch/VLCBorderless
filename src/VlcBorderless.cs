// VlcBorderless - removes the title bar from VLC's main window and lets you drag the video to move it.
//
// VLC's Lua extension API has no access to window styles, so the VLC extension (borderless.lua) just starts / stops
// this helper. The helper can also be run on its own.
//
// The window is made completely frameless (no title bar, no resize border, no strip above the video). Resizing is done
// by the helper itself through invisible grips around the video's edge, and is locked to the video's aspect ratio, so
// resizing never creates black bars.
//
// Build: build.ps1 (uses the C# 5 compiler that ships with Windows - keep the syntax at C# 5).
//
// dev: lelskitch

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace VlcBorderless
{
    internal static class Native
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData; public uint flags; public uint time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

        public const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
        public const uint WS_EX_TOPMOST = 0x00000008;
        public const uint WS_CAPTION = 0x00C00000;
        public const uint WS_THICKFRAME = 0x00040000;
        public const uint WS_MAXIMIZEBOX = 0x00010000;

        public const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010, SWP_FRAMECHANGED = 0x0020,
                          SWP_ASYNCWINDOWPOS = 0x4000;

        public const int WH_MOUSE_LL = 14;
        public const int WM_MOUSEMOVE = 0x0200, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202;
        public const int WM_NCHITTEST = 0x0084, WM_NCLBUTTONDOWN = 0x00A1, WM_MOUSEACTIVATE = 0x0021;
        public const int HTCLIENT = 1, HTTRANSPARENT = -1, HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13, HTTOPRIGHT = 14,
                         HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;
        public const uint WS_EX_TOOLWINDOW = 0x00000080, WS_EX_NOACTIVATE = 0x08000000;
        public const uint GW_HWNDPREV = 3;
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1), HWND_TOP = IntPtr.Zero;
        public const int VK_MENU = 0x12, VK_LBUTTON = 0x01;
        public const int SM_CXDRAG = 68, SM_CYDRAG = 69;
        public const uint GA_ROOT = 2;
        public const uint MONITOR_DEFAULTTONEAREST = 2;
        public const int DWMWA_CAPTION_COLOR = 35;
        public const uint DWMWA_COLOR_DEFAULT = 0xFFFFFFFF;

        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr l);
        [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc cb, IntPtr l);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] public static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(POINT p, uint flags);
        [DllImport("user32.dll")] public static extern bool GetMonitorInfo(IntPtr mon, ref MONITORINFO mi);
        [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint cmd);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
        [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr SetWindowsHookEx(int id, LowLevelMouseProc proc, IntPtr mod, uint thread);
        [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string name);
        [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref uint value, int size);
    }

    ///The user's data folders.
    internal static class Paths
    {
        public static string AppData { get { return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); } }
        public static string LocalAppData { get { return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); } }
    }

    internal static class Log
    {
        public static bool Enabled;
        static readonly object gate = new object();
        public static void Write(string msg)
        {
            if (!Enabled) return;
            try
            {
                string dir = Path.Combine(Paths.LocalAppData, "VlcBorderless");
                Directory.CreateDirectory(dir);
                lock (gate) File.AppendAllText(Path.Combine(dir, "helper.log"), DateTime.Now.ToString("HH:mm:ss.fff") + " " + msg + "\r\n");
            }
            catch { }
        }
    }

    /// Finds VLC's main window(s) and strips / restores their title bar. Runs on the worker thread
    internal sealed class TitleBarManager
    {
        readonly bool frameless;
        readonly object gate = new object();
        readonly HashSet<long> managed = new HashSet<long>();   // windows whose title bar we removed

		// Qt still believes the window has a title bar. When VLC changes its window flags, it puts the bar back and re places the window, 
		// so the picture jumps by about ~height of said bar. 
		// Per window we remember where the client area last sat on the screen and put it back after re-stripping.
        sealed class Snap { public int ClientX, ClientY, ClientW, ClientH; }
        readonly Dictionary<long, Snap> snaps = new Dictionary<long, Snap>();

        // True while the user is dragging or resizing the window (set by the mouse hook / the resize ring): leave it alone.
        public static volatile bool UserIsAdjusting;
        // Remove black bars around the picture by fitting the window to it (--no-autofit turns this off)
        public static bool AutoFitEnabled = true;
        sealed class FitState { public string Signature; public int StableTicks, Attempts; }
        readonly Dictionary<long, FitState> fitStates = new Dictionary<long, FitState>();

        // VLC saves its window geometry when it quits - while we have the title bar stripped - and next time restores it
        // WITH a title bar, which sits the video one bar-height lower. Left alone, the video would sink a little every
        // session. instead we persist where the stripped window last was, and when a window shows up looking exactly like
        // that restore (same top-left corner, same video size) we put the video back where it was. (? does this make sense...?)
        static readonly string LastFile = Path.Combine(Paths.LocalAppData, "VlcBorderless", "last-placement.txt");
        string lastWritten;
        int lastWriteTick;
        readonly Dictionary<long, string> newWindowShape = new Dictionary<long, string>();
        readonly Dictionary<long, int> newWindowSince = new Dictionary<long, int>();

        // True while a not-yet-handled window is still changing shape (give up waiting after 1.5 s).
        bool StillBeingBuilt(IntPtr h, long key)
        {
            Native.RECT w;
            if (!Native.GetWindowRect(h, out w)) return false;
            string shape = w.Left + "," + w.Top + "," + (w.Right - w.Left) + "," + (w.Bottom - w.Top);
            string was;
            if (!newWindowShape.TryGetValue(key, out was)) { newWindowShape[key] = shape; newWindowSince[key] = Environment.TickCount; return true; }   // first look
            if (was == shape) return false;                                                                                                         // unchanged since last scan
            newWindowShape[key] = shape;
            return unchecked(Environment.TickCount - newWindowSince[key]) < 1500;
        }

        static int[] LoadLast()                         // outerLeft, outerTop, clientX, clientY, clientW, clientH
        {
            try
            {
                string[] p = File.ReadAllText(LastFile).Trim().Split(' ');
                if (p.Length != 6) return null;
                int[] v = new int[6];
                for (int i = 0; i < 6; i++) v[i] = int.Parse(p[i]);
                return v;
            }
            catch { return null; }
        }

        void SaveLast(int outerL, int outerT, int cx, int cy, int cw, int ch)
        {
            string s = outerL + " " + outerT + " " + cx + " " + cy + " " + cw + " " + ch;
            if (s == lastWritten || unchecked(Environment.TickCount - lastWriteTick) < 500) return;   // unchanged, or written very recently
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LastFile));
                File.WriteAllText(LastFile, s);
                lastWritten = s; lastWriteTick = Environment.TickCount;
            }
            catch { }
        }

        // true: also remove the resize border (no strip above the video); false: keep Windows' own border
        public TitleBarManager(bool frameless) { this.frameless = frameless; }

        uint Mask { get { return Native.WS_CAPTION | (frameless ? Native.WS_THICKFRAME : 0u); } }

        public IntPtr[] ManagedWindows()
        {
            lock (gate)
            {
                IntPtr[] a = new IntPtr[managed.Count];
                int i = 0;
                foreach (long h in managed) a[i++] = new IntPtr(h);
                return a;
            }
        }

        static uint GetStyle(IntPtr h) { return unchecked((uint)Native.GetWindowLongPtr(h, Native.GWL_STYLE).ToInt64()); }
        static void SetStyle(IntPtr h, uint s) { Native.SetWindowLongPtr(h, Native.GWL_STYLE, new IntPtr(unchecked((int)s))); }

        static string TextOf(IntPtr h)
        {
            StringBuilder sb = new StringBuilder(256);
            Native.GetWindowText(h, sb, 256);
            return sb.ToString();
        }

        public static string ClassOf(IntPtr h)
        {
            StringBuilder sb = new StringBuilder(128);
            Native.GetClassName(h, sb, 128);
            return sb.ToString();
        }

        static bool IsMainTitle(string t)
        {
            // "VLC media player" when idle, "<media> - VLC media player" while playing. Not "About VLC media player".
            return t == "VLC media player" || t.EndsWith(" - VLC media player", StringComparison.Ordinal);
        }

        // VLC's main (Qt) window(s): visible top-level windows of vlc.exe with the main-window title.
        public static List<IntPtr> FindMainWindows(HashSet<uint> vlcPids)
        {
            List<IntPtr> found = new List<IntPtr>();
            Native.EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                uint pid;
                Native.GetWindowThreadProcessId(h, out pid);
                if (!vlcPids.Contains(pid) || !Native.IsWindowVisible(h)) return true;
                if (!ClassOf(h).StartsWith("Qt", StringComparison.Ordinal)) return true;
                if ((GetStyle(h) & Native.WS_MAXIMIZEBOX) == 0) return true;     // dialogs (About, Preferences...) have none
                if (!IsMainTitle(TextOf(h))) return true;
                found.Add(h);
                return true;
            }, IntPtr.Zero);
            return found;
        }

        public static HashSet<uint> VlcPids()
        {
            HashSet<uint> pids = new HashSet<uint>();
            try
            {
                foreach (Process p in Process.GetProcessesByName("vlc"))
                {
                    pids.Add((uint)p.Id);
                    p.Dispose();
                }
            }
            catch { }
            return pids;
        }

        /// The VLC the user is most likely in: the owner of the top-most VLC main window (EnumWindows lists windows in
        /// z-order, and the one whose menu was just clicked is on top). If VLC has no window yet, the newest VLC process.
        public static uint TopmostVlcPid(HashSet<uint> exclude)
        {
            HashSet<uint> all = VlcPids();
            if (exclude != null) all.ExceptWith(exclude);          // e.g. VLCs this helper already handles
            if (all.Count == 0) return 0;
            List<IntPtr> mains = FindMainWindows(all);
            if (mains.Count > 0) { uint pid; Native.GetWindowThreadProcessId(mains[0], out pid); return pid; }
            uint newest = 0; DateTime when = DateTime.MinValue;
            foreach (uint p in all)
            {
                try { using (Process pr = Process.GetProcessById((int)p)) { if (pr.StartTime > when) { when = pr.StartTime; newest = p; } } }
                catch { }
            }
            return newest;
        }

        public static bool IsAlive(uint pid)
        {
            try { using (Process p = Process.GetProcessById((int)pid)) return !p.HasExited; }
            catch { return false; }
        }

        //One pass: strip the title bar from any main window that has one.
        public void Scan(HashSet<uint> pids)
        {
            List<IntPtr> mains = FindMainWindows(pids);
            foreach (IntPtr h in mains)
            {
                if (Native.IsIconic(h)) continue;
                long key = h.ToInt64();
                bool settled = !Native.IsZoomed(h) && !IsFullscreen(h);
                if ((GetStyle(h) & Native.WS_CAPTION) != 0)
                {
                    // a new window, or VLC put its title bar back (Always on top toggled, fullscreen left).
                    Snap before; snaps.TryGetValue(key, out before);
                    Snap restoredFromLastSession = null;
                    Native.RECT area0 = new Native.RECT(), picture0 = new Native.RECT();
                    bool measured = false;
                    if (before == null && settled)
                    {
                        // a window we have not handled yet. VLC builds its window in a couple of steps (an odd transitional
                        // shape first), so wait until the shape has stopped changing before looking at it.
                        if (StillBeingBuilt(h, key)) continue;
                        restoredFromLastSession = LooksLikeRestoredGeometry(h);
                        // The window is calm right now: note where VLC has put the picture inside its video area, so any black
                        // bars can be removed in the same breath as the title bar (see FitAtStrip).
                        measured = restoredFromLastSession == null && AutoFitEnabled && VideoArea.Find(h, out area0) && VideoArea.FindOutput(h, out picture0);
                    }
                    Reshape(h, false);
                    lock (gate) managed.Add(key);
                    newWindowShape.Remove(key); newWindowSince.Remove(key);
                    if (before != null && settled) PutVideoBack(h, before, false);
                    else if (restoredFromLastSession != null) { Log.Write("window looks like VLC's restored geometry from the last session"); PutVideoBack(h, restoredFromLastSession, true); }
                    if (measured) FitAtStrip(h, area0, picture0);
                    if (settled) Remember(h);
                }
                else if (settled)
                {
                    bool ours; lock (gate) ours = managed.Contains(key);
                    if (ours) { Remember(h); AutoFit(h, key); }   // follow the window as the user drags / resizes it; fit it to the picture if bars show
                }
            }
            // Forget a window only once it is really gone. Qt hides the window for a moment while it swaps flags (Always on
            // top); a scan landing in that gap must not throw away where the video was, or it could not be put back.
            lock (gate) managed.RemoveWhere(delegate(long h) { return !Native.IsWindow(new IntPtr(h)); });
            List<long> gone = new List<long>();
            foreach (long k in snaps.Keys) if (!Native.IsWindow(new IntPtr(k))) gone.Add(k);
            foreach (long k in gone) snaps.Remove(k);
            List<long> goneFits = new List<long>();
            foreach (long k in fitStates.Keys) if (!Native.IsWindow(new IntPtr(k))) goneFits.Add(k);
            foreach (long k in goneFits) fitStates.Remove(k);
        }

        /// Black bars around the picture (a window stretched wider or taller than the video) that have been sitting there, unchanged,
        /// for about a second while the user is not dragging: fit the window to the picture. The picture stays exactly where it is on
        /// screen, only the bars go, and VLC's fixed controls keep their size. Gives up after three tries so it never fights VLC.
        void AutoFit(IntPtr h, long key)
        {
            if (!AutoFitEnabled) return;
            FitState s;
            if (!fitStates.TryGetValue(key, out s)) { s = new FitState(); fitStates[key] = s; }
            if (UserIsAdjusting || (Native.GetAsyncKeyState(Native.VK_LBUTTON) & 0x8000) != 0) { s.StableTicks = 0; return; }

            Native.RECT win, area, output, fit;
            if (!Native.GetWindowRect(h, out win) || !VideoArea.Find(h, out area) || !VideoArea.FindOutput(h, out output) || !PictureFit.Compute(win, area, output, out fit))
            { s.Signature = null; s.StableTicks = 0; s.Attempts = 0; return; }                // no bars (or no picture): nothing to do, and re-arm

            string sig = win.Left + "," + win.Top + "," + win.Right + "," + win.Bottom + "|" + output.Left + "," + output.Top + "," + output.Right + "," + output.Bottom;
            if (sig != s.Signature) { s.Signature = sig; s.StableTicks = 0; return; }          // still changing: VLC is laying itself out
            if (++s.StableTicks < 8 || s.Attempts >= 3) return;                                // ~0.8 s of calm; never fight VLC
            s.Attempts++; s.StableTicks = 0;
            Native.SetWindowPos(h, IntPtr.Zero, fit.Left, fit.Top, fit.Right - fit.Left, fit.Bottom - fit.Top, Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
            Log.Write("black bars around the picture: window fitted to it (" + (win.Right - win.Left) + "x" + (win.Bottom - win.Top) + " -> " + (fit.Right - fit.Left) + "x" + (fit.Bottom - fit.Top) + "), the picture did not move");
        }

        /// the window was already stretched around its picture when the title bar went (the extension was just ticked): fit it to the
        /// picture right now, so the bars vanish together with the title bar instead of about a second later. Uses the layout measured
        /// BEFORE the strip - the strip keeps the client area on the same pixels, so those rectangles still hold, and VLC's own
        /// re-layout (on its video thread, a moment later) is not waited for. If anything looks off (the window moved, the user has
        /// the mouse down) it does nothing and the ordinary AutoFit takes over after about a second.
        void FitAtStrip(IntPtr h, Native.RECT area0, Native.RECT picture0)
        {
            if (UserIsAdjusting || (Native.GetAsyncKeyState(Native.VK_LBUTTON) & 0x8000) != 0) return;
            Native.RECT win, fit;
            if (!Native.GetWindowRect(h, out win)) return;
            // the area measured before the strip must still be inside the window (it is, unless VLC or Qt moved the window meanwhile)
            if (area0.Left < win.Left - 1 || area0.Top < win.Top - 1 || area0.Right > win.Right + 1 || area0.Bottom > win.Bottom + 1) return;
            if (!PictureFit.Compute(win, area0, picture0, out fit)) return;
            Native.SetWindowPos(h, IntPtr.Zero, fit.Left, fit.Top, fit.Right - fit.Left, fit.Bottom - fit.Top, Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
            Log.Write("black bars around the picture: window fitted to it together with the title bar (" + (win.Right - win.Left) + "x" + (win.Bottom - win.Top) + " -> " + (fit.Right - fit.Left) + "x" + (fit.Bottom - fit.Top) + "), the picture did not move");
        }

        // Record where the video (client area) is on screen right now. 
        void Remember(IntPtr h)
        {
            Native.RECT c, w; Native.POINT o; o.X = 0; o.Y = 0;
            if (!Native.GetClientRect(h, out c) || !Native.ClientToScreen(h, ref o) || !Native.GetWindowRect(h, out w)) return;
            Snap s;
            if (!snaps.TryGetValue(h.ToInt64(), out s)) { s = new Snap(); snaps[h.ToInt64()] = s; }
            s.ClientX = o.X; s.ClientY = o.Y; s.ClientW = c.Right - c.Left; s.ClientH = c.Bottom - c.Top;
            SaveLast(w.Left, w.Top, s.ClientX, s.ClientY, s.ClientW, s.ClientH);
        }

        /// For a window we have not seen before, which still has its title bar: does it have the same top-left corner and
        /// video size as the stripped window we last recorded?; then VLC just restore its saved geometry, and the video
        /// should go back to where it was (returned as a snapshot to restore), not stay one title bar lower.
        Snap LooksLikeRestoredGeometry(IntPtr h)
        {
            int[] last = LoadLast();
            if (last == null || Native.IsZoomed(h)) return null;
            Native.RECT w;
            if (!Native.GetWindowRect(h, out w)) return null;
            // the top-left corner is what VLC restores exactly; the size may legitimately differ (a different video).
            if (Math.Abs(w.Left - last[0]) > 2 || Math.Abs(w.Top - last[1]) > 2) return null;
            Snap s = new Snap(); s.ClientX = last[2]; s.ClientY = last[3]; s.ClientW = last[4]; s.ClientH = last[5];
            return s;
        }

        // after re-stripping a window that VLC had just re-dressed: if the video is the same size but has moved,
        // move the window so the video is back where it was. (A different size means VLC really resized: leave it be.)
        void PutVideoBack(IntPtr h, Snap was, bool anySize)
        {
            Native.RECT c; Native.POINT o; o.X = 0; o.Y = 0;
            if (!Native.GetClientRect(h, out c) || !Native.ClientToScreen(h, ref o)) return;
            if (!anySize && (c.Right - c.Left != was.ClientW || c.Bottom - c.Top != was.ClientH)) { Log.Write("title bar re-applied; video size changed, leaving the window alone"); return; }
            int dx = was.ClientX - o.X, dy = was.ClientY - o.Y;
            if (dx == 0 && dy == 0) { Log.Write("title bar re-applied; video did not move"); return; }
            Native.RECT w; Native.GetWindowRect(h, out w);
            Native.SetWindowPos(h, IntPtr.Zero, w.Left + dx, w.Top + dy, 0, 0, Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
            Log.Write("title bar re-applied by VLC and the video jumped by " + (-dx) + "," + (-dy) + " - put back");
        }

        public void RestoreAll()
        {
            IntPtr[] list = ManagedWindows();
            foreach (IntPtr h in list)
            {
                try { if (Native.IsWindow(h) && !Native.IsIconic(h) && !IsFullscreen(h)) Reshape(h, true); }
                catch (Exception e) { Log.Write("restore failed: " + e.Message); }
            }
            lock (gate) managed.Clear();
        }

        public static bool IsFullscreen(IntPtr h)
        {
            Native.RECT r;
            if (!Native.GetWindowRect(h, out r)) return false;
            Native.MONITORINFO mi = new Native.MONITORINFO();
            mi.cbSize = Marshal.SizeOf(typeof(Native.MONITORINFO));
            IntPtr mon = Native.MonitorFromWindow(h, Native.MONITOR_DEFAULTTONEAREST);
            if (mon == IntPtr.Zero || !Native.GetMonitorInfo(mon, ref mi)) return false;
            return r.Left <= mi.rcMonitor.Left && r.Top <= mi.rcMonitor.Top && r.Right >= mi.rcMonitor.Right && r.Bottom >= mi.rcMonitor.Bottom;
        }

        // Remove (or put back) the title bar while keeping the client area - the video - exactly where it is on screen,
        // so the bar just disappears instead of the picture shifting or rescaling.
        void Reshape(IntPtr h, bool restore)
        {
            bool zoomed = Native.IsZoomed(h);
            Native.RECT c0; Native.GetClientRect(h, out c0);
            Native.POINT o0; o0.X = 0; o0.Y = 0; Native.ClientToScreen(h, ref o0);

            uint style = GetStyle(h);
            style = restore ? (style | Mask) : (style & ~Mask);
            SetStyle(h, style);
            uint color = restore ? Native.DWMWA_COLOR_DEFAULT : 0x00000000;      // black: the top resize strip blends into the video letterbox
            try { Native.DwmSetWindowAttribute(h, Native.DWMWA_CAPTION_COLOR, ref color, 4); } catch { }   // Windows 11 only; harmless elsewhere
            Native.SetWindowPos(h, IntPtr.Zero, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_FRAMECHANGED);

            if (!zoomed)
            {
                Native.RECT c1; Native.GetClientRect(h, out c1);
                Native.POINT o1; o1.X = 0; o1.Y = 0; Native.ClientToScreen(h, ref o1);
                Native.RECT w1; Native.GetWindowRect(h, out w1);
                int dx = o0.X - o1.X, dy = o0.Y - o1.Y;
                int dw = (c0.Right - c0.Left) - (c1.Right - c1.Left), dh = (c0.Bottom - c0.Top) - (c1.Bottom - c1.Top);
                if (dx != 0 || dy != 0 || dw != 0 || dh != 0)
                    Native.SetWindowPos(h, IntPtr.Zero, w1.Left + dx, w1.Top + dy, (w1.Right - w1.Left) + dw, (w1.Bottom - w1.Top) + dh,
                        Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
            }
            Log.Write((restore ? "restored title bar on 0x" : "removed title bar from 0x") + h.ToInt64().ToString("X"));
        }
    }

    // Drag the video to move the window (with the title bar gone there is nothing else to grab).
    //
    // Windows' own move loop cannot be started from outside here: VLC's video child window lives on a different thread
    // from the Qt main window, and the loop checks the button state of the Qt thread's queue, which never saw the press.
    // So the helper moves the window itself: after a press on the video, once the mouse has travelled a few pixels with
    // the button held, the window follows the cursor.
    //
    // The hook only watches. It never swallows or injects mouse input, so VLC still receives every event (an embedded VLC
    // video ignores a plain press / drag on the picture, so nothing unwanted happens). Alt+drag moves the window from
    // anywhere on it (controls, audio-only files). Presses on the resize edge are never used.
    internal sealed class DragHook : IDisposable
    {
        enum Mode { Idle, Pending, Dragging }

        readonly Func<IntPtr[]> managed;
        readonly Native.LowLevelMouseProc proc;
        IntPtr hook;
        Mode mode;
        Native.POINT pressPt;
        IntPtr target;
        int originX, originY;
        int lastMoveTick;

        public DragHook(Func<IntPtr[]> managedWindows)
        {
            managed = managedWindows;
            proc = Callback;                                    // keep the delegate alive
            hook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, proc, Native.GetModuleHandle(null), 0);
            Log.Write("mouse hook " + (hook == IntPtr.Zero ? "FAILED" : "installed"));
        }

        IntPtr Callback(int code, IntPtr wParam, IntPtr lParam)
        {
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                if (code >= 0)
                {
                    int msg = wParam.ToInt32();
                    Native.MSLLHOOKSTRUCT ms = (Native.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.MSLLHOOKSTRUCT));
                    if (msg == Native.WM_LBUTTONDOWN) { mode = Mode.Idle; TitleBarManager.UserIsAdjusting = false; BeginPress(ms.pt); LogSlow(t0, "press"); }
                    else if (msg == Native.WM_MOUSEMOVE) { if (mode != Mode.Idle) OnMove(ms.pt); }
                    else if (msg == Native.WM_LBUTTONUP && mode != Mode.Idle) EndPress(ms.pt);
                }
            }
            catch (Exception e) { Log.Write("hook error: " + e.Message); mode = Mode.Idle; TitleBarManager.UserIsAdjusting = false; }
            return Native.CallNextHookEx(hook, code, wParam, lParam);                          // always pass the event on
        }

        static void LogSlow(long t0, string what)
        {
            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            if (ms > 10) Log.Write("slow hook callback (" + what + "): " + ms.ToString("F0") + " ms");
        }

        void BeginPress(Native.POINT pt)
        {
            IntPtr win = IntPtr.Zero;
            foreach (IntPtr h in managed())
            {
                Native.RECT r;
                if (Native.GetWindowRect(h, out r) && pt.X >= r.Left && pt.X < r.Right && pt.Y >= r.Top && pt.Y < r.Bottom) { win = h; break; }
            }
            if (win == IntPtr.Zero || Native.IsZoomed(win)) return;                            // not on a window we manage

            IntPtr under = Native.WindowFromPoint(pt);
            if (under == IntPtr.Zero || Native.GetAncestor(under, Native.GA_ROOT) != win) return;   // another window is on top of VLC here
            bool alt = (Native.GetAsyncKeyState(Native.VK_MENU) & 0x8000) != 0;
            bool onVideo = TitleBarManager.ClassOf(under).StartsWith("VLC video", StringComparison.Ordinal);
            if (!onVideo && !alt) return;                                                      // controls / resize edge: leave alone

            Native.RECT wr;
            Native.GetWindowRect(win, out wr);
            target = win; pressPt = pt; originX = wr.Left; originY = wr.Top; lastMoveTick = 0;
            mode = Mode.Pending;
            TitleBarManager.UserIsAdjusting = true;
        }

        void OnMove(Native.POINT pt)
        {
            int dx = pt.X - pressPt.X, dy = pt.Y - pressPt.Y;
            if (mode == Mode.Pending)
            {
                if (Math.Abs(dx) <= Native.GetSystemMetrics(Native.SM_CXDRAG) && Math.Abs(dy) <= Native.GetSystemMetrics(Native.SM_CYDRAG)) return;
                mode = Mode.Dragging;
                Log.Write("drag started");
            }
            int now = Environment.TickCount;
            if (unchecked(now - lastMoveTick) < 8) return;                                      // ~120 moves/s is plenty
            lastMoveTick = now;
            MoveTo(originX + dx, originY + dy);
        }

        void EndPress(Native.POINT pt)
        {
            if (mode == Mode.Dragging) { MoveTo(originX + pt.X - pressPt.X, originY + pt.Y - pressPt.Y); Log.Write("drag ended"); }
            mode = Mode.Idle;
            TitleBarManager.UserIsAdjusting = false;
        }

        void MoveTo(int x, int y)
        {
            Native.SetWindowPos(target, IntPtr.Zero, x, y, 0, 0,
                Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_ASYNCWINDOWPOS);   // async: never blocks on a busy VLC
        }

        public void Dispose()
        {
            if (hook != IntPtr.Zero) { Native.UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
        }
    }

    /// VLC remembers its main window's state in vlc-qt-interface.ini ([General] geometry, a Qt saveGeometry blob). If VLC is
    /// closed while fullscreen, the next VLC starts fullscreen: no title bar, nothing to grab, and nothing the extension can
    /// undo - which looks exactly like "borderless can't be reverted". After the VLC we handle has closed we clear that one
    /// flag, and only that, so VLC starts in a normal window again. (Backup kept in %LOCALAPPDATA%\VlcBorderless.)
    internal static class VlcState
    {
        public static bool Enabled = true;                                           // --keep-saved-fullscreen turns it off
        static readonly Encoding Latin1 = Encoding.GetEncoding("iso-8859-1");        // byte-for-byte round trip of the file

        public static string DefaultIni
        {
            get { return Path.Combine(Paths.AppData, "vlc", "vlc-qt-interface.ini"); }
        }

        const int FullscreenByte = 45, BlobLength = 50;                              // Qt 5 saveGeometry layout: ... screen(4) maximized(1) fullscreen(1) width(4)

        //The escaped bytes of Qt's @ByteArray(...) text as tokens (keeping the original spelling) and values
        static bool Tokenize(string s, List<string> tokens, List<byte> bytes)
        {
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (c != '\\') { tokens.Add(c.ToString()); bytes.Add((byte)c); i++; continue; }
                if (i + 1 >= s.Length) return false;
                char e = s[i + 1];
                if (e == 'x')
                {
                    int j = i + 2, v = 0, n = 0;
                    while (j < s.Length && n < 2 && Uri.IsHexDigit(s[j])) { v = v * 16 + Convert.ToInt32(s[j].ToString(), 16); j++; n++; }
                    if (n == 0) return false;
                    tokens.Add(s.Substring(i, j - i)); bytes.Add((byte)v); i = j; continue;
                }
                int val;
                switch (e)
                {
                    case '0': val = 0; break;  case 'a': val = 7; break;  case 'b': val = 8; break;  case 't': val = 9; break;
                    case 'n': val = 10; break; case 'v': val = 11; break; case 'f': val = 12; break; case 'r': val = 13; break;
                    case '\\': val = 92; break; case '"': val = 34; break;
                    default: val = e; break;
                }
                tokens.Add(s.Substring(i, 2)); bytes.Add((byte)val); i += 2;
            }
            return true;
        }

        //Pure text transformation. Returns true when it changed something
        public static bool ClearFullscreenInText(string text, out string patched, out string status)
        {
            patched = text;
            System.Text.RegularExpressions.Match general = System.Text.RegularExpressions.Regex.Match(text, @"(?ms)^\[General\]\r?\n(?<body>.*?)(?=^\[|\z)");
            if (!general.Success) { status = "no [General] section"; return false; }
            System.Text.RegularExpressions.Match g = System.Text.RegularExpressions.Regex.Match(general.Groups["body"].Value, @"(?m)^geometry=@ByteArray\((?<v>.*)\)\s*$");
            if (!g.Success) { status = "no main window geometry saved"; return false; }

            List<string> tokens = new List<string>(); List<byte> bytes = new List<byte>();
            if (!Tokenize(g.Groups["v"].Value, tokens, bytes)) { status = "geometry not understood"; return false; }
            if (bytes.Count != BlobLength || bytes[0] != 0x01 || bytes[1] != 0xD9 || bytes[2] != 0xD0 || bytes[3] != 0xCB) { status = "geometry has an unexpected layout - left alone"; return false; }
            if ((bytes[FullscreenByte] & 4) == 0) { status = "not saved as fullscreen"; return false; }

            int cleared = bytes[FullscreenByte] & ~4;
            tokens[FullscreenByte] = cleared == 0 ? "\\0" : "\\x" + cleared.ToString("x");
            string newValue = string.Concat(tokens.ToArray());
            int valueStart = general.Groups["body"].Index + g.Groups["v"].Index, valueLength = g.Groups["v"].Length;
            patched = text.Substring(0, valueStart) + newValue + text.Substring(valueStart + valueLength);
            status = "cleared the saved fullscreen flag";
            return true;
        }

        /// clear the saved fullscreen flag in a VLC ini file. A backup of the original is kept (once).
        public static string ClearSavedFullscreen(string iniPath)
        {
            try
            {
                if (!File.Exists(iniPath)) return "no file at " + iniPath;
                string text = Latin1.GetString(File.ReadAllBytes(iniPath)), patched, status;
                if (!ClearFullscreenInText(text, out patched, out status)) return status;
                string dir = Path.Combine(Paths.LocalAppData, "VlcBorderless");
                Directory.CreateDirectory(dir);
                string backup = Path.Combine(dir, "vlc-qt-interface.ini.bak");
                if (!File.Exists(backup)) File.Copy(iniPath, backup);
                File.WriteAllBytes(iniPath, Latin1.GetBytes(patched));
                return status;
            }
            catch (Exception e) { return "error: " + e.Message; }
        }
    }

    internal enum Edge { Left, Right, Top, Bottom, TopLeft, TopRight, BottomLeft, BottomRight }

    //Resizing that always keeps the video's aspect ratio, so no black bars ever appear.
    internal static class AspectMath
    {
        // The new window rectangle after dragging "edge" of "start" by (dx, dy).
        // The window is the video area plus fixed "chrome" (VLC's control bar / playlist; zero in Minimal Interface). Only the
        // video area - the window minus chromeW x chromeH - is kept at "aspect" (to within a pixel of rounding);
        // the chrome keeps its size. Side edges keep the window vertically centred, top / bottom edges keep it horizontally
        // centred, corners keep the opposite corner fixed.
        public static Native.RECT Resize(Native.RECT start, Edge edge, int dx, int dy, double aspect, int chromeW, int chromeH, int minWidth, Native.RECT bounds)
        {
            int w0 = start.Right - start.Left, h0 = start.Bottom - start.Top;
            int iw0 = Math.Max(1, w0 - chromeW), ih0 = Math.Max(1, h0 - chromeH);        // the video area now
            double wx, wy;                                  // the video width implied by the horizontal / the vertical part of the drag
            switch (edge)
            {
                case Edge.Left:        wx = iw0 - dx; wy = wx; break;
                case Edge.Right:       wx = iw0 + dx; wy = wx; break;
                case Edge.Top:         wy = (ih0 - dy) * aspect; wx = wy; break;
                case Edge.Bottom:      wy = (ih0 + dy) * aspect; wx = wy; break;
                case Edge.TopLeft:     wx = iw0 - dx; wy = (ih0 - dy) * aspect; break;
                case Edge.TopRight:    wx = iw0 + dx; wy = (ih0 - dy) * aspect; break;
                case Edge.BottomLeft:  wx = iw0 - dx; wy = (ih0 + dy) * aspect; break;
                default:               wx = iw0 + dx; wy = (ih0 + dy) * aspect; break;  // BottomRight
            }
            double w = Math.Max(wx, wy);                    // corners: follow the cursor in whichever direction it moved more
            double maxW = Math.Min((bounds.Right - bounds.Left) - chromeW, ((bounds.Bottom - bounds.Top) - chromeH) * aspect);
            w = Math.Max(minWidth, Math.Min(w, maxW));
            int ih = (int)Math.Round(w / aspect);
            int iw = (int)Math.Round(ih * aspect);
            int ww = iw + chromeW, hh = ih + chromeH;       // back to the whole window

            int cx = start.Left + w0 / 2, cy = start.Top + h0 / 2;
            int left, top;
            switch (edge)
            {
                case Edge.Right:       left = start.Left;        top = cy - hh / 2; break;
                case Edge.Left:        left = start.Right - ww;  top = cy - hh / 2; break;
                case Edge.Bottom:      left = cx - ww / 2;       top = start.Top; break;
                case Edge.Top:         left = cx - ww / 2;       top = start.Bottom - hh; break;
                case Edge.BottomLeft:  left = start.Right - ww;  top = start.Top; break;
                case Edge.TopRight:    left = start.Left;        top = start.Bottom - hh; break;
                case Edge.TopLeft:     left = start.Right - ww;  top = start.Bottom - hh; break;
                default:               left = start.Left;        top = start.Top; break;   // BottomRight
            }
            Native.RECT r; r.Left = left; r.Top = top; r.Right = left + ww; r.Bottom = top + hh;
            return r;
        }
    }

    /// Where VLC draws the video: its "VLC video main" child window. The rest of the window is fixed "chrome".
    internal static class VideoArea
    {
        /// The picture itself: VLC's "VLC video output" child, exactly the video's displayed size, centred inside the video area.
        /// When the window is stretched wider (or taller) than the video, the black bars are the gap around this window.
        public static bool FindOutput(IntPtr vlcWindow, out Native.RECT output)
        {
            Native.RECT found = new Native.RECT();
            bool ok = false;
            Native.EnumChildWindows(vlcWindow, delegate(IntPtr h, IntPtr l)
            {
                if (!Native.IsWindowVisible(h)) return true;
                if (!TitleBarManager.ClassOf(h).StartsWith("VLC video output", StringComparison.Ordinal)) return true;
                Native.RECT r;
                if (Native.GetWindowRect(h, out r) && r.Right > r.Left && r.Bottom > r.Top) { found = r; ok = true; return false; }
                return true;
            }, IntPtr.Zero);
            output = found;
            return ok;
        }

        public static bool Find(IntPtr vlcWindow, out Native.RECT area)
        {
            Native.RECT found = new Native.RECT();
            bool ok = false;
            Native.EnumChildWindows(vlcWindow, delegate(IntPtr h, IntPtr l)
            {
                if (!Native.IsWindowVisible(h)) return true;
                if (!TitleBarManager.ClassOf(h).StartsWith("VLC video main", StringComparison.Ordinal)) return true;
                Native.RECT r;
                if (Native.GetWindowRect(h, out r) && r.Right > r.Left && r.Bottom > r.Top) { found = r; ok = true; return false; }
                return true;
            }, IntPtr.Zero);
            area = found;
            return ok;
        }
    }

    //Where the window should be to remove the black bars around the picture, with the picture staying exactly where it is.
    internal static class PictureFit
    {
        public const int MinGap = 12;                       // smaller gaps are layout margins, not visible bars

        // /"win" = the whole window, "area" = the video area inside it (the rest is VLC's fixed controls),
        /// "output" = the picture, centred in the area. When the picture is smaller than its area by at least MinGap
        /// along an axis, the new window is the old one minus that gap (left gap off the left, right gap off the right, ...), so the
        /// controls keep their size and the picture does not move on screen. False when there is nothing to do or the geometry
        /// does not look like VLC's (the picture must lie inside the area).
        public static bool Compute(Native.RECT win, Native.RECT area, Native.RECT output, out Native.RECT fit)
        {
            fit = win;
            int ow = output.Right - output.Left, oh = output.Bottom - output.Top;
            if (ow < 32 || oh < 32) return false;
            if (output.Left < area.Left - 1 || output.Top < area.Top - 1 || output.Right > area.Right + 1 || output.Bottom > area.Bottom + 1) return false;
            bool barsX = (area.Right - area.Left) - ow >= MinGap, barsY = (area.Bottom - area.Top) - oh >= MinGap;
            if (!barsX && !barsY) return false;
            if (barsX) { fit.Left = win.Left + (output.Left - area.Left); fit.Right = win.Right - (area.Right - output.Right); }
            if (barsY) { fit.Top = win.Top + (output.Top - area.Top); fit.Bottom = win.Bottom - (area.Bottom - output.Bottom); }
            return fit.Right - fit.Left >= 32 && fit.Bottom - fit.Top >= 32;
        }
    }

    /// How many pure-black lines there are along each edge of a picture.
    internal struct Bars { public int Left, Right, Top, Bottom; }

    ///Measures the black bars VLC draws around a video that does not fill its window.
    internal static class BarDetector
    {
        const int BlackLevel = 12;                          // a pixel is black when R, G and B are all at most this
        const double BlackLine = 0.90;                      // a line is "black" when this much of it is: a stray mark (VLC draws a white 2x16 px one at its edge) must not hide a bar

        static bool IsBlack(byte[] buf, int o) { return buf[o] <= BlackLevel && buf[o + 1] <= BlackLevel && buf[o + 2] <= BlackLevel; }

        static bool ColumnBlack(byte[] buf, int stride, int x, int h)
        {
            int total = 0, black = 0;
            for (int y = 0; y < h; y += 2) { total++; if (IsBlack(buf, y * stride + x * 4)) black++; }
            return black >= total * BlackLine;
        }

        static bool RowBlack(byte[] buf, int stride, int y, int w)
        {
            int total = 0, black = 0;
            for (int x = 0; x < w; x += 2) { total++; if (IsBlack(buf, y * stride + x * 4)) black++; }
            return black >= total * BlackLine;
        }

        public static Bars Measure(Bitmap bmp)
        {
            int w = bmp.Width, h = bmp.Height;
            BitmapData d = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] buf = new byte[d.Stride * h];
                Marshal.Copy(d.Scan0, buf, 0, buf.Length);
                Bars b = new Bars();
                while (b.Left < w / 2 && ColumnBlack(buf, d.Stride, b.Left, h)) b.Left++;
                while (b.Right < w / 2 && ColumnBlack(buf, d.Stride, w - 1 - b.Right, h)) b.Right++;
                while (b.Top < h / 2 && RowBlack(buf, d.Stride, b.Top, w)) b.Top++;
                while (b.Bottom < h / 2 && RowBlack(buf, d.Stride, h - 1 - b.Bottom, w)) b.Bottom++;
                return b;
            }
            finally { bmp.UnlockBits(d); }
        }

        /// The shape of the picture inside the bars, or 0 when that cannot be trusted: no bars, lopsided bars (VLC centres
        /// its bars; a dark scene or a vignette is not centred), or a black frame.
        public static double ContentAspect(int w, int h, Bars b)
        {
            if (b.Left + b.Right < 4 && b.Top + b.Bottom < 4) return 0;                       // no bars
            if (Math.Abs(b.Left - b.Right) > 3 || Math.Abs(b.Top - b.Bottom) > 3) return 0;   // not centred
            if (b.Left >= w / 2 || b.Right >= w / 2 || b.Top >= h / 2 || b.Bottom >= h / 2) return 0;   // (nearly) all black
            int cw = w - b.Left - b.Right, ch = h - b.Top - b.Bottom;
            if (cw < 32 || ch < 32) return 0;
            return (double)cw / ch;
        }

        /// Capture the given screen rectangle and return the shape of the picture inside its bars (0 = unknown)
        public static double ContentAspectOnScreen(Native.RECT area)
        {
            int w = area.Right - area.Left, h = area.Bottom - area.Top;
            if (w < 64 || h < 64) return 0;
            try
            {
                using (Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(bmp)) g.CopyFromScreen(area.Left, area.Top, 0, 0, new Size(w, h));
                    return ContentAspect(w, h, Measure(bmp));
                }
            }
            catch { return 0; }
        }
    }

    /// The aspect ratio to lock a resize to, and the fixed controls around the video
    internal sealed class LockInfo
    {
        public double Aspect;
        public int ChromeW, ChromeH;
        public string Reason;
    }

    /// Where the aspect ratio to lock to comes from.
    internal static class VideoAspect
    {
        public static readonly string HintFile = Path.Combine(Paths.LocalAppData, "VlcBorderless", "video-size.txt");

        /// The ratio is that of the VIDEO (the video area, never the whole window), read from VLC's own layout:
        ///  1. VLC draws the picture in a child window ("VLC video output") that is exactly the picture's size, centred in the video
        ///     area. The black "bars" of a stretched window are just the gap around it. When that child is smaller than the area, its
        ///     shape IS the video's displayed shape (non-square pixels included): lock to it, so resizing never keeps the bars.
        ///  2. Otherwise the area's own shape (there are no bars to remove).
        /// The size the extension publishes (video-size.txt) only makes either of those exact, when it agrees within 1.5%.
        /// If the picture's child window cannot be found (a VLC laid out differently) the older approach applies: the published size
        /// when it matches the area, else black bars measured on the picture, else the area's shape. A remembered ratio stops
        /// repeated resizes drifting by a pixel at a time.
        public static LockInfo For(IntPtr vlcWindow, double remembered)
        {
            LockInfo info = new LockInfo();
            Native.RECT win;
            Native.GetWindowRect(vlcWindow, out win);
            int ww = win.Right - win.Left, wh = win.Bottom - win.Top;
            Native.RECT area;
            bool haveArea = VideoArea.Find(vlcWindow, out area);
            int aw = ww, ah = wh;
            if (haveArea)
            {
                aw = area.Right - area.Left; ah = area.Bottom - area.Top;
                info.ChromeW = Math.Max(0, ww - aw); info.ChromeH = Math.Max(0, wh - ah);
                if (info.ChromeW > ww / 2 || info.ChromeH > wh / 2) { haveArea = false; info.ChromeW = info.ChromeH = 0; aw = ww; ah = wh; }   // not plausibly chrome
            }
            double current = (aw > 0 && ah > 0) ? (double)aw / ah : 16.0 / 9.0;
            double hint = haveArea ? Hint(vlcWindow) : 0;                 // a published size only means something while a video is showing

            Native.RECT output;
            if (haveArea && VideoArea.FindOutput(vlcWindow, out output))
            {
                int ow = output.Right - output.Left, oh = output.Bottom - output.Top;
                bool inside = output.Left >= area.Left - 1 && output.Top >= area.Top - 1 && output.Right <= area.Right + 1 && output.Bottom <= area.Bottom + 1;
                bool smaller = inside && ow >= 32 && oh >= 32 && (aw - ow >= PictureFit.MinGap || ah - oh >= PictureFit.MinGap);
                double shape = smaller ? (double)ow / oh : current;
                info.Reason = smaller ? "VLC shows the picture smaller than its area (black bars): locking to the picture's own shape"
                                      : "no black bars: kept the video area's shape";
                if (hint > 0.1 && Math.Abs(shape - hint) / hint < 0.015) { info.Aspect = hint; info.Reason += ", made exact by the published size"; return info; }
                info.Aspect = (!smaller && remembered > 0.1 && Math.Abs(shape - remembered) / remembered < 0.015) ? remembered : shape;
                return info;
            }

            // fallback: no picture window found
            if (hint > 0.1)
            {
                if (Math.Abs(current - hint) / hint < 0.015) { info.Aspect = hint; info.Reason = "the published video size matches the window"; return info; }
                double content = BarDetector.ContentAspectOnScreen(area);
                if (content > 0 && Math.Abs(content - hint) / hint < 0.02)
                { info.Aspect = hint; info.Reason = "black bars found around the picture: locking to the video's own shape"; return info; }
                info.Reason = "published size differs from the window but no matching bars (non-square pixels?): kept the window's shape";
            }
            else info.Reason = haveArea ? "no published video size: kept the window's shape" : "no video showing: kept the window's shape";

            info.Aspect = (remembered > 0.1 && Math.Abs(current - remembered) / remembered < 0.015) ? remembered : current;
            return info;
        }

        static double Hint(IntPtr vlcWindow)
        {
            try
            {
                if (!File.Exists(HintFile)) return 0;
                uint pid; Native.GetWindowThreadProcessId(vlcWindow, out pid);
                DateTime started;
                using (Process p = Process.GetProcessById((int)pid)) started = p.StartTime;
                if (File.GetLastWriteTime(HintFile) < started) return 0;            // left over from an earlier VLC session
                string[] t = File.ReadAllText(HintFile).Trim().Split(' ');
                if (t.Length != 2) return 0;
                int w = int.Parse(t[0]), h = int.Parse(t[1]);
                return (w > 0 && h > 0) ? (double)w / h : 0;
            }
            catch { return 0; }
        }
    }

    /// An invisible frame around the edge of VLC's (frameless) window that you can grab to resize it. It is a layered
    /// window one pixel of alpha thick, shaped like a ring, answering WM_NCHITTEST with the sizing codes, so Windows
    /// shows the proper resize cursors. The resize itself is done here, locked to the video's aspect ratio.
    internal sealed class ResizeRing : Form
    {
        const int Grip = 6, Corner = 16, MinWidth = 160;

        public readonly IntPtr Target;
        int ringW, ringH;
        bool resizing;
        Edge edge;
        Native.RECT startRect;
        Point startCursor;
        double aspect;
        double learnedAspect;                                          // the ratio locked to last time, kept so repeated resizes cannot drift
        int chromeW, chromeH;                                          // VLC's fixed controls around the video area (0 in Minimal Interface)
        int lastTick;

        public bool IsResizing { get { return resizing; } }

        public ResizeRing(IntPtr target)
        {
            Target = target;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Black;
            Opacity = 0.004;                                           // alpha 1 of 255: invisible, but still receives the mouse
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= unchecked((int)(Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE));   // no taskbar / Alt+Tab entry, never takes focus
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        /// Cover exactly this rectangle (VLC's window), shaped as a ring GripWidth thick.
        public void Follow(Native.RECT r)
        {
            int w = r.Right - r.Left, h = r.Bottom - r.Top;
            if (w < 2 * Grip + 4 || h < 2 * Grip + 4) return;
            if (w != ringW || h != ringH)
            {
                Region ring = new Region(new Rectangle(0, 0, w, h));
                ring.Exclude(new Rectangle(Grip, Grip, w - 2 * Grip, h - 2 * Grip));
                Region = ring;
                ringW = w; ringH = h;
            }
            Native.SetWindowPos(Handle, IntPtr.Zero, r.Left, r.Top, w, h, Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
        }

        /// Keep the ring directly above VLC in the z-order (VLC moves to the front when it is clicked).
        public void PlaceAbove(bool vlcIsTopmost)
        {
            IntPtr prev = Native.GetWindow(Target, Native.GW_HWNDPREV);
            if (prev == Handle) return;
            IntPtr after = prev == IntPtr.Zero ? (vlcIsTopmost ? Native.HWND_TOPMOST : Native.HWND_TOP) : prev;
            Native.SetWindowPos(Handle, after, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }

        int HitTest(int screenX, int screenY)
        {
            Rectangle b = Bounds;
            int x = screenX - b.Left, y = screenY - b.Top, w = b.Width, h = b.Height;
            bool inL = x < Grip, inR = x >= w - Grip, inT = y < Grip, inB = y >= h - Grip;
            bool nearL = x < Corner, nearR = x >= w - Corner, nearT = y < Corner, nearB = y >= h - Corner;
            if ((inT && nearL) || (inL && nearT)) return Native.HTTOPLEFT;
            if ((inT && nearR) || (inR && nearT)) return Native.HTTOPRIGHT;
            if ((inB && nearL) || (inL && nearB)) return Native.HTBOTTOMLEFT;
            if ((inB && nearR) || (inR && nearB)) return Native.HTBOTTOMRIGHT;
            if (inT) return Native.HTTOP;
            if (inB) return Native.HTBOTTOM;
            if (inL) return Native.HTLEFT;
            if (inR) return Native.HTRIGHT;
            return Native.HTTRANSPARENT;
        }

        static bool EdgeFromHit(int hit, out Edge e)
        {
            switch (hit)
            {
                case Native.HTLEFT: e = Edge.Left; return true;
                case Native.HTRIGHT: e = Edge.Right; return true;
                case Native.HTTOP: e = Edge.Top; return true;
                case Native.HTBOTTOM: e = Edge.Bottom; return true;
                case Native.HTTOPLEFT: e = Edge.TopLeft; return true;
                case Native.HTTOPRIGHT: e = Edge.TopRight; return true;
                case Native.HTBOTTOMLEFT: e = Edge.BottomLeft; return true;
                case Native.HTBOTTOMRIGHT: e = Edge.BottomRight; return true;
            }
            e = Edge.Left; return false;
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case Native.WM_NCHITTEST:
                {
                    int v = unchecked((int)m.LParam.ToInt64());
                    m.Result = new IntPtr(HitTest(unchecked((short)(v & 0xFFFF)), unchecked((short)((v >> 16) & 0xFFFF))));
                    return;
                }
                case Native.WM_MOUSEACTIVATE:
                    m.Result = new IntPtr(3);                          // MA_NOACTIVATE
                    return;
                case Native.WM_NCLBUTTONDOWN:
                {
                    Edge e;
                    if (EdgeFromHit(m.WParam.ToInt32(), out e)) BeginResize(e);
                    return;                                            // not passed on: we are not resizing this ring itself
                }
            }
            base.WndProc(ref m);
        }

        void BeginResize(Edge e)
        {
            Native.RECT r;
            if (!Native.GetWindowRect(Target, out r)) return;
            edge = e; startRect = r; startCursor = Cursor.Position;
            LockInfo lk = VideoAspect.For(Target, learnedAspect);
            aspect = learnedAspect = lk.Aspect; chromeW = lk.ChromeW; chromeH = lk.ChromeH;
            lastTick = 0;
            resizing = true;
            TitleBarManager.UserIsAdjusting = true;
            Capture = true;
            Log.Write("resize started (" + e + "): locked to " + aspect.ToString("F4") + " - " + lk.Reason + "; fixed controls " + chromeW + "x" + chromeH + " px");
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!resizing) return;
            int now = Environment.TickCount;
            if (unchecked(now - lastTick) < 8) return;                 // ~120 updates a second is plenty
            lastTick = now;
            Apply(false);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!resizing) return;
            Apply(false);
            resizing = false;
            TitleBarManager.UserIsAdjusting = false;
            Capture = false;
            Log.Write("resize ended");
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture) { resizing = false; TitleBarManager.UserIsAdjusting = false; }   // e.g. another window grabbed the mouse
        }

        void Apply(bool synchronous)
        {
            Point p = Cursor.Position;
            Native.RECT n = AspectMath.Resize(startRect, edge, p.X - startCursor.X, p.Y - startCursor.Y, aspect, chromeW, chromeH, MinWidth, WorkAreaOf(startRect));
            Place(n, synchronous);
        }

        void Place(Native.RECT n, bool synchronous)
        {
            Native.SetWindowPos(Target, IntPtr.Zero, n.Left, n.Top, n.Right - n.Left, n.Bottom - n.Top,
                Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | (synchronous ? 0u : Native.SWP_ASYNCWINDOWPOS));   // async: never blocks on a busy VLC
            Follow(n);                                                 // do not wait for the next tick
        }

        static Native.RECT WorkAreaOf(Native.RECT r)
        {
            // the monitor the window is mostly on; the work area excludes the taskbar
            Native.MONITORINFO mi = new Native.MONITORINFO();
            mi.cbSize = Marshal.SizeOf(typeof(Native.MONITORINFO));
            Native.POINT centre; centre.X = (r.Left + r.Right) / 2; centre.Y = (r.Top + r.Bottom) / 2;
            IntPtr mon = Native.MonitorFromPoint(centre, Native.MONITOR_DEFAULTTONEAREST);
            if (mon != IntPtr.Zero && Native.GetMonitorInfo(mon, ref mi)) return mi.rcWork;
            Native.RECT fallback; fallback.Left = 0; fallback.Top = 0; fallback.Right = 1920; fallback.Bottom = 1040;
            return fallback;
        }
    }

    internal sealed class TrayApp : ApplicationContext
    {
        readonly TitleBarManager mgr;
        readonly DragHook drag;
        readonly NotifyIcon tray;
        readonly EventWaitHandle stopEvent;
        readonly EventWaitHandle addEvent;            // set by a second launch: "the extension was ticked in another VLC too"
        readonly Control marshal;
        readonly bool linger;
        readonly bool useRings;
        readonly Dictionary<long, ResizeRing> rings = new Dictionary<long, ResizeRing>();
        readonly System.Windows.Forms.Timer ringTimer = new System.Windows.Forms.Timer();
        volatile bool stopping;

        public TrayApp(TitleBarManager manager, EventWaitHandle stop, EventWaitHandle add, bool lingerAfterVlc, bool resizeGrips)
        {
            mgr = manager; stopEvent = stop; addEvent = add; linger = lingerAfterVlc; useRings = resizeGrips;
            marshal = new Control();
            IntPtr force = marshal.Handle;                                      // create the handle so BeginInvoke works

            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem info = new ToolStripMenuItem("VLC Borderless - active");
            info.Enabled = false;
            menu.Items.Add(info);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Put the title bar back and exit", null, delegate { stopping = true; });

            tray = new NotifyIcon();
            tray.Text = "VLC Borderless";
            tray.Icon = LoadIcon();
            tray.ContextMenuStrip = menu;
            tray.Visible = true;

            drag = new DragHook(mgr.ManagedWindows);

            if (useRings)
            {
                ringTimer.Interval = 60;
                ringTimer.Tick += delegate { UpdateRings(); };
                ringTimer.Start();
            }

            Thread worker = new Thread(Loop);
            worker.IsBackground = true;
            worker.Name = "VlcBorderless-watch";
            worker.Start();
        }

        /// UI thread: keep one invisible resize ring on each window we manage (position, visibility, z-order).
        void UpdateRings()
        {
            try
            {
                IntPtr[] list = mgr.ManagedWindows();
                HashSet<long> live = new HashSet<long>();
                foreach (IntPtr h in list)
                {
                    if (!Native.IsWindow(h)) continue;
                    long key = h.ToInt64();
                    live.Add(key);
                    ResizeRing ring;
                    if (!rings.TryGetValue(key, out ring)) { ring = new ResizeRing(h); rings[key] = ring; }

                    bool shown = Native.IsWindowVisible(h) && !Native.IsIconic(h) && !Native.IsZoomed(h) && !TitleBarManager.IsFullscreen(h);
                    if (!shown) { if (ring.Visible) ring.Hide(); continue; }

                    Native.RECT r;
                    if (!Native.GetWindowRect(h, out r)) continue;
                    bool topmost = (unchecked((uint)Native.GetWindowLongPtr(h, Native.GWL_EXSTYLE).ToInt64()) & Native.WS_EX_TOPMOST) != 0;
                    if (!ring.IsResizing) ring.Follow(r);
                    if (!ring.Visible) ring.Show();
                    ring.PlaceAbove(topmost);
                }
                List<long> stale = new List<long>();
                foreach (long k in rings.Keys) if (!live.Contains(k)) stale.Add(k);
                foreach (long k in stale) { rings[k].Close(); rings.Remove(k); }
            }
            catch (Exception e) { Log.Write("ring update error: " + e.Message); }
        }

        void CloseRings()
        {
            ringTimer.Stop();
            foreach (ResizeRing ring in rings.Values) { try { ring.Close(); } catch { } }
            rings.Clear();
        }

        static Icon LoadIcon()
        {
            try
            {
                foreach (Process p in Process.GetProcessesByName("vlc"))
                {
                    try { string f = p.MainModule.FileName; Icon i = Icon.ExtractAssociatedIcon(f); if (i != null) return i; }
                    catch { }
                    finally { p.Dispose(); }
                }
            }
            catch { }
            return SystemIcons.Application;
        }

        /// Worker thread: a 10 Hz tick that handles VLC windows.
        ///
        /// By default the helper belongs to ONE VLC - the one it was started for (the extension ticked in it). The moment that
        /// VLC closes, the helper exits, so a VLC relaunched straight away starts with its normal title bar and the option
        /// shows unticked, as it is. Ticking the extension in a second VLC adds that one (a second launch signals this helper).
        /// With --linger the helper instead handles every VLC that appears, for as long as it runs.
        void Loop()
        {
            HashSet<uint> bound = new HashSet<uint>();
            HashSet<uint> everyVlc = new HashSet<uint>();
            bool everHadVlc = false, vlcClosed = false;
            int tick = 0, noVlcTicks = 0;
            try
            {
                while (!stopping)
                {
                    if (stopEvent.WaitOne(100)) { Log.Write("stop requested (unticked in VLC, or --stop) - putting the title bar back"); break; }   // otherwise a 10 Hz tick

                    HashSet<uint> pids;
                    if (linger)
                    {
                        if (everyVlc.Count == 0 || tick++ % 10 == 0) everyVlc = TitleBarManager.VlcPids();   // process list about once a second
                        if (everyVlc.Count == 0) continue;
                        pids = everyVlc;
                    }
                    else
                    {
                        if (addEvent.WaitOne(0))                                 // the extension was ticked in another VLC as well
                        {
                            uint extra = TitleBarManager.TopmostVlcPid(bound);
                            if (extra != 0 && bound.Add(extra)) { everHadVlc = true; Log.Write("also handling VLC " + extra); }
                        }
                        bound.RemoveWhere(delegate(uint p) { return !TitleBarManager.IsAlive(p); });
                        if (bound.Count == 0)
                        {
                            if (everHadVlc) { Log.Write("the VLC this helper was started for has closed - exiting"); vlcClosed = true; break; }
                            uint first = TitleBarManager.TopmostVlcPid(null);    // just started: bind to the VLC the user is in
                            if (first != 0) { bound.Add(first); everHadVlc = true; Log.Write("handling VLC " + first); }
                            else if (++noVlcTicks >= 50) { Log.Write("no VLC running - exiting"); break; }   // started with no VLC at all: give up after 5 s
                            continue;
                        }
                        pids = bound;
                    }
                    try { mgr.Scan(pids); } catch (Exception e) { Log.Write("scan error: " + e.Message); }
                }
            }
            finally
            {
                try { mgr.RestoreAll(); } catch (Exception e) { Log.Write("restore error: " + e.Message); }
                if (!vlcClosed && !linger && bound.Count > 0)
                {
                    // Stopped (untick, tray menu) while its VLC is still running. If that VLC is itself in the middle of closing, give it
                    // a moment to finish so its saved window state can still be cleaned; a plain untick leaves it running and we move on.
                    for (int i = 0; i < 10 && !vlcClosed; i++)
                    {
                        bound.RemoveWhere(delegate(uint p) { return !TitleBarManager.IsAlive(p); });
                        if (bound.Count == 0) vlcClosed = true; else Thread.Sleep(100);
                    }
                }
                if (vlcClosed && VlcState.Enabled)
                {
                    // VLC wrote its saved window state while closing. If it was closed fullscreen, undo just that flag, so the next
                    // VLC does not start fullscreen (no title bar, nothing to grab). Only when no VLC is left that could rewrite it.
                    try
                    {
                        for (int i = 0; i < 20 && TitleBarManager.VlcPids().Count > 0; i++) Thread.Sleep(100);
                        if (TitleBarManager.VlcPids().Count == 0) Log.Write("VLC's saved window state: " + VlcState.ClearSavedFullscreen(VlcState.DefaultIni));
                    }
                    catch (Exception e) { Log.Write("saved-state cleanup error: " + e.Message); }
                }
                try { marshal.BeginInvoke(new Action(delegate { tray.Visible = false; drag.Dispose(); CloseRings(); ExitThread(); })); }
                catch { Environment.Exit(0); }
            }
        }
    }

    internal static class Program
    {
        static bool Has(string[] args, string flag)
        {
            foreach (string a in args) if (string.Equals(a, flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// Extra options from %LOCALAPPDATA%\VlcBorderless\options.txt (flags separated by spaces / lines; "#" starts a comment),
        /// so a mode can be chosen even when the helper is started by the VLC extension, which passes no flags.
        static string[] WithOptionsFile(string[] args)
        {
            try
            {
                string f = Path.Combine(Paths.LocalAppData, "VlcBorderless", "options.txt");
                if (!File.Exists(f)) return args;
                List<string> all = new List<string>(args);
                foreach (string line in File.ReadAllLines(f))
                {
                    string text = line;
                    int hash = text.IndexOf('#');
                    if (hash >= 0) text = text.Substring(0, hash);
                    foreach (string token in text.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)) all.Add(token);
                }
                return all.ToArray();
            }
            catch { return args; }
        }

        [STAThread]
        static int Main(string[] args)
        {
            args = WithOptionsFile(args);
            Log.Enabled = Has(args, "--log");
            if (Has(args, "--keep-saved-fullscreen")) VlcState.Enabled = false;
            if (Has(args, "--no-autofit")) TitleBarManager.AutoFitEnabled = false;
            if (Has(args, "--fix-vlc-start"))
            {
                // Repair: clear a saved "fullscreen" so VLC starts in a normal window. --ini PATH repairs another file.
                string ini = VlcState.DefaultIni;
                for (int i = 0; i + 1 < args.Length; i++) if (string.Equals(args[i], "--ini", StringComparison.OrdinalIgnoreCase)) ini = args[i + 1];
                bool custom = !string.Equals(ini, VlcState.DefaultIni, StringComparison.OrdinalIgnoreCase);
                string result = (!custom && TitleBarManager.VlcPids().Count > 0) ? "VLC is running - close it first (it would write its state again when it closes)" : VlcState.ClearSavedFullscreen(ini);
                try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "vlcborderless-fix.txt"), result); } catch { }
                Log.Write("--fix-vlc-start: " + result);
                return result.StartsWith("cleared") || result.StartsWith("not saved") ? 0 : 1;
            }
            try { Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }     // per-monitor v2: real pixels on every display

            EventWaitHandle stopEvent = new EventWaitHandle(false, EventResetMode.ManualReset, @"Local\VlcBorderless.Stop");
            if (Has(args, "--stop")) { Log.Write("--stop received"); stopEvent.Set(); return 0; }
            EventWaitHandle addEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\VlcBorderless.Add");
            try { Directory.CreateDirectory(Path.GetDirectoryName(VideoAspect.HintFile)); } catch { }   // make sure the data folder exists

            bool first;
            using (Mutex single = new Mutex(true, @"Local\VlcBorderless.Instance", out first))
            {
                if (!first) { addEvent.Set(); return 0; }                                // a helper is already running: tell it about this VLC
                stopEvent.Reset();
                addEvent.Reset();
                Log.Write("started");
                Application.EnableVisualStyles();
                bool nativeResize = Has(args, "--native-resize");              // old behaviour: keep Windows' resize border
                bool grips = !nativeResize && !Has(args, "--no-resize");       // default: frameless + aspect-locked grips
                Application.Run(new TrayApp(new TitleBarManager(!nativeResize), stopEvent, addEvent, Has(args, "--linger"), grips));
                Log.Write("exited");
            }
            return 0;
        }
    }
}
