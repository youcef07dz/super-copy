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
//    SuperCopy.exe /agent             background helper: Ctrl+V in Explorer copies with Super Copy
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
using System.Reflection;
using System.Runtime.InteropServices;
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
[assembly: System.Reflection.AssemblyFileVersion("3.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("2.0.0")]
[assembly: System.Reflection.AssemblyTrademark("Super Copy")]
// major part must mirror Updates.Current (v2 = 2.0.0.0); bump both + update.txt on release
[assembly: System.Reflection.AssemblyVersion("3.0.0.0")]

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
            string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "/settings";
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
            int rc = Gui(mode, rest);
            try { IconMaker.FreeIcons(); } catch { }
            return rc;
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
                    case "/agent":
                        return HookAgent.Run();
                    case "/install":
                        Installer.Install();
                        Msg.Show(L.T("Explorer menu installed.", "تم تثبيت قائمة المستكشف.", "Menu de l'Explorateur installé."), "Super Copy", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return 0;
                    case "/uninstall":
                        Installer.Remove();
                        Msg.Show(L.T("Explorer menu removed.", "تمت إزالة قائمة المستكشف.", "Menu de l'Explorateur supprimé."), "Super Copy", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return 0;
                    case "/settings":
                        HookAgent.Ensure();     // the helper starts with Windows while it is the default copy
                        Application.Run(new SettingsForm());
                        return 0;
                    default:
                        // an unknown switch must not silently start the helper or a window
                        Msg.Show(L.T("Unknown option: ", "خيار غير معروف: ", "Option inconnue : ") + mode,
                                 "Super Copy", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return 2;
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

        // a question where the safe answer is not the first button
        public static DialogResult ShowSafe(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            return MessageBox.Show(owner, text, caption, buttons, icon, MessageBoxDefaultButton.Button2, Opts);
        }
    }

    // ================================================================
    //  Paths + Settings
    // ================================================================
    static class Paths
    {
        public static string AppDir, ClipFile, ClipDevice, QueueDir, Ini, ExeInstalled, ExeLegacy;

        public static void Init()
        {
            string ad = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (ad == null || ad.Length == 0) ad = Path.GetTempPath();
            AppDir = Path.Combine(ad, "SuperCopy");
            ClipFile = Path.Combine(AppDir, "clipboard.txt");
            ClipDevice = Path.Combine(AppDir, "clipboard.device.txt"); // separate so it never clobbers a pending /copy
            QueueDir = Path.Combine(AppDir, "queue");
            Ini = Path.Combine(AppDir, "settings.ini");
            // the installed copy carries its own name: two "SuperCopy.exe" in the task
            // manager (the app the user starts and this one) could not be told apart
            ExeInstalled = Path.Combine(AppDir, "SuperCopyHelper.exe");
            ExeLegacy = Path.Combine(AppDir, "SuperCopy.exe");     // older installs: the app's own name
            try { Directory.CreateDirectory(QueueDir); } catch { }
            try { File.Delete(ExeInstalled + ".old"); } catch { }   // leftover from removing while our own copy ran
            try { File.Delete(ExeLegacy + ".old"); } catch { }      // same, for the older file name
            // a claimed job file older than 10 minutes is a leftover from a crash: clean it up
            try
            {
                string[] stale = Directory.GetFiles(QueueDir, "*.claimed");
                DateTime lim = DateTime.Now.AddMinutes(-10);
                for (int i = 0; i < stale.Length; i++)
                    if (File.GetLastWriteTime(stale[i]) < lim) File.Delete(stale[i]);
            }
            catch { }
        }

        // write via a temp file, then swap it in; a crash mid-write cannot truncate the real file
        public static void WriteAllLinesSafe(string path, string[] lines, Encoding enc)
        {
            string tmp = path + ".tmp";
            try { File.Delete(tmp); } catch { }
            if (enc == null) File.WriteAllLines(tmp, lines); else File.WriteAllLines(tmp, lines, enc);
            // File.Copy overwrites in place: a reader can see a half-written file.
            // File.Replace/Move swap the name, so a reader always sees one whole file.
            try
            {
                if (File.Exists(path))
                {
                    try { File.Replace(tmp, path, null); }
                    catch (Exception)
                    {
                        File.Delete(path);      // fallback for filesystems without Replace
                        File.Move(tmp, path);
                    }
                }
                else File.Move(tmp, path);
            }
            finally { try { File.Delete(tmp); } catch { } }
        }
    }

    static class Settings
    {
        public const int DefaultConcurrency = 2;             // single source of truth for "Reset to default"
        public const int DefaultBufferKB = 256;
        public const int DefaultConflict = 0;
        public const int DefaultEngine = 0;
        public const bool DefaultVerify = true;
        public const bool DefaultDefaultCopy = true;    // Ctrl+V in Explorer is done by Super Copy

        public static volatile int Concurrency = DefaultConcurrency;   // simultaneous file copies (1..32) when smart disabled
        public static volatile bool SmartConcurrency = true;           // use file-size based concurrency (default)
        public static volatile int BufferKB = DefaultBufferKB;         // copy buffer per thread
        public static volatile int Conflict = DefaultConflict;         // 0 overwrite, 1 skip, 2 keep both, 3 overwrite if newer, 4 ask
        public static volatile int AskSession = -1;   // "ask first" answer kept for this run only (-1 = none yet)
        public static int AskGen;                 // bumped when a prompt is cancelled: the other waiters skip too
        public static int AskPrompting;               // 1 while a worker owns the "ask first" dialog (Interlocked)
        public static volatile int Engine = DefaultEngine;             // 0 built-in, 1 robocopy
        public static volatile bool Verify = DefaultVerify;            // hash source+destination after every copy/move
        public static volatile bool DefaultCopy = DefaultDefaultCopy;  // Ctrl+V in Explorer runs Super Copy instead of the shell
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
                    string k = line.Substring(0, i).Trim().ToLowerInvariant();
                    string v = line.Substring(i + 1).Trim();
                    int n;
                    switch (k)
                    {
                        case "concurrency": if (int.TryParse(v, out n)) Concurrency = Clamp(n, 1, 32); break;
                        case "smartconcurrency": SmartConcurrency = (v == "1"); break;
                        case "bufferkb": if (int.TryParse(v, out n)) BufferKB = Clamp(n, 16, 16384); break;
                        case "conflict": if (int.TryParse(v, out n)) Conflict = Clamp(n, 0, 4); break;
                        case "engine": if (int.TryParse(v, out n)) Engine = Clamp(n, 0, 1); break;
                        case "verify": Verify = (v == "1"); break;
                        case "defaultcopy": DefaultCopy = (v == "1"); break;
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
                    "Verify=" + (Verify ? "1" : "0"),
                    "DefaultCopy=" + (DefaultCopy ? "1" : "0"),
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
            Verify = DefaultVerify;
            DefaultCopy = DefaultDefaultCopy;
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
    //  Windows bitness (managed: PROCESSOR_ARCHITEW6432 / ARCHITECTURE)
    // ================================================================
    static class Bitness
    {
        public static readonly bool Is64Os;

        static Bitness()
        {
            string a = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432");
            if (a == null || a.Length == 0) a = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
            Is64Os = a != null && a.IndexOf("64") >= 0;
        }
    }

    // ================================================================
    //  a hidden window that never steals the focus
    // ================================================================
    class HostForm : Form
    {
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000;      // WS_EX_NOACTIVATE
                return cp;
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
        public const int Current = 3;   // must match AssemblyVersion's major part
        const string UpdateTxtUrl = "https://raw.githubusercontent.com/youcef07dz/super-copy/main/update.txt";
        const string ReleasesUrl = "https://github.com/youcef07dz/super-copy/releases";

        public static int ParseVersion(string s)
        {
            if (s == null) return 0;
            int v;
            return int.TryParse(s.Trim(), out v) ? v : 0;
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
                if (!File.Exists(Paths.ExeInstalled)) return false;
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Classes\*\shell\SuperCopy\command"))
                    return k != null;   // shared HKCU keys are visible to 32- and 64-bit Explorer
            }
            catch { return false; }
        }

        // registers the menu verbs to point at "exe" (the installed %APPDATA% copy)
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
    //  Install / remove: deploy the running exe to %APPDATA%\SuperCopy
    //  and point every Explorer verb at that copy, so all shell
    //  operations use the installed exe rather than the build folder.
    // ================================================================
    static class Installer
    {
        public static void Install()
        {
            DropLegacy();   // an older install kept a copy under the app's own name
            bool wasOn = Settings.DefaultCopy;
            try
            {
                if (!string.Equals(Application.ExecutablePath, Paths.ExeInstalled, StringComparison.OrdinalIgnoreCase))
                {
                    Directory.CreateDirectory(Paths.AppDir);
                    // a running helper holds the installed copy: let it leave and free the path
                    if (wasOn) { Settings.DefaultCopy = false; Settings.Save(); }
                    RemoveFile(Paths.ExeInstalled, 20);
                    File.Copy(Application.ExecutablePath, Paths.ExeInstalled, true);
                    if (!File.Exists(Paths.ExeInstalled))
                        throw new IOException("the program could not be copied to " + Paths.AppDir);
                }
            }
            catch (Exception ex)
            {
                throw new IOException(ex.Message, ex);
            }
            finally { if (wasOn) { Settings.DefaultCopy = true; Settings.Save(); } }
            ShellIntegration.Install(Paths.ExeInstalled);
            HookAgent.Apply();   // the helper follows the setting: it runs only while Super Copy is the default copy
        }

        public static void Remove()
        {
            ShellIntegration.Uninstall();
            HookAgent.Disable();
            Settings.DefaultCopy = false;
            Settings.Save();      // the helper watches settings.ini and leaves within a couple of seconds
            DropLegacy();         // the old copy may still be running too
            string exe = Paths.ExeInstalled;
            if (!File.Exists(exe)) return;
            bool self = string.Equals(Application.ExecutablePath, exe, StringComparison.OrdinalIgnoreCase);
            RemoveFile(exe, self ? 1 : 20);   // the helper holds the file while it runs
        }

        // delete a file that a running copy may be holding: retry for a while,
        // then rename it away (renaming a running exe works, deleting it does not)
        internal static bool RemoveFile(string path, int tries)
        {
            for (int i = 0; i < tries; i++)
            {
                if (!File.Exists(path)) return true;
                try { File.Delete(path); } catch { }
                if (!File.Exists(path)) return true;
                Thread.Sleep(200);
            }
            try { File.Move(path, path + ".old"); } catch { }
            return !File.Exists(path);
        }

        // an older install kept a copy under the app's own name: two identical
        // entries in the task manager, and the file could not be deleted while it ran
        static void DropLegacy()
        {
            string legacy = Paths.ExeLegacy;
            if (!File.Exists(legacy)) return;
            if (string.Equals(Application.ExecutablePath, legacy, StringComparison.OrdinalIgnoreCase)) return;   // we are it
            try { File.Delete(legacy); return; } catch { }
            // the old helper still runs from it: it leaves when it sees the option off
            bool wasOn = Settings.DefaultCopy;
            try
            {
                // the old helper leaves when it sees the option off
                if (wasOn) { Settings.DefaultCopy = false; Settings.Save(); }
                RemoveFile(legacy, 20);
            }
            catch { }
            finally { if (wasOn) { Settings.DefaultCopy = true; Settings.Save(); } }
        }
    }

    // ================================================================
    //  Default-copy agent  (SuperCopy.exe /agent)
    //
    //  A small background helper that takes over Ctrl+V inside
    //  Explorer: the paste is done by Super Copy (same engine, same
    //  conflict rules, XXH3 verify, progress window) instead of the
    //  shell's own copy.  It watches Ctrl+V only, only while an
    //  Explorer window has the focus, and never stores a keystroke.
    // ================================================================
    static class HookAgent
    {
        delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        struct GuiThreadInfo
        {
            public int cbSize, flags;
            public IntPtr hwndActive, hwndFocus, hwndCapture, hwndMenuOwner, hwndMoveSize, hwndCaret;
            public Rectangle rcCaret;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern int GetClassName(IntPtr hWnd, StringBuilder s, int n);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)] static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")] static extern bool GetGUIThreadInfo(uint idThread, ref GuiThreadInfo gti);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr pid);
        [DllImport("user32.dll")] static extern bool OpenClipboard(IntPtr hWndNewOwner);
        [DllImport("user32.dll")] static extern bool CloseClipboard();
        [DllImport("user32.dll")] static extern IntPtr GetClipboardData(uint uFormat);
        [DllImport("user32.dll")] static extern bool IsClipboardFormatAvailable(uint format);
        [DllImport("kernel32.dll")] static extern IntPtr GlobalLock(IntPtr hMem);
        [DllImport("kernel32.dll")] static extern bool GlobalUnlock(IntPtr hMem);
        [DllImport("shell32.dll", CharSet = CharSet.Auto)] static extern uint DragQueryFile(IntPtr hDrop, uint iFile, StringBuilder lpszFile, int cch);

        const int WH_KEYBOARD_LL = 13;
        const int WM_KEYDOWN = 0x0100;
        const uint CF_HDROP = 15;
        const uint CF_PREFERREDDROPEFFECT = 11;
        const int DROPEFFECT_MOVE = 2;

        static HookProc proc;                       // keep the delegate alive
        static IntPtr hook = IntPtr.Zero;
        static ApplicationContext ctx;
        static Form host;                           // hidden window: marshals work onto the UI thread
        static NotifyIcon tray;
        static System.Windows.Forms.Timer watch;
        static MainForm win;                        // the progress window of the agent
        static bool pasteBusy;

        // ------------------------------------------------------------
        //  run / stop
        // ------------------------------------------------------------
        public static int Run()
        {
            bool created;
            Mutex m = new Mutex(true, "SuperCopy_Agent_v1", out created);
            if (!created) { m.Close(); return 0; }     // already running
            try
            {
                if (!Settings.DefaultCopy) return 0;  // switched off meanwhile

                host = new HostForm();
                host.ShowInTaskbar = false;
                host.StartPosition = FormStartPosition.Manual;
                host.Location = new Point(-32000, -32000);
                host.Size = new Size(1, 1);
                IntPtr hh = host.Handle;               // create the handle for BeginInvoke
                if (hh == IntPtr.Zero) return 1;

                ctx = new ApplicationContext();
                ctx.MainForm = host;   // the hidden window keeps the message loop alive

                tray = new NotifyIcon();
                tray.Icon = IconMaker.MakeIcon();
                tray.Text = "Super Copy";
                tray.Visible = true;
                ContextMenu menu = new ContextMenu();
                menu.MenuItems.Add(L.T("Settings", "الإعدادات", "Paramètres"), new EventHandler(OpenSettings));
                menu.MenuItems.Add("-");
                menu.MenuItems.Add(L.T("Exit", "خروج", "Quitter"), new EventHandler(Stop));
                tray.ContextMenu = menu;

                watch = new System.Windows.Forms.Timer();
                watch.Interval = 2000;
                watch.Tick += new EventHandler(Watch);
                watch.Start();

                proc = new HookProc(Hook);
                try { hook = SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(null), 0); }
                catch (Exception ex) { Msg.Show(ex.Message, "Super Copy", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
                if (hook == IntPtr.Zero)
                {
                    Msg.Show(L.T("The keyboard hook could not be installed.",
                                 "تعذّر تثبيت خطّاف لوحة المفاتيح.",
                                 "Le hook clavier n'a pas pu être installé."),
                             "Super Copy", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 1;
                }

                Application.Run(ctx);
                return 0;
            }
            finally
            {
                if (hook != IntPtr.Zero) { try { UnhookWindowsHookEx(hook); } catch { } hook = IntPtr.Zero; }
                if (watch != null) { try { watch.Stop(); watch.Dispose(); } catch { } watch = null; }
                if (tray != null) { try { tray.Visible = false; tray.Dispose(); } catch { } tray = null; }
                if (host != null) { try { host.Dispose(); } catch { } host = null; }
                try { m.ReleaseMutex(); } catch { }
                m.Close();
            }
        }

        // the helper follows the setting: it leaves when the option is switched off
        static DateTime iniStamp = DateTime.MinValue;
        static bool iniSeen = false;
        static void Watch(object s, EventArgs e)
        {
            try
            {
                // only read the file when it really changed (this runs all day)
                DateTime w;
                try { w = File.GetLastWriteTimeUtc(Paths.Ini); } catch { w = DateTime.MinValue; }
                if (iniSeen && w == iniStamp) return;
                iniSeen = true;
                iniStamp = w;
                Settings.Load();
                if (!Settings.DefaultCopy) Stop(s, e);
            }
            catch { }
        }

        static void Stop(object s, EventArgs e)
        {
            try { if (ctx != null) ctx.ExitThread(); } catch { }
        }

        static void OpenSettings(object s, EventArgs e)
        {
            try { Process.Start(new ProcessStartInfo(ExePath(), "/settings")); } catch { }
        }

        // ------------------------------------------------------------
        //  enabling / disabling the helper
        // ------------------------------------------------------------
        public static void Apply()
        {
            if (Settings.DefaultCopy) Enable(); else Disable();
        }

        // the startup entry follows the option: the helper runs with Windows
        // while Super Copy is the default copy
        public static void Ensure()
        {
            if (Settings.DefaultCopy) { Autostart(true); Start(); }
            else Autostart(false);
        }

        public static void Enable()
        {
            Autostart(true);
            Start();
        }

        public static void Disable()
        {
            Autostart(false);
        }

        static void Autostart(bool on)
        {
            try
            {
                using (RegistryKey r = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (r == null) return;
                    // only the installed copy may start with Windows, never the build folder
                    string exe = Paths.ExeInstalled;
                    if (on && File.Exists(exe)) r.SetValue("SuperCopy", "\"" + exe + "\" /agent");
                    else r.DeleteValue("SuperCopy", false);
                }
            }
            catch { }
        }

        public static void Start()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(ExePath(), "/agent");
                psi.UseShellExecute = false;
                Process.Start(psi);
            }
            catch { }
        }

        static string ExePath()
        {
            try { if (File.Exists(Paths.ExeInstalled)) return Paths.ExeInstalled; } catch { }
            return Application.ExecutablePath;
        }

        // ------------------------------------------------------------
        //  keyboard hook
        // ------------------------------------------------------------
        static IntPtr Hook(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && (int)wParam == WM_KEYDOWN)
                {
                    int vk = Marshal.ReadInt32(lParam) & 0xFF;
                    // the cheap test first: this callback runs for every key press of the whole system
                    if (vk == 0x56 &&
                        (GetAsyncKeyState(0x11) & 0x8000) != 0 && (GetAsyncKeyState(0x10) & 0x8000) == 0)
                    {
                        // only take over when the clipboard really holds files
                        bool hasFiles = false;
                        try { hasFiles = IsClipboardFormatAvailable(CF_HDROP); } catch { }
                        if (hasFiles)
                        {
                            IntPtr fg = GetForegroundWindow();
                            if (IsExplorer(fg) && !InEditBox(fg))
                            {
                                Post(fg);
                                return new IntPtr(1);          // swallow: Explorer must not paste
                            }
                        }
                    }
                }
            }
            catch { }
            return CallNextHookEx(hook, nCode, wParam, lParam);
        }

        static void Post(IntPtr fg)
        {
            try { host.BeginInvoke(new MethodInvoker(delegate { OnKey(fg); })); }
            catch { }
        }

        static void OnKey(IntPtr fg)
        {
            if (pasteBusy) return;
            pasteBusy = true;
            try { Paste(fg); }
            catch (Exception ex)
            {
                // never let an exception kill the helper
                try { Msg.Show(ex.Message, "Super Copy", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
            }
            finally { pasteBusy = false; }
        }

        static void Paste(IntPtr fg)
        {
            List<string> src = new List<string>();
            bool move;
            src = ClipFiles(out move);          // Explorer keeps the cut/copy flag on the clipboard
            if (src.Count == 0)
            {
                Msg.Show(L.T("Nothing to paste: the clipboard holds no files.",
                             "لا يوجد ما يتم لصقه: الحافظة لا تحتوي على ملفات.",
                             "Rien à coller : le presse-papiers ne contient aucun fichier."),
                         "Super Copy", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string dest = ExplorerDest(fg);
            if (dest == null || !Directory.Exists(dest))
            {
                Msg.Show(L.T("Cannot find the destination folder.",
                             "تعذّر العثور على مجلد الوجهة.",
                             "Dossier de destination introuvable."),
                         "Super Copy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            RunJob(src, dest, move);
        }

        // a running Super Copy window takes the job, otherwise we do it here
        static void RunJob(List<string> src, string dest, bool move)
        {
            bool created;
            Mutex m = null;
            try { m = new Mutex(true, "SuperCopy_MainInstance_v1", out created); }
            catch (AbandonedMutexException) { created = true; }   // the previous owner died: we own it now
            if (!created)
            {
                if (m != null) m.Close();
                QueueStore.Enqueue(move, dest, src);   // the open window picks it up
                return;
            }
            try
            {
                if (win == null || win.IsDisposed)
                {
                    win = new MainForm(null);
                    win.FormClosed += delegate(object s, FormClosedEventArgs e) { win = null; };
                    win.Show();
                }
                win.AddJob(new CopyJob(src, dest, move));
                try { win.Activate(); } catch { }
            }
            catch (Exception ex)
            {
                try { Msg.Show(ex.Message, "Super Copy", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
            }
            finally
            {
                try { m.ReleaseMutex(); } catch { }
                m.Close();
            }
        }

        // ------------------------------------------------------------
        //  clipboard (the file list Explorer put there)
        // ------------------------------------------------------------
        static List<string> ClipFiles(out bool move)
        {
            List<string> r = new List<string>();
            move = false;
            bool open = false;
            for (int i = 0; i < 5 && !open; i++)
            {
                try { open = OpenClipboard(IntPtr.Zero); } catch { }
                if (!open) Thread.Sleep(20);
            }
            if (!open) return r;
            try
            {
                // the drop effect lives in global memory: it must be locked while it is read
                IntPtr pref = GetClipboardData(CF_PREFERREDDROPEFFECT);
                if (pref != IntPtr.Zero)
                {
                    IntPtr mem = GlobalLock(pref);
                    try { if (mem != IntPtr.Zero) move = (Marshal.ReadInt32(mem) == DROPEFFECT_MOVE); }
                    finally { if (mem != IntPtr.Zero) { try { GlobalUnlock(pref); } catch { } } }
                }
                IntPtr h = GetClipboardData(CF_HDROP);
                if (h == IntPtr.Zero) return r;
                uint n = DragQueryFile(h, 0xFFFFFFFF, null, 0);
                for (uint i = 0; i < n; i++)
                {
                    // ask for the real length first: a fixed buffer would cut long paths
                    int len = (int)DragQueryFile(h, i, null, 0);
                    StringBuilder sb = new StringBuilder(len + 1);
                    if (DragQueryFile(h, i, sb, sb.Capacity) > 0) r.Add(sb.ToString());
                }
            }
            catch { }
            finally { try { CloseClipboard(); } catch { } }
            return r;
        }

        // ------------------------------------------------------------
        //  Explorer: which window, which folder, which selection
        // ------------------------------------------------------------
        static object FindWindow(IntPtr fg)
        {
            try
            {
                Type t = Type.GetTypeFromProgID("Shell.Application");
                if (t == null) return null;
                object shell = Activator.CreateInstance(t);
                object windows = Invoke(shell, "Windows");
                if (windows == null) return null;
                foreach (object w in (System.Collections.IEnumerable)windows)
                {
                    try { if (Convert.ToInt64(Get(w, "HWND")) == fg.ToInt64()) return w; } catch { }
                }
            }
            catch { }
            return null;
        }

        // one selected folder -> paste into it, otherwise the folder of the window
        static string ExplorerDest(IntPtr fg)
        {
            try
            {
                object w = FindWindow(fg);
                if (w != null)
                {
                    try
                    {
                        object doc = Get(w, "Document");
                        object items = Get(doc, "SelectedItems");
                        if (items != null && Convert.ToInt32(Get(items, "Count")) == 1)
                        {
                            string p = (string)Get(Invoke(items, "Item", 1), "Path");
                            if (p != null && Directory.Exists(p)) return p;
                        }
                    }
                    catch { }
                    try
                    {
                        string path = UrlToPath((string)Get(w, "LocationURL"));
                        if (path != null) return path;
                    }
                    catch { }
                }
                if (IsDesktop(ClassName(fg)))
                    return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            }
            catch { }
            return null;
        }

        // late binding for the Shell.Application COM objects
        // (GetProperty alone fails on SelectedItems: the binder only finds it
        //  when the method flag is there as well)
        static object Get(object o, string name)
        {
            return o.GetType().InvokeMember(name, BindingFlags.GetProperty | BindingFlags.InvokeMethod, null, o, null);
        }

        static object Invoke(object o, string name, params object[] args)
        {
            return o.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, o, args);
        }

        public static string UrlToPath(string url)
        {
            if (url == null) return null;
            try
            {
                if (!url.StartsWith("file:")) return null;
                string p = new Uri(url).LocalPath;
                if (p.Length > 3 && p.EndsWith("\\")) p = p.Substring(0, p.Length - 1);
                return p;
            }
            catch { return null; }
        }

        // ------------------------------------------------------------
        //  window helpers
        // ------------------------------------------------------------
        public static bool IsExplorerClass(string cls)
        {
            return cls == "CabinetWClass" || IsDesktop(cls);
        }

        static bool IsDesktop(string cls)
        {
            return cls == "Progman" || cls == "WorkerW";
        }

        static bool IsExplorer(IntPtr h)
        {
            return IsExplorerClass(ClassName(h));
        }

        static string ClassName(IntPtr h)
        {
            try
            {
                StringBuilder sb = new StringBuilder(64);
                GetClassName(h, sb, sb.Capacity);
                return sb.ToString();
            }
            catch { return ""; }
        }

        // an open text box (address bar, rename) must keep its own Ctrl+V
        static bool InEditBox(IntPtr fg)
        {
            try
            {
                uint tid = GetWindowThreadProcessId(fg, IntPtr.Zero);
                GuiThreadInfo gti = new GuiThreadInfo();
                gti.cbSize = Marshal.SizeOf(typeof(GuiThreadInfo));
                if (!GetGUIThreadInfo(tid, ref gti)) return false;
                if (gti.hwndFocus == IntPtr.Zero) return false;
                string c = ClassName(gti.hwndFocus);
                return c == "Edit" || c == "ComboBox" || c == "msctls_edit" ||
                       c == "RICHEDIT" || c == "RICHEDIT50W";
            }
            catch { return false; }
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
        public static readonly Pen PenBorder = new Pen(Border);

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
                g.DrawPath(Theme.PenBorder, path);
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
            {
                IntPtr h = b.GetHicon();
                handles.Add(h);
                return Icon.FromHandle(h);
            }
        }

        // GetHicon gives out an unmanaged icon: it must be destroyed by hand
        static List<IntPtr> handles = new List<IntPtr>();
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr hIcon);
        public static void FreeIcons()
        {
            for (int i = 0; i < handles.Count; i++) { try { DestroyIcon(handles[i]); } catch { } }
            handles.Clear();
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
        readonly Dictionary<string, object> dstLocks = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        readonly object dstLockMap = new object();

        public long TotalBytes, DoneBytes;
        public int Errors, FilesDone;
        public volatile bool ScanDone, Paused, Cancelled, Finished, Started;
        public volatile bool AccessDenied;   // at least one item failed with UnauthorizedAccessException
        public string LastError;
        public double Speed;
        public int ActiveMs;
        long lastDone;
        int next, workers;
        bool scanStarted;                // the scan thread is running (or already finished)
        long spaceLimit = -1, needBytes;   // destination free space (bytes); -1 = unknown
        bool noSpace;                      // scanner-only: true once the queued bytes exceed free space

        public CopyJob(List<string> sources, string dest, bool move)
        {
            Sources = sources; Dest = dest; Move = move;
            string first = "";
            try { if (sources != null && sources.Count > 0 && sources[0] != null) first = Path.GetFileName(sources[0].TrimEnd('\\', '/')); }
            catch { first = ""; }
            if (first == null || first.Length == 0) first = (sources != null && sources.Count > 0 && sources[0] != null) ? sources[0] : (dest == null ? "" : dest);
            string what = (sources == null || sources.Count == 1) ? "\"" + first + "\"" : sources.Count + " " + L.T("items", "عنصر", "éléments");
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
            Prepare();     // scan may already be running (started when the job was queued)
            for (int i = 0; i < n; i++) SpawnWorker();
        }

        // Scan in the background as soon as the job is queued, so the file list and the
        // total size are known right away (the copy itself still waits for Start()).
        public void Prepare()
        {
            lock (sync)
            {
                if (scanStarted) return;
                scanStarted = true;
            }
            Thread st = new Thread(new ThreadStart(ScanMain), 256 * 1024);
            st.IsBackground = true;
            st.Start();
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
            int targetTotal = ComputeTarget(null);
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
        // the file that is set aside while the destination is written. The name is unique, so a
        // user file called "<name>.sctmp" is never touched and two copies cannot collide.
        static string BakPath(string dst)
        {
            return dst + ".sctmp." + Guid.NewGuid().ToString("N").Substring(0, 8);
        }
        // removes a destination this run created; if the file is locked (scanner) it is aged, so a
        // later run with "overwrite if newer" can never keep the broken leftover
        static void RemovePartial(string dst)
        {
            try { File.Delete(dst); }
            catch { try { File.SetLastWriteTime(dst, new DateTime(1601, 1, 1)); } catch { } }
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
        // XXH3-64 (xxHash3, default secret, seed 0). Portable scalar port of the reference
        // implementation, so copy/move verification is fast enough to run on every file.
        static class XXH3
        {
            const ulong P32_1 = 0x9E3779B1UL, P32_2 = 0x85EBCA77UL, P32_3 = 0xC2B2AE3DUL;
            const ulong P64_1 = 0x9E3779B185EBCA87UL, P64_2 = 0xC2B2AE3D27D4EB4FUL;
            const ulong P64_3 = 0x165667B19E3779F9UL, P64_4 = 0x85EBCA77C2B2AE63UL, P64_5 = 0x27D4EB2F165667C5UL;
            const ulong MX1 = 0x165667919E3779F9UL, MX2 = 0x9FB21C651E98DF25UL;
            const int SecretSize = 192, StripeLen = 64, ConsumeRate = 8, BlockLen = StripeLen * 16;

            static readonly byte[] S = new byte[] {
                0xb8, 0xfe, 0x6c, 0x39, 0x23, 0xa4, 0x4b, 0xbe, 0x7c, 0x01, 0x81, 0x2c, 0xf7, 0x21, 0xad, 0x1c,
                0xde, 0xd4, 0x6d, 0xe9, 0x83, 0x90, 0x97, 0xdb, 0x72, 0x40, 0xa4, 0xa4, 0xb7, 0xb3, 0x67, 0x1f,
                0xcb, 0x79, 0xe6, 0x4e, 0xcc, 0xc0, 0xe5, 0x78, 0x82, 0x5a, 0xd0, 0x7d, 0xcc, 0xff, 0x72, 0x21,
                0xb8, 0x08, 0x46, 0x74, 0xf7, 0x43, 0x24, 0x8e, 0xe0, 0x35, 0x90, 0xe6, 0x81, 0x3a, 0x26, 0x4c,
                0x3c, 0x28, 0x52, 0xbb, 0x91, 0xc3, 0x00, 0xcb, 0x88, 0xd0, 0x65, 0x8b, 0x1b, 0x53, 0x2e, 0xa3,
                0x71, 0x64, 0x48, 0x97, 0xa2, 0x0d, 0xf9, 0x4e, 0x38, 0x19, 0xef, 0x46, 0xa9, 0xde, 0xac, 0xd8,
                0xa8, 0xfa, 0x76, 0x3f, 0xe3, 0x9c, 0x34, 0x3f, 0xf9, 0xdc, 0xbb, 0xc7, 0xc7, 0x0b, 0x4f, 0x1d,
                0x8a, 0x51, 0xe0, 0x4b, 0xcd, 0xb4, 0x59, 0x31, 0xc8, 0x9f, 0x7e, 0xc9, 0xd9, 0x78, 0x73, 0x64,
                0xea, 0xc5, 0xac, 0x83, 0x34, 0xd3, 0xeb, 0xc3, 0xc5, 0x81, 0xa0, 0xff, 0xfa, 0x13, 0x63, 0xeb,
                0x17, 0x0d, 0xdd, 0x51, 0xb7, 0xf0, 0xda, 0x49, 0xd3, 0x16, 0x55, 0x26, 0x29, 0xd4, 0x68, 0x9e,
                0x2b, 0x16, 0xbe, 0x58, 0x7d, 0x47, 0xa1, 0xfc, 0x8f, 0xf8, 0xb8, 0xd1, 0x7a, 0xd0, 0x31, 0xce,
                0x45, 0xcb, 0x3a, 0x8f, 0x95, 0x16, 0x04, 0x28, 0xaf, 0xd7, 0xfb, 0xca, 0xbb, 0x4b, 0x40, 0x7e };

            static readonly ulong[] InitAcc = new ulong[] { P32_3, P64_1, P64_2, P64_3, P64_4, P32_2, P64_5, P32_1 };

            public static ulong HashFile(string path)
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.SequentialScan))
                {
                    long len = fs.Length;
                    if (len <= 240)
                    {
                        byte[] small = new byte[(int)len];
                        ReadFull(fs, small, 0, small.Length);
                        return HashShort(small, small.Length);
                    }

                    ulong[] acc = (ulong[])InitAcc.Clone();
                    byte[] block = new byte[BlockLen];
                    long blocks = (len - 1) / BlockLen;
                    for (long b = 0; b < blocks; b++)
                    {
                        ReadFull(fs, block, 0, BlockLen);
                        Accumulate(acc, block, 0, BlockLen / StripeLen);
                        Scramble(acc, SecretSize - StripeLen);
                    }
                    int stripes = (int)(((len - 1) - blocks * BlockLen) / StripeLen);
                    if (stripes > 0)
                    {
                        ReadFull(fs, block, 0, stripes * StripeLen);
                        Accumulate(acc, block, 0, stripes);
                    }
                    byte[] last = new byte[StripeLen];
                    fs.Seek(len - StripeLen, SeekOrigin.Begin);
                    ReadFull(fs, last, 0, StripeLen);
                    Accumulate512(acc, last, 0, SecretSize - StripeLen - 7);
                    return Merge(acc, 11, (ulong)len * P64_1);
                }
            }

            public static ulong HashShort(byte[] d, int len)
            {
                if (len <= 16) return Len0to16(d, len);
                if (len <= 128) return Len17to128(d, len);
                return Len129to240(d, len);
            }

            static ulong Len0to16(byte[] d, int len)
            {
                if (len > 8)
                {
                    ulong lo = Read64(d, 0) ^ (Read64(S, 24) ^ Read64(S, 32));
                    ulong hi = Read64(d, len - 8) ^ (Read64(S, 40) ^ Read64(S, 48));
                    return Avalanche((ulong)len + Swap64(lo) + hi + Mul128Fold64(lo, hi));
                }
                if (len >= 4)
                {
                    ulong input = Read32(d, len - 4) + ((ulong)Read32(d, 0) << 32);
                    ulong keyed = input ^ (Read64(S, 8) ^ Read64(S, 16));
                    return Rrmxmx(keyed, (ulong)len);
                }
                if (len > 0)
                {
                    uint combined = ((uint)d[0] << 16) | ((uint)d[len >> 1] << 24) | d[len - 1] | ((uint)len << 8);
                    return XXH64Avalanche((ulong)combined ^ ((ulong)Read32(S, 0) ^ Read32(S, 4)));
                }
                return XXH64Avalanche(Read64(S, 56) ^ Read64(S, 64));
            }

            static ulong Mix16(byte[] d, int i, int s)
            {
                return Mul128Fold64(Read64(d, i) ^ Read64(S, s), Read64(d, i + 8) ^ Read64(S, s + 8));
            }

            static ulong Len17to128(byte[] d, int len)
            {
                ulong acc = (ulong)len * P64_1;
                if (len > 32)
                {
                    if (len > 64)
                    {
                        if (len > 96)
                        {
                            acc += Mix16(d, 48, 96);
                            acc += Mix16(d, len - 64, 112);
                        }
                        acc += Mix16(d, 32, 64);
                        acc += Mix16(d, len - 48, 80);
                    }
                    acc += Mix16(d, 16, 32);
                    acc += Mix16(d, len - 32, 48);
                }
                acc += Mix16(d, 0, 0);
                acc += Mix16(d, len - 16, 16);
                return Avalanche(acc);
            }

            static ulong Len129to240(byte[] d, int len)
            {
                ulong acc = (ulong)len * P64_1;
                int rounds = len / 16;
                for (int i = 0; i < 8; i++) acc += Mix16(d, 16 * i, 16 * i);
                ulong accEnd = Mix16(d, len - 16, 136 - 17);
                acc = Avalanche(acc);
                for (int i = 8; i < rounds; i++) accEnd += Mix16(d, 16 * i, 16 * (i - 8) + 3);
                return Avalanche(acc + accEnd);
            }

            static void Accumulate(ulong[] acc, byte[] input, int inputOff, int stripes)
            {
                for (int n = 0; n < stripes; n++)
                    Accumulate512(acc, input, inputOff + n * StripeLen, n * ConsumeRate);
            }

            static void Accumulate512(ulong[] acc, byte[] input, int inputOff, int secretOff)
            {
                for (int i = 0; i < 8; i++)
                {
                    ulong val = Read64(input, inputOff + i * 8);
                    ulong key = val ^ Read64(S, secretOff + i * 8);
                    acc[i ^ 1] += val;
                    acc[i] += (ulong)(uint)key * (ulong)(uint)(key >> 32);
                }
            }

            static void Scramble(ulong[] acc, int secretOff)
            {
                for (int i = 0; i < 8; i++)
                {
                    ulong a = XorShift(acc[i], 47) ^ Read64(S, secretOff + i * 8);
                    acc[i] = a * P32_1;
                }
            }

            static ulong Merge(ulong[] acc, int secretOff, ulong start)
            {
                ulong r = start;
                for (int i = 0; i < 4; i++)
                    r += Mul128Fold64(acc[2 * i] ^ Read64(S, secretOff + 16 * i),
                                      acc[2 * i + 1] ^ Read64(S, secretOff + 16 * i + 8));
                return Avalanche(r);
            }

            static ulong Mul128Fold64(ulong a, ulong b)
            {
                ulong aLo = a & 0xFFFFFFFFUL, aHi = a >> 32, bLo = b & 0xFFFFFFFFUL, bHi = b >> 32;
                ulong lo = aLo * bLo;
                ulong mid = (lo >> 32) + (aLo * bHi & 0xFFFFFFFFUL) + (aHi * bLo & 0xFFFFFFFFUL);
                ulong low = (lo & 0xFFFFFFFFUL) | (mid << 32);
                ulong high = aHi * bHi + (aLo * bHi >> 32) + (aHi * bLo >> 32) + (mid >> 32);
                return low ^ high;
            }

            static ulong Avalanche(ulong h) { h = XorShift(h, 37); h *= MX1; return XorShift(h, 32); }
            static ulong Rrmxmx(ulong h, ulong len)
            {
                h ^= Rotl64(h, 49) ^ Rotl64(h, 24);
                h *= MX2;
                h ^= (h >> 35) + len;
                h *= MX2;
                return XorShift(h, 28);
            }
            static ulong XXH64Avalanche(ulong h)
            {
                h ^= h >> 33; h *= P64_2; h ^= h >> 29; h *= P64_3; h ^= h >> 32;
                return h;
            }
            static ulong XorShift(ulong v, int s) { return v ^ (v >> s); }
            static ulong Rotl64(ulong v, int r) { return (v << r) | (v >> (64 - r)); }
            static ulong Swap64(ulong x)
            {
                return ((x & 0x00000000000000FFUL) << 56) | ((x & 0x000000000000FF00UL) << 40) |
                       ((x & 0x0000000000FF0000UL) << 24) | ((x & 0x00000000FF000000UL) << 8) |
                       ((x & 0x000000FF00000000UL) >> 8) | ((x & 0x0000FF0000000000UL) >> 24) |
                       ((x & 0x00FF000000000000UL) >> 40) | ((x & 0xFF00000000000000UL) >> 56);
            }
            static ulong Read64(byte[] b, int i)
            {
                return (ulong)b[i] | ((ulong)b[i + 1] << 8) | ((ulong)b[i + 2] << 16) | ((ulong)b[i + 3] << 24) |
                       ((ulong)b[i + 4] << 32) | ((ulong)b[i + 5] << 40) | ((ulong)b[i + 6] << 48) | ((ulong)b[i + 7] << 56);
            }
            static uint Read32(byte[] b, int i)
            {
                return (uint)b[i] | ((uint)b[i + 1] << 8) | ((uint)b[i + 2] << 16) | ((uint)b[i + 3] << 24);
            }
            static void ReadFull(Stream s, byte[] b, int off, int n)
            {
                while (n > 0)
                {
                    int r = s.Read(b, off, n);
                    if (r <= 0) throw new EndOfStreamException();
                    off += r; n -= r;
                }
            }
        }

        // false on any read error: the safe direction (never delete the source on doubt)
        static bool SameHash(string a, string b)
        {
            try { return XXH3.HashFile(a) == XXH3.HashFile(b); }
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

        // access denied gets a one-time hint to restart the app elevated
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

            // XXH3-64 known-answer vectors (reference xxhash.h scalar build)
            byte[] abc = new byte[] { (byte)'a', (byte)'b', (byte)'c' };
            if (XXH3.HashShort(new byte[0], 0) != 0x2D06800538D394C2UL) return 22;
            if (XXH3.HashShort(abc, 3) != 0x78AF5F94892F3950UL) return 24;
            byte[] v240 = new byte[240];
            for (int i = 0; i < v240.Length; i++) v240[i] = (byte)(i * 7 + 3);
            if (XXH3.HashShort(v240, 16) != 0xB8C859B0F030B585UL) return 26;
            if (XXH3.HashShort(v240, 17) != 0x714A04408E79B80FUL) return 27;
            if (XXH3.HashShort(v240, 128) != 0x67425A03650261BFUL) return 28;
            if (XXH3.HashShort(v240, 240) != 0x64556DC6B462A6CFUL) return 25;

            // long path (>240 bytes) and its 1024-byte block boundary, through HashFile
            string hf = Path.Combine(Path.GetTempPath(), "sc_selftest_xxh3.bin");
            byte[] v = new byte[3000];
            for (int i = 0; i < v.Length; i++) v[i] = (byte)(i * 7 + 3);
            int[] lens = new int[] { 241, 1024, 1025, 3000 };
            ulong[] want = new ulong[] { 0x8BEADD3A8874FE17UL, 0x9B81661C641C72B1UL,
                                         0x806C2072ED713576UL, 0xC89178BB873C6B3DUL };
            try
            {
                for (int i = 0; i < lens.Length; i++)
                {
                    byte[] part = new byte[lens[i]];
                    Array.Copy(v, part, part.Length);
                    File.WriteAllBytes(hf, part);
                    if (XXH3.HashFile(hf) != want[i]) return 23;
                }
            }
            finally { try { File.Delete(hf); } catch { } }

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
                    Settings.AskSession = 0;   // the dialog keeps the answer for the session
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

            // safety net 1: a source that vanished after the scan is an error, not a silent skip
            string goneDir = Path.Combine(Path.GetTempPath(), "sc_selftest_gone");
            try
            {
                Directory.CreateDirectory(goneDir);
                string src = Path.Combine(goneDir, "gone.txt"), dst = Path.Combine(goneDir, "dst.txt");
                File.WriteAllText(src, "data");
                CopyJob gj = new CopyJob(new List<string>(new string[] { src }), goneDir, false);
                CopyItem gi = new CopyItem(); gi.Src = src; gi.Dst = dst; gi.Name = "gone.txt"; gi.Size = 4;
                File.Delete(src);                 // the source disappears before the copy starts
                gj.DoCopy(gi, new byte[4096]);
                if (gi.State != 3) return 24;     // must be an error
                if (File.Exists(dst)) return 24;  // and nothing may be written
            }
            finally { try { Directory.Delete(goneDir, true); } catch { } }

            // safety net 2: a copy that fails must put the old destination back
            string bakDir = Path.Combine(Path.GetTempPath(), "sc_selftest_bak");
            try
            {
                Directory.CreateDirectory(bakDir);
                string src = Path.Combine(bakDir, "s.txt"), dst = Path.Combine(bakDir, "d.txt");
                File.WriteAllText(src, "new");
                File.WriteAllText(dst, "old");
                CopyJob bj = new CopyJob(new List<string>(new string[] { src }), bakDir, false);
                CopyItem bi = new CopyItem(); bi.Src = src; bi.Dst = dst; bi.Name = "d.txt"; bi.Size = 3;
                bool threw = false;
                using (FileStream lk = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    try { bj.DoCopy(bi, new byte[4096]); }   // the locked source makes the copy fail
                    catch (Exception) { threw = true; }
                }
                if (!threw) return 25;
                if (!File.Exists(dst) || File.ReadAllText(dst) != "old") return 25;   // the old file must be back
                string[] left = Directory.GetFiles(bakDir, "*.sctmp*");
                if (left.Length != 0) return 25;                // no leftover backup
            }
            finally { try { Directory.Delete(bakDir, true); } catch { } }

            // local version compare (offline update check: running exe vs Program Files copy)            if (Updates.FileVer(Application.ExecutablePath) < 1) return 21;
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

            // default-copy helper: on by default, knows the Explorer windows and
            // turns a shell URL into a folder path
            if (!Settings.DefaultCopy) return 29;
            if (HookAgent.UrlToPath("file:///C:/Temp/") != "C:\\Temp") return 29;
            if (HookAgent.UrlToPath("http://example.com/x") != null) return 29;
            if (!HookAgent.IsExplorerClass("CabinetWClass")) return 29;
            if (HookAgent.IsExplorerClass("Notepad")) return 29;

            // the installed copy must go away even while it runs: a file that is in
            // use cannot be deleted, so it is renamed away instead - and it is never lost
            string rf = Path.Combine(Path.GetTempPath(), "sc_selftest_rm.txt");
            try
            {
                File.WriteAllText(rf, "x");
                if (!Installer.RemoveFile(rf, 2) || File.Exists(rf)) return 30;
                File.WriteAllText(rf, "x");
                using (FileStream lk = new FileStream(rf, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    Installer.RemoveFile(rf, 2);        // in use: it cannot be deleted
                    if (!File.Exists(rf)) return 30;    // and it must not be lost
                }
                if (!Installer.RemoveFile(rf, 2) || File.Exists(rf)) return 30;   // free again: it goes
            }
            finally
            {
                try { File.Delete(rf); } catch { }
                try { File.Delete(rf + ".old"); } catch { }
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
            try { spaceLimit = FreeSpace(Dest); }
            catch { spaceLimit = -1; }
            try
            {
                foreach (string src in Sources)
                {
                    if (Cancelled || noSpace) break;
                    if (src == null) continue;
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
            catch (Exception ex)
            {
                // a crash here would kill the whole app: report it and let the workers continue
                try { AddFinished("scan", 3, ex.Message); } catch { }
            }
            finally
            {
                // largest files first: display order == copy order. Gate is ScanDone (set last) so
                // workers never consume the list mid-sort.
                try
                {
                    lock (sync)
                    {
                        items.Sort(delegate(CopyItem a, CopyItem b) { return b.Size.CompareTo(a.Size); });
                        for (int i = 0; i < items.Count; i++) items[i].Index = i;
                    }
                }
                catch { }
                ScanDone = true;
            }
        }

        // ---------- worker threads ----------
        void WorkerMain()
        {
            byte[] buf = null;
            bool last = false;
            try
            {
                while (true)
                {
                    CopyItem it = null;
                    bool wait = false;
                    lock (sync)
                    {
                        if (Cancelled) { workers--; last = (workers == 0); break; }
                        int target = ComputeTarget(null);
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
                    if (want < 4096) want = 4096;
                    if (buf == null || buf.Length != want)
                    {
                        // a huge buffer can fail with OutOfMemory: the copy continues with a small one
                        try { buf = new byte[want]; }
                        catch (OutOfMemoryException) { buf = new byte[65536]; }
                    }
                    ProcessItem(it, buf);
                }
            }
            catch (Exception ex)
            {
                // an unhandled exception here would end the whole app: keep the job alive
                try
                {
                    LastError = ex.Message;
                    lock (sync) { workers--; last = (workers == 0); }
                }
                catch { }
            }
            if (last) OnFinished();
        }

        // how many workers may run now: the same rule for the spawner and for the workers, so no
        // worker is created and dropped again right away
        int ComputeTarget(CopyItem pending)
        {
            if (!Settings.SmartConcurrency) return Math.Max(1, Math.Min(32, Settings.Concurrency));
            CopyItem cand = pending;
            lock (activeLock)
            {
                for (int i = 0; i < active.Count; i++)
                    if (active[i] != null && (cand == null || active[i].Size > cand.Size)) cand = active[i];
            }
            if (cand == null) lock (sync) { if (next < items.Count) cand = items[next]; }
            return GetTargetConcurrency(cand);
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

        // a failed item counts as handled: the progress must not stay below 100% for ever
        void Fail(CopyItem it, string msg)
        {
            it.Error = msg;
            LastError = it.Name + ": " + msg;
            it.State = 3;
            long rem = it.Size - Interlocked.Read(ref it.Done);
            if (rem > 0) Interlocked.Add(ref DoneBytes, rem);
            Interlocked.Increment(ref Errors);
        }

        void DoCopy(CopyItem it, byte[] buf)
        {
            // two items can end up at the same destination (same file name from two folders):
            // they must never write it at the same time
            lock (LockFor(it.Dst)) { DoCopyInner(it, buf); }
        }

        object LockFor(string dst)
        {
            string key = dst == null ? "" : dst;
            lock (dstLockMap)
            {
                object o;
                if (!dstLocks.TryGetValue(key, out o)) { o = new object(); dstLocks[key] = o; }
                return o;
            }
        }

        void DoCopyInner(CopyItem it, byte[] buf)
        {
            string src = it.Src, dst = it.Dst;

            if (!File.Exists(src))
            {
                // the source vanished between the scan and the copy: that is an error, not a skip
                Fail(it, "Source no longer exists");
                return;
            }

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
                        int gen = Settings.AskGen;
                        while (askChoice < 0)
                        {
                            if (Settings.AskGen != gen) { Skip(it); return; }   // the user cancelled: skip this file too
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
                                        // the answer is kept for the whole session only when the user
                                        // asked for it (the dialog sets AskSession itself)
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
                    string mb = BakPath(dst);
                    try { File.Replace(src, dst, mb); }          // atomic; both files stay intact if this throws
                    catch
                    {
                        // File.Replace needs NTFS: fall back to a rename that keeps the original safe
                        try { File.Move(dst, mb); File.Move(src, dst); }
                        catch { try { File.Move(mb, dst); } catch { } throw; }
                    }
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
                bak = BakPath(dst);
                try { File.Move(dst, bak); }
                catch (Exception ex)
                {
                    // the original cannot be set aside: never overwrite it in place
                    Fail(it, "Cannot set the existing file aside: " + ex.Message);
                    return;
                }
            }
            bool ok;
            try { ok = Settings.Engine == 1 ? RoboCopy(src, dst, it, buf) : CopyData(src, dst, it, buf); }
            catch
            {
                RemovePartial(dst);
                if (bak != null) { try { File.Move(bak, dst); } catch { } }
                throw;
            }
            if (!ok)
            {
                RemovePartial(dst);
                if (bak != null) { try { File.Move(bak, dst); } catch { } }
                if (Cancelled) { it.State = 5; return; }
                Fail(it, "The copy failed");   // robocopy error: the old file is back in place
                return;
            }
            // the copy must be complete before the backup or the source is dropped - the length
            // is checked even when the hash verification is off
            long dlen = -1, slen = -1;
            try { dlen = new FileInfo(dst).Length; slen = new FileInfo(src).Length; } catch { }
            if (slen >= 0 && dlen != slen)
            {
                RemovePartial(dst);
                if (bak != null) { try { File.Move(bak, dst); } catch { } }
                Fail(it, "Incomplete copy - source kept");
                return;
            }
            // hash both files before deleting the backup or the source: a bad transfer can
            // never destroy the only good copy, and copies are verified too, not just moves
            if (Settings.Verify && !SameHash(dst, src))
            {
                RemovePartial(dst);
                if (bak != null) { try { File.Move(bak, dst); } catch { } }
                Fail(it, "XXH3 checksum mismatch - source kept");
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
                try { File.SetAttributes(src, FileAttributes.Normal); File.Delete(src); }
                catch (Exception ex)
                {
                    Fail(it, "Copied, source not deleted: " + ex.Message);
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
                    RemovePartial(dst);   // remove the partial file we created
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
                if (!fi.Exists) return false;
                long cur = -1;
                try { cur = new FileInfo(src).Length; } catch { }   // the file may have changed since the scan
                if (cur >= 0 && fi.Length != cur) return false;
                if (cur < 0 && fi.Length != it.Size) return false;
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
        public double Frac;                 // all files of the job
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
        static Bitmap iconBmp;          // built once: a new bitmap on every paint leaks GDI handles

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
            if (iconBmp == null) iconBmp = IconMaker.MakeBitmap(32);
            g.DrawImage(iconBmp, L.Rtl ? Width - 46 : 14, (Height - 32) / 2);
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
        int paintedCount = -1;                // list state at the last repaint (repaint only on change)
        bool paintedBusy, headerDirty = true;
        int paintedJobs = -1;                 // job count at the last job-box repaint
        int paintedErrs = -1;                 // error count at the last list repaint
        int lastActiveRow = -1;               // row of the active file (scroll only when it moves)
        bool closingAfter;                    // the window closes itself when the workers are done
        int closingAt;
        volatile string promptError;          // set when the conflict prompt could not be shown
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
            catch (Exception ex)
            {
                // the prompt could not be shown: report it, do not silently drop the file
                promptError = ex.Message;
                return 1;      // skip: never overwrite when the question could not be asked
            }
        }

        int AskConflictNow(string src, string dst)
        {
            if (Settings.AskSession >= 0) return Settings.AskSession;   // another worker answered already
            using (ConflictDialog d = new ConflictDialog(dst))
            {
                d.TopMost = true;      // the prompt must not hide behind the Explorer window
                timer.Stop();          // no repaint behind the modal prompt
                DialogResult res = d.ShowDialog(this);
                timer.Start();
                if (res != DialogResult.OK)
                {
                    Settings.AskSession = -1;
                    Interlocked.Increment(ref Settings.AskGen);   // the waiting workers skip their file too
                    return -1;   // cancelled -> skip this file
                }
                if (d.SaveForSession) Settings.AskSession = d.Choice;
                else Settings.AskSession = -1;
                return d.Choice;
            }
        }

        // ------------------------------------------------------------
        public void AddJob(CopyJob j)
        {
            jobs.Add(j);
            j.Prepare();      // scan now: the files and the total size show up in the list at once
            lastDest = j.Dest;
            jobBox.Items.Add(j);
            doneHandled = false;
            closeAt = -1;
            bool multi = jobs.Count >= 2;
            jobBox.Visible = multi && !compact;
            jobBox.Height = Math.Min(jobs.Count, 4) * 26 + 2;
            if (cur == null) SetCur(j);
            lv.VirtualListSize = ListCount();
            ShowOnTop();      // the user has to see the copy that just started
        }

        // the main list holds the files of every job in the queue, one after the other
        int ListCount()
        {
            int n = 0;
            for (int i = 0; i < jobs.Count; i++) n += jobs[i].ItemCount;
            return n;
        }

        // row -> (job, item); rows are the items of all jobs, in queue order
        CopyItem ListItem(int row, out CopyJob job)
        {
            for (int i = 0; i < jobs.Count; i++)
            {
                int c = jobs[i].ItemCount;
                if (row < c) { job = jobs[i]; return job.GetItem(row); }
                row -= c;
            }
            job = null;
            return null;
        }

        // row of the first item of a job (for the "keep the active file visible" logic)
        int ListRowOf(CopyJob j)
        {
            int n = 0;
            for (int i = 0; i < jobs.Count; i++)
            {
                if (jobs[i] == j) return n;
                n += jobs[i].ItemCount;
            }
            return -1;
        }

        // progress over the whole queue (all jobs, running and waiting)
        double QueueFraction()
        {
            long total = 0, done = 0;
            for (int i = 0; i < jobs.Count; i++) { total += jobs[i].Total; done += jobs[i].DoneNow; }
            if (total <= 0) return 0;
            double f = (double)done / total;
            return f > 1 ? 1 : (f < 0 ? 0 : f);
        }

        void SetCur(CopyJob j)
        {
            cur = j;
            lv.VirtualListSize = ListCount();
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
                // claim the file first: two windows must never run the same job
                string mine = fs[k] + ".claimed";
                try { File.Move(fs[k], mine); }
                catch { continue; }
                try
                {
                    string[] ln = File.ReadAllLines(mine, Encoding.UTF8);
                    if (ln.Length >= 3)
                    {
                        List<string> src = new List<string>();
                        for (int i = 2; i < ln.Length; i++) if (ln[i].Length > 0) src.Add(ln[i]);
                        if (src.Count > 0) AddJob(new CopyJob(src, ln[1], ln[0] == "cut"));
                    }
                    File.Delete(mine);   // only after the job is enqueued; a throw keeps the file for retry
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

            if (tickN % 4 == 0) PollQueue();      // every second: hand-over jobs from other instances
            if (promptError != null)
            {
                string e2 = promptError;
                promptError = null;
                Msg.Show(this, L.T("The conflict prompt could not be shown, the file was skipped:\n", "تعذّر عرض نافذة التعارض، وتم تخطي الملف:\n", "La demande de conflit n'a pas pu s'afficher, le fichier a été ignoré :\n") + e2,
                    "Super Copy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            if (running != null && !running.Finished)
            {
                running.EnsureWorkers();
                running.Sample(dt);
            }
            Advance();

            // caption / tray tooltip stay current even when hidden
            string cap = "Super Copy";
            if (cur != null && cur.Started && !cur.Finished) cap = ((int)(QueueFraction() * 100)) + "% - Super Copy";
            if (Text != cap) Text = cap;
            if (tray.Visible && tray.Text != cap) tray.Text = cap;

            if (Visible && WindowState != FormWindowState.Minimized && cur != null)
            {
                int cnt = ListCount();
                if (lv.VirtualListSize != cnt) lv.VirtualListSize = cnt;
                if (cur == running && !cur.Finished && cnt > 0 && lv.Visible)
                {
                    int idx = cur.FirstActiveIndex();
                    if (idx >= 0)
                    {
                        int row = ListRowOf(cur);
                        if (row >= 0) idx += row;
                        if (idx != lastActiveRow && !MouseInList())   // scroll only when the active file changes
                        {
                            lastActiveRow = idx;
                            try
                            {
                                lv.EnsureVisible(Math.Min(cnt - 1, idx + 8));
                                lv.EnsureVisible(idx);
                            }
                            catch { }
                        }
                    }
                }
                UpdateHeader();
                UpdateButtons();
                header.Tick = tickN;
                if (headerDirty || header.Marquee) header.Invalidate();
                // the rows only change while a job runs, when the count changes or when a job ends
                bool busy = running != null && !running.Finished && !running.Paused;
                int errs = 0;
                for (int i = 0; i < jobs.Count; i++) errs += jobs[i].Errors;
                if (busy || cnt != paintedCount || busy != paintedBusy || errs != paintedErrs)
                {
                    lv.Invalidate();
                    paintedCount = cnt;
                    paintedBusy = busy;
                    paintedErrs = errs;
                }
                if (jobBox.Visible && (busy || jobBox.Items.Count != paintedJobs))
                {
                    jobBox.Invalidate();
                    paintedJobs = jobBox.Items.Count;
                }
            }

            if (closeAt >= 0 && tickN >= closeAt) { exiting = true; Close(); }
            if (closingAfter && (!AnyRunning() || Environment.TickCount - closingAt > 10000))
            {
                closingAfter = false;      // the workers are done (or too slow): close now
                exiting = true;
                Close();
            }
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
                    CopyJob failed = running;      // the modal dialog re-enters this method: keep the job
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
                        if (QueueFailed(failed) && Admin.Relaunch())
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
            try { TopMost = false; } catch { }      // back to the normal z-order
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
            HeaderPanel h = header;
            // remember what is on screen: the header is repainted only when it really changes
            string oTitle = h.Title, oElapsed = h.Elapsed, oBar = h.BarText;
            string oL = h.TxtL, oM = h.TxtM, oR = h.TxtR;
            double oFrac = h.Frac; Color oA = h.ColA, oB = h.ColB; bool oMar = h.Marquee;
            CopyJob run = null, queued = null, last = null;
            long total = 0, done = 0, active = 0;
            int files = 0, filesDone = 0, errors = 0;
            bool scanning = false, allFinished = true;

            // the numbers cover the whole queue, the title describes the job that matters now
            for (int i = 0; i < jobs.Count; i++)
            {
                CopyJob j = jobs[i];
                total += j.Total; done += j.DoneNow; active += j.ActiveMs;
                files += j.ItemCount; filesDone += j.FilesDone; errors += j.Errors;
                if (!j.ScanDone) scanning = true;
                if (j.Started && !j.Finished) { run = j; allFinished = false; }
                else if (!j.Started) { allFinished = false; if (queued == null) queued = j; }
                last = j;
            }
            CopyJob j2 = run != null ? run : (queued != null ? queued : last);
            if (j2 == null) return;

            long speed = run != null ? (long)run.Speed : 0;
            double eta = speed > 0 ? (double)(total - done) / speed : -1;
            double frac = total > 0 ? QueueFraction() : (allFinished ? 1.0 : 0.0);
            if (frac > 1) frac = 1; if (frac < 0) frac = 0;

            string errs = errors > 0 ? "    " + L.T("Errors: ", "الأخطاء: ", "Erreurs : ") + errors : "";
            string filesTxt = filesDone + " / " + files + (scanning ? "+" : "") + " " +
                              L.T("files", "ملف", "fichiers") + "    " +
                              Fmt.Size(done) + " / " + Fmt.Size(total) + (scanning ? "+" : "") + errs;
            string verb = j2.Move ? L.T("Moving ", "نقل ", "Déplacement de ") : L.T("Copying ", "نسخ ", "Copie de ");
            h.Elapsed = Fmt.Time(active / 1000.0);
            h.Marquee = false;
            h.Frac = frac;
            h.TxtR = filesTxt;

            if (!j2.Started)
            {
                h.Title = L.T("Queued: ", "في الانتظار: ", "En attente : ") + j2.Summary;
                h.BarText = L.T("Waiting...", "بالانتظار...", "En attente...");
                h.Frac = 0;
                h.ColA = Color.FromArgb(70, 80, 120); h.ColB = Color.FromArgb(100, 110, 150);
                h.TxtL = L.T("Speed: -", "السرعة: -", "Vitesse : -");
                h.TxtM = L.T("Total ETA: -", "الوقت المتبقي: -", "Temps restant : -");
            }
            else if (j2.Finished)
            {
                if (j2.Cancelled)
                {
                    h.Title = L.T("Cancelled: ", "ملغى: ", "Annulé : ") + j2.Summary;
                    h.ColA = Color.FromArgb(210, 120, 30); h.ColB = Color.FromArgb(255, 190, 90);
                }
                else if (errors > 0)
                {
                    h.Title = L.T("Finished with errors: ", "انتهى مع أخطاء: ", "Terminé avec des erreurs : ") + j2.Summary;
                    h.Frac = 1.0;
                    h.ColA = Color.FromArgb(200, 40, 60); h.ColB = Color.FromArgb(255, 120, 100);
                }
                else
                {
                    h.Title = L.T("Completed: ", "مكتمل: ", "Terminé : ") + j2.Summary;
                    h.Frac = 1.0;
                    h.ColA = Color.FromArgb(30, 170, 100); h.ColB = Color.FromArgb(120, 240, 160);
                }
                h.BarText = ((int)Math.Round(h.Frac * 100)) + "%";
                double avg = active > 0 ? done * 1000.0 / active : 0;
                h.TxtL = L.T("Average speed: ", "متوسط السرعة: ", "Vitesse moyenne : ") + Fmt.Size((long)avg) + "/s";
                h.TxtM = L.T("Total time: ", "الوقت الإجمالي: ", "Durée totale : ") + Fmt.Time(active / 1000.0);
            }
            else
            {
                bool paused = j2.Paused;
                h.Title = (paused ? L.T("Paused - ", "متوقف - ", "En pause - ") : verb) + j2.Summary;
                if (scanning)
                {
                    h.Marquee = true;
                    h.BarText = L.T("Scanning folders...  ", "جارٍ فحص المجلدات...  ", "Analyse des dossiers...  ") +
                                files + " " + L.T("files found", "ملف", "fichiers");
                }
                else h.BarText = (h.Frac * 100).ToString("0.0") + "%";
                if (paused) { h.ColA = Theme.Orange; h.ColB = Color.FromArgb(255, 210, 110); }
                else { h.ColA = Theme.Cyan; h.ColB = Theme.Violet; }
                h.TxtL = paused ? L.T("Speed: paused", "السرعة: متوقفة", "Vitesse : en pause")
                                : L.T("Speed: ", "السرعة: ", "Vitesse : ") + Fmt.Size(speed) + "/s";
                h.TxtM = paused ? L.T("Total ETA: -", "الوقت المتبقي: -", "Temps restant : -")
                                : (scanning ? L.T("Total ETA: calculating...", "الوقت المتبقي: قيد الحساب...", "Temps restant : calcul...")
                                            : L.T("Total ETA: ", "الوقت المتبقي: ", "Temps restant : ") + Fmt.Time(eta));
            }
            string lastErr = null;
            for (int i = 0; i < jobs.Count; i++) if (jobs[i].LastError != null) lastErr = jobs[i].LastError;
            lblErr.Text = lastErr == null ? "" : L.T("Last error: ", "آخر خطأ: ", "Dernière erreur : ") + lastErr;

            headerDirty = oTitle != h.Title || oElapsed != h.Elapsed || oBar != h.BarText ||
                          oL != h.TxtL || oM != h.TxtM || oR != h.TxtR ||
                          oFrac != h.Frac || oA != h.ColA || oB != h.ColB || oMar != h.Marquee;
        }

        void UpdateButtons()
        {
            CopyJob act = (running != null && !running.Finished) ? running : cur;
            bool active = act != null && !act.Finished;
            btnPause.Enabled = active;
            btnCancel.Enabled = active;
            string t = (act != null && act.Paused) ? L.T("Resume", "متابعة", "Reprendre") : L.T("Pause", "إيقاف", "Pause");
            if (btnPause.Text != t) btnPause.Text = t;
            bool showErr = false;
            for (int i = 0; i < jobs.Count; i++) if (jobs[i].Errors > 0) { showErr = true; break; }
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
            // the job that is really copying, not the one shown in the job strip
            CopyJob j = (running != null && !running.Finished) ? running : cur;
            if (j != null && !j.Finished) j.Paused = !j.Paused;
        }

        void btnCancel_Click(object s, EventArgs e)
        {
            CopyJob j = (running != null && !running.Finished) ? running : cur;
            if (j != null && !j.Finished) j.Cancel();
        }

        // Error report popup: full per-file error text with a copy-to-clipboard button.
        void btnErrReport_Click(object s, EventArgs e)
        {
            if (cur == null) return;
            string text = "";
            for (int i = 0; i < jobs.Count; i++)     // every job in the queue
            {
                string t = jobs[i].ErrorReport();
                if (t.Length > 0) text += (text.Length > 0 ? "\r\n" : "") + t;
            }
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
            timer.Stop();                 // no repaint behind the modal window
            using (SettingsForm f = new SettingsForm()) f.ShowDialog(this);
            timer.Start();
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

        // the copy window must be seen: it stays above the Explorer window
        // while the job runs and comes back to the normal z-order afterwards
        public void ShowOnTop()
        {
            try
            {
                if (!Visible) Show();
                if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
                TopMost = true;
                Activate();
                BringToFront();
            }
            catch { }
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
                DialogResult r = Msg.ShowSafe(this,
                    L.T("Copy operations are still running.\n\nYes = cancel and exit\nNo = hide to the tray and keep copying",
                        "عمليات النسخ ما زالت قيد التشغيل.\n\nنعم = إلغاء والخروج\nلا = الإخفاء إلى شريط المهام ومتابعة النسخ",
                        "Des copies sont encore en cours.\n\nOui = annuler et quitter\nNon = masquer dans la zone de notification et continuer"),
                    "Super Copy", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel) { e.Cancel = true; return; }
                if (r == DialogResult.No) { e.Cancel = true; HideToTray(); return; }
            }
            if (AnyRunning())
            {
                // cancel and wait for the workers - but never block the UI thread here: a worker
                // may be waiting for this thread (the conflict prompt) and would deadlock
                for (int i = 0; i < jobs.Count; i++) jobs[i].Cancel();
                closingAfter = true;
                closingAt = Environment.TickCount;
                e.Cancel = true;
                return;
            }
            Cleanup();
            base.OnFormClosing(e);
        }

        void Cleanup()
        {
            try { timer.Stop(); timer.Dispose(); } catch { }
            try { tray.Visible = false; tray.Dispose(); } catch { }
            try { trayMenu.Dispose(); } catch { }
            try { ConflictPrompt.Ask = null; } catch { }
        }

        // ------------------------------------------------------------
        //  owner-drawn file list
        // ------------------------------------------------------------
        void lv_Retrieve(object s, RetrieveVirtualItemEventArgs e)
        {
            CopyJob oj;
            CopyItem it = ListItem(e.ItemIndex, out oj);
            // the text is painted by lv_DrawSub, but the real values make the list
            // readable for screen readers and for copying a row
            e.Item = new ListViewItem(new string[]
            {
                it == null || it.Name == null ? "" : it.Name,
                it == null || it.Src == null ? "" : Fmt.Size(it.Size),
                "", "", "", ""
            });
        }

        void lv_DrawHeader(object s, DrawListViewColumnHeaderEventArgs e)
        {
            Rectangle r = e.Bounds;
            if (r.Width <= 0 || r.Height <= 0) return;
            using (LinearGradientBrush lb = new LinearGradientBrush(r, Theme.Panel2, Theme.Panel, 90f))
                e.Graphics.FillRectangle(lb, r);
            {
                Pen p = Theme.PenBorder;
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
            CopyJob oj;
            CopyItem it = ListItem(e.ItemIndex, out oj);
            Graphics g = e.Graphics;
            Rectangle r = e.Bounds;
            if (e.ColumnIndex == 0) r.Width = lv.Columns[0].Width;   // first-column bounds quirk
            bool sel = (e.ItemState & ListViewItemStates.Selected) != 0;
            g.FillRectangle(sel ? Theme.BrSel : ((e.ItemIndex & 1) == 0 ? Theme.BrRow1 : Theme.BrRow2), r);
            if (it == null) return;

            Rectangle tr = new Rectangle(r.X + 6, r.Y, Math.Max(1, r.Width - 10), r.Height);
            bool paused = oj != null && oj.Paused;
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
        CheckBox chkClose, chkVerify, chkDefault;
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
            ClientSize = new Size(430, 578);
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

            chkVerify = new CheckBox();
            chkVerify.Text = L.T("Verify copies and moves with XXH3",
                                 "التحقق من النسخ والنقل باستخدام XXH3",
                                 "Vérifier les copies et déplacements avec XXH3");
            chkVerify.AutoSize = true;
            chkVerify.ForeColor = Theme.Text;
            chkVerify.BackColor = Theme.Bg;
            chkVerify.Checked = Settings.Verify;
            chkVerify.Location = new Point(20, 320);
            Controls.Add(chkVerify);

            chkDefault = new CheckBox();
            chkDefault.Text = L.T("Use Super Copy as the default copy (Ctrl+V in Explorer)",
                                  "استخدام Super Copy كنسخ افتراضي (Ctrl+V في المستكشف)",
                                  "Utiliser Super Copy comme copie par défaut (Ctrl+V dans l'Explorateur)");
            chkDefault.AutoSize = true;
            chkDefault.ForeColor = Theme.Text;
            chkDefault.BackColor = Theme.Bg;
            chkDefault.Checked = Settings.DefaultCopy;
            chkDefault.Location = new Point(20, 346);
            Controls.Add(chkDefault);

            Controls.Add(MakeLabel(L.T("Explorer right-click menu", "قائمة النقر بزر الماوس الأيمن في المستكشف", "Menu clic droit de l'Explorateur"), 20, 390, Theme.Cyan));
            Button bi2 = UI.MakeButton(L.T("Install menu", "تثبيت القائمة", "Installer le menu"), 180, Color.FromArgb(40, 150, 100));
            Button br = UI.MakeButton(L.T("Remove menu", "إزالة القائمة", "Supprimer le menu"), 180, Color.FromArgb(190, 60, 80));
            bi2.Location = new Point(20, 412);
            br.Location = new Point(220, 412);
            bi2.Click += new EventHandler(OnInstall);
            br.Click += new EventHandler(OnRemove);
            Controls.Add(bi2);
            Controls.Add(br);
            lblState = MakeLabel("", 20, 450, Theme.Dim);
            Controls.Add(lblState);
            Controls.Add(MakeLabel(L.T("Windows 11: entries are under \"Show more options\" (Shift+F10).",
                                       "ويندوز 11: تظهر الإدخالات تحت \"إظهار المزيد من الخيارات\" (Shift+F10).",
                                       "Windows 11 : les entrées sont sous « Afficher plus d'options » (Maj+F10)."), 20, 470, Theme.Dim));
            RefreshState();

            Controls.Add(MakeLabel(L.T("Version ", "الإصدار ", "Version ") + Updates.Current, 20, 508, Theme.Text));
            Button babt = UI.MakeButton(L.T("About", "حول", "À propos"), 84, Color.FromArgb(90, 96, 130));
            babt.Location = new Point(132, 502);
            babt.Click += new EventHandler(OnAbout);
            Controls.Add(babt);
            Button bupd = UI.MakeButton(L.T("Check for updates", "التحقق من التحديثات", "Rechercher des mises à jour"), 190, Color.FromArgb(60, 120, 140));
            bupd.Location = new Point(226, 502);
            bupd.Click += new EventHandler(OnCheckUpdate);
            Controls.Add(bupd);

            Button ok = UI.MakeButton(L.T("Save", "حفظ", "Enregistrer"), 90, Color.FromArgb(70, 90, 170));
            Button cancel = UI.MakeButton(L.T("Cancel", "إلغاء", "Annuler"), 90, Color.FromArgb(70, 76, 110));
            Button reset = UI.MakeButton(L.T("Reset to default", "استعادة الافتراضي", "Réinitialiser"), 165, Color.FromArgb(120, 96, 60));
            ok.Location = new Point(236, 542);
            cancel.Location = new Point(334, 542);
            reset.Location = new Point(20, 542);
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
                Installer.Install();
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
            try { Installer.Remove(); RefreshState(); }
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
            Settings.Verify = chkVerify.Checked;
            Settings.DefaultCopy = chkDefault.Checked;
            Settings.AutoClose = chkClose.Checked;
            Settings.Language = Math.Max(0, Math.Min(3, cmbLang.SelectedIndex));
            L.Apply(Settings.Language);
            Settings.Save();
            HookAgent.Apply();   // start/stop the default-copy helper
            // menu labels live in the registry: rewrite them for the current language
            if (ShellIntegration.IsInstalled())
            {
                try { ShellIntegration.Install(Paths.ExeInstalled); } catch { }
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        void OnCancel(object s, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        // Fills the controls with the built-in defaults. The live settings are not
        // touched, so Cancel still discards the reset.
        void OnReset(object s, EventArgs e)
        {
            numConc.Value = Settings.DefaultConcurrency;
            if (chkSmartConc != null)
            {
                chkSmartConc.Checked = true;
                numConc.Enabled = false;
            }
            int bi = 1;
            for (int i = 0; i < BufSizes.Length; i++) if (BufSizes[i] == Settings.DefaultBufferKB) bi = i;
            cmbBuf.SelectedIndex = bi;
            cmbEngine.SelectedIndex = Math.Max(0, Math.Min(cmbEngine.Items.Count - 1, Settings.DefaultEngine));
            cmbConf.SelectedIndex = Math.Max(0, Math.Min(4, Settings.DefaultConflict));
            chkVerify.Checked = Settings.DefaultVerify;
            chkDefault.Checked = Settings.DefaultDefaultCopy;
            chkClose.Checked = false;
            cmbLang.SelectedIndex = 0;
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
            opts[1].Checked = true;      // "skip" is the safe default: Enter never destroys a file

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
