using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Sandbox.Graphics.GUI;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Plugins;
using VRage.Utils;

namespace DaneHud
{
    /// <summary>
    /// DaneHud — okna z danymi grida, w którym siedzisz, rysowane na HUD (zamiast Automatic LCDs 2 + HudLcd):
    /// rudy, sztabki, ładownie, zbiorniki, moc, wodór, ciąg silników, rafinerie (jedna linia), autocrafting Isy.
    /// /dane = ustawienia (6 okien, każde z własną listą elementów, położeniem, czcionką i kolorami).
    /// </summary>
    internal class Snapshot
    {
        public List<WindowCfg> Windows;
        public List<Row>[] Rows;
    }

    public class Plugin : IPlugin
    {
        internal const string Version = "3";
        internal static Config Cfg;
        internal static volatile Snapshot Shown;      // okna i wiersze do narysowania (null = nic)
        internal static bool Seated;
        internal static long GridId;                  // grid, w którym ostatnio siedziałeś
        internal static string GridName = "";
        internal static GridProfile ActiveProfile;    // profil grida, w którym siedzisz (null = szablon)
        long magazynGrid;
        internal static volatile bool Dirty;

        static readonly ConcurrentQueue<Action> mainThread = new ConcurrentQueue<Action>();
        IMySession hooked;
        Overlay overlay;
        int tick;

        public void Init(object gameInstance)
        {
            Cfg = Config.Load();
            VirtualTerminal.Apply();
            MyLog.Default.WriteLine("DaneHud v" + Version + " loaded");
        }

        public void OpenConfigDialog()
        {
            MyGuiSandbox.AddScreen(new MainScreen(Cfg ?? (Cfg = Config.Load())));
        }

        public void Dispose() { Magazyn.I.Release(); Unhook(); }

        public void Update()
        {
            while (mainThread.TryDequeue(out var a))
                try { a(); } catch (Exception e) { Log("main: " + e); }

            var s = MyAPIGateway.Session;
            if (s == null || s.Player == null || MyAPIGateway.Utilities == null)
            {
                if (hooked != null) Unhook();
                return;
            }
            if (!ReferenceEquals(s, hooked))
            {
                Unhook();
                MyAPIGateway.Utilities.MessageEntered += OnMessage;
                hooked = s;
                try { overlay = new Overlay(); MyGuiSandbox.AddScreen(overlay); } catch (Exception e) { Log("overlay: " + e); overlay = null; }
            }

            if (++tick % Math.Max(10, Cfg.RefreshTicks) == 0 || Dirty) Refresh();
        }

        void Refresh()
        {
            Dirty = false;
            Ctx ctx = null;
            try { ctx = Ctx.Collect(); } catch (Exception e) { LogOnce("collect", e); }
            Seated = ctx != null;
            if (ctx == null)
            {
                Shown = null;
                ActiveProfile = null;
                VirtualTerminal.EntityId = 0;
                Magazyn.I.Release();
                return;
            }

            GridId = ctx.Grid.EntityId;
            GridName = ctx.Grid.CustomName ?? "";
            var profile = Cfg.PerGrid ? Cfg.FindProfile(GridId, GridName) : null;
            ActiveProfile = profile;

            // magazyn (części Isy) — działa także przy ukrytych oknach
            var mag = profile?.Magazyn;
            bool magOn = mag != null && mag.Enabled;
            if (magOn)
            {
                if (magazynGrid != GridId) { Magazyn.I.Release(); magazynGrid = GridId; }
                try { Magazyn.I.Run(ctx, mag); } catch (Exception e) { LogOnce("magazyn", e); }
            }
            else Magazyn.I.Release();

            if (!Cfg.Visible)
            {
                Shown = null;
                VirtualTerminal.EntityId = magOn && Cfg.LiveInventory && MyAPIGateway.Multiplayer != null && !MyAPIGateway.Multiplayer.IsServer ? ctx.Controller.EntityId : 0;
                return;
            }
            var windows = profile != null ? profile.Windows : Cfg.Windows;

            var rows = new List<Row>[windows.Count];
            bool needInv = magOn;
            for (int i = 0; i < windows.Count; i++)
            {
                var w = windows[i];
                if (!w.Enabled) continue;
                foreach (var e in w.Elements)
                    if (e.Kind == ElementKind.Items || e.Kind == ElementKind.CargoBar || e.Kind == ElementKind.Refineries ||
                        e.Kind == ElementKind.PowerTime || e.Kind == ElementKind.Amount || e.Kind == ElementKind.Autocrafting)
                        needInv = true;
                try { rows[i] = RowBuilder.Build(w, ctx); }
                catch (Exception e) { LogOnce("rows" + i, e); }
            }
            Shown = new Snapshot { Windows = windows, Rows = rows };

            // wirtualny terminal tylko, gdy siedzisz, okna są widoczne i któreś potrzebuje zawartości ładowni
            VirtualTerminal.EntityId = Cfg.LiveInventory && needInv && MyAPIGateway.Multiplayer != null && !MyAPIGateway.Multiplayer.IsServer
                ? ctx.Controller.EntityId : 0;
        }

        /// <summary>Wiersze okna do podglądu w menu (null = okno nie jest teraz pokazywane).</summary>
        internal static List<Row> RowsOf(WindowCfg w)
        {
            var s = Shown;
            if (s == null) return null;
            int i = s.Windows.IndexOf(w);
            return i >= 0 && i < s.Rows.Length ? s.Rows[i] : null;
        }

        void Unhook()
        {
            Magazyn.I.Release();
            VirtualTerminal.EntityId = 0;
            Seated = false;
            Shown = null;
            try { overlay?.CloseScreenNow(false); } catch { }
            overlay = null;
            if (hooked != null && MyAPIGateway.Utilities != null)
                MyAPIGateway.Utilities.MessageEntered -= OnMessage;
            hooked = null;
        }

        void OnMessage(string text, ref bool sendToOthers)
        {
            if (text == null) return;
            var t = text.Trim().ToLowerInvariant();
            string cmd = t.StartsWith("/dane") ? "/dane" : t.StartsWith("/data") ? "/data" : null;
            if (cmd == null || (t.Length > 5 && t[5] != ' ')) return;
            sendToOthers = false;
            var arg = t.Length > 5 ? t.Substring(5).Trim() : "";
            if (arg == "" || arg == "opcje" || arg == "menu")
                mainThread.Enqueue(OpenConfigDialog);   // po zamknięciu czatu
            else if (arg == "ukryj" || arg == "off" || arg == "hide")
                mainThread.Enqueue(() => { Cfg.Visible = false; Cfg.Save(); Dirty = true; Chat(L.T("Okna ukryte (/dane pokaz = przywróć).", "Windows hidden (/data show = restore).")); });
            else if (arg == "pokaz" || arg == "pokaż" || arg == "on" || arg == "show")
                mainThread.Enqueue(() => { Cfg.Visible = true; Cfg.Save(); Dirty = true; Chat(L.T("Okna widoczne.", "Windows visible.")); });
            else if (arg == "en" || arg == "english")
                mainThread.Enqueue(() => { Cfg.Language = 1; Cfg.Save(); Dirty = true; Chat("Language: English."); });
            else if (arg == "pl" || arg == "polski")
                mainThread.Enqueue(() => { Cfg.Language = 0; Cfg.Save(); Dirty = true; Chat("Język: polski."); });
            else
                Chat(L.T("/dane — ustawienia okien; /dane ukryj — schowaj okna; /dane pokaz — pokaż; /dane en — English.",
                         "/data — window settings; /data hide — hide windows; /data show — show; /data pl — polski."));
        }

        internal static void Chat(string msg)
        {
            try { MyAPIGateway.Utilities?.ShowMessage("Dane", msg); } catch { }
        }

        // ------------------------------------------------------------------ log

        static readonly HashSet<string> logged = new HashSet<string>();

        internal static void LogOnce(string key, Exception e)
        {
            if (logged.Add(key)) Log(key + ": " + e);
        }

        internal static void Log(string msg)
        {
            try
            {
                MyLog.Default.WriteLine("DaneHud: " + msg);
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SpaceEngineers", "Plugins", "DaneHud.log");
                File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + msg + Environment.NewLine);
            }
            catch { }
        }
    }
}
