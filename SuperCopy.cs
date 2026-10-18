// =====================================================================
//  SuperCopy.cs  -  lightweight "Super Copier" for Windows XP .. 11
//  C# 2.0 / .NET Framework 2.0, compiled with csc.exe (no Visual Studio)
//
//  Modes (command line):
//    SuperCopy.exe                    open Settings (install / remove menu)
//    SuperCopy.exe /install           add Explorer right-click entries
//    SuperCopy.exe /uninstall         remove them
//    SuperCopy.exe /copy <paths...>   remember items (silent)
//    SuperCopy.exe /cut  <paths...>   remember items for a move (silent)
//    SuperCopy.exe /paste <folder>    copy/move remembered items here
//    SuperCopy.exe /queue             run jobs from the hand-off queue (elevated retry, no args)
//
//  Contents (top to bottom):
//    Program ......... entry point, command-line modes
//    Paths ........... %APPDATA% locations
//    Settings ........ settings.ini load/save
//    ClipStore ....... clipboard (items remembered by /copy and /cut)
//    QueueStore ...... hand-off queue for a running instance
//    Bitness ......... 32/64-bit detection
//    Admin ........... UAC elevation (retry access-denied jobs as administrator)
//    Updates ......... manual update check against GitHub (update.txt)
//    ShellIntegration  Explorer right-click menu install/remove
//    Theme/Fmt/Gfx/IconMaker  colours, drawing helpers, tray icon
//    CopyItem/CopyJob  copy engine (scan, workers, DoCopy/CopyData/RoboCopy)
//    BufferedListView/HeaderPanel/TitleBar/UI  small UI controls
//    ConflictPrompt .. "Ask first" hook
//    MainForm ........ progress window
//    SettingsForm .... settings window
//    ConflictDialog .. "file already exists" prompt
// =====================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("Super Copy")]
[assembly: System.Reflection.AssemblyDescription("Lightweight multi-threaded file copier")]
[assembly: System.Reflection.AssemblyCompany("youcef07dz")]
[assembly: System.Reflection.AssemblyProduct("Super Copy")]
[assembly: System.Reflection.AssemblyCopyright("Copyright (C) 2026 youcef07dz")]
// major part must mirror Updates.Current (v1 = 1.0.0.0); bump both + update.txt on release
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]

namespace SuperCopyApp
{
    // ================================================================
    //  Program entry (kept tiny so /copy and /cut start very fast:
    //  the WinForms code is only JIT-compiled when a window is needed)
    // ================================================================
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            string mode = args.Length > 0 ? args[0].ToLower() : "/settings";
            List<string> rest = new List<string>();
            for (int i = 1; i < args.Length; i++)
            {
                string a = args[i].Trim('"');
                if (a.Length > 0) rest.Add(a);
            }

            Paths.Init();

            if (mode == "/selftest") return CopyJob.SelfTest();

            if (mode == "/copy" || mode == "/cut")
            {
                try { if (rest.Count > 0) ClipStore.Add(mode == "/cut", rest.ToArray()); }
                catch { }
                return 0;
            }
            return Gui(mode, rest);
        }

        static int Gui(string mode, List<string> rest)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Settings.Load();
            L.Apply(Settings.Language);
            try
            {
                switch (mode)
                {
                    case "/paste":
                        return DoPaste(string.Join(" ", rest.ToArray()));
                    case "/todevice":
                        return DoDevice(rest);
                    case "/queue":
                        Application.Run(new MainForm(null));   // PollQueue feeds it the queued retry jobs
                        return 0;
                    case "/install":
                        ShellIntegration.Install(Application.ExecutablePath);
                        Msg.Show(L.T("Explorer menu installed.", "تم تثبيت قائمة المستكشف.", "Menu de l'Explorateur installé."), "Super Copy", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return 0;
                    case "/uninstall":
                        ShellIntegration.Uninstall();
                        Msg.Show(L.T("Explorer menu removed.", "تمت إزالة قائمة المستكشف.", "Menu de l'Explorateur supprimé."), "Super Copy", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return 0;
                    default:
                        Application.Run(new SettingsForm());
                        return 0;
                }
            }
            catch (Exception ex)
            {
                Msg.Show(ex.Message, "Super Copy", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        static int DoPaste(string dest)
        {
            dest = dest.Trim().Trim('"');
            // "D:\" arrives as  D:"  because of the quote-escaping rule
            if (dest.Length == 2 && dest[1] == ':') dest += "\\";
            if (dest.Length == 0 || !Directory.Exists(dest))
            {
                Msg.Show(L.T("Destination folder not found:\n", "مجلد الوجهة غير موجود:\n", "Dossier de destination introuvable :\n") + dest, "Super Copy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 1;
            }

            bool cut;
            List<string> src = ClipStore.Read(out cut);
            if (src.Count == 0)
            {
                Msg.Show(L.T("Nothing to paste.\nRight-click a file or folder and choose \"Super Copy\" first.",
                                    "لا يوجد ما يتم لصقه.\nانقر بزر الماوس الأيمن على ملف أو مجلد واختر \"Super Copy\" أولاً.",
                                    "Rien à coller.\nFaites un clic droit sur un fichier ou dossier et choisissez « Super Copy » d'abord."),
                    "Super Copy", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }

            bool created;
            Mutex m = new Mutex(true, "SuperCopy_MainInstance_v1", out created);
            if (!created)
            {
                // another window is already running: hand the job over through the queue folder
                QueueStore.Enqueue(cut, dest, src);
                if (cut) ClipStore.Clear();
                m.Close();
                return 0;
            }
            try
            {
                if (cut) ClipStore.Clear();
                Application.Run(new MainForm(new CopyJob(src, dest, cut)));
            }
            finally
            {
                try { m.ReleaseMutex(); } catch { }
                m.Close();
            }
            return 0;
        }

        static int DoDevice(List<string> rest)
        {
            // collect this window's selection into a device-only clip so a pending /copy is untouched
            try { if (rest.Count > 0) ClipStore.Add(false, rest.ToArray(), Paths.ClipDevice); } catch { }

            bool created;
            Mutex m = new Mutex(true, "SuperCopy_MainInstance_v1", out created);
            if (!created) { m.Close(); return 0; } // the owning window shows one picker for the whole selection
            try
            {
                // wait for the other selected items to finish merging (~800 ms quiet, max 3 s)
                // shortcut: a window starting >~800 ms in is missed, matching ClipStore.Add's 3 s merge heuristic
                DateTime last = DateTime.MinValue;
                int waited = 0;
                while (waited < 3000)
                {
                    if (!File.Exists(Paths.ClipDevice)) break;
                    DateTime w = File.GetLastWriteTime(Paths.ClipDevice);
                    if (w == last && (DateTime.Now - w).TotalMilliseconds >= 800) break;
                    last = w;
                    Thread.Sleep(200);
                    waited += 200;
                }

                bool cut;
                List<string> src = ClipStore.Read(out cut, Paths.ClipDevice);
                ClipStore.Clear(Paths.ClipDevice);
                if (src.Count == 0)
                {
                    Msg.Show(L.T("Nothing to copy.\nRight-click a file or folder and choose \"Copy to dest...\" first.",
                                        "لا يوجد ما يتم نسخه.\nانقر بزر الماوس الأيمن على ملف أو مجلد واختر \"نسخ إلى مجلد...\" أولاً.",
                                        "Rien à copier.\nFaites un clic droit sur un fichier ou dossier et choisissez « Copier vers... » d'abord."),
                        "Super Copy", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }

                FolderBrowserDialog fb = new FolderBrowserDialog();
                fb.Description = L.T("Choose the destination folder", "اختر مجلد الوجهة", "Choisissez le dossier de destination");
                if (fb.ShowDialog() != DialogResult.OK || fb.SelectedPath == null || fb.SelectedPath.Length == 0) return 0;

                Application.Run(new MainForm(new CopyJob(src, fb.SelectedPath, false)));
            }
            finally
            {
                try { m.ReleaseMutex(); } catch { }
                m.Close();
            }
            return 0;
        }
    }

    // MessageBox with RTL reading order/alignment when Arabic is active.
    static class Msg
    {
        static MessageBoxOptions Opts
        {
            get { return L.Rtl ? (MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign) : 0; }
        }

        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            return MessageBox.Show(text, caption, buttons, icon, MessageBoxDefaultButton.Button1, Opts);
        }

        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            return MessageBox.Show(owner, text, caption, buttons, icon, MessageBoxDefaultButton.Button1, Opts);
        }
    }

    // ================================================================
    //  Paths + Settings
    // ================================================================
    static class Paths
    {
        public static string AppDir, ClipFile, ClipDevice, QueueDir, Ini;

        public static void Init()
        {
            string ad = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (ad == null || ad.Length == 0) ad = Path.GetTempPath();
            AppDir = Path.Combine(ad, "SuperCopy");
            ClipFile = Path.Combine(AppDir, "clipboard.txt");
            ClipDevice = Path.Combine(AppDir, "clipboard.device.txt"); // separate so it never clobbers a pending /copy
            QueueDir = Path.Combine(AppDir, "queue");
            Ini = Path.Combine(AppDir, "settings.ini");
            try { Directory.CreateDirectory(QueueDir); } catch { }
        }

        // write via a temp file, then overwrite; a crash mid-write cannot truncate the real file
        public static void WriteAllLinesSafe(string path, string[] lines, Encoding enc)
        {
            string tmp = path + ".tmp";
            try { File.Delete(tmp); } catch { }
            if (enc == null) File.WriteAllLines(tmp, lines); else File.WriteAllLines(tmp, lines, enc);
            File.Copy(tmp, path, true);
            try { File.Delete(tmp); } catch { }
        }
    }

    static class Settings
    {
        const int DefaultConcurrency = 2;             // single source of truth for "Reset to default"
        const int DefaultBufferKB = 256;
        const int DefaultConflict = 0;
        const int DefaultEngine = 0;

        public static volatile int Concurrency = DefaultConcurrency;   // simultaneous file copies (1..32) when smart disabled
        public static volatile bool SmartConcurrency = true;           // use file-size based concurrency (default)
        public static volatile int BufferKB = DefaultBufferKB;         // copy buffer per thread
        public static volatile int Conflict = DefaultConflict;         // 0 overwrite, 1 skip, 2 keep both, 3 overwrite if newer, 4 ask
        public static volatile int AskSession = -1;   // "ask first" answer kept for this run only (-1 = none yet)
        public static int AskPrompting;               // 1 while a worker owns the "ask first" dialog (Interlocked)
        public static volatile int Engine = DefaultEngine;             // 0 built-in, 1 robocopy
        public static bool RobocopyAvailable { get { try { return File.Exists(Path.Combine(Environment.SystemDirectory, "robocopy.exe")); } catch { return false; } } }
        public static bool AutoClose = false;
        public static bool Compact = false;           // start in compact (header-only) mode
        public static volatile int Language = 0;      // 0 auto (OS), 1 English, 2 Arabic, 3 French

        static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }

        public static void Load()
        {
            try
            {
                if (!File.Exists(Paths.Ini)) return;
                foreach (string line in File.ReadAllLines(Paths.Ini))
                {
                    int i = line.IndexOf('=');
                    if (i <= 0) continue;
                    string k = line.Substring(0, i).Trim().ToLower();
                    string v = line.Substring(i + 1).Trim();
                    int n;
                    switch (k)
                    {
                        case "concurrency": if (int.TryParse(v, out n)) Concurrency = Clamp(n, 1, 32); break;
                        case "smartconcurrency": SmartConcurrency = (v == "1"); break;
                        case "bufferkb": if (int.TryParse(v, out n)) BufferKB = Clamp(n, 16, 16384); break;
                        case "conflict": if (int.TryParse(v, out n)) Conflict = Clamp(n, 0, 4); break;
                        case "engine": if (int.TryParse(v, out n)) Engine = Clamp(n, 0, 1); break;
                        case "autoclose": AutoClose = (v == "1"); break;
                        case "compact": Compact = (v == "1"); break;
                        case "language": if (int.TryParse(v, out n)) Language = Clamp(n, 0, 3); break;
                    }
                }
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                Paths.WriteAllLinesSafe(Paths.Ini, new string[] {
                    "Concurrency=" + Concurrency,
                    "SmartConcurrency=" + (SmartConcurrency ? "1" : "0"),
                    "BufferKB=" + BufferKB,
                    "Conflict=" + Conflict,
                    "Engine=" + Engine,
                    "AutoClose=" + (AutoClose ? "1" : "0"),
                    "Compact=" + (Compact ? "1" : "0"),
                    "Language=" + Language }, null);
            }
            catch { }
        }

        // Resets every value shown in the Settings window. Compact is a view mode
        // toggled from the main window, not a settings field, so it is left alone.
        public static void ResetDefaults()
        {
            Concurrency = DefaultConcurrency;
            SmartConcurrency = true;
            BufferKB = DefaultBufferKB;
            Conflict = DefaultConflict;
            Engine = DefaultEngine;
            AutoClose = false;
            Language = 0;
        }
    }

    // ================================================================
    //  Localization: English / Arabic / French (0 = auto from the OS)
    // ================================================================
    static class L
    {
        // resolved language: 1 English, 2 Arabic, 3 French
        public static int Code = 1;

        public static void Apply(int setting)
        {
            Code = setting;
            if (setting == 0)
            {
                try
                {
                    string c = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLower();
                    Code = c == "ar" ? 2 : (c == "fr" ? 3 : 1);
                }
                catch { Code = 1; }
            }
            if (Code < 1 || Code > 3) Code = 1;
        }

        public static string T(string en, string ar, string fr)
        {
            return Code == 2 ? ar : (Code == 3 ? fr : en);
        }

        public static bool Rtl { get { return Code == 2; } }

        // custom-drawn text: swap the physical alignment in RTL
        public static TextFormatFlags Left
        {
            get { return Rtl ? TextFormatFlags.Right : TextFormatFlags.Left; }
        }
        public static TextFormatFlags Right
        {
            get { return Rtl ? TextFormatFlags.Left : TextFormatFlags.Right; }
        }

        // standard controls mirror themselves; custom paint checks L.Rtl.
        // fullLayout also mirrors the form's control positions (dialogs only).
        public static void ApplyRtl(Form f, bool fullLayout)
        {
            f.RightToLeft = Rtl ? RightToLeft.Yes : RightToLeft.No;
            if (fullLayout) f.RightToLeftLayout = Rtl;
        }
    }

    // ================================================================
    //  "Clipboard" (list of items remembered by Super Copy / Super Cut)
    // ================================================================
    static class ClipStore
    {
        public static void Add(bool cut, string[] paths) { Add(cut, paths, Paths.ClipFile); }

        public static void Add(bool cut, string[] paths, string f)
        {
            Mutex m = new Mutex(false, "SuperCopy_ClipLock_v1");
            bool owned = false;
            try
            {
                try { owned = m.WaitOne(3000, false); } catch { }
                string mode = cut ? "cut" : "copy";
                List<string> list = new List<string>();
                // clicks that arrive within 3 s of each other belong to one multi-selection
                if (File.Exists(f) && (DateTime.Now - File.GetLastWriteTime(f)).TotalMilliseconds < 3000)
                {
                    string[] old = File.ReadAllLines(f, Encoding.UTF8);
                    if (old.Length > 0 && old[0] == mode)
                        for (int i = 1; i < old.Length; i++) if (old[i].Length > 0) list.Add(old[i]);
                }
                for (int i = 0; i < paths.Length; i++)
                    if (!list.Contains(paths[i])) list.Add(paths[i]);

                string[] lines = new string[list.Count + 1];
                lines[0] = mode;
                list.CopyTo(lines, 1);
                Paths.WriteAllLinesSafe(f, lines, Encoding.UTF8);
            }
            finally
            {
                if (owned) { try { m.ReleaseMutex(); } catch { } }
                m.Close();
            }
        }

        public static List<string> Read(out bool cut) { return Read(out cut, Paths.ClipFile); }

        public static List<string> Read(out bool cut, string f)
        {
            cut = false;
            List<string> r = new List<string>();
            try
            {
                if (!File.Exists(f)) return r;
                string[] ln = File.ReadAllLines(f, Encoding.UTF8);
                if (ln.Length > 0)
                {
                    cut = (ln[0] == "cut");
                    for (int i = 1; i < ln.Length; i++) if (ln[i].Length > 0) r.Add(ln[i]);
                }
            }
            catch { }
            return r;
        }

        public static void Clear() { Clear(Paths.ClipFile); }

        public static void Clear(string f)
        {
            try { File.Delete(f); } catch { }
        }
    }

    // ================================================================
    //  Hand-off queue (a second SuperCopy instance drops a .job file
    //  that the already-running instance picks up)
    // ================================================================
    static class QueueStore
    {
        public static void Enqueue(bool cut, string dest, List<string> src)
        {
            List<string> lines = new List<string>();
            lines.Add(cut ? "cut" : "copy");
            lines.Add(dest);
            lines.AddRange(src);
            string name = DateTime.Now.Ticks.ToString("D19") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
            string tmp = Path.Combine(Paths.QueueDir, name + ".tmp");
            string fin = Path.Combine(Paths.QueueDir, name + ".job");
            File.WriteAllLines(tmp, lines.ToArray(), Encoding.UTF8);
            File.Move(tmp, fin);
        }
    }

    // ================================================================
    //  Windows bitness (works on .NET 2.0: IsWow64Process P/Invoke)
    // ================================================================
    static class Bitness
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")]
        static extern bool IsWow64Process(IntPtr hProcess, out bool wow64);

        public static readonly bool Is64Os;

        static Bitness()
        {
            Is64Os = IntPtr.Size == 8;      // 64-bit process => 64-bit OS
            if (!Is64Os)
            {
                try
                {
                    bool wow;               // 32-bit process on 64-bit OS = WOW64
                    if (IsWow64Process(GetCurrentProcess(), out wow)) Is64Os = wow;
                }
                catch { }
            }
        }
    }

    // ================================================================
    //  Elevation (UAC): detect admin, relaunch elevated for retries
    // ================================================================
    static class Admin
    {
        public static bool IsElevated
        {
            get
            {
                try
                {
                    using (WindowsIdentity id = WindowsIdentity.GetCurrent())
                        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
                }
                catch { return false; }
            }
        }

        // starts a second elevated instance (/queue runs the hand-off queue); false when UAC is declined
        public static bool Relaunch()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(Application.ExecutablePath, "/queue");
                psi.UseShellExecute = true;     // Verb=runas needs the shell
                psi.Verb = "runas";
                Process.Start(psi);
                return true;
            }
            catch { return false; }
        }
    }

    // ================================================================
    //  Manual update check: update.txt on GitHub holds the latest version number
    // ================================================================
    static class Updates
    {
        public const int Current = 1;   // must match AssemblyVersion's major part
        const string UpdateTxtUrl = "https://raw.githubusercontent.com/youcef07dz/super-copy/main/update.txt";
        const string ReleasesUrl = "https://github.com/youcef07dz/super-copy/releases";

        public static int ParseVersion(string s)
        {
            if (s == null) return 0;
            int v;
            return int.TryParse(s.Trim(), out v) ? v : 0;
        }

        // short timeout so the startup check cannot hang on a dead connection
        class QuickWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                WebRequest r = base.GetWebRequest(address);
                r.Timeout = 5000;
                return r;
            }
        }

        // silent fetch for the startup check; -1 = unreachable/invalid
        public static int GetLatest()
        {
            try
            {
                try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; } catch { }  // TLS 1.2
                using (WebClient wc = new QuickWebClient())
                    return ParseVersion(wc.DownloadString(UpdateTxtUrl));
            }
            catch { return -1; }
        }

        // assembly major version of a file (mirrors Current); 0 = unreadable
        public static int FileVer(string path)
        {
            try { return FileVersionInfo.GetVersionInfo(path).FileMajorPart; }
            catch { return 0; }
        }

        public static void Check(Form owner)
        {
            int latest;
            try
            {
                try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; } catch { }  // TLS 1.2 (needed for GitHub)
                using (WebClient wc = new WebClient())
                    latest = ParseVersion(wc.DownloadString(UpdateTxtUrl));
            }
            catch (Exception ex)
            {
                Msg.Show(owner, L.T("Could not check for updates:\n", "تعذّر التحقق من التحديثات:\n", "Impossible de vérifier les mises à jour :\n") + ex.Message, "Super Copy",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (latest <= Current)
            {
                Msg.Show(owner, L.T("You already have the latest version (v", "لديك بالفعل أحدث إصدار (v", "Vous avez déjà la dernière version (v") + Current + ").", "Super Copy",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            OfferUpdate(owner, latest);
        }

        // "version vX is available": open the release page in the browser.
        // shortcut: no in-app download-and-swap (that dropper pattern is what AV
        // engines flag); the user downloads the new exe from the page themselves
        public static void OfferUpdate(Form owner, int latest)
        {
            if (Msg.Show(owner,
                L.T("Version v", "الإصدار v", "La version v") + latest +
                L.T(" is available (you have v", " متاح (لديك v", " est disponible (vous avez v") + Current + ").\n\n" +
                L.T("Open the download page?", "هل تريد فتح صفحة التنزيل؟", "Ouvrir la page de téléchargement ?"),
                "Super Copy", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            OpenUrl(owner, ReleasesUrl);
        }

        static void OpenUrl(Form owner, string url)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(url);
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Msg.Show(owner, L.T("Could not open the browser:\n", "تعذّر فتح المتصفح:\n", "Impossible d'ouvrir le navigateur :\n") + ex.Message, "Super Copy",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // ================================================================
    //  Explorer context menu (HKCU\Software\Classes - no admin needed)
    // ================================================================
    static class ShellIntegration
    {
        [DllImport("shell32.dll")]
        static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        // flat top-level verbs only: a static cascade (ExtendedSubCommandsKey)
        // disappears from the menu on multi-selections over ~15 items even with
        // MultiSelectModel=Player, while flat Player verbs keep working (MS docs:
        // legacy Player verbs show up to 100 items); paste is empty-space only
        static readonly string[] AllKeys = new string[] {
            @"*\shell\SuperCopy",
            @"*\shell\SuperCut",
            @"*\shell\SuperToDevice",
            @"Directory\shell\SuperCopy",
            @"Directory\shell\SuperCut",
            @"Directory\shell\SuperToDevice",
            @"Directory\Background\shell\SuperPaste" };

        // retired keys: cascade leftovers and older flat names
        static readonly string[] OldKeys = new string[] {
            @"*\shell\SuperCopy.Copy",
            @"*\shell\SuperCopy.Cut",
            @"Directory\shell\SuperCopy.Copy",
            @"Directory\shell\SuperCopy.Cut",
            @"Directory\shell\SuperCopy.Paste",
            @"Directory\shell\SuperPaste",
            @"Directory\Background\shell\SuperCopy.Paste",
            @"Directory\Background\shell\SuperCopy",
            @"Drive\shell\SuperCopy.Paste",
            @"Drive\shell\SuperCopy",
            @"Drive\shell\SuperPaste" };

        static void Verb(RegistryKey cls, string key, string label, string ico, string q, string arg)
        {
            using (RegistryKey k = cls.CreateSubKey(key))
            {
                k.SetValue("", label);
                k.SetValue("Icon", ico);
                k.SetValue("MultiSelectModel", "Player");
                using (RegistryKey c = k.CreateSubKey("command"))
                    c.SetValue("", q + " " + arg);
            }
        }

        public static bool IsInstalled()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Classes\*\shell\SuperCopy\command"))
                    return k != null;   // shared HKCU keys are visible to 32- and 64-bit Explorer
            }
            catch { return false; }
        }

        // registers the menu verbs to point at "exe" (no self-copy into %APPDATA%)
        public static void Install(string exe)
        {
            string q = "\"" + exe + "\"";
            string ico = "\"" + exe + "\",0";   // the icon is embedded in the exe
            using (RegistryKey cls = Registry.CurrentUser.CreateSubKey(@"Software\Classes"))
            {
                DeleteAll(cls, AllKeys, false);   // wipe previous trees; AddVerbs recreates them
                DeleteAll(cls, OldKeys, false);   // drop retired flat entries and leftovers
                AddVerbs(cls, false, ico, q);
                // 64-bit OS mirror for 32-bit shell hosts (Wow6432Node view)
                if (Bitness.Is64Os)
                {
                    DeleteAll(cls, AllKeys, true);
                    DeleteAll(cls, OldKeys, true);
                    AddVerbs(cls, true, ico, q);
                }
            }
            Notify();
        }

        static void AddVerbs(RegistryKey cls, bool wow, string ico, string q)
        {
            string p = wow ? @"Wow6432Node\" : "";
            foreach (string root in new string[] { @"*\shell", @"Directory\shell" })
            {
                Verb(cls, p + root + @"\SuperCopy", L.T("Super Copy", "نسخ فائق", "Super copie"), ico, q, "/copy \"%1\"");
                Verb(cls, p + root + @"\SuperCut", L.T("Super Cut (move)", "قص فائق (نقل)", "Super coupe (déplacer)"), ico, q, "/cut \"%1\"");
                Verb(cls, p + root + @"\SuperToDevice", L.T("Copy to dest...", "نسخ إلى مجلد...", "Copier vers..."), ico, q, "/todevice \"%1\"");
            }
            // empty space only: %V is the folder itself
            Verb(cls, p + @"Directory\Background\shell\SuperPaste", L.T("Super Paste here", "لصق هنا", "Coller ici"), ico, q, "/paste \"%V\"");
        }

        public static void Uninstall()
        {
            using (RegistryKey cls = Registry.CurrentUser.CreateSubKey(@"Software\Classes"))
            {
                DeleteAll(cls, AllKeys, false);
                DeleteAll(cls, OldKeys, false);
                if (Bitness.Is64Os)
                {
                    DeleteAll(cls, AllKeys, true);
                    DeleteAll(cls, OldKeys, true);
                }
            }
            Notify();
        }

        static void DeleteAll(RegistryKey cls, string[] paths, bool wow)
        {
            string p = wow ? @"Wow6432Node\" : "";
            for (int i = 0; i < paths.Length; i++)
            {
                try { cls.DeleteSubKeyTree(p + paths[i]); } catch { }
            }
        }

        static void Notify()
        {
            try { SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero); } catch { }
        }
    }

    // ================================================================
    //  Theme + drawing helpers (everything is drawn with GDI+, no images)
    // ================================================================
    static class Theme
    {
        // "Default" palette (everything is drawn with GDI+, no images).
        public static readonly Color Bg = Color.FromArgb(20, 24, 40);
        public static readonly Color HeadA = Color.FromArgb(42, 50, 88);
        public static readonly Color HeadB = Color.FromArgb(26, 31, 54);
        public static readonly Color Panel = Color.FromArgb(30, 36, 60);
        public static readonly Color Panel2 = Color.FromArgb(46, 55, 92);
        public static readonly Color Row1 = Color.FromArgb(24, 29, 48);
        public static readonly Color Row2 = Color.FromArgb(29, 35, 58);
        public static readonly Color Sel = Color.FromArgb(52, 68, 118);
        public static readonly Color Text = Color.FromArgb(226, 231, 247);
        public static readonly Color Dim = Color.FromArgb(138, 148, 180);
        public static readonly Color Track = Color.FromArgb(13, 16, 30);
        public static readonly Color Border = Color.FromArgb(64, 75, 112);
        public static readonly Color Cyan = Color.FromArgb(0, 200, 255);
        public static readonly Color Violet = Color.FromArgb(150, 92, 255);
        public static readonly Color Green = Color.FromArgb(70, 220, 130);
        public static readonly Color Orange = Color.FromArgb(255, 170, 60);
        public static readonly Color Red = Color.FromArgb(255, 90, 100);

        public static readonly SolidBrush BrTrack = new SolidBrush(Track);
        public static readonly SolidBrush BrRow1 = new SolidBrush(Row1);
        public static readonly SolidBrush BrRow2 = new SolidBrush(Row2);
        public static readonly SolidBrush BrSel = new SolidBrush(Sel);
        public static readonly SolidBrush BrPanel = new SolidBrush(Panel);
        public static readonly SolidBrush BrGloss = new SolidBrush(Color.FromArgb(40, 255, 255, 255));

        public static readonly Font Cell = new Font("Tahoma", 8.25f);
        public static readonly Font Bold = new Font("Tahoma", 8.25f, FontStyle.Bold);
        public static readonly Font Title = new Font("Tahoma", 11f, FontStyle.Bold);
        public static readonly Font Big = new Font("Tahoma", 12f, FontStyle.Bold);

        // state: 0 pending, 1 copying, 2 done, 3 error, 4 skipped, 5 cancelled
        public static void BarColors(int state, bool paused, out Color a, out Color b)
        {
            switch (state)
            {
                case 1:
                    if (paused) { a = Orange; b = Color.FromArgb(255, 210, 110); }
                    else { a = Color.FromArgb(40, 110, 255); b = Cyan; }
                    break;
                case 2: a = Color.FromArgb(30, 170, 100); b = Color.FromArgb(120, 240, 160); break;
                case 3: a = Color.FromArgb(200, 40, 60); b = Color.FromArgb(255, 120, 100); break;
                case 4: a = Color.FromArgb(90, 95, 140); b = Color.FromArgb(150, 155, 200); break;
                case 5: a = Color.FromArgb(210, 120, 30); b = Color.FromArgb(255, 190, 90); break;
                default: a = Color.FromArgb(70, 80, 120); b = Color.FromArgb(90, 100, 140); break;
            }
        }
    }

    static class Fmt
    {
        public static string Size(long b)
        {
            if (b < 0) b = 0;
            if (b < 1024) return b + " B";
            double v = b / 1024.0;
            string[] u = new string[] { "KB", "MB", "GB", "TB" };
            int i = 0;
            while (v >= 1024 && i < 3) { v /= 1024; i++; }
            return v.ToString(v >= 100 ? "0" : (v >= 10 ? "0.0" : "0.00")) + " " + u[i];
        }

        public static string Time(double sec)
        {
            if (double.IsNaN(sec) || double.IsInfinity(sec) || sec < 0 || sec > 8640000) return "--:--";
            int s = (int)sec;
            int h = s / 3600, m = (s / 60) % 60, ss = s % 60;
            if (h > 0) return h + ":" + m.ToString("00") + ":" + ss.ToString("00");
            return m.ToString("00") + ":" + ss.ToString("00");
        }
    }

    static class Gfx
    {
        public static GraphicsPath Round(Rectangle r, int rad)
        {
            GraphicsPath p = new GraphicsPath();
            if (rad <= 0 || r.Width < rad * 2 || r.Height < rad * 2) { p.AddRectangle(r); return p; }
            int d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // Colorful progress bar: rounded track, gradient fill, gloss, centered text.
        public static void Bar(Graphics g, Rectangle r, double frac, Color c1, Color c2,
                               string text, bool marquee, int tick, int rad)
        {
            if (r.Width < 6 || r.Height < 5) return;
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle tr = new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1);
            using (GraphicsPath path = Round(tr, rad))
            {
                g.FillPath(Theme.BrTrack, path);
                GraphicsState st = g.Save();
                g.SetClip(path, CombineMode.Intersect);

                Rectangle fr = Rectangle.Empty;
                if (marquee)
                {
                    int bw = Math.Max(30, tr.Width / 4);
                    int x = (tick * 16) % (tr.Width + bw) - bw;
                    if (L.Rtl) x = tr.Width - x - bw;
                    fr = new Rectangle(tr.X + x, tr.Y, bw, tr.Height);
                }
                else
                {
                    if (frac < 0) frac = 0;
                    if (frac > 1) frac = 1;
                    int w = (int)Math.Round(tr.Width * frac);
                    if (w > 0) fr = L.Rtl ? new Rectangle(tr.Right - w, tr.Y, w, tr.Height)
                                          : new Rectangle(tr.X, tr.Y, w, tr.Height);
                }
                if (fr.Width > 0)
                {
                    Rectangle gr = marquee ? fr : tr;
                    using (LinearGradientBrush lb = new LinearGradientBrush(gr, c1, c2, LinearGradientMode.Horizontal))
                        g.FillRectangle(lb, fr);
                    g.FillRectangle(Theme.BrGloss, new Rectangle(fr.X, fr.Y, fr.Width, fr.Height / 2));
                }
                g.Restore(st);
                using (Pen p = new Pen(Theme.Border)) g.DrawPath(p, path);
            }
            g.SmoothingMode = old;

            if (text != null && text.Length > 0)
            {
                TextFormatFlags fl = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                                     TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
                TextRenderer.DrawText(g, text, Theme.Bold, new Rectangle(r.X + 1, r.Y + 1, r.Width, r.Height), Color.FromArgb(0, 0, 0), fl);
                TextRenderer.DrawText(g, text, Theme.Bold, r, Color.White, fl);
            }
        }
    }

    static class IconMaker
    {
        public static Bitmap MakeBitmap(int size)
        {
            Bitmap b = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                Rectangle r = new Rectangle(0, 0, size - 1, size - 1);
                using (GraphicsPath p = Gfx.Round(r, size / 4))
                using (LinearGradientBrush lb = new LinearGradientBrush(r, Theme.Cyan, Theme.Violet, 45f))
                    g.FillPath(lb, p);

                float s = size;
                PointF[] c1 = new PointF[] {
                    new PointF(.26f * s, .26f * s), new PointF(.50f * s, .50f * s), new PointF(.26f * s, .74f * s),
                    new PointF(.38f * s, .74f * s), new PointF(.62f * s, .50f * s), new PointF(.38f * s, .26f * s) };
                PointF[] c2 = new PointF[c1.Length];
                for (int i = 0; i < c1.Length; i++) c2[i] = new PointF(c1[i].X + .16f * s, c1[i].Y);
                g.FillPolygon(Brushes.White, c1);
                g.FillPolygon(Brushes.White, c2);
            }
            return b;
        }

        public static Icon MakeIcon()
        {
            using (Bitmap b = MakeBitmap(32))
                return Icon.FromHandle(b.GetHicon());
        }
    }

    // ================================================================
    //  Copy engine
    // ================================================================
    class CopyItem
    {
        public string Src, Dst, Name, Error;
        public long Size;
        public long Done;                 // bytes copied (Interlocked)
        public volatile int State;        // 0 pending, 1 copying, 2 done, 3 error, 4 skipped, 5 cancelled
        public int Index;
        public double Speed;              // bytes/s (UI thread)
        public long SampBytes;

        public double Fraction
        {
            get
            {
                if (State == 2 || State == 4) return 1.0;
                if (Size <= 0) return 0.0;
                double f = (double)Interlocked.Read(ref Done) / Size;
                return f > 1 ? 1 : (f < 0 ? 0 : f);
            }
        }
    }

    class CopyJob
    {
        public readonly List<string> Sources;
        public readonly string Dest;
        public readonly bool Move;
        public readonly string Summary;

        readonly List<CopyItem> items = new List<CopyItem>();
        readonly List<CopyItem> active = new List<CopyItem>();
        readonly List<string> srcDirs = new List<string>();
        readonly object sync = new object();
        readonly object activeLock = new object();

        public long TotalBytes, DoneBytes;
        public int Errors, FilesDone;
        public volatile bool ScanDone, Paused, Cancelled, Finished, Started;
        public volatile bool AccessDenied;   // at least one item failed with UnauthorizedAccessException
        public string LastError;
        public double Speed;
        public int ActiveMs;
        long lastDone;
        int next, workers;
        long spaceLimit = -1, needBytes;   // destination free space (bytes); -1 = unknown
        bool noSpace;                      // scanner-only: true once the queued bytes exceed free space

        public CopyJob(List<string> sources, string dest, bool move)
        {
            Sources = sources; Dest = dest; Move = move;
            string first = Path.GetFileName(sources[0].TrimEnd('\\', '/'));
            if (first == null || first.Length == 0) first = sources[0];
            string what = sources.Count == 1 ? "\"" + first + "\"" : sources.Count + " " + L.T("items", "عنصر", "éléments");
            Summary = what + " " + L.T("to", "إلى", "vers") + " " + dest;
        }

        public long Total { get { return Interlocked.Read(ref TotalBytes); } }
        public long DoneNow { get { return Interlocked.Read(ref DoneBytes); } }

        public int ItemCount { get { lock (sync) { return items.Count; } } }

        public CopyItem GetItem(int i)
        {
            lock (sync) { return (i >= 0 && i < items.Count) ? items[i] : null; }
        }

        public double Fraction
        {
            get
            {
                long t = Total;
                if (t <= 0) return Finished ? 1.0 : 0.0;
                double f = (double)DoneNow / t;
                return f > 1 ? 1 : (f < 0 ? 0 : f);
            }
        }

        public double Eta
        {
            get
            {
                if (Speed < 1) return -1;
                return (Total - DoneNow) / Speed;
            }
        }

        public int FirstActiveIndex()
        {
            int min = -1;
            lock (activeLock)
            {
                for (int i = 0; i < active.Count; i++)
                    if (min < 0 || active[i].Index < min) min = active[i].Index;
            }
            return min;
        }

        // ---------- control ----------
        public static int GetSmartCopyConcurrency(long bytes)
        {
            if (bytes <= 0) return 1;
            if (bytes < 500000)
            {
                double p = bytes / 500000.0;
                double c = 10 - p * 5;
                int r = (int)Math.Round(c);
                if (r < 1) r = 1;
                if (r > 10) r = 10;
                return r;
            }
            if (bytes <= 100000000)
            {
                double span = 100000000 - 500000;
                double pos = bytes - 500000;
                double p2 = span <= 0 ? 1.0 : (pos / span);
                double c2 = 5 - p2 * 4;
                int r2 = (int)Math.Round(c2);
                if (r2 < 1) r2 = 1;
                if (r2 > 5) r2 = 5;
                return r2;
            }
            return 1;
        }

        public int GetTargetConcurrency(CopyItem it)
        {
            if (!Settings.SmartConcurrency) return Math.Max(1, Math.Min(32, Settings.Concurrency));
            int c = GetSmartCopyConcurrency(it == null ? 0L : it.Size);
            return Math.Max(1, Math.Min(32, c));
        }

        public void Start()
        {
            Started = true;
            Settings.AskSession = -1;
            Settings.AskPrompting = 0;
            int n = Settings.SmartConcurrency ? 1 : Math.Max(1, Math.Min(32, Settings.Concurrency));
            lock (sync) { workers += n; }
            Thread st = new Thread(new ThreadStart(ScanMain), 256 * 1024);
            st.IsBackground = true;
            st.Start();
            for (int i = 0; i < n; i++) SpawnWorker();
        }

        void SpawnWorker()
        {
            Thread t = new Thread(new ThreadStart(WorkerMain), 256 * 1024);
            t.IsBackground = true;
            t.Start();
        }

        // lets the "concurrent copies" setting grow while a job is running
        public void EnsureWorkers()
        {
            if (!Started || Finished || Cancelled) return;
            bool spawn = false;
            int targetTotal = Settings.Concurrency;
            if (Settings.SmartConcurrency)
            {
                targetTotal = 0;
                CopyItem[] actArr;
                lock (activeLock) { actArr = active.ToArray(); }
                foreach (CopyItem ci in actArr)
                {
                    if (ci != null) targetTotal = Math.Max(targetTotal, GetSmartCopyConcurrency(ci.Size));
                }
                if (targetTotal == 0) targetTotal = 1;
            }
            lock (sync)
            {
                if (workers > 0 && workers < targetTotal && next < items.Count)
                {
                    workers++;
                    spawn = true;
                }
            }
            if (spawn) SpawnWorker();
        }

        public void Cancel()
        {
            Cancelled = true;
            if (!Started) { Started = true; ScanDone = true; Finished = true; }
        }

        // called from the UI timer: speed + per-file speed + active time
        public void Sample(int dt)
        {
            long done = DoneNow;
            if (Paused) Speed = 0;
            else
            {
                double inst = (done - lastDone) * 1000.0 / dt;
                double a = 1.0 - Math.Exp(-dt / 1500.0);
                Speed = Speed <= 0 ? inst : Speed + (inst - Speed) * a;
                ActiveMs += dt;
            }
            lastDone = done;

            CopyItem[] arr;
            lock (activeLock) { arr = active.ToArray(); }
            double ai = 1.0 - Math.Exp(-dt / 1000.0);
            for (int i = 0; i < arr.Length; i++)
            {
                CopyItem it = arr[i];
                long d = Interlocked.Read(ref it.Done);
                if (Paused) it.Speed = 0;
                else
                {
                    double v = (d - it.SampBytes) * 1000.0 / dt;
                    it.Speed = it.Speed <= 0 ? v : it.Speed + (v - it.Speed) * ai;
                }
                it.SampBytes = d;
            }
        }

        // ---------- helpers ----------
        static string Norm(string p)
        {
            try { return Path.GetFullPath(p).TrimEnd('\\', '/'); } catch { return p.TrimEnd('\\', '/'); }
        }
        static bool SamePath(string a, string b)
        {
            return string.Equals(Norm(a), Norm(b), StringComparison.OrdinalIgnoreCase);
        }
        static bool IsSubOrSame(string path, string parent)
        {
            string a = Norm(path), b = Norm(parent);
            return a.Equals(b, StringComparison.OrdinalIgnoreCase) ||
                   a.StartsWith(b + "\\", StringComparison.OrdinalIgnoreCase);
        }
        static bool SameRoot(string a, string b)
        {
            try
            {
                return string.Equals(Path.GetPathRoot(Path.GetFullPath(a)), Path.GetPathRoot(Path.GetFullPath(b)),
                                     StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }
        static string UniqueFile(string path)
        {
            string dir = Path.GetDirectoryName(path);
            string nm = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);
            for (int n = 2; n < 100000; n++)
            {
                string c = Path.Combine(dir, nm + " (" + n + ")" + ext);
                if (!File.Exists(c) && !Directory.Exists(c)) return c;
            }
            return path;
        }
        static string UniqueDir(string parent, string name)
        {
            string c = Path.Combine(parent, name);
            for (int n = 2; (Directory.Exists(c) || File.Exists(c)) && n < 100000; n++)
                c = Path.Combine(parent, name + " (" + n + ")");
            return c;
        }

        // available free space on the drive holding path, or -1 when unknown (UNC, bad path)
        static long FreeSpace(string path)
        {
            try { return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))).AvailableFreeSpace; }
            catch { return -1; }
        }
        static string HashFile(string path)
        {
            using (MD5 md5 = MD5.Create())
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                byte[] h = md5.ComputeHash(fs);
                StringBuilder sb = new StringBuilder(h.Length * 2);
                for (int i = 0; i < h.Length; i++) sb.Append(h[i].ToString("x2"));
                return sb.ToString();
            }
        }
        // false on any read error: the safe direction (never delete the source on doubt)
        static bool SameHash(string a, string b)
        {
            try { return HashFile(a) == HashFile(b); }
            catch { return false; }
        }

        // ---------- scanner thread ----------
        void AddFile(string src, string dst, string name)
        {
            long size = 0;
            try { size = new FileInfo(src).Length; } catch { }
            CopyItem it = new CopyItem();
            it.Src = src; it.Dst = dst; it.Name = name; it.Size = size;
            lock (sync) { it.Index = items.Count; items.Add(it); }
            Interlocked.Add(ref TotalBytes, size);

            // free-space guard: only count bytes that actually add usage on the destination drive.
            // Overwrites and same-drive moves reuse existing space, so exclude them.
            if (spaceLimit >= 0 && !noSpace && !File.Exists(dst) && !(Move && SameRoot(src, dst)))
            {
                needBytes += size;
                if (needBytes > spaceLimit)
                {
                    noSpace = true;
                    AddFinished("Destination", 3, "Not enough free space on destination (" +
                                Fmt.Size(needBytes) + " needed, " + Fmt.Size(spaceLimit) + " free)");
                }
            }
        }

        void AddFinished(string name, int state, string msg)
        {
            CopyItem it = new CopyItem();
            it.Name = name; it.State = state; it.Error = msg;
            lock (sync) { it.Index = items.Count; items.Add(it); }
            Interlocked.Increment(ref FilesDone);
            if (state == 3) { Interlocked.Increment(ref Errors); LastError = name + ": " + msg; }
        }

        // One line per failed item, for the "Error report" popup.
        public string ErrorReport()
        {
            StringBuilder sb = new StringBuilder();
            lock (sync)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    CopyItem it = items[i];
                    if (it.State != 3) continue;
                    sb.Append(it.Name);
                    sb.Append(": ");
                    sb.Append(it.Error == null ? L.T("unknown error", "خطأ غير معروف", "erreur inconnue") : it.Error);
                    sb.Append("\r\n");
                }
            }
            if (sb.Length == 0 && LastError != null) sb.Append(LastError);
            return sb.ToString();
        }

        // access denied is the one failure a UAC relaunch can fix
        internal static bool IsAccessDenied(Exception ex) { return ex is UnauthorizedAccessException; }

        // assert-based self-check for ErrorReport (run: SuperCopy.exe /selftest -> exit 0 ok, 1 fail)
        internal static int SelfTest()
        {
            CopyJob j = new CopyJob(new List<string>(new string[] { "x" }), "y", false);
            j.AddFinished("ok.txt", 2, null);
            j.AddFinished("bad.txt", 3, "disk full");
            string r = j.ErrorReport();
            if (r.IndexOf("bad.txt: disk full") < 0) return 1;
            if (r.IndexOf("ok.txt") >= 0) return 1;

            // device clip must be isolated from the normal clip
            string fA = Path.Combine(Path.GetTempPath(), "sc_selftest_a.txt");
            string fB = Path.Combine(Path.GetTempPath(), "sc_selftest_b.txt");
            try
            {
                ClipStore.Clear(fA); ClipStore.Clear(fB);
                ClipStore.Add(false, new string[] { "a1" }, fA);
                ClipStore.Add(false, new string[] { "b1" }, fB);
                ClipStore.Add(false, new string[] { "b2" }, fB);
                bool c1, c2;
                List<string> a = ClipStore.Read(out c1, fA);
                List<string> b = ClipStore.Read(out c2, fB);
                if (a.Count != 1 || a[0] != "a1") return 2;
                if (b.Count != 2 || b[0] != "b1" || b[1] != "b2") return 3;
                ClipStore.Clear(fB);
                if (!File.Exists(fA)) return 4;
                if (ClipStore.Read(out c1, fA).Count != 1) return 5;
            }
            finally { ClipStore.Clear(fA); ClipStore.Clear(fB); }

            // move-verify + free-space helpers
            string f1 = Path.Combine(Path.GetTempPath(), "sc_selftest_h1.txt");
            string f2 = Path.Combine(Path.GetTempPath(), "sc_selftest_h2.txt");
            try
            {
                File.WriteAllText(f1, "same");
                File.WriteAllText(f2, "same");
                if (!SameHash(f1, f2)) return 6;
                File.WriteAllText(f2, "diff");
                if (SameHash(f1, f2)) return 7;
                if (FreeSpace(Path.GetTempPath()) < 0) return 8;
                if (FreeSpace("\0") != -1) return 9;
            }
            finally { try { File.Delete(f1); } catch { } try { File.Delete(f2); } catch { } }

            // big-first ordering: scan must leave items sorted by Size descending
            string sd = Path.Combine(Path.GetTempPath(), "sc_selftest_sort");
            try
            {
                Directory.CreateDirectory(sd);
                File.WriteAllText(Path.Combine(sd, "a.txt"), "aaaaa");
                File.WriteAllText(Path.Combine(sd, "b.txt"), new string('b', 1000));
                File.WriteAllText(Path.Combine(sd, "c.txt"), new string('c', 200));
                CopyJob s = new CopyJob(new List<string>(new string[] { sd }),
                                        Path.Combine(Path.GetTempPath(), "sc_selftest_sort_dst"), false);
                s.ScanMain();
                if (s.ItemCount != 3) return 10;
                if (s.GetItem(0).Size < s.GetItem(1).Size || s.GetItem(1).Size < s.GetItem(2).Size) return 10;
            }
            finally { try { Directory.Delete(sd, true); } catch { } }

            // drag & drop: the FileDrop payload round-trips (what OnDragDrop relies on)
            DataObject d = new DataObject(DataFormats.FileDrop, new string[] { "dropped" });
            if (!d.GetDataPresent(DataFormats.FileDrop)) return 11;
            string[] got = (string[])d.GetData(DataFormats.FileDrop);
            if (got == null || got.Length != 1 || got[0] != "dropped") return 11;

            // access denied detection (drives the "retry as administrator?" prompt)
            if (!IsAccessDenied(new UnauthorizedAccessException("denied"))) return 18;
            if (IsAccessDenied(new IOException("other"))) return 18;

            // update.txt version parsing (drives the update check)
            if (Updates.ParseVersion(" 1 ") != 1) return 19;
            if (Updates.ParseVersion("garbage") != 0) return 19;

            // concurrent "ask first": two workers on conflicting files must prompt once
            string askDir = Path.Combine(Path.GetTempPath(), "sc_selftest_ask");
            try
            {
                Directory.CreateDirectory(askDir);
                string s1 = Path.Combine(askDir, "s1.txt"), s2 = Path.Combine(askDir, "s2.txt");
                string d1 = Path.Combine(askDir, "d1.txt"), d2 = Path.Combine(askDir, "d2.txt");
                File.WriteAllText(s1, "one"); File.WriteAllText(s2, "two");
                File.WriteAllText(d1, "old"); File.WriteAllText(d2, "old");
                int oldConflict = Settings.Conflict;
                int asks = 0;
                Settings.Conflict = 4;
                Settings.AskSession = -1;
                Settings.AskPrompting = 0;
                ConflictPrompt.Ask = delegate(string a, string b)
                {
                    Interlocked.Increment(ref asks);
                    Thread.Sleep(300);   // give the second worker time to reach the prompt
                    return 0;            // overwrite
                };
                CopyJob aj = new CopyJob(new List<string>(new string[] { s1 }), askDir, false);
                CopyItem i1 = new CopyItem(); i1.Src = s1; i1.Dst = d1; i1.Name = "d1.txt"; i1.Size = 3;
                CopyItem i2 = new CopyItem(); i2.Src = s2; i2.Dst = d2; i2.Name = "d2.txt"; i2.Size = 3;
                Thread w1 = new Thread(new ThreadStart(delegate { aj.DoCopy(i1, new byte[4096]); }));
                Thread w2 = new Thread(new ThreadStart(delegate { aj.DoCopy(i2, new byte[4096]); }));
                w1.Start(); w2.Start();
                w1.Join(); w2.Join();
                if (asks != 1) return 20;
                if (Settings.AskSession != 0 || i1.State != 2 || i2.State != 2) return 20;
                Settings.Conflict = oldConflict;
                Settings.AskSession = -1;
                ConflictPrompt.Ask = null;
            }
            finally
            {
                ConflictPrompt.Ask = null;
                Settings.AskSession = -1;
                try { Directory.Delete(askDir, true); } catch { }
            }

            // local version compare (offline update check: running exe vs Program Files copy)
            if (Updates.FileVer(Application.ExecutablePath) < 1) return 21;
            if (Updates.FileVer(Path.Combine(Path.GetTempPath(), "sc_no_such_file.exe")) != 0) return 21;

            // flat verb layout (skipped until installed): SuperCopy/SuperCut/SuperToDevice
            // are top-level Player verbs with their own command, no cascade anywhere,
            // paste is a flat empty-space verb only, and the retired keys are gone
            using (RegistryKey pk = Registry.CurrentUser.OpenSubKey(@"Software\Classes\*\shell\SuperCopy"))
            {
                if (pk != null)
                {
                    if (pk.GetValue("ExtendedSubCommandsKey") != null) return 12;   // cascade must stay gone
                    if (!"Player".Equals(pk.GetValue("MultiSelectModel"))) return 13;
                    using (RegistryKey lk = Registry.CurrentUser.OpenSubKey(@"Software\Classes\*\shell\SuperCopy\command"))
                        if (lk == null || lk.GetValue("") == null) return 14;
                    using (RegistryKey old = Registry.CurrentUser.OpenSubKey(@"Software\Classes\*\shell\SuperCut"))
                    {
                        if (old == null || old.GetValue("ExtendedSubCommandsKey") != null) return 15;
                        using (RegistryKey oc = Registry.CurrentUser.OpenSubKey(@"Software\Classes\*\shell\SuperCut\command"))
                            if (oc == null || oc.GetValue("") == null) return 15;
                    }
                    using (RegistryKey bg = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Directory\Background\shell\SuperPaste"))
                    {
                        if (bg == null) return 16;
                        if (bg.GetValue("ExtendedSubCommandsKey") != null) return 16;   // must stay flat
                        using (RegistryKey bgc = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Directory\Background\shell\SuperPaste\command"))
                            if (bgc == null || bgc.GetValue("") == null) return 16;
                    }
                    using (RegistryKey bgold = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Directory\Background\shell\SuperCopy"))
                        if (bgold != null) return 17;   // retired empty-space cascade
                    using (RegistryKey dr = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Drive\shell\SuperCopy"))
                        if (dr != null) return 17;      // drive has no Super Copy entry
                }
            }
            return 0;
        }

        void ScanDir(string src, string dst, string rel)
        {
            if (Cancelled || noSpace) return;
            Directory.CreateDirectory(dst);
            if (Move) lock (sync) { srcDirs.Add(src); }

            string[] files;
            try { files = Directory.GetFiles(src); }
            catch (Exception ex) { AddFinished(rel, 3, ex.Message); files = new string[0]; if (IsAccessDenied(ex)) AccessDenied = true; }
            for (int i = 0; i < files.Length; i++)
            {
                if (Cancelled || noSpace) return;
                string fn = Path.GetFileName(files[i]);
                AddFile(files[i], Path.Combine(dst, fn), rel + "\\" + fn);
            }

            string[] dirs;
            try { dirs = Directory.GetDirectories(src); }
            catch (Exception ex) { AddFinished(rel, 3, ex.Message); dirs = new string[0]; if (IsAccessDenied(ex)) AccessDenied = true; }
            for (int i = 0; i < dirs.Length; i++)
            {
                if (Cancelled || noSpace) return;
                try { if ((File.GetAttributes(dirs[i]) & FileAttributes.ReparsePoint) != 0) continue; } catch { }
                string dn = Path.GetFileName(dirs[i]);
                ScanDir(dirs[i], Path.Combine(dst, dn), rel + "\\" + dn);
            }
        }

        void ScanMain()
        {
            spaceLimit = FreeSpace(Dest);
            try
            {
                foreach (string src in Sources)
                {
                    if (Cancelled || noSpace) break;
                    try
                    {
                        if (Directory.Exists(src))
                        {
                            string name = Path.GetFileName(src.TrimEnd('\\', '/'));
                            if (name == null || name.Length == 0) name = src.Replace(":", "").Replace("\\", "");
                            if (IsSubOrSame(Dest, src))
                            {
                                AddFinished(name, 3, "Destination is inside the source folder");
                                continue;
                            }
                            string target = Path.Combine(Dest, name);
                            if (SamePath(target, src))
                            {
                                if (Move) { AddFinished(name, 4, "Same folder"); continue; }
                                target = UniqueDir(Dest, name + " - Copy");
                            }
                            ScanDir(src, target, name);
                        }
                        else if (File.Exists(src))
                        {
                            string fn = Path.GetFileName(src);
                            AddFile(src, Path.Combine(Dest, fn), fn);
                        }
                        else AddFinished(src, 3, "Not found");
                    }
                    catch (Exception ex) { AddFinished(src, 3, ex.Message); if (IsAccessDenied(ex)) AccessDenied = true; }
                }
            }
            finally
            {
                // largest files first: display order == copy order. Gate is ScanDone (set last) so
                // workers never consume the list mid-sort.
                lock (sync)
                {
                    items.Sort(delegate(CopyItem a, CopyItem b) { return b.Size.CompareTo(a.Size); });
                    for (int i = 0; i < items.Count; i++) items[i].Index = i;
                }
                ScanDone = true;
            }
        }

        // ---------- worker threads ----------
        void WorkerMain()
        {
            byte[] buf = null;
            bool last = false;
            while (true)
            {
                CopyItem it = null;
                bool wait = false;
                lock (sync)
                {
                    if (Cancelled) { workers--; last = (workers == 0); break; }
                int target = Settings.Concurrency;
                if (Settings.SmartConcurrency)
                {
                    CopyItem candidate = it;
                    if (candidate == null && next < items.Count) candidate = items[next];
                    target = GetTargetConcurrency(candidate);
                }
                if (workers > target && target >= 1) { workers--; return; }
                if (ScanDone)
                {
                    if (next < items.Count) it = items[next++];
                    else { workers--; last = (workers == 0); break; }
                }
                else wait = true;
            }
            if (wait) { Thread.Sleep(25); continue; }

            int want = Settings.BufferKB * 1024;
            if (buf == null || buf.Length != want) buf = new byte[want];
            ProcessItem(it, buf);
        }
        if (last) OnFinished();
    }

        void ProcessItem(CopyItem it, byte[] buf)
        {
            if (it.State != 0) return;
            it.Speed = 0; it.SampBytes = 0;
            it.State = 1;
            lock (activeLock) { active.Add(it); }
            try { DoCopy(it, buf); }
            catch (Exception ex)
            {
                it.State = 3; it.Error = ex.Message;
                LastError = it.Name + ": " + ex.Message;
                Interlocked.Increment(ref Errors);
                if (IsAccessDenied(ex)) AccessDenied = true;
                long rem = it.Size - Interlocked.Read(ref it.Done);
                if (rem > 0) Interlocked.Add(ref DoneBytes, rem);
            }
            finally
            {
                lock (activeLock) { active.Remove(it); }
                Interlocked.Increment(ref FilesDone);
            }
        }

        void Skip(CopyItem it)
        {
            Interlocked.Add(ref DoneBytes, it.Size);
            it.Done = it.Size;
            it.State = 4;
        }

        void DoCopy(CopyItem it, byte[] buf)
        {
            string src = it.Src, dst = it.Dst;

            if (File.Exists(dst))
            {
                if (SamePath(src, dst))
                {
                    if (Move) { Skip(it); return; }
                    dst = UniqueFile(dst);
                }
                else
                {
                    int c = Settings.Conflict;
                    if (c == 4)
                    {
                        int askChoice = Settings.AskSession;
                        while (askChoice < 0)
                        {
                            if (Interlocked.CompareExchange(ref Settings.AskPrompting, 1, 0) == 0)
                            {
                                try
                                {
                                    // the modal dialog pumps Invoke calls from the other workers,
                                    // so only the CAS winner may ask; the rest wait and reuse the answer
                                    askChoice = Settings.AskSession;   // re-check: winner may have answered while we waited
                                    if (askChoice < 0)
                                    {
                                        ConflictAsker ask = ConflictPrompt.Ask;
                                        askChoice = ask == null ? 1 : ask(src, dst);
                                        if (askChoice >= 0) Settings.AskSession = askChoice;
                                    }
                                }
                                finally { Interlocked.Exchange(ref Settings.AskPrompting, 0); }
                                break;   // answered (session applies) or cancelled (-1: skip this file)
                            }
                            Thread.Sleep(50);
                            askChoice = Settings.AskSession;
                        }
                        if (askChoice < 0) { Skip(it); return; }     // dialog cancelled -> skip
                        c = askChoice;
                    }
                    if (c == 1) { Skip(it); return; }
                    if (c == 2) dst = UniqueFile(dst);
                    else if (c == 3 && File.GetLastWriteTime(src) <= File.GetLastWriteTime(dst)) { Skip(it); return; }
                    else File.SetAttributes(dst, FileAttributes.Normal);   // overwrite: clear read-only
                }
                it.Dst = dst;
            }

            // same drive + move = instant rename
            if (Move && SameRoot(src, dst))
            {
                if (File.Exists(dst))
                {
                    // shortcut: a kill between here and the delete leaves dst+".sctmp"; upgrade to a shutdown sweep if that matters
                    string mb = dst + ".sctmp";
                    try { File.Delete(mb); } catch { }
                    File.Replace(src, dst, mb);          // atomic; both files stay intact if this throws
                    try { File.Delete(mb); } catch { }
                }
                else File.Move(src, dst);
                Interlocked.Add(ref DoneBytes, it.Size);
                it.Done = it.Size;
                it.State = 2;
                return;
            }

            // move an existing destination aside first, so a failed or cancelled transfer cannot
            // destroy the original; it is restored on failure and discarded on success
            string bak = null;
            if (File.Exists(dst))
            {
                bak = dst + ".sctmp";
                try { File.Delete(bak); } catch { }
                try { File.Move(dst, bak); } catch { bak = null; }   // degrade to plain overwrite
            }
            bool ok;
            try { ok = Settings.Engine == 1 ? RoboCopy(src, dst, it, buf) : CopyData(src, dst, it, buf); }
            catch
            {
                try { File.Delete(dst); } catch { }
                if (bak != null) { try { File.Move(bak, dst); } catch { } }
                throw;
            }
            if (!ok)
            {
                try { File.Delete(dst); } catch { }
                if (bak != null) { try { File.Move(bak, dst); } catch { } }
                it.State = 5;
                return;
            }
            if (bak != null) { try { File.Delete(bak); } catch { } }

            long d = Interlocked.Read(ref it.Done);
            if (d != it.Size) { Interlocked.Add(ref TotalBytes, d - it.Size); it.Size = d; }

            try
            {
                File.SetCreationTime(dst, File.GetCreationTime(src));
                File.SetLastWriteTime(dst, File.GetLastWriteTime(src));
                File.SetAttributes(dst, File.GetAttributes(src));
            }
            catch { }

            if (Move)
            {
                // shortcut: re-reads both files to compare; switch to hash-as-you-copy if large-file move speed matters
                if (!SameHash(dst, src))
                {
                    it.Error = "Checksum mismatch - source kept";
                    LastError = it.Name + ": " + it.Error;
                    it.State = 3;
                    Interlocked.Increment(ref Errors);
                    return;
                }
                try { File.SetAttributes(src, FileAttributes.Normal); File.Delete(src); }
                catch (Exception ex)
                {
                    it.Error = "Copied, source not deleted: " + ex.Message;
                    LastError = it.Name + ": " + it.Error;
                    it.State = 3;
                    Interlocked.Increment(ref Errors);
                    if (IsAccessDenied(ex)) AccessDenied = true;
                    return;
                }
            }
            it.State = 2;
        }

        // returns false when cancelled
        bool CopyData(string src, string dst, CopyItem it, byte[] buf)
        {
            FileStream fi = null, fo = null;
            bool ok = false;
            try
            {
                fi = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.SequentialScan);
                fo = new FileStream(dst, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.SequentialScan);
                int n;
                while ((n = fi.Read(buf, 0, buf.Length)) > 0)
                {
                    while (Paused && !Cancelled) Thread.Sleep(100);
                    if (Cancelled) break;
                    fo.Write(buf, 0, n);
                    Interlocked.Add(ref it.Done, n);
                    Interlocked.Add(ref DoneBytes, n);
                }
                ok = !Cancelled;
            }
            catch
            {
                if (fi != null) { try { fi.Close(); } catch { } }
                if (fo != null)
                {
                    try { fo.Close(); } catch { }
                    try { File.Delete(dst); } catch { }   // remove the partial file we created
                }
                throw;
            }
            if (fi != null) fi.Close();
            if (fo != null) fo.Close();
            return ok;
        }

        // copies one file with robocopy.exe. Keeps all scanning, conflict and move logic; only the
        // byte transfer changes. Falls back to the built-in copier if robocopy is missing, or when a
        // rename is needed (robocopy cannot rename a single file), so no copy ever fails because of it.
        bool RoboCopy(string src, string dst, CopyItem it, byte[] buf)
        {
            string exe = Path.Combine(Environment.SystemDirectory, "robocopy.exe");
            string dir = Path.GetDirectoryName(dst), name = Path.GetFileName(dst);
            if (!File.Exists(exe) || Path.GetFileName(src) != name) return CopyData(src, dst, it, buf);

            ProcessStartInfo psi = new ProcessStartInfo(exe);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.Arguments = "\"" + Path.GetDirectoryName(src) + "\" \"" + dir + "\" \"" + name +
                "\" /R:0 /W:0 /NP /NJH /NJS /NDL /NFL /NC /NS /IS /IT";
            try
            {
                using (Process p = Process.Start(psi))
                {
                    while (!p.WaitForExit(200))                     // poll so Cancel can stop it
                        if (Cancelled) { try { p.Kill(); } catch { } return false; }
                    if (p.ExitCode >= 8) return false;              // 0..7 = success
                }
                FileInfo fi = new FileInfo(dst);
                if (!fi.Exists || fi.Length != it.Size) return false;
                Interlocked.Add(ref it.Done, it.Size);
                Interlocked.Add(ref DoneBytes, it.Size);
                return true;
            }
            catch { return CopyData(src, dst, it, buf); }
        }

        void OnFinished()
        {
            try
            {
                if (Move && !Cancelled && Errors == 0)
                {
                    // children were added after parents, so walk backwards; only empty folders go away
                    for (int i = srcDirs.Count - 1; i >= 0; i--)
                    {
                        try { Directory.Delete(srcDirs[i], false); } catch { }
                    }
                }
                lock (sync)
                {
                    for (int i = next; i < items.Count; i++)
                        if (items[i].State == 0) items[i].State = 5;
                }
            }
            catch { }
            Finished = true;
            Settings.AskSession = -1;
        }
    }

    // ================================================================
    //  UI controls
    // ================================================================
    class BufferedListView : ListView
    {
        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public BufferedListView()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { SendMessage(Handle, 0x1036, (IntPtr)0x10000, (IntPtr)0x10000); } catch { }  // LVS_EX_DOUBLEBUFFER
        }
    }

    class HeaderPanel : Panel
    {
        public string Title = "", Elapsed = "0:00", BarText = "", TxtL = "", TxtM = "", TxtR = "";
        public double Frac;
        public bool Marquee;
        public int Tick;
        public Color ColA = Theme.Cyan, ColB = Theme.Violet;
        public Button HideButton, ModeButton;

        public HeaderPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Relayout();
        }

        public void Relayout()
        {
            if (L.Rtl)
            {
                int x = 14;
                if (HideButton != null) { HideButton.Location = new Point(x, 9); x += HideButton.Width + 6; }
                if (ModeButton != null) ModeButton.Location = new Point(x, 9);
                return;
            }
            int x2 = Width - 14;
            if (HideButton != null) { HideButton.Location = new Point(x2 - HideButton.Width, 9); x2 -= HideButton.Width + 6; }
            if (ModeButton != null) ModeButton.Location = new Point(x2 - ModeButton.Width, 9);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            if (Width < 60 || Height < 40) return;
            Rectangle rc = ClientRectangle;
            using (LinearGradientBrush lb = new LinearGradientBrush(rc, Theme.HeadA, Theme.HeadB, 90f))
                g.FillRectangle(lb, rc);
            using (LinearGradientBrush ab = new LinearGradientBrush(new Rectangle(0, Height - 3, Width, 3), Theme.Cyan, Theme.Violet, 0f))
                g.FillRectangle(ab, 0, Height - 3, Width, 3);

            Rectangle timeR, titleR;
            TextFormatFlags titleFlag, timeFlag;
            if (L.Rtl)
            {
                int btnRight = 14;
                if (ModeButton != null) btnRight = Math.Max(btnRight, ModeButton.Right);
                if (HideButton != null) btnRight = Math.Max(btnRight, HideButton.Right);
                timeR = new Rectangle(btnRight + 12, 10, 110, 24);
                titleR = new Rectangle(timeR.Right + 10, 10, Math.Max(40, Width - 14 - (timeR.Right + 10)), 24);
                titleFlag = TextFormatFlags.Right;   // title hugs the right edge
                timeFlag = TextFormatFlags.Left;     // elapsed sits near the buttons
            }
            else
            {
                int hideLeft = ModeButton != null ? ModeButton.Left : (HideButton != null ? HideButton.Left : Width - 90);
                timeR = new Rectangle(hideLeft - 118, 10, 110, 24);
                titleR = new Rectangle(14, 10, Math.Max(40, timeR.Left - 24), 24);
                titleFlag = TextFormatFlags.Left;
                timeFlag = TextFormatFlags.Right;
            }
            TextRenderer.DrawText(g, Title, Theme.Title, titleR, Theme.Text,
                titleFlag | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            TextRenderer.DrawText(g, Elapsed, Theme.Big, timeR, Theme.Cyan,
                timeFlag | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

            Rectangle bar = new Rectangle(14, 42, Width - 28, 26);
            Gfx.Bar(g, bar, Frac, ColA, ColB, BarText, Marquee, Tick, 8);

            Rectangle sr = new Rectangle(14, 74, Width - 28, 20);
            TextRenderer.DrawText(g, TxtL, Theme.Bold, sr, Theme.Text,
                L.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            TextRenderer.DrawText(g, TxtM, Theme.Bold, sr, Theme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            TextRenderer.DrawText(g, TxtR, Theme.Cell, sr, Theme.Dim,
                L.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }
    }

    class TitleBar : Panel
    {
        public string Title = "", Sub = "";

        public TitleBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            if (Width < 20 || Height < 10) return;
            using (LinearGradientBrush lb = new LinearGradientBrush(ClientRectangle, Theme.Cyan, Theme.Violet, 0f))
                g.FillRectangle(lb, ClientRectangle);
            g.DrawImage(IconMaker.MakeBitmap(32), L.Rtl ? Width - 46 : 14, (Height - 32) / 2);
            Rectangle trTitle = L.Rtl ? new Rectangle(14, 8, Width - 60, 22) : new Rectangle(56, 8, Width - 60, 22);
            Rectangle trSub = L.Rtl ? new Rectangle(14, 30, Width - 60, 18) : new Rectangle(56, 30, Width - 60, 18);
            TextRenderer.DrawText(g, Title, Theme.Title, trTitle, Color.White,
                L.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, Sub, Theme.Cell, trSub, Color.FromArgb(230, 235, 255),
                L.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    static class UI
    {
        public static Button MakeButton(string text, int w, Color back)
        {
            Button b = new Button();
            b.Text = text;
            b.Size = new Size(w, 28);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.UseVisualStyleBackColor = false;
            b.BackColor = back;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(
                Math.Min(255, back.R + 35), Math.Min(255, back.G + 35), Math.Min(255, back.B + 35));
            b.ForeColor = Color.White;
            b.Font = Theme.Bold;
            b.Cursor = Cursors.Hand;
            return b;
        }
    }

    // "Ask first" prompt. Returns 0 overwrite, 1 skip, 2 keep both, 3 overwrite if newer, -1 cancel.
    delegate int ConflictAsker(string src, string dst);

    static class ConflictPrompt
    {
        public static ConflictAsker Ask;      // set by the UI; null when there is no window to ask from
    }

    // ================================================================
    //  Progress window
    // ================================================================
    class MainForm : Form
    {
        readonly List<CopyJob> jobs = new List<CopyJob>();
        CopyJob cur, running;
        string lastDest;   // most recent job's folder: drop menu offers it as "same destination"
        HeaderPanel header;
        BufferedListView lv;
        ColumnHeader colFile;
        ListBox jobBox;
        Panel bottom;
        Button btnPause, btnCancel, btnSettings, btnHide, btnMode;
        CheckBox chkClose;
        Label lblErr;
        Button btnErrReport;
        System.Windows.Forms.Timer timer;
        NotifyIcon tray;
        ContextMenu trayMenu;
        bool exiting, doneHandled, settingSel, compact;
        bool adminPrompted, retryEscalated;   // access-denied -> "retry as admin?" asked / elevating
        int lastTick, tickN, closeAt = -1;
        Size savedSize;

        static TextFormatFlags CellFlags
        {
            get
            {
                return L.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                       TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            }
        }

        public MainForm(CopyJob first)
        {
            Text = "Super Copy";
            Font = Theme.Cell;
            BackColor = Theme.Bg;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(800, 520);
            MinimumSize = new Size(660, 380);
            Icon = IconMaker.MakeIcon();
            L.ApplyRtl(this, false);

            // ----- file list -----
            lv = new BufferedListView();
            lv.Dock = DockStyle.Fill;
            lv.View = View.Details;
            lv.VirtualMode = true;
            lv.OwnerDraw = true;
            lv.FullRowSelect = true;
            lv.HideSelection = false;
            lv.MultiSelect = false;
            lv.BorderStyle = BorderStyle.None;
            lv.BackColor = Theme.Row1;
            lv.ForeColor = Theme.Text;
            BuildColumns();
            ImageList rowH = new ImageList();      // trick: taller rows for the progress bars
            rowH.ImageSize = new Size(1, 24);
            lv.SmallImageList = rowH;
            lv.RetrieveVirtualItem += new RetrieveVirtualItemEventHandler(lv_Retrieve);
            lv.DrawColumnHeader += new DrawListViewColumnHeaderEventHandler(lv_DrawHeader);
            lv.DrawItem += new DrawListViewItemEventHandler(lv_DrawItem);
            lv.DrawSubItem += new DrawListViewSubItemEventHandler(lv_DrawSub);
            lv.Resize += new EventHandler(lv_Resize);

            // ----- bottom bar -----
            bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 46;
            bottom.BackColor = Theme.Panel;
            btnPause = UI.MakeButton(L.T("Pause", "إيقاف", "Pause"), 78, Color.FromArgb(214, 140, 30));
            btnCancel = UI.MakeButton(L.T("Cancel", "إلغاء", "Annuler"), 78, Color.FromArgb(200, 55, 75));
            btnSettings = UI.MakeButton(L.T("Settings", "الإعدادات", "Paramètres"), 84, Color.FromArgb(70, 90, 170));
            btnPause.Location = new Point(12, 9);
            btnCancel.Location = new Point(98, 9);
            btnSettings.Location = new Point(184, 9);
            chkClose = new CheckBox();
            chkClose.Text = L.T("Close when finished", "إغلاق عند الانتهاء", "Fermer à la fin");
            chkClose.AutoSize = true;
            chkClose.ForeColor = Theme.Text;
            chkClose.BackColor = Theme.Panel;
            chkClose.Location = new Point(284, 14);
            chkClose.Checked = Settings.AutoClose;
            chkClose.CheckedChanged += new EventHandler(chkClose_Changed);
            lblErr = new Label();
            lblErr.AutoSize = false;
            lblErr.AutoEllipsis = true;
            lblErr.ForeColor = Theme.Red;
            lblErr.BackColor = Theme.Panel;
            lblErr.TextAlign = ContentAlignment.MiddleRight;
            lblErr.SetBounds(440, 14, 300, 18);
            btnErrReport = UI.MakeButton(L.T("Error report", "تقرير الأخطاء", "Rapport d'erreurs"), 132, Color.FromArgb(200, 55, 75));
            btnErrReport.Visible = false;
            bottom.Controls.Add(btnPause);
            bottom.Controls.Add(btnCancel);
            bottom.Controls.Add(btnSettings);
            bottom.Controls.Add(chkClose);
            bottom.Controls.Add(lblErr);
            bottom.Controls.Add(btnErrReport);
            bottom.Resize += new EventHandler(bottom_Resize);
            btnPause.Click += new EventHandler(btnPause_Click);
            btnCancel.Click += new EventHandler(btnCancel_Click);
            btnSettings.Click += new EventHandler(btnSettings_Click);
            btnErrReport.Click += new EventHandler(btnErrReport_Click);

            // ----- job strip (visible when more than one operation is queued) -----
            jobBox = new ListBox();
            jobBox.Dock = DockStyle.Top;
            jobBox.DrawMode = DrawMode.OwnerDrawFixed;
            jobBox.ItemHeight = 26;
            jobBox.IntegralHeight = false;
            jobBox.BorderStyle = BorderStyle.None;
            jobBox.BackColor = Theme.Panel;
            jobBox.Visible = false;
            jobBox.DrawItem += new DrawItemEventHandler(jobBox_DrawItem);
            jobBox.SelectedIndexChanged += new EventHandler(jobBox_Changed);

            // ----- header -----
            header = new HeaderPanel();
            header.Dock = DockStyle.Top;
            header.Height = 104;
            btnHide = UI.MakeButton(L.T("Hide", "إخفاء", "Masquer"), 70, Color.FromArgb(70, 90, 170));
            btnHide.Height = 26;
            header.HideButton = btnHide;
            header.Controls.Add(btnHide);
            btnHide.Location = new Point(header.Width - btnHide.Width - 14, 9);
            btnHide.Click += new EventHandler(btnHide_Click);
            btnMode = UI.MakeButton(L.T("Compact", "مضغوط", "Compact"), 78, Color.FromArgb(60, 120, 140));
            btnMode.Height = 26;
            header.ModeButton = btnMode;
            header.Controls.Add(btnMode);
            btnMode.Click += new EventHandler(btnMode_Click);

            // docking order: fill first, outermost (top-most) last
            Controls.Add(lv);
            Controls.Add(bottom);
            Controls.Add(jobBox);
            Controls.Add(header);

            // ----- drag & drop (works while a copy is running) -----
            AllowDrop = true;
            DragEnter += new DragEventHandler(OnDragEnter);
            DragDrop += new DragEventHandler(OnDragDrop);
            Control[] dropTargets = new Control[] { lv, header, bottom, jobBox };
            for (int i = 0; i < dropTargets.Length; i++)
            {
                dropTargets[i].AllowDrop = true;
                dropTargets[i].DragEnter += new DragEventHandler(OnDragEnter);
                dropTargets[i].DragDrop += new DragEventHandler(OnDragDrop);
            }

            // ----- tray icon (for "Hide") -----
            tray = new NotifyIcon();
            tray.Icon = Icon;
            tray.Text = "Super Copy";
            tray.Visible = false;
            tray.DoubleClick += new EventHandler(tray_Show);
            trayMenu = new ContextMenu();
            trayMenu.MenuItems.Add(L.T("Show window", "إظهار النافذة", "Afficher la fenêtre"), new EventHandler(tray_Show));
            trayMenu.MenuItems.Add(L.T("Pause / Resume", "إيقاف / متابعة", "Pause / Reprendre"), new EventHandler(btnPause_Click));
            trayMenu.MenuItems.Add("-");
            trayMenu.MenuItems.Add(L.T("Cancel all and exit", "إلغاء الكل والخروج", "Tout annuler et quitter"), new EventHandler(tray_Exit));
            tray.ContextMenu = trayMenu;

            if (first != null) AddJob(first);   // null: /queue mode, jobs arrive via PollQueue

            timer = new System.Windows.Forms.Timer();
            timer.Interval = 250;
            timer.Tick += new EventHandler(OnTick);
            lastTick = Environment.TickCount;
            timer.Start();

            lv_Resize(null, null);
            bottom_Resize(null, null);
            if (Settings.Compact) SetCompact(true);
            ConflictPrompt.Ask = AskConflict;      // let worker threads pop the "file exists" prompt
        }

        // Re-apply the chosen language to this window (after Settings is saved).
        public void ApplyLanguage()
        {
            L.ApplyRtl(this, false);
            btnCancel.Text = L.T("Cancel", "إلغاء", "Annuler");
            btnSettings.Text = L.T("Settings", "الإعدادات", "Paramètres");
            btnHide.Text = L.T("Hide", "إخفاء", "Masquer");
            btnErrReport.Text = L.T("Error report", "تقرير الأخطاء", "Rapport d'erreurs");
            chkClose.Text = L.T("Close when finished", "إغلاق عند الانتهاء", "Fermer à la fin");
            trayMenu.MenuItems[0].Text = L.T("Show window", "إظهار النافذة", "Afficher la fenêtre");
            trayMenu.MenuItems[1].Text = L.T("Pause / Resume", "إيقاف / متابعة", "Pause / Reprendre");
            trayMenu.MenuItems[3].Text = L.T("Cancel all and exit", "إلغاء الكل والخروج", "Tout annuler et quitter");
            BuildColumns();
            btnMode.Text = compact ? L.T("Files", "الملفات", "Fichiers") : L.T("Compact", "مضغوط", "Compact");
            UpdateButtons();
            lv_Resize(null, EventArgs.Empty);
            bottom_Resize(null, EventArgs.Empty);
            header.Relayout();
            header.Invalidate();
            lv.Invalidate();
            jobBox.Invalidate();
        }

        // ------------------------------------------------------------
        //  drag & drop onto the window -> another job on the queue
        // ------------------------------------------------------------
        void OnDragEnter(object s, DragEventArgs e)
        {
            e.Effect = e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy : DragDropEffects.None;
        }

        void OnDragDrop(object s, DragEventArgs e)
        {
            string[] paths = null;
            try { paths = (string[])e.Data.GetData(DataFormats.FileDrop); }
            catch { }
            if (paths == null || paths.Length == 0) return;

            // two options: same destination (last job's folder) / another (browse)
            if (lastDest == null)
            {
                PickDropDest(paths, null);   // first job ever: only the browse dialog
                return;
            }
            ContextMenu cm = new ContextMenu();
            string same = lastDest;
            string shortDest = same.Length > 40 ? "..." + same.Substring(same.Length - 37) : same;
            MenuItem mSame = new MenuItem(L.T("1. Same destination (", "1. نفس الوجهة (", "1. Même destination (") + shortDest + ")");
            mSame.Click += delegate { cm.Dispose(); AddJob(new CopyJob(new List<string>(paths), same, false)); };
            MenuItem mOther = new MenuItem(L.T("2. Another destination...", "2. وجهة أخرى...", "2. Autre destination..."));
            mOther.Click += delegate { cm.Dispose(); PickDropDest(paths, same); };
            cm.MenuItems.Add(mSame);
            cm.MenuItems.Add(mOther);
            cm.Show(this, PointToClient(Cursor.Position));
        }

        // shortcut: always copies (Move=false), no dedup vs existing sources, and a dropped path may equal/contain Dest; upgrade if these matter
        void PickDropDest(string[] paths, string seed)
        {
            using (FolderBrowserDialog fb = new FolderBrowserDialog())
            {
                fb.Description = L.T("Destination folder for the dropped items", "مجلد الوجهة للعناصر المسحوبة", "Dossier de destination des éléments déposés");
                if (seed != null) fb.SelectedPath = seed;
                if (fb.ShowDialog(this) != DialogResult.OK) return;
                AddJob(new CopyJob(new List<string>(paths), fb.SelectedPath, false));
            }
        }

        // ------------------------------------------------------------
        //  "Ask first" conflict prompt (called from a copy worker thread)
        // ------------------------------------------------------------
        int AskConflict(string src, string dst)
        {
            try
            {
                if (!IsHandleCreated) return 1;
                return (int)Invoke(new ConflictAsker(AskConflictNow), new object[] { src, dst });
            }
            catch { return 1; }
        }

        int AskConflictNow(string src, string dst)
        {
            if (Settings.AskSession >= 0) return Settings.AskSession;   // another worker answered already
            using (ConflictDialog d = new ConflictDialog(dst))
            {
                if (d.ShowDialog(this) != DialogResult.OK) { Settings.AskSession = -1; return -1; }   // cancelled -> skip this file
                if (d.SaveForSession) Settings.AskSession = d.Choice;
                else Settings.AskSession = -1;
                return d.Choice;
            }
        }

        // ------------------------------------------------------------
        void AddJob(CopyJob j)
        {
            jobs.Add(j);
            lastDest = j.Dest;
            jobBox.Items.Add(j);
            doneHandled = false;
            closeAt = -1;
            bool multi = jobs.Count >= 2;
            jobBox.Visible = multi && !compact;
            jobBox.Height = Math.Min(jobs.Count, 4) * 26 + 2;
            if (cur == null) SetCur(j);
        }

        void SetCur(CopyJob j)
        {
            cur = j;
            lv.VirtualListSize = j.ItemCount;
            lv.Invalidate();
            int idx = jobs.IndexOf(j);
            if (idx >= 0 && jobBox.SelectedIndex != idx)
            {
                bool old = settingSel;
                settingSel = true;
                jobBox.SelectedIndex = idx;
                settingSel = old;
            }
        }

        void PollQueue()
        {
            if (retryEscalated) return;   // this instance is about to exit; the elevated one drains the queue
            string[] fs;
            try { fs = Directory.GetFiles(Paths.QueueDir, "*.job"); } catch { return; }
            if (fs.Length == 0) return;
            Array.Sort(fs);
            for (int k = 0; k < fs.Length; k++)
            {
                try
                {
                    string[] ln = File.ReadAllLines(fs[k], Encoding.UTF8);
                    if (ln.Length >= 3)
                    {
                        List<string> src = new List<string>();
                        for (int i = 2; i < ln.Length; i++) if (ln[i].Length > 0) src.Add(ln[i]);
                        if (src.Count > 0) AddJob(new CopyJob(src, ln[1], ln[0] == "cut"));
                    }
                    File.Delete(fs[k]);   // only after the job is enqueued; a throw keeps the file for retry
                }
                catch { }
            }
        }

        bool AnyRunning()
        {
            for (int i = 0; i < jobs.Count; i++) if (!jobs[i].Finished) return true;
            return false;
        }

        bool MouseInList()
        {
            try { return lv.ClientRectangle.Contains(lv.PointToClient(Cursor.Position)); } catch { return false; }
        }

        // ------------------------------------------------------------
        //  timer: sampling, scheduling, refresh
        // ------------------------------------------------------------
        void OnTick(object sender, EventArgs e)
        {
            tickN++;
            int now = Environment.TickCount;
            int dt = now - lastTick;
            lastTick = now;
            if (dt < 1) dt = 1;

            if (tickN % 2 == 0) PollQueue();
            if (running != null && !running.Finished)
            {
                running.EnsureWorkers();
                running.Sample(dt);
            }
            Advance();

            // caption / tray tooltip stay current even when hidden
            string cap = "Super Copy";
            if (cur != null && cur.Started && !cur.Finished) cap = ((int)(cur.Fraction * 100)) + "% - Super Copy";
            if (Text != cap) Text = cap;
            if (tray.Visible && tray.Text != cap) tray.Text = cap;

            if (Visible && WindowState != FormWindowState.Minimized && cur != null)
            {
                int cnt = cur.ItemCount;
                if (lv.VirtualListSize != cnt) lv.VirtualListSize = cnt;
                if (cur == running && !cur.Finished && cnt > 0 && lv.Visible && !MouseInList())
                {
                    int idx = cur.FirstActiveIndex();
                    if (idx >= 0)
                    {
                        try
                        {
                            lv.EnsureVisible(Math.Min(cnt - 1, idx + 8));
                            lv.EnsureVisible(idx);
                        }
                        catch { }
                    }
                }
                UpdateHeader();
                UpdateButtons();
                header.Tick = tickN;
                header.Invalidate();
                lv.Invalidate();
                if (jobBox.Visible) jobBox.Invalidate();
            }

            if (closeAt >= 0 && tickN >= closeAt) { exiting = true; Close(); }
        }

        void Advance()
        {
            if (running == null || running.Finished)
            {
                // access denied: once per session, offer to re-run the failed items elevated
                if (running != null && running.Finished && running.AccessDenied &&
                    !adminPrompted && !retryEscalated && !Admin.IsElevated)
                {
                    adminPrompted = true;
                    if (!Visible) ShowFromTray();
                    if (Msg.Show(this,
                        L.T("Some files could not be copied: access is denied.\n\n" +
                            "Retry the failed items as administrator?\n" +
                            "Super Copy will restart elevated and continue.",
                            "تعذّر نسخ بعض الملفات: تم رفض الوصول.\n\n" +
                            "هل تريد إعادة المحاولة للملفات الفاشلة كمسؤول؟\n" +
                            "ستُعاد Super Copy التشغيل بصلاحيات المسؤول وتتابع.",
                            "Certains fichiers n'ont pas pu être copiés : accès refusé.\n\n" +
                            "Réessayer les éléments échoués en tant qu'administrateur ?\n" +
                            "Super Copy redémarrera avec élévation et continuera."),
                        "Super Copy", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    {
                        // stop polling first: the UAC prompt pumps messages and the old
                        // instance would otherwise eat its own queue file
                        retryEscalated = true;
                        if (QueueFailed(running) && Admin.Relaunch())
                        {
                            exiting = true;
                            Close();
                            return;
                        }
                        retryEscalated = false;
                        Msg.Show(this, L.T("Could not start the elevated copy.", "تعذّر بدء النسخ بصلاحيات المسؤول.", "Impossible de démarrer la copie élevée."), "Super Copy",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }

                CopyJob nx = null;
                for (int i = 0; i < jobs.Count; i++)
                    if (!jobs[i].Started) { nx = jobs[i]; break; }
                if (nx != null)
                {
                    running = nx;
                    nx.Start();
                    SetCur(nx);
                    doneHandled = false;
                    closeAt = -1;
                }
                else if (!doneHandled && running != null)
                {
                    doneHandled = true;
                    AllDone();
                }
            }
        }

        // re-enqueue the failed items of a finished job so the elevated instance picks them up
        bool QueueFailed(CopyJob j)
        {
            List<string> src = new List<string>();
            for (int i = 0; i < j.ItemCount; i++)
            {
                CopyItem it = j.GetItem(i);
                if (it != null && it.State == 3 && it.Src != null) src.Add(it.Src);
            }
            if (src.Count == 0) return false;   // scan-time failures have no Src; user can re-run manually
            try { QueueStore.Enqueue(j.Move, j.Dest, src); return true; }
            catch { return false; }
        }

        void AllDone()
        {
            int errs = 0;
            bool canc = false;
            for (int i = 0; i < jobs.Count; i++) { errs += jobs[i].Errors; canc = canc || jobs[i].Cancelled; }
            if (errs > 0 || canc)
            {
                if (!Visible) ShowFromTray();     // let the user see what happened
            }
            else if (Settings.AutoClose)
            {
                closeAt = tickN + 6;
            }
            else if (!Visible)
            {
                try { tray.ShowBalloonTip(3000, "Super Copy", L.T("All operations completed.", "اكتملت جميع العمليات.", "Toutes les opérations sont terminées."), ToolTipIcon.Info); } catch { }
            }
        }

        void UpdateHeader()
        {
            CopyJob j = cur;
            HeaderPanel h = header;
            long total = j.Total, done = j.DoneNow;
            int files = j.ItemCount;
            string errs = j.Errors > 0 ? "    " + L.T("Errors: ", "الأخطاء: ", "Erreurs : ") + j.Errors : "";
            string filesTxt = j.FilesDone + " / " + files + (j.ScanDone ? "" : "+") + " " +
                              L.T("files", "ملف", "fichiers") + "    " +
                              Fmt.Size(done) + " / " + Fmt.Size(total) + (j.ScanDone ? "" : "+") + errs;
            string verb = j.Move ? L.T("Moving ", "نقل ", "Déplacement de ") : L.T("Copying ", "نسخ ", "Copie de ");
            h.Elapsed = Fmt.Time(j.ActiveMs / 1000.0);
            h.Marquee = false;
            h.Frac = j.Fraction;
            h.TxtR = filesTxt;

            if (!j.Started)
            {
                h.Title = L.T("Queued: ", "في الانتظار: ", "En attente : ") + j.Summary;
                h.BarText = L.T("Waiting...", "بالانتظار...", "En attente...");
                h.Frac = 0;
                h.ColA = Color.FromArgb(70, 80, 120); h.ColB = Color.FromArgb(100, 110, 150);
                h.TxtL = L.T("Speed: -", "السرعة: -", "Vitesse : -");
                h.TxtM = L.T("Total ETA: -", "الوقت المتبقي: -", "Temps restant : -");
            }
            else if (j.Finished)
            {
                if (j.Cancelled)
                {
                    h.Title = L.T("Cancelled: ", "ملغى: ", "Annulé : ") + j.Summary;
                    h.ColA = Color.FromArgb(210, 120, 30); h.ColB = Color.FromArgb(255, 190, 90);
                }
                else if (j.Errors > 0)
                {
                    h.Title = L.T("Finished with errors: ", "انتهى مع أخطاء: ", "Terminé avec des erreurs : ") + j.Summary;
                    h.Frac = 1.0;
                    h.ColA = Color.FromArgb(200, 40, 60); h.ColB = Color.FromArgb(255, 120, 100);
                }
                else
                {
                    h.Title = L.T("Completed: ", "مكتمل: ", "Terminé : ") + j.Summary;
                    h.Frac = 1.0;
                    h.ColA = Color.FromArgb(30, 170, 100); h.ColB = Color.FromArgb(120, 240, 160);
                }
                h.BarText = ((int)Math.Round(h.Frac * 100)) + "%";
                double avg = j.ActiveMs > 0 ? done * 1000.0 / j.ActiveMs : 0;
                h.TxtL = L.T("Average speed: ", "متوسط السرعة: ", "Vitesse moyenne : ") + Fmt.Size((long)avg) + "/s";
                h.TxtM = L.T("Total time: ", "الوقت الإجمالي: ", "Durée totale : ") + Fmt.Time(j.ActiveMs / 1000.0);
            }
            else
            {
                bool paused = j.Paused;
                h.Title = (paused ? L.T("Paused - ", "متوقف - ", "En pause - ") : verb) + j.Summary;
                if (!j.ScanDone)
                {
                    h.Marquee = true;
                    h.BarText = L.T("Scanning folders...  ", "جارٍ فحص المجلدات...  ", "Analyse des dossiers...  ") +
                                files + " " + L.T("files found", "ملف", "fichiers");
                }
                else h.BarText = (h.Frac * 100).ToString("0.0") + "%";
                if (paused) { h.ColA = Theme.Orange; h.ColB = Color.FromArgb(255, 210, 110); }
                else { h.ColA = Theme.Cyan; h.ColB = Theme.Violet; }
                h.TxtL = paused ? L.T("Speed: paused", "السرعة: متوقفة", "Vitesse : en pause")
                                : L.T("Speed: ", "السرعة: ", "Vitesse : ") + Fmt.Size((long)j.Speed) + "/s";
                h.TxtM = paused ? L.T("Total ETA: -", "الوقت المتبقي: -", "Temps restant : -")
                                : (j.ScanDone ? L.T("Total ETA: ", "الوقت المتبقي: ", "Temps restant : ") + Fmt.Time(j.Eta)
                                              : L.T("Total ETA: calculating...", "الوقت المتبقي: قيد الحساب...", "Temps restant : calcul..."));
            }
            lblErr.Text = j.LastError == null ? "" : L.T("Last error: ", "آخر خطأ: ", "Dernière erreur : ") + j.LastError;
        }

        void UpdateButtons()
        {
            bool active = cur != null && !cur.Finished;
            btnPause.Enabled = active;
            btnCancel.Enabled = active;
            string t = (cur != null && cur.Paused) ? L.T("Resume", "متابعة", "Reprendre") : L.T("Pause", "إيقاف", "Pause");
            if (btnPause.Text != t) btnPause.Text = t;
            bool showErr = cur != null && cur.Errors > 0;
            if (btnErrReport.Visible != showErr)
            {
                btnErrReport.Visible = showErr;
                bottom_Resize(null, EventArgs.Empty);
            }
        }

        // ------------------------------------------------------------
        //  events
        // ------------------------------------------------------------
        void btnPause_Click(object s, EventArgs e)
        {
            if (cur != null && !cur.Finished) cur.Paused = !cur.Paused;
        }

        void btnCancel_Click(object s, EventArgs e)
        {
            if (cur != null && !cur.Finished) cur.Cancel();
        }

        // Error report popup: full per-file error text with a copy-to-clipboard button.
        void btnErrReport_Click(object s, EventArgs e)
        {
            if (cur == null) return;
            string text = cur.ErrorReport();
            if (text.Length == 0) text = L.T("No errors.", "لا توجد أخطاء.", "Aucune erreur.");

            Form f = new Form();
            f.Text = L.T("Error report", "تقرير الأخطاء", "Rapport d'erreurs");
            f.Font = Theme.Cell;
            f.BackColor = Theme.Bg;
            f.ForeColor = Theme.Text;
            f.StartPosition = FormStartPosition.CenterParent;
            f.ClientSize = new Size(580, 360);
            f.MinimumSize = new Size(380, 200);
            L.ApplyRtl(f, true);

            TextBox tb = new TextBox();
            tb.Multiline = true;
            tb.ReadOnly = true;
            tb.ScrollBars = ScrollBars.Both;
            tb.WordWrap = false;
            tb.BorderStyle = BorderStyle.None;
            tb.BackColor = Theme.Row1;
            tb.ForeColor = Theme.Text;
            tb.Dock = DockStyle.Fill;
            tb.Text = text;

            Panel bar = new Panel();
            bar.Dock = DockStyle.Bottom;
            bar.Height = 46;
            bar.BackColor = Theme.Panel;
            Button copy = UI.MakeButton(L.T("Copy", "نسخ", "Copier"), 84, Color.FromArgb(70, 90, 170));
            Button close = UI.MakeButton(L.T("Close", "إغلاق", "Fermer"), 84, Color.FromArgb(120, 120, 130));
            copy.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            copy.Location = new Point(f.ClientSize.Width - 190, 9);
            close.Location = new Point(f.ClientSize.Width - 96, 9);
            copy.Click += delegate
            {
                try { Clipboard.SetText(tb.Text); copy.Text = L.T("Copied", "تم النسخ", "Copié"); }
                catch (Exception) { copy.Text = L.T("Failed", "فشل", "Échec"); }
            };
            close.Click += delegate { f.Close(); };
            bar.Controls.Add(copy);
            bar.Controls.Add(close);
            f.Controls.Add(tb);
            f.Controls.Add(bar);
            f.ShowDialog(this);
            f.Dispose();
        }

        void btnSettings_Click(object s, EventArgs e)
        {
            int before = L.Code;
            using (SettingsForm f = new SettingsForm()) f.ShowDialog(this);
            chkClose.Checked = Settings.AutoClose;
            if (L.Code != before) ApplyLanguage();
        }

        void btnHide_Click(object s, EventArgs e) { HideToTray(); }

        void btnMode_Click(object s, EventArgs e) { SetCompact(!compact); }

        // compact mode = progress header only; file list and bottom bar are hidden
        void SetCompact(bool on)
        {
            if (compact == on) return;
            compact = on;
            Settings.Compact = on;
            Settings.Save();
            if (on) savedSize = Size;
            MinimumSize = Size.Empty;      // lift constraints before resizing
            MaximumSize = Size.Empty;
            lv.Visible = !on;
            bottom.Visible = !on;
            jobBox.Visible = !on && jobs.Count >= 2;
            btnMode.Text = on ? L.T("Files", "الملفات", "Fichiers") : L.T("Compact", "مضغوط", "Compact");
            MaximizeBox = !on;
            if (on)
            {
                MinimumSize = new Size(420, 0);
                ClientSize = new Size(520, header.Height);
                MaximumSize = new Size(int.MaxValue, header.Height + (Height - ClientSize.Height));
            }
            else
            {
                MinimumSize = new Size(660, 380);
                if (savedSize.Width >= 660 && savedSize.Height >= 380) Size = savedSize;
                else ClientSize = new Size(800, 520);
            }
        }

        void chkClose_Changed(object s, EventArgs e)
        {
            Settings.AutoClose = chkClose.Checked;
            Settings.Save();
        }

        void HideToTray()
        {
            tray.Visible = true;
            Hide();
        }

        void ShowFromTray()
        {
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            tray.Visible = false;
        }

        void tray_Show(object s, EventArgs e) { ShowFromTray(); }

        void tray_Exit(object s, EventArgs e) { exiting = true; Close(); }

        void bottom_Resize(object s, EventArgs e)
        {
            int bw = btnErrReport != null && btnErrReport.Visible ? btnErrReport.Width : 0;
            if (L.Rtl)
            {
                int right = bottom.ClientSize.Width - 12;
                btnPause.SetBounds(right - btnPause.Width, 9, btnPause.Width, 28);
                btnCancel.SetBounds(btnPause.Left - 8 - btnCancel.Width, 9, btnCancel.Width, 28);
                btnSettings.SetBounds(btnCancel.Left - 8 - btnSettings.Width, 9, btnSettings.Width, 28);
                int chkLeft = btnSettings.Left - 16 - chkClose.Width;
                chkClose.Location = new Point(chkLeft, 14);
                int left = 12;
                if (bw > 0) { btnErrReport.SetBounds(left, 9, bw, 28); left += bw + 6; }
                lblErr.TextAlign = ContentAlignment.MiddleLeft;
                lblErr.SetBounds(left, 14, Math.Max(40, chkLeft - 14 - left), 18);
                return;
            }
            int x = chkClose.Right + 14;
            int rightLtr = bottom.ClientSize.Width - 12;
            if (bw > 0) btnErrReport.SetBounds(rightLtr - bw, 9, bw, 28);
            lblErr.TextAlign = ContentAlignment.MiddleRight;
            lblErr.SetBounds(x, 14, Math.Max(40, rightLtr - bw - 6 - x), 18);
        }

        void lv_Resize(object s, EventArgs e)
        {
            if (lv == null || colFile == null) return;
            int others = 0;
            for (int i = 0; i < lv.Columns.Count; i++)
                if (lv.Columns[i] != colFile) others += lv.Columns[i].Width;
            int w = lv.ClientSize.Width - others;
            if (w < 120) w = 120;
            if (colFile.Width != w) colFile.Width = w;
        }

        // Column 0 is File in English/French; in RTL the physical order is
        // reversed so the primary column sits on the right like Windows itself.
        void BuildColumns()
        {
            lv.Columns.Clear();
            if (L.Rtl)
            {
                lv.Columns.Add(L.T("Status", "الحالة", "État"), 120);
                lv.Columns.Add(L.T("ETA", "المتبقي", "Restant"), 90);
                lv.Columns.Add(L.T("Speed", "السرعة", "Vitesse"), 85);
                lv.Columns.Add(L.T("Progress", "التقدم", "Progression"), 150);
                lv.Columns.Add(L.T("Size", "الحجم", "Taille"), 80);
                colFile = lv.Columns.Add(L.T("File", "ملف", "Fichier"), 300);
            }
            else
            {
                colFile = lv.Columns.Add(L.T("File", "ملف", "Fichier"), 300);
                lv.Columns.Add(L.T("Size", "الحجم", "Taille"), 80);
                lv.Columns.Add(L.T("Progress", "التقدم", "Progression"), 150);
                lv.Columns.Add(L.T("Speed", "السرعة", "Vitesse"), 85);
                lv.Columns.Add(L.T("ETA", "المتبقي", "Restant"), 90);
                lv.Columns.Add(L.T("Status", "الحالة", "État"), 120);
            }
            lv_Resize(null, EventArgs.Empty);
        }

        void jobBox_Changed(object s, EventArgs e)
        {
            if (settingSel) return;
            int i = jobBox.SelectedIndex;
            if (i >= 0 && i < jobs.Count) SetCur(jobs[i]);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!exiting && AnyRunning())
            {
                DialogResult r = Msg.Show(this,
                    L.T("Copy operations are still running.\n\nYes = cancel and exit\nNo = hide to the tray and keep copying",
                        "عمليات النسخ ما زالت قيد التشغيل.\n\nنعم = إلغاء والخروج\nلا = الإخفاء إلى شريط المهام ومتابعة النسخ",
                        "Des copies sont encore en cours.\n\nOui = annuler et quitter\nNon = masquer dans la zone de notification et continuer"),
                    "Super Copy", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel) { e.Cancel = true; return; }
                if (r == DialogResult.No) { e.Cancel = true; HideToTray(); return; }
            }
            if (AnyRunning())
            {
                for (int i = 0; i < jobs.Count; i++) jobs[i].Cancel();
                int t0 = Environment.TickCount;
                while (Environment.TickCount - t0 < 3000)
                {
                    bool all = true;
                    for (int i = 0; i < jobs.Count; i++) if (!jobs[i].Finished) all = false;
                    if (all) break;
                    Thread.Sleep(30);
                }
            }
            timer.Stop();
            tray.Visible = false;
            tray.Dispose();
            base.OnFormClosing(e);
        }

        // ------------------------------------------------------------
        //  owner-drawn file list
        // ------------------------------------------------------------
        void lv_Retrieve(object s, RetrieveVirtualItemEventArgs e)
        {
            e.Item = new ListViewItem(new string[] { "", "", "", "", "", "" });
        }

        void lv_DrawHeader(object s, DrawListViewColumnHeaderEventArgs e)
        {
            Rectangle r = e.Bounds;
            if (r.Width <= 0 || r.Height <= 0) return;
            using (LinearGradientBrush lb = new LinearGradientBrush(r, Theme.Panel2, Theme.Panel, 90f))
                e.Graphics.FillRectangle(lb, r);
            using (Pen p = new Pen(Theme.Border))
            {
                int sepX = L.Rtl ? r.Left : r.Right - 1;
                e.Graphics.DrawLine(p, sepX, r.Top + 4, sepX, r.Bottom - 5);
                e.Graphics.DrawLine(p, r.Left, r.Bottom - 1, r.Right, r.Bottom - 1);
            }
            TextRenderer.DrawText(e.Graphics, e.Header.Text, Theme.Bold,
                new Rectangle(r.X + 6, r.Y, Math.Max(1, r.Width - 8), r.Height), Theme.Text, CellFlags);
        }

        void lv_DrawItem(object s, DrawListViewItemEventArgs e)
        {
            e.DrawDefault = false;   // everything is painted per cell in DrawSubItem
        }

        void lv_DrawSub(object s, DrawListViewSubItemEventArgs e)
        {
            CopyItem it = cur == null ? null : cur.GetItem(e.ItemIndex);
            Graphics g = e.Graphics;
            Rectangle r = e.Bounds;
            if (e.ColumnIndex == 0) r.Width = lv.Columns[0].Width;   // first-column bounds quirk
            bool sel = (e.ItemState & ListViewItemStates.Selected) != 0;
            g.FillRectangle(sel ? Theme.BrSel : ((e.ItemIndex & 1) == 0 ? Theme.BrRow1 : Theme.BrRow2), r);
            if (it == null) return;

            Rectangle tr = new Rectangle(r.X + 6, r.Y, Math.Max(1, r.Width - 10), r.Height);
            bool paused = cur.Paused;
            int logical = lv.Columns.Count - 1 - e.ColumnIndex;
            if (!L.Rtl) logical = e.ColumnIndex;
            switch (logical)
            {
                case 0:
                    TextRenderer.DrawText(g, it.Name == null ? "" : it.Name, Theme.Cell, tr, Theme.Text,
                        L.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.PathEllipsis |
                        TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
                    break;
                case 1:
                    TextRenderer.DrawText(g, it.Src == null ? "" : Fmt.Size(it.Size), Theme.Cell, tr, Theme.Dim,
                        L.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
                    break;
                case 2:
                    {
                        int st = it.State;
                        Color a, b;
                        Theme.BarColors(st, paused, out a, out b);
                        double f = it.Fraction;
                        string txt = (st == 0 || it.Src == null) ? "" : ((int)(f * 100)) + "%";
                        Gfx.Bar(g, new Rectangle(r.X + 4, r.Y + 4, r.Width - 8, r.Height - 8), f, a, b, txt, false, 0, 3);
                    }
                    break;
                case 3:
                    if (it.State == 1)
                        TextRenderer.DrawText(g, paused ? L.T("paused", "متوقف", "en pause") : Fmt.Size((long)it.Speed) + "/s", Theme.Cell, tr, Theme.Text,
                            L.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
                    break;
                case 4:
                    if (it.State == 1 && !paused && it.Speed > 1)
                    {
                        double left = (it.Size - Interlocked.Read(ref it.Done)) / it.Speed;
                        TextRenderer.DrawText(g, Fmt.Time(left), Theme.Cell, tr, Theme.Text,
                            L.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
                    }
                    break;
                case 5:
                    {
                        string t; Color c;
                        switch (it.State)
                        {
                            case 1: t = paused ? L.T("Paused", "متوقف", "En pause") : L.T("Copying", "نسخ", "Copie"); c = paused ? Theme.Orange : Theme.Cyan; break;
                            case 2: t = L.T("Done", "تم", "Terminé"); c = Theme.Green; break;
                            case 3: t = L.T("Error: ", "خطأ: ", "Erreur : ") + it.Error; c = Theme.Red; break;
                            case 4: t = L.T("Skipped", "تم التخطي", "Ignoré"); c = Theme.Dim; break;
                            case 5: t = L.T("Cancelled", "ملغى", "Annulé"); c = Theme.Orange; break;
                            default: t = L.T("Pending", "قيد الانتظار", "En attente"); c = Theme.Dim; break;
                        }
                        TextRenderer.DrawText(g, t, Theme.Cell, tr, c, CellFlags);
                    }
                    break;
            }
        }

        // ------------------------------------------------------------
        //  owner-drawn job strip
        // ------------------------------------------------------------
        void jobBox_DrawItem(object s, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= jobs.Count) return;
            CopyJob j = jobs[e.Index];
            Graphics g = e.Graphics;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            g.FillRectangle(sel ? Theme.BrSel : Theme.BrPanel, e.Bounds);

            Rectangle tr = L.Rtl
                ? new Rectangle(e.Bounds.X + 232, e.Bounds.Y, Math.Max(10, e.Bounds.Width - 240), e.Bounds.Height)
                : new Rectangle(e.Bounds.X + 8, e.Bounds.Y, Math.Max(10, e.Bounds.Width - 240), e.Bounds.Height);
            TextRenderer.DrawText(g, (e.Index + 1) + ".  " + (j.Move ? L.T("Move ", "نقل ", "Déplacer ") : L.T("Copy ", "نسخ ", "Copier ")) + j.Summary,
                Theme.Cell, tr, Theme.Text, CellFlags);

            int st = !j.Started ? 0 : (j.Finished ? (j.Cancelled ? 5 : (j.Errors > 0 ? 3 : 2)) : 1);
            Color a, b;
            Theme.BarColors(st, j.Paused, out a, out b);
            double f = (j.Finished && !j.Cancelled) ? 1.0 : j.Fraction;
            string txt = !j.Started ? L.T("Queued", "في الانتظار", "En attente") : ((int)Math.Round(f * 100)) + "%";
            int barX = L.Rtl ? e.Bounds.X + 12 : e.Bounds.Right - 218;
            Gfx.Bar(g, new Rectangle(barX, e.Bounds.Y + 5, 206, e.Bounds.Height - 10), f, a, b, txt, false, 0, 4);
        }
    }

    // ================================================================
    //  Settings window
    // ================================================================
    class SettingsForm : Form
    {
        NumericUpDown numConc;
        CheckBox chkSmartConc;
        ComboBox cmbBuf, cmbConf, cmbEngine, cmbLang;
        CheckBox chkClose;
        Label lblState;
        bool startupChecked;   // startup install/update suggestion runs once per window
        static readonly int[] BufSizes = new int[] { 64, 256, 1024, 4096 };

        Label MakeLabel(string text, int x, int y, Color c)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.ForeColor = c;
            l.BackColor = Theme.Bg;
            l.Location = new Point(x, y);
            return l;
        }

        public SettingsForm()
        {
            Text = L.T("Super Copy - Settings", "Super Copy - الإعدادات", "Super Copy - Paramètres");
            Font = Theme.Cell;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(430, 522);
            BackColor = Theme.Bg;
            Icon = IconMaker.MakeIcon();
            L.ApplyRtl(this, true);

            TitleBar tb = new TitleBar();
            tb.Title = "Super Copy";
            tb.Sub = L.T("Fast, light file copier with Explorer integration",
                         "ناسخ ملفات سريع وخفيف مع تكامل المستكشف",
                         "Copieur de fichiers rapide et léger avec intégration Explorateur");
            tb.Dock = DockStyle.Top;
            tb.Height = 56;
            Controls.Add(tb);

            Controls.Add(MakeLabel(L.T("Language:", "اللغة:", "Langue :"), 20, 72, Theme.Text));
            cmbLang = new ComboBox();
            cmbLang.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbLang.Items.AddRange(new object[] {
                L.T("Auto (system)", "تلقائي (النظام)", "Auto (système)"),
                "English", "العربية", "Français" });
            cmbLang.SelectedIndex = Math.Max(0, Math.Min(3, Settings.Language));
            cmbLang.SetBounds(240, 68, 160, 22);
            Controls.Add(cmbLang);

            chkSmartConc = new CheckBox();
            chkSmartConc.Text = L.T("Use smart concurrency (auto by file size)",
                                    "استخدام تزامن ذكي (تلقائي حسب حجم الملف)",
                                    "Utiliser la concurrence intelligente (auto selon la taille)");
            chkSmartConc.AutoSize = true;
            chkSmartConc.ForeColor = Theme.Text;
            chkSmartConc.BackColor = Theme.Bg;
            chkSmartConc.Checked = Settings.SmartConcurrency;
            chkSmartConc.Location = new Point(20, 106);
            chkSmartConc.CheckedChanged += delegate(object s, EventArgs e) { if (numConc != null) numConc.Enabled = !chkSmartConc.Checked; };
            Controls.Add(chkSmartConc);
            Controls.Add(MakeLabel(L.T("Concurrent copies (1 - 32):", "النسخ المتزامن (1 - 32):", "Copies simultanées (1 - 32) :"), 20, 134, Theme.Text));
            numConc = new NumericUpDown();
            numConc.Minimum = 1; numConc.Maximum = 32;
            numConc.Value = Math.Max(1, Math.Min(32, Settings.Concurrency));
            numConc.SetBounds(300, 130, 100, 22);
            numConc.BackColor = Theme.Panel2; numConc.ForeColor = Color.White;
            numConc.Enabled = !chkSmartConc.Checked;
            Controls.Add(numConc);
            Label hint = MakeLabel(L.T("Smart: <500KB→up to 10, 500KB-100MB→taper to 1, >100MB→1. Disable to set fixed value.",
                                       "ذكي: <500 ك.ب→حتى 10، 500 ك.ب-100 م.ب→تناقص إلى 1، >100 م.ب→1. عطّل لتعيين قيمة ثابتة.",
                                       "Intelligent : <500 Ko→jusqu'à 10, 500 Ko-100 Mo→réduction à 1, >100 Mo→1. Désactiver pour fixer une valeur."),
                                  20, 154, Theme.Dim);
            hint.AutoSize = false;
            hint.SetBounds(20, 154, 390, 32);
            Controls.Add(hint);

            Controls.Add(MakeLabel(L.T("Copy buffer per thread:", "ذاكرة النسخ لكل خيط:", "Tampon de copie par thread :"), 20, 186, Theme.Text));
            cmbBuf = new ComboBox();
            cmbBuf.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbBuf.Items.AddRange(new object[] { "64 KB", "256 KB", "1 MB", "4 MB" });
            int bi = 1;
            for (int i = 0; i < BufSizes.Length; i++) if (BufSizes[i] == Settings.BufferKB) bi = i;
            cmbBuf.SelectedIndex = bi;
            cmbBuf.SetBounds(240, 182, 160, 22);
            Controls.Add(cmbBuf);

            Controls.Add(MakeLabel(L.T("Copy engine:", "محرك النسخ:", "Moteur de copie :"), 20, 222, Theme.Text));
            cmbEngine = new ComboBox();
            cmbEngine.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbEngine.Items.Add(L.T("Built-in", "مدمج", "Intégré"));
            if (Settings.RobocopyAvailable) cmbEngine.Items.Add(L.T("Robocopy (Windows)", "Robocopy (ويندوز)", "Robocopy (Windows)"));
            cmbEngine.SelectedIndex = Math.Max(0, Math.Min(cmbEngine.Items.Count - 1, Settings.Engine));
            cmbEngine.Enabled = cmbEngine.Items.Count > 1;
            cmbEngine.SetBounds(240, 218, 160, 22);
            Controls.Add(cmbEngine);

            Controls.Add(MakeLabel(L.T("If a file already exists:", "إذا كان الملف موجوداً:", "Si un fichier existe déjà :"), 20, 258, Theme.Text));
            cmbConf = new ComboBox();
            cmbConf.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbConf.Items.AddRange(new object[] {
                L.T("Overwrite", "استبدال", "Remplacer"),
                L.T("Skip", "تخطي", "Ignorer"),
                L.T("Keep both (rename)", "الاحتفاظ بالاثنين (إعادة تسمية)", "Garder les deux (renommer)"),
                L.T("Overwrite only if newer", "استبدال فقط إذا كان أحدث", "Remplacer seulement si plus récent"),
                L.T("Ask first", "السؤال أولاً", "Demander d'abord") });
            cmbConf.SelectedIndex = Math.Max(0, Math.Min(4, Settings.Conflict));
            cmbConf.SetBounds(240, 254, 160, 22);
            Controls.Add(cmbConf);

            chkClose = new CheckBox();
            chkClose.Text = L.T("Close the window automatically when finished",
                                "إغلاق النافذة تلقائياً عند الانتهاء",
                                "Fermer la fenêtre automatiquement à la fin");
            chkClose.AutoSize = true;
            chkClose.ForeColor = Theme.Text;
            chkClose.BackColor = Theme.Bg;
            chkClose.Checked = Settings.AutoClose;
            chkClose.Location = new Point(20, 294);
            Controls.Add(chkClose);

            Controls.Add(MakeLabel(L.T("Explorer right-click menu", "قائمة النقر بزر الماوس الأيمن في المستكشف", "Menu clic droit de l'Explorateur"), 20, 334, Theme.Cyan));
            Button bi2 = UI.MakeButton(L.T("Install menu", "تثبيت القائمة", "Installer le menu"), 180, Color.FromArgb(40, 150, 100));
            Button br = UI.MakeButton(L.T("Remove menu", "إزالة القائمة", "Supprimer le menu"), 180, Color.FromArgb(190, 60, 80));
            bi2.Location = new Point(20, 356);
            br.Location = new Point(220, 356);
            bi2.Click += new EventHandler(OnInstall);
            br.Click += new EventHandler(OnRemove);
            Controls.Add(bi2);
            Controls.Add(br);
            lblState = MakeLabel("", 20, 394, Theme.Dim);
            Controls.Add(lblState);
            Controls.Add(MakeLabel(L.T("Windows 11: entries are under \"Show more options\" (Shift+F10).",
                                       "ويندوز 11: تظهر الإدخالات تحت \"إظهار المزيد من الخيارات\" (Shift+F10).",
                                       "Windows 11 : les entrées sont sous « Afficher plus d'options » (Maj+F10)."), 20, 414, Theme.Dim));
            RefreshState();

            Controls.Add(MakeLabel(L.T("Version ", "الإصدار ", "Version ") + Updates.Current, 20, 452, Theme.Text));
            Button babt = UI.MakeButton(L.T("About", "حول", "À propos"), 84, Color.FromArgb(90, 96, 130));
            babt.Location = new Point(132, 446);
            babt.Click += new EventHandler(OnAbout);
            Controls.Add(babt);
            Button bupd = UI.MakeButton(L.T("Check for updates", "التحقق من التحديثات", "Rechercher des mises à jour"), 190, Color.FromArgb(60, 120, 140));
            bupd.Location = new Point(226, 446);
            bupd.Click += new EventHandler(OnCheckUpdate);
            Controls.Add(bupd);

            Button ok = UI.MakeButton(L.T("Save", "حفظ", "Enregistrer"), 90, Color.FromArgb(70, 90, 170));
            Button cancel = UI.MakeButton(L.T("Cancel", "إلغاء", "Annuler"), 90, Color.FromArgb(70, 76, 110));
            Button reset = UI.MakeButton(L.T("Reset to default", "استعادة الافتراضي", "Réinitialiser"), 165, Color.FromArgb(120, 96, 60));
            ok.Location = new Point(236, 486);
            cancel.Location = new Point(334, 486);
            reset.Location = new Point(20, 486);
            ok.Click += new EventHandler(OnSave);
            cancel.Click += new EventHandler(OnCancel);
            reset.Click += new EventHandler(OnReset);
            Controls.Add(ok);
            Controls.Add(cancel);
            Controls.Add(reset);
            AcceptButton = ok;
            CancelButton = cancel;
            Shown += new EventHandler(OnShownOnce);
        }

        // on open: offer to add the per-user Explorer menu when it is missing,
        // and check GitHub for a newer version in the background
        void OnShownOnce(object s, EventArgs e)
        {
            if (startupChecked) return;
            startupChecked = true;
            if (!ShellIntegration.IsInstalled())
            {
                if (Msg.Show(this,
                    L.T("The Super Copy right-click menu is not installed.\n\nInstall it now?",
                        "قائمة النقر بزر الماوس الأيمن لـ Super Copy غير مثبتة.\n\nهل تريد تثبيتها الآن؟",
                        "Le menu clic droit de Super Copy n'est pas installé.\n\nL'installer maintenant ?"),
                    "Super Copy", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    OnInstall(this, EventArgs.Empty);
                }
            }
            Thread th = new Thread(new ThreadStart(StartupUpdateCheck));
            th.IsBackground = true;
            th.Start();
        }

        // GitHub check off the UI thread (5 s timeout, silent when offline)
        void StartupUpdateCheck()
        {
            try
            {
                int latest = Updates.GetLatest();
                if (latest <= Updates.Current) return;
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke(new EventHandler(delegate(object bs, EventArgs ea)
                {
                    if (!IsDisposed) Updates.OfferUpdate(this, latest);
                }));
            }
            catch { }
        }

        void RefreshState()
        {
            bool on = ShellIntegration.IsInstalled();
            string os = (Bitness.Is64Os ? L.T("64-bit", "64 بت", "64 bits") : L.T("32-bit", "32 بت", "32 bits")) +
                        " " + L.T("Windows", "ويندوز", "Windows");
            lblState.Text = (on
                ? L.T("Status: installed (", "الحالة: مثبتة (", "État : installé (") + os + ")"
                : L.T("Status: not installed (", "الحالة: غير مثبتة (", "État : non installé (") + os + ")");
            lblState.ForeColor = on ? Theme.Green : Theme.Dim;
        }

        void OnInstall(object s, EventArgs e)
        {
            try
            {
                ShellIntegration.Install(Application.ExecutablePath);
                RefreshState();
                Msg.Show(this, L.T(
                    "Done.\n\n1. Right-click files/folders > Super Copy / Super Cut (move) / Copy to dest...\n2. Right-click empty space inside a folder > Super Paste here",
                    "تم.\n\n1. انقر بزر الماوس الأيمن على الملفات/المجلدات > Super Copy / Super Cut (نقل) / Copy to dest...\n2. انقر بزر الماوس الأيمن على مساحة فارغة داخل مجلد > Super Paste here",
                    "Terminé.\n\n1. Clic droit sur fichiers/dossiers > Super Copy / Super Cut (déplacer) / Copy to dest...\n2. Clic droit dans un espace vide d'un dossier > Super Paste here"),
                    "Super Copy", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { Msg.Show(this, ex.Message, "Super Copy", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        void OnRemove(object s, EventArgs e)
        {
            try { ShellIntegration.Uninstall(); RefreshState(); }
            catch (Exception ex) { Msg.Show(this, ex.Message, "Super Copy", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        void OnCheckUpdate(object s, EventArgs e) { Updates.Check(this); }

        void OnAbout(object s, EventArgs e)
        {
            string exe = Application.ExecutablePath;
            string created = "unknown";
            try { created = File.GetCreationTime(exe).ToString("yyyy-MM-dd HH:mm"); } catch { }
            Msg.Show(this,
                L.T("Super Copy - version ", "Super Copy - الإصدار ", "Super Copy - version ") + Updates.Current + "\n" +
                L.T("Lightweight multi-threaded file copier for Windows XP-11.\n\n",
                    "ناسخ ملفات خفيف متعدد الخيوط لويندوز XP-11.\n\n",
                    "Copieur de fichiers léger et multi-thread pour Windows XP-11.\n\n") +
                L.T("Built: ", "البناء: ", "Compilé : ") + created + "\n" +
                "https://github.com/youcef07dz/super-copy",
                L.T("About Super Copy", "حول Super Copy", "À propos de Super Copy"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void OnSave(object s, EventArgs e)
        {
            Settings.Concurrency = (int)numConc.Value;
            if (chkSmartConc != null) Settings.SmartConcurrency = chkSmartConc.Checked;
            Settings.BufferKB = BufSizes[Math.Max(0, cmbBuf.SelectedIndex)];
            Settings.Conflict = Math.Max(0, cmbConf.SelectedIndex);
            Settings.Engine = Math.Max(0, cmbEngine.SelectedIndex);
            Settings.AutoClose = chkClose.Checked;
            Settings.Language = Math.Max(0, Math.Min(3, cmbLang.SelectedIndex));
            L.Apply(Settings.Language);
            Settings.Save();
            DialogResult = DialogResult.OK;
            Close();
        }

        void OnCancel(object s, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        // Fills the controls with the built-in defaults. Nothing is written until
        // the user presses Save, so Cancel still discards the reset.
        void OnReset(object s, EventArgs e)
        {
            Settings.ResetDefaults();
            numConc.Value = Settings.Concurrency;
            if (chkSmartConc != null)
            {
                chkSmartConc.Checked = Settings.SmartConcurrency;
                numConc.Enabled = !chkSmartConc.Checked;
            }
            int bi = 1;
            for (int i = 0; i < BufSizes.Length; i++) if (BufSizes[i] == Settings.BufferKB) bi = i;
            cmbBuf.SelectedIndex = bi;
            cmbEngine.SelectedIndex = Math.Max(0, Math.Min(cmbEngine.Items.Count - 1, Settings.Engine));
            cmbConf.SelectedIndex = Math.Max(0, Math.Min(4, Settings.Conflict));
            chkClose.Checked = Settings.AutoClose;
            cmbLang.SelectedIndex = Math.Max(0, Math.Min(3, Settings.Language));
        }
    }

    // ================================================================
    //  "File already exists" prompt used by the "Ask first" setting
    // ================================================================
    class ConflictDialog : Form
    {
        readonly RadioButton[] opts = new RadioButton[4];
        CheckBox chkSession;

        public int Choice
        {
            get { for (int i = 0; i < opts.Length; i++) if (opts[i].Checked) return i; return 1; }
        }
        public bool SaveForSession { get { return chkSession.Checked; } }

        public ConflictDialog(string dst)
        {
            Text = L.T("Super Copy - File exists", "Super Copy - الملف موجود", "Super Copy - Le fichier existe");
            Font = Theme.Cell;
            BackColor = Theme.Bg;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(400, 236);
            L.ApplyRtl(this, true);

            Controls.Add(MakeLabel(L.T("A file already exists in the destination:", "يوجد ملف بهذا الاسم في الوجهة:", "Un fichier existe déjà dans la destination :"), 16, 14, Theme.Text));
            Label name = MakeLabel(Path.GetFileName(dst), 16, 36, Theme.Cyan);
            name.AutoSize = false;
            name.AutoEllipsis = true;
            name.SetBounds(16, 36, 368, 18);
            Controls.Add(name);

            string[] cap = { L.T("Overwrite the existing file", "استبدال الملف الموجود", "Remplacer le fichier existant"),
                             L.T("Skip this file", "تخطي هذا الملف", "Ignorer ce fichier"),
                             L.T("Keep both (rename the incoming file)", "الاحتفاظ بالاثنين (إعادة تسمية الملف الوارد)", "Garder les deux (renommer le fichier entrant)"),
                             L.T("Overwrite only if the source is newer", "استبدال فقط إذا كان المصدر أحدث", "Remplacer seulement si la source est plus récente") };
            for (int i = 0; i < cap.Length; i++)
            {
                opts[i] = new RadioButton();
                opts[i].Text = cap[i];
                opts[i].AutoSize = true;
                opts[i].ForeColor = Theme.Text;
                opts[i].BackColor = Theme.Bg;
                opts[i].Location = new Point(18, 64 + i * 24);
                Controls.Add(opts[i]);
            }
            opts[0].Checked = true;

            chkSession = new CheckBox();
            chkSession.Text = L.T("Do this for the rest of this session", "افعل ذلك لبقية هذه الجلسة", "Faire cela pour le reste de la session");
            chkSession.AutoSize = true;
            chkSession.ForeColor = Theme.Text;
            chkSession.BackColor = Theme.Bg;
            chkSession.Location = new Point(18, 168);
            Controls.Add(chkSession);

            Button ok = UI.MakeButton(L.T("OK", "موافق", "OK"), 90, Color.FromArgb(70, 90, 170));
            Button cancel = UI.MakeButton(L.T("Cancel", "إلغاء", "Annuler"), 90, Color.FromArgb(70, 76, 110));
            ok.Location = new Point(196, 196);
            cancel.Location = new Point(294, 196);
            ok.Click += new EventHandler(OnOk);
            cancel.Click += new EventHandler(OnCancel);
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        Label MakeLabel(string text, int x, int y, Color c)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.ForeColor = c;
            l.BackColor = Theme.Bg;
            l.Location = new Point(x, y);
            return l;
        }

        void OnOk(object s, EventArgs e) { DialogResult = DialogResult.OK; Close(); }
        void OnCancel(object s, EventArgs e) { DialogResult = DialogResult.Cancel; Close(); }
    }

}
