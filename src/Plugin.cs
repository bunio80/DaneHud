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
    public class Plugin : IPlugin
    {
        internal const string Version = "1";
        internal static Config Cfg;
        internal static List<Row>[] Rows = new List<Row>[Config.WindowCount];
        internal static bool Seated;
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

        public void Dispose() => Unhook();

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

            bool needInv = false;
            for (int i = 0; i < Rows.Length; i++)
            {
                var w = i < Cfg.Windows.Count ? Cfg.Windows[i] : null;
                if (ctx == null || w == null || !w.Enabled || !Cfg.Visible) { Rows[i] = null; continue; }
                foreach (var e in w.Elements)
                    if (e.Kind == ElementKind.Items || e.Kind == ElementKind.CargoBar || e.Kind == ElementKind.Refineries || e.Kind == ElementKind.PowerTime)
                        needInv = true;
                try { Rows[i] = RowBuilder.Build(w, ctx); }
                catch (Exception e) { LogOnce("rows" + i, e); Rows[i] = null; }
            }

            // wirtualny terminal tylko, gdy siedzisz, okna są widoczne i któreś potrzebuje zawartości ładowni
            VirtualTerminal.EntityId = ctx != null && Cfg.LiveInventory && needInv && MyAPIGateway.Multiplayer != null && !MyAPIGateway.Multiplayer.IsServer
                ? ctx.Controller.EntityId : 0;
        }

        void Unhook()
        {
            VirtualTerminal.EntityId = 0;
            Seated = false;
            for (int i = 0; i < Rows.Length; i++) Rows[i] = null;
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
            if (t != "/dane" && !t.StartsWith("/dane ")) return;
            sendToOthers = false;
            var arg = t.Length > 5 ? t.Substring(5).Trim() : "";
            if (arg == "" || arg == "opcje" || arg == "menu")
                mainThread.Enqueue(OpenConfigDialog);   // po zamknięciu czatu
            else if (arg == "ukryj" || arg == "off")
                mainThread.Enqueue(() => { Cfg.Visible = false; Cfg.Save(); Dirty = true; Chat("Okna ukryte (/dane pokaz = przywróć)."); });
            else if (arg == "pokaz" || arg == "pokaż" || arg == "on")
                mainThread.Enqueue(() => { Cfg.Visible = true; Cfg.Save(); Dirty = true; Chat("Okna widoczne."); });
            else
                Chat("/dane — ustawienia okien; /dane ukryj — schowaj wszystkie okna; /dane pokaz — pokaż.");
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
