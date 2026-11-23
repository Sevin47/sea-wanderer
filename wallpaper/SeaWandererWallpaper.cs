// Sea Wanderer as a live desktop wallpaper.
//
// Opens the game in a frameless Edge window (Edge ships with Windows) and tucks that window behind the
// desktop icons, the same "WorkerW" trick the wallpaper apps use. A low-level mouse hook passes clicks,
// drags and the wheel on empty desktop through to the game. The wallpaper hides itself (so the game
// pauses) while a maximised or full-screen window covers it. A tray icon has Pause, Reload and Quit.
//
// Built with the C# compiler that comes with Windows: run build.cmd.
// Options: --left N / --bottom N (pixels to keep clear), and for testing --debug-port N (Edge remote
// debugging) and --always-show (never hide when covered).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

static class SeaWandererWallpaper
{
    static string gamePath, profileDir, edgePath;
    static int leftArg = -1, bottomArg = -1, debugPort = 0;
    static Process edge;
    static IntPtr edgeWnd, workerW;
    static Rectangle screenRect;
    static bool paused, hiddenForCover, quitting, alwaysShow;
    static NotifyIcon tray;
    static MenuItem pauseItem;
    static IntPtr hook;
    static LowLevelMouseProc hookProc;
    static bool dragging;
    static int wheelAcc;
    static bool primed;
    static IntPtr focusTo;
    static System.Windows.Forms.Timer focusTimer;
    static System.Windows.Forms.Timer watch;
    static int failures;

    [STAThread]
    static void Main(string[] args)
    {
        bool fresh;
        var one = new Mutex(true, "SeaWandererWallpaper.once", out fresh);
        if (!fresh) { MessageBox.Show("Sea Wanderer is already running as the wallpaper. Its icon is in the tray, by the clock.", "Sea Wanderer"); return; }
        SetProcessDPIAware();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--left") leftArg = int.Parse(args[i + 1]);
            if (args[i] == "--bottom") bottomArg = int.Parse(args[i + 1]);
            if (args[i] == "--debug-port") debugPort = int.Parse(args[i + 1]);
        }
        alwaysShow = Array.IndexOf(args, "--always-show") >= 0;
        string here = AppDomain.CurrentDomain.BaseDirectory;
        gamePath = Path.GetFullPath(Path.Combine(here, "..", "index.html"));
        if (!File.Exists(gamePath)) { MessageBox.Show("Can't find the game at " + gamePath, "Sea Wanderer"); return; }
        profileDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SeaWandererWallpaper", "edge");
        try { Directory.CreateDirectory(profileDir); File.Delete(Path.Combine(Path.GetDirectoryName(profileDir), "wallpaper.log")); } catch { }
        edgePath = FindEdge();
        if (edgePath == null) { MessageBox.Show("Microsoft Edge wasn't found, and the wallpaper needs it.", "Sea Wanderer"); return; }

        Application.EnableVisualStyles();
        MakeTray();
        // at sign-in the desktop may not be up yet
        for (var t = Stopwatch.StartNew(); t.ElapsedMilliseconds < 60000 && FindDefView() == IntPtr.Zero; ) Thread.Sleep(500);
        if (!Attach()) { Cleanup(); return; }
        focusTimer = new System.Windows.Forms.Timer { Interval = 250 };
        focusTimer.Tick += (s, e) => { focusTimer.Stop(); GiveFocusBack(focusTo); };
        hookProc = MouseHook;
        hook = SetWindowsHookEx(WH_MOUSE_LL, hookProc, GetModuleHandle(null), 0);
        watch = new System.Windows.Forms.Timer { Interval = 1000 };
        watch.Tick += (s, e) => Watch();
        watch.Start();
        Application.ApplicationExit += (s, e) => Cleanup();
        Application.Run();
    }

    // ---------- launching and parenting ----------
    static bool Attach()
    {
        workerW = FindWorkerW();
        Log("attach: workerW=" + workerW.ToString("X") + " class=" + ClassOf(workerW));
        if (workerW == IntPtr.Zero) { MessageBox.Show("Couldn't find the desktop layer behind the icons.", "Sea Wanderer"); return false; }
        screenRect = Screen.PrimaryScreen.Bounds;
        IntPtr before = GetForegroundWindow();
        if (edge == null || edge.HasExited || !IsWindow(edgeWnd))
        {
            KillOurEdge();
            edge = Process.Start(new ProcessStartInfo(edgePath, EdgeArgs()) { UseShellExecute = false });
            edgeWnd = IntPtr.Zero;
            var t = Stopwatch.StartNew();
            while (edgeWnd == IntPtr.Zero && t.ElapsedMilliseconds < 45000) { Thread.Sleep(150); edgeWnd = FindEdgeWindow(); }
            Log("edge pid=" + edge.Id + " exited=" + edge.HasExited + " window=" + edgeWnd.ToString("X"));
            if (edgeWnd == IntPtr.Zero) { MessageBox.Show("Edge didn't open the game window.", "Sea Wanderer"); return false; }
        }
        // strip the frame, make it a child of the layer behind the icons, and stretch it over the screen
        long style = GetWindowLongPtr(edgeWnd, GWL_STYLE).ToInt64();
        style &= ~(long)(WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | WS_MAXIMIZEBOX | WS_MINIMIZEBOX | WS_POPUP);
        style |= WS_CHILD;
        SetWindowLongPtr(edgeWnd, GWL_STYLE, new IntPtr(style));
        long ex = GetWindowLongPtr(edgeWnd, GWL_EXSTYLE).ToInt64();
        ex &= ~(long)(WS_EX_APPWINDOW | WS_EX_WINDOWEDGE | WS_EX_CLIENTEDGE | WS_EX_DLGMODALFRAME);
        ex |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
        SetWindowLongPtr(edgeWnd, GWL_EXSTYLE, new IntPtr(ex));
        SetParent(edgeWnd, workerW);
        Place();
        hiddenForCover = false;
        Thread.Sleep(300);
        GiveFocusBack(before);
        primed = false;
        return true;
    }
    // a full-screen Edge takes the keyboard when it opens; hand it back to whatever had it
    static void GiveFocusBack(IntPtr to)
    {
        IntPtr fg = GetForegroundWindow();
        if (fg != edgeWnd && !IsChild(edgeWnd, fg)) return;
        if (to == IntPtr.Zero || to == edgeWnd || IsChild(edgeWnd, to) || !IsWindow(to)) to = DesktopTop();
        uint pid, fgThread = GetWindowThreadProcessId(fg, out pid), me = GetCurrentThreadId();
        AttachThreadInput(me, fgThread, true);
        SetForegroundWindow(to);
        AttachThreadInput(me, fgThread, false);
    }
    static IntPtr FindDefView()
    {
        IntPtr dv = IntPtr.Zero;
        EnumWindows((h, l) => { dv = FindWindowEx(h, IntPtr.Zero, "SHELLDLL_DefView", null); return dv == IntPtr.Zero; }, IntPtr.Zero);
        return dv;
    }
    // the window holding the desktop icons: clicking the desktop normally makes it the active window
    static IntPtr DesktopTop()
    {
        IntPtr top = IntPtr.Zero;
        EnumWindows((h, l) => { if (FindWindowEx(h, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero) { top = h; return false; } return true; }, IntPtr.Zero);
        return top != IntPtr.Zero ? top : FindWindow("Progman", null);
    }
    // Edge makes itself the active window when it takes a click; a moment later, give that back
    static void FocusBackSoon(IntPtr to)
    {
        focusTo = to;
        focusTimer.Stop(); focusTimer.Start();
    }
    // the game only takes keys (the wheel arrives as keys) once Edge has had a click; give it one at the screen's edge
    static void PrimeKeys()
    {
        var p = new POINT { x = screenRect.Right - 3, y = screenRect.Top + screenRect.Height / 2 };
        IntPtr was = GetForegroundWindow();
        Forward(WM_MOUSEMOVE, 0, p); Forward(WM_LBUTTONDOWN, MK_LBUTTON, p); Forward(WM_LBUTTONUP, 0, p);
        primed = true;
        FocusBackSoon(was);
    }
    static void Place()
    {
        var pts = new POINT[] { new POINT { x = screenRect.Left, y = screenRect.Top } };
        MapWindowPoints(IntPtr.Zero, workerW, pts, 1);
        uint vis = paused || hiddenForCover ? SWP_HIDEWINDOW : SWP_SHOWWINDOW;
        SetWindowPos(edgeWnd, IntPtr.Zero, pts[0].x, pts[0].y, screenRect.Width, screenRect.Height, SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED | vis);
        // Edge keeps a resize border inside its window even without a frame: grow the window so the page itself fills the screen
        RECT c; GetClientRect(edgeWnd, out c);
        var o = new POINT(); ClientToScreen(edgeWnd, ref o);
        int padL = o.x - screenRect.Left, padT = o.y - screenRect.Top, padR = screenRect.Width - padL - c.right, padB = screenRect.Height - padT - c.bottom;
        if (padL != 0 || padT != 0 || padR != 0 || padB != 0)
            SetWindowPos(edgeWnd, IntPtr.Zero, pts[0].x - padL, pts[0].y - padT, screenRect.Width + padL + padR, screenRect.Height + padT + padB, SWP_NOZORDER | SWP_NOACTIVATE | vis);
    }
    static string EdgeArgs()
    {
        var wa = Screen.PrimaryScreen.WorkingArea;
        int bottom = bottomArg >= 0 ? bottomArg : Math.Max(0, screenRect.Bottom - wa.Bottom);
        int left = leftArg >= 0 ? leftArg : Math.Max(IconColumnsWidth(), wa.Left - screenRect.Left);
        string url = new Uri(gamePath).AbsoluteUri + "?wallpaper&left=" + left + "&bottom=" + bottom;
        var a = new StringBuilder();
        a.Append("--user-data-dir=\"").Append(profileDir).Append("\" ");
        a.Append("--app=\"").Append(url).Append("\" ");
        // full screen, or Edge draws its own title bar inside the page area
        a.Append("--start-fullscreen --window-position=" + screenRect.Left + "," + screenRect.Top + " --window-size=" + screenRect.Width + "," + screenRect.Height + " ");
        a.Append("--no-first-run --no-default-browser-check --disable-sync --hide-crash-restore-bubble ");
        // the tray app decides when it is covered; Edge's own guess is wrong for a window behind the icons
        a.Append("--disable-features=CalculateNativeWinOcclusion,msEdgeSidebarV2,msHubApps --disable-backgrounding-occluded-windows ");
        if (debugPort > 0) a.Append("--remote-debugging-port=" + debugPort + " --remote-allow-origins=* ");
        return a.ToString();
    }
    static IntPtr FindWorkerW()
    {
        IntPtr progman = FindWindow("Progman", null);
        IntPtr res;
        // ask the desktop to raise its wallpaper layer, as it does for a slideshow fade
        SendMessageTimeout(progman, 0x052C, new IntPtr(0xD), new IntPtr(1), SMTO_NORMAL, 1000, out res);
        SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, SMTO_NORMAL, 1000, out res);
        IntPtr w = IntPtr.Zero;
        EnumWindows((top, l) =>
        {
            if (FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
                w = FindWindowEx(IntPtr.Zero, top, "WorkerW", null);
            return true;
        }, IntPtr.Zero);
        return w != IntPtr.Zero ? w : progman;
    }
    static IntPtr FindEdgeWindow()
    {
        IntPtr found = IntPtr.Zero;
        uint pid = (uint)edge.Id;
        EnumWindows((h, l) =>
        {
            uint p; GetWindowThreadProcessId(h, out p);
            if (p == pid && IsWindowVisible(h) && ClassOf(h) == "Chrome_WidgetWin_1" && GetWindowTextLength(h) > 0) { found = h; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }
    // an Edge left over from an earlier run with this profile would swallow the new launch
    static void KillOurEdge()
    {
        try
        {
            using (var q = new ManagementObjectSearcher("SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'msedge.exe'"))
                foreach (ManagementObject o in q.Get())
                {
                    var cl = o["CommandLine"] as string;
                    if (cl != null && cl.IndexOf(profileDir, StringComparison.OrdinalIgnoreCase) >= 0)
                        try { Process.GetProcessById((int)(uint)o["ProcessId"]).Kill(); } catch { }
                }
        }
        catch { }
    }
    static string FindEdge()
    {
        foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) })
        {
            var p = Path.Combine(root, @"Microsoft\Edge\Application\msedge.exe");
            if (File.Exists(p)) return p;
        }
        return null;
    }
    // how wide the columns of desktop icons are, so the game's panels can sit to their right
    static int IconColumnsWidth()
    {
        IntPtr lv = DesktopListView();
        if (lv == IntPtr.Zero || !IsWindowVisible(lv)) return 0;
        int count = SendMessage(lv, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero).ToInt32();
        if (count <= 0) return 0;
        int sp = SendMessage(lv, LVM_GETITEMSPACING, IntPtr.Zero, IntPtr.Zero).ToInt32();
        int cx = sp & 0xffff, cy = (sp >> 16) & 0xffff;
        if (cx <= 0 || cy <= 0) return 0;
        int rows = Math.Max(1, Screen.PrimaryScreen.WorkingArea.Height / cy);
        int cols = (count + rows - 1) / rows;
        return cols * cx + 8;
    }
    static IntPtr DesktopListView()
    {
        IntPtr lv = IntPtr.Zero;
        EnumWindows((top, l) =>
        {
            IntPtr dv = FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (dv != IntPtr.Zero) { lv = FindWindowEx(dv, IntPtr.Zero, "SysListView32", null); return false; }
            return true;
        }, IntPtr.Zero);
        return lv;
    }

    // ---------- every second: still attached? covered? ----------
    static void Watch()
    {
        if (quitting) return;
        bool lost = edge == null || edge.HasExited || !IsWindow(edgeWnd) || GetParent(edgeWnd) != workerW;
        if (lost)
        {
            Log("lost: edgeExited=" + (edge == null || edge.HasExited) + " isWindow=" + IsWindow(edgeWnd) + " parent=" + GetParent(edgeWnd).ToString("X"));
            // Explorer restarted, or Edge closed: start over, but give up after repeated failures
            if (++failures > 5) { tray.ShowBalloonTip(5000, "Sea Wanderer", "The wallpaper keeps failing to start, so it has stopped.", ToolTipIcon.Warning); Application.Exit(); return; }
            if (!IsWindow(edgeWnd)) edgeWnd = IntPtr.Zero;
            // relaunching can take seconds; the mouse hook must not stall the pointer meanwhile
            if (hook != IntPtr.Zero) { UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
            dragging = false;
            if (Attach()) failures = 0;
            hook = SetWindowsHookEx(WH_MOUSE_LL, hookProc, GetModuleHandle(null), 0);
            return;
        }
        if (Screen.PrimaryScreen.Bounds != screenRect) { screenRect = Screen.PrimaryScreen.Bounds; Place(); }
        bool covered = !alwaysShow && Covered();
        if (covered != hiddenForCover) { hiddenForCover = covered; Log(covered ? "covered: hiding" : "uncovered: showing"); Show(); }
    }
    static void Show() { ShowWindow(edgeWnd, paused || hiddenForCover ? SW_HIDE : SW_SHOWNOACTIVATE); }
    // a maximised or full-screen window on the main screen hides the desktop, so the game may rest
    static bool Covered()
    {
        bool cov = false;
        EnumWindows((h, l) =>
        {
            if (!IsWindowVisible(h) || IsIconic(h) || h == workerW) return true;
            string c = ClassOf(h);
            if (c == "Progman" || c == "WorkerW" || c == "Shell_TrayWnd" || c == "Shell_SecondaryTrayWnd") return true;
            long ex = GetWindowLongPtr(h, GWL_EXSTYLE).ToInt64();
            if ((ex & (WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE)) != 0) return true;
            int cloaked; if (DwmGetWindowAttribute(h, DWMWA_CLOAKED, out cloaked, 4) == 0 && cloaked != 0) return true;
            RECT r; if (!GetWindowRect(h, out r)) return true;
            var wr = Rectangle.FromLTRB(r.left, r.top, r.right, r.bottom);
            var mon = Screen.FromHandle(h);
            if (!mon.Primary) return true;
            if (IsZoomed(h) || (wr.Left <= mon.Bounds.Left && wr.Top <= mon.Bounds.Top && wr.Right >= mon.Bounds.Right && wr.Bottom >= mon.Bounds.Bottom))
            { cov = true; return false; }
            return true;
        }, IntPtr.Zero);
        return cov;
    }

    // ---------- the mouse on empty desktop goes to the game ----------
    static IntPtr MouseHook(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && !paused && !hiddenForCover && edgeWnd != IntPtr.Zero)
        {
            var m = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
            int msg = wParam.ToInt32();
            if (msg == WM_MOUSEMOVE)
            {
                if (dragging || OverDesktop(m.pt)) Forward(WM_MOUSEMOVE, dragging ? MK_LBUTTON : 0, m.pt);
            }
            else if (msg == WM_LBUTTONDOWN)
            {
                if (OverDesktop(m.pt) && !OverIcon(m.pt)) { dragging = true; primed = true; Forward(WM_MOUSEMOVE, 0, m.pt); Forward(WM_LBUTTONDOWN, MK_LBUTTON, m.pt); return new IntPtr(1); }
            }
            else if (msg == WM_LBUTTONUP)
            {
                if (dragging) { dragging = false; Forward(WM_LBUTTONUP, 0, m.pt); FocusBackSoon(DesktopTop()); return new IntPtr(1); }
            }
            else if (msg == WM_MOUSEWHEEL && OverDesktop(m.pt))
            {
                // Edge throws away wheel messages when another program's window is under the pointer, which the
                // icon layer always is; so each notch goes as F13 (in) or F14 (out) and the game zooms on the pointer
                if (!primed) PrimeKeys();
                Forward(WM_MOUSEMOVE, 0, m.pt);
                wheelAcc += (short)((m.mouseData >> 16) & 0xffff);
                while (Math.Abs(wheelAcc) >= 120)
                {
                    int vk = wheelAcc > 0 ? VK_F13 : VK_F14;
                    PostMessage(edgeWnd, WM_KEYDOWN, new IntPtr(vk), new IntPtr(0x00640001));
                    PostMessage(edgeWnd, WM_KEYUP, new IntPtr(vk), new IntPtr(unchecked((int)0xC0640001)));
                    wheelAcc -= Math.Sign(wheelAcc) * 120;
                }
                return new IntPtr(1);
            }
        }
        return CallNextHookEx(hook, code, wParam, lParam);
    }
    static void Forward(int msg, int keys, POINT screen)
    {
        var p = screen; ScreenToClient(edgeWnd, ref p);
        PostMessage(edgeWnd, msg, new IntPtr(keys), MakeLParam(p.x, p.y));
    }
    static IntPtr MakeLParam(int x, int y) { return new IntPtr((y << 16) | (x & 0xffff)); }
    // the pointer is over the desktop itself (icons layer or empty desktop), not over some window
    static bool OverDesktop(POINT pt)
    {
        IntPtr h = WindowFromPoint(pt);
        if (h == IntPtr.Zero) return false;
        if (h == edgeWnd || IsChild(edgeWnd, h)) return false;    // icons hidden: the game already gets it directly
        string c = ClassOf(h);
        return c == "SysListView32" || c == "SHELLDLL_DefView" || c == "WorkerW" || c == "Progman";
    }
    // a desktop icon under the pointer keeps its clicks
    static bool OverIcon(POINT pt)
    {
        try
        {
            Accessibility.IAccessible acc; object child;
            if (AccessibleObjectFromPoint(pt, out acc, out child) != 0 || acc == null) return false;
            object role = acc.get_accRole(child);
            return role is int && (int)role == ROLE_SYSTEM_LISTITEM;
        }
        catch { return false; }
    }

    // ---------- tray ----------
    static void MakeTray()
    {
        var menu = new ContextMenu();
        pauseItem = new MenuItem("Pause", (s, e) => { paused = !paused; pauseItem.Checked = paused; Show(); });
        menu.MenuItems.Add(pauseItem);
        menu.MenuItems.Add(new MenuItem("Reload", (s, e) => { if (edge != null && !edge.HasExited) try { edge.Kill(); } catch { } }));
        menu.MenuItems.Add("-");
        menu.MenuItems.Add(new MenuItem("Quit", (s, e) => Application.Exit()));
        tray = new NotifyIcon { Icon = MakeIcon(), Text = "Sea Wanderer wallpaper", ContextMenu = menu, Visible = true };
    }
    static Icon MakeIcon()
    {
        var b = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(b))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillEllipse(new SolidBrush(Color.FromArgb(18, 52, 58)), 1, 1, 30, 30);
            g.FillPolygon(new SolidBrush(Color.FromArgb(240, 214, 140)), new[] { new Point(15, 6), new Point(15, 21), new Point(7, 21) });
            g.FillPolygon(new SolidBrush(Color.FromArgb(214, 188, 120)), new[] { new Point(17, 9), new Point(17, 21), new Point(24, 21) });
            g.FillPolygon(new SolidBrush(Color.FromArgb(120, 78, 40)), new[] { new Point(6, 23), new Point(26, 23), new Point(22, 27), new Point(10, 27) });
        }
        return Icon.FromHandle(b.GetHicon());
    }

    static void Cleanup()
    {
        if (quitting) return;
        quitting = true;
        if (watch != null) watch.Stop();
        if (hook != IntPtr.Zero) { UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
        if (edgeWnd != IntPtr.Zero && IsWindow(edgeWnd)) PostMessage(edgeWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);   // lets the game save
        if (edge != null) { try { if (!edge.WaitForExit(4000)) edge.Kill(); } catch { } }
        KillOurEdge();
        if (workerW != IntPtr.Zero) RedrawWindow(workerW, IntPtr.Zero, IntPtr.Zero, RDW_INVALIDATE | RDW_ERASE | RDW_ALLCHILDREN | RDW_UPDATENOW);
        if (tray != null) { tray.Visible = false; tray.Dispose(); }
    }

    static void Log(string m)
    {
        try { File.AppendAllText(Path.Combine(Path.GetDirectoryName(profileDir), "wallpaper.log"), DateTime.Now.ToString("HH:mm:ss ") + m + Environment.NewLine); } catch { }
    }
    static string ClassOf(IntPtr h) { var sb = new StringBuilder(64); GetClassName(h, sb, 64); return sb.ToString(); }

    // ---------- Win32 ----------
    const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
    const long WS_CAPTION = 0xC00000, WS_THICKFRAME = 0x40000, WS_SYSMENU = 0x80000, WS_MAXIMIZEBOX = 0x10000, WS_MINIMIZEBOX = 0x20000,
        WS_POPUP = 0x80000000L, WS_CHILD = 0x40000000;
    const long WS_EX_APPWINDOW = 0x40000, WS_EX_WINDOWEDGE = 0x100, WS_EX_CLIENTEDGE = 0x200, WS_EX_DLGMODALFRAME = 0x1,
        WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x8000000, WS_EX_TRANSPARENT = 0x20;
    const uint SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10, SWP_FRAMECHANGED = 0x20, SWP_SHOWWINDOW = 0x40, SWP_HIDEWINDOW = 0x80;
    const int SW_HIDE = 0, SW_SHOWNOACTIVATE = 4;
    const uint SMTO_NORMAL = 0;
    const int WH_MOUSE_LL = 14, WM_MOUSEMOVE = 0x200, WM_LBUTTONDOWN = 0x201, WM_LBUTTONUP = 0x202, WM_MOUSEWHEEL = 0x20A, WM_CLOSE = 0x10, MK_LBUTTON = 1,
        WM_KEYDOWN = 0x100, WM_KEYUP = 0x101, VK_F13 = 0x7C, VK_F14 = 0x7D;
    const uint LLMHF_INJECTED = 1;
    const int LVM_GETITEMCOUNT = 0x1004, LVM_GETITEMSPACING = 0x1033;
    const int ROLE_SYSTEM_LISTITEM = 0x22, DWMWA_CLOAKED = 14;
    const uint GA_ROOT = 2;
    const uint RDW_INVALIDATE = 0x1, RDW_ERASE = 0x4, RDW_ALLCHILDREN = 0x80, RDW_UPDATENOW = 0x100;

    [StructLayout(LayoutKind.Sequential)] struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int left, top, right, bottom; }
    [StructLayout(LayoutKind.Sequential)] struct MSLLHOOKSTRUCT { public POINT pt; public int mouseData; public uint flags; public uint time; public IntPtr extra; }
    delegate bool EnumWindowsProc(IntPtr h, IntPtr l);
    delegate IntPtr LowLevelMouseProc(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h, int msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsChild(IntPtr parent, IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr h, uint flags);
    [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder sb, int n);
    [DllImport("user32.dll")] static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll")] static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
    [DllImport("user32.dll")] static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern int MapWindowPoints(IntPtr from, IntPtr to, [In, Out] POINT[] pts, int n);
    [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT p);
    [DllImport("user32.dll")] static extern bool RedrawWindow(IntPtr h, IntPtr rect, IntPtr rgn, uint flags);
    [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id, LowLevelMouseProc proc, IntPtr mod, uint thread);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr w, IntPtr l);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint a, uint b, bool attach);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int value, int size);
    [DllImport("oleacc.dll")] static extern int AccessibleObjectFromPoint(POINT pt, [MarshalAs(UnmanagedType.Interface)] out Accessibility.IAccessible acc, [MarshalAs(UnmanagedType.Struct)] out object child);
}
