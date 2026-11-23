using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Sandbox.Definitions;
using Sandbox.Graphics;
using Sandbox.Graphics.GUI;
using VRage.Game;
using VRage.Utils;
using VRageMath;

namespace DaneHud
{
    // ====================================================================== Magazyn (zamiast Isy)

    internal class MagazynScreen : DialogBase
    {
        readonly GridProfile g;
        readonly Action onClose;
        MagazynCfg M => g.Magazyn ?? (g.Magazyn = new MagazynCfg());

        public MagazynScreen(GridProfile g, Action onClose) : base(new Vector2(0.84f, 0.95f))
        {
            this.g = g;
            this.onClose = onClose;
            EnabledBackgroundFade = true;
            RecreateControls(true);
        }

        public override string GetFriendlyName() => "DaneHudMagazyn";
        string installMsg = "";

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);
            var m = M;
            AddCaption(T("Magazyn: ", "Storage: ") + g.GridName);
            const float x = -0.39f, row = 0.042f;
            float y = -0.4f;

            Check(x, y, T("Magazyn włączony dla tego grida", "Storage enabled for this grid"), m.Enabled, v => m.Enabled = v); y += row * 0.8f;
            Label(x + 0.035f, y, T("Działa, gdy siedzisz w tym gridzie. Wyłącz wtedy PB Isy (inaczej będą się kłócić).", "Works while you sit in this grid. Turn Isy's PB off then (or they will fight)."), 0.64f); y += row;

            // --- sortowanie
            Check(x, y, T("Sortowanie do kontenerów ze słowem w nazwie", "Sort into containers with a keyword in the name"), m.Sort, v => m.Sort = v); y += row;
            Kw(x + 0.035f, y, T("Rudy", "Ores"), m.OreKw, v => m.OreKw = v);
            Kw(x + 0.28f, y, T("Sztabki", "Ingots"), m.IngotKw, v => m.IngotKw = v);
            Kw(x + 0.525f, y, T("Komponenty", "Components"), m.ComponentKw, v => m.ComponentKw = v); y += row;
            Kw(x + 0.035f, y, T("Narzędzia", "Tools"), m.ToolKw, v => m.ToolKw = v);
            Kw(x + 0.28f, y, T("Amunicja", "Ammo"), m.AmmoKw, v => m.AmmoKw = v);
            Kw(x + 0.525f, y, T("Butle", "Bottles"), m.BottleKw, v => m.BottleKw = v); y += row;
            Label(x + 0.035f, y, T("Pomijane bloki:", "Skipped blocks:"), 0.7f);
            TextBox(x + 0.2f, y, 0.55f, m.LockedKw, 300, v => m.LockedKw = v); y += row;

            // --- kontenery specjalne
            Check(x, y, T("Kontenery specjalne, słowo w nazwie:", "Special containers, keyword:"), m.Specials, v => m.Specials = v);
            TextBox(x + 0.38f, y, 0.12f, m.SpecialKw, 40, v => m.SpecialKw = v);
            Check(x + 0.53f, y, T("biorą z niższych [p]", "take from lower [p]"), m.SpecialSteal, v => m.SpecialSteal = v); y += row * 0.8f;
            Label(x + 0.035f, y, T("Jak u Isy: zawartość w CustomData (item=100, 100M, 100L, All), priorytet [p1] [pmax] [pmin] w nazwie.", "Like Isy: contents in CustomData (item=100, 100M, 100L, All), priority [p1] [pmax] [pmin] in the name."), 0.62f); y += row;

            // --- rafinerie
            Check(x, y, T("Rafinerie: każda ruda po równo we wszystkich", "Refineries: every ore split equally"), m.Refineries, v => m.Refineries = v); y += row * 0.8f;
            Label(x + 0.035f, y, T("Na czas pracy rafinerie nie pobierają rudy z konwejera (jak Twój Isy v4); gdy wstaniesz — pobierają znowu.", "While running, refineries don't pull ore themselves (like your Isy v4); when you stand up they do again."), 0.62f); y += row * 0.8f;
            Label(x + 0.035f, y, T("Kolejność rud:", "Ore order:"), 0.7f);
            string order = string.IsNullOrWhiteSpace(m.OrePriority) ? T("(brak — po równo wg ilości)", "(none — by amount)") : m.OrePriority;
            Button(x + 0.4f, y, order + "  ...", () => MyGuiSandbox.AddScreen(new OrePriorityScreen(m, () => RecreateControls(false))), 0.42f); y += row;

            // --- skrypt rafinerii 24/7
            Ctx here0 = null;
            if (Plugin.ActiveProfile == g) try { here0 = Ctx.Collect(); } catch { }
            var spb = Magazyn.RefineryScript(here0);
            Label(x + 0.035f, y, T("Rafinerie 24/7 (skrypt PB): ", "Refineries 24/7 (PB script): ") +
                (here0 == null ? T("usiądź w bazie", "sit in the base") : spb != null ? T("działa w ", "running in ") + ((Sandbox.ModAPI.Ingame.IMyTerminalBlock)spb).CustomName : T("brak", "none")), 0.66f);
            Button(0.25f, y, T("Wgraj / zaktualizuj skrypt", "Install / update script"), () =>
            {
                Ctx cc = null;
                try { cc = Ctx.Collect(); } catch { }
                installMsg = Magazyn.InstallScript(cc, M);
                RecreateControls(false);
            }, 0.3f); y += row * 0.8f;
            Label(x + 0.035f, y, string.IsNullOrEmpty(installMsg)
                ? T("Pracuje na serwerze także bez Ciebie (Nexus, offline). Wgrywa do PB z „Rafinerie” w nazwie albo do pustego PB.",
                    "Runs on the server without you too (Nexus, offline). Installs into a PB named \"Refineries\" or an empty PB.")
                : installMsg, 0.62f); y += row;

            // --- lód, uran
            Check(x, y, T("Lód do generatorów H2/O2, wypełnienie %:", "Ice into H2/O2 generators, fill %:"), m.Ice, v => m.Ice = v);
            Number(x + 0.45f, y, 0.08f, m.IceFillPct, v => m.IceFillPct = (int)Math.Max(5, Math.Min(100, v))); y += row;
            Check(x, y, T("Uran do reaktorów: duże / małe:", "Uranium into reactors: large / small:"), m.Uranium, v => m.Uranium = v);
            Number(x + 0.45f, y, 0.08f, m.UraniumLarge, v => m.UraniumLarge = Math.Max(0, v));
            Number(x + 0.54f, y, 0.08f, m.UraniumSmall, v => m.UraniumSmall = Math.Max(0, v)); y += row;

            // --- autocrafting
            Check(x, y, T("Autocrafting wg limitów", "Autocrafting by limits"), m.Autocraft, v =>
            {
                m.Autocraft = v;
                if (v)
                    foreach (var w in g.Windows)
                        foreach (var e in w.Elements)
                            if (e.Kind == ElementKind.Autocrafting) e.FromPlugin = true;
            });
            int set = m.Quotas.Count(q => q.Amount > 0);
            Button(0.2f, y, T($"Limity autocraftingu... ({set})", $"Autocrafting limits... ({set})"), () => MyGuiSandbox.AddScreen(new QuotaScreen(g, () => RecreateControls(false))), 0.3f); y += row * 0.8f;
            Label(x + 0.035f, y, T("Nauka jak u Isy: \"!learn\" w nazwie montażowni + przedmiot w jej kolejce = nowy limit (\"!learnMany\" uczy dalej).",
                                   "Learning like Isy: \"!learn\" in an assembler name + item in its queue = new limit (\"!learnMany\" keeps learning)."), 0.62f); y += row * 0.9f;

            Label(x, y, T("Przeniesień na sekundę:", "Moves per second:"), 0.7f);
            Number(x + 0.2f, y, 0.08f, m.MovesPerSecond, v => m.MovesPerSecond = (int)Math.Max(2, Math.Min(60, v))); y += row * 1.1f;

            // --- stan
            var mg = Magazyn.I;
            bool here = Plugin.ActiveProfile == g;
            Label(x, y, T("Stan: ", "State: ") + (!m.Enabled ? T("wyłączony", "off") : !here ? T("usiądź w tym gridzie, żeby ruszył", "sit in this grid to start it") : mg.State)
                + "   " + T("przeniesienia: ", "moves: ") + mg.Moved + "   " + T("kolejka: ", "queued: ") + mg.Queued, 0.7f); y += row * 0.8f;
            if (!string.IsNullOrEmpty(mg.Warning)) { var l = Label(x, y, mg.Warning, 0.7f); l.ColorMask = new Vector4(1f, 0.35f, 0.35f, 1f); y += row * 0.8f; }
            if (mg.Unreachable.Count > 0)
            {
                var l2 = Label(x, y, T("Bez połączenia konwejerem (nie da się przenieść): ", "Not connected by conveyor (cannot move): ") + string.Join(", ", mg.Unreachable.Take(5)), 0.64f);
                l2.ColorMask = new Vector4(1f, 0.6f, 0.3f, 1f); y += row * 0.7f;
            }
            foreach (var r in mg.Recent.Take(3)) { Label(x, y, r, 0.6f); y += row * 0.6f; }

            Button(0f, 0.43f, "OK", () => CloseScreen(false), 0.18f, 0.048f);
        }

        void Kw(float x, float y, string label, string value, Action<string> set)
        {
            Label(x, y, label + ":", 0.68f);
            TextBox(x + 0.105f, y, 0.115f, value, 40, set);
        }

        protected override void OnClosed()
        {
            Plugin.Cfg?.Save();
            Plugin.Dirty = true;
            try { onClose?.Invoke(); } catch { }
            base.OnClosed();
        }
    }

    // ====================================================================== kolejność rud w rafineriach

    internal class OrePriorityScreen : DialogBase
    {
        readonly MagazynCfg m;
        readonly Action onClose;
        readonly List<string> order;
        int selAll = -1, selOrder = -1;

        public OrePriorityScreen(MagazynCfg m, Action onClose) : base(new Vector2(0.74f, 0.78f))
        {
            this.m = m;
            this.onClose = onClose;
            order = (m.OrePriority ?? "").Split(',').Select(z => z.Trim()).Where(z => z.Length > 0).ToList();
            EnabledBackgroundFade = true;
            RecreateControls(true);
        }

        public override string GetFriendlyName() => "DaneHudOreOrder";

        /// <summary>Wszystkie rudy z definicji gry i modów (czytane na bieżąco, nic nie jest wpisane na stałe).</summary>
        static List<MyPhysicalItemDefinition> AllOres()
        {
            try
            {
                return MyDefinitionManager.Static.GetPhysicalItemDefinitions()
                    .Where(d => d != null && d.Public && d.Id.TypeId.ToString() == "MyObjectBuilder_Ore")
                    .OrderBy(d => d.DisplayNameText).ToList();
            }
            catch { return new List<MyPhysicalItemDefinition>(); }
        }

        static string Show(MyPhysicalItemDefinition d) => (d.DisplayNameText ?? d.Id.SubtypeName) + (d.DisplayNameText == d.Id.SubtypeName ? "" : "  (" + d.Id.SubtypeName + ")");

        MyGuiControlListbox List(float x, float y, IEnumerable<string> items, int sel, Action<int> onSel, Action<int> onDouble)
        {
            var lb = new MyGuiControlListbox(new Vector2(x, y), MyGuiControlListboxStyleEnum.Default)
            {
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP,
                VisibleRowsCount = 12,
                MultiSelect = false,
            };
            lb.Size = new Vector2(0.3f, lb.Size.Y);
            int i = 0;
            foreach (var t in items)
            {
                var it = new MyGuiControlListbox.Item(new System.Text.StringBuilder(t), t, null, i);
                lb.Add(it);
                if (i == sel) lb.SelectSingleItem(it);
                i++;
            }
            lb.ItemsSelected += l => { if (l.SelectedItems.Count > 0) onSel((int)l.SelectedItems[0].UserData); };
            lb.ItemDoubleClicked += l => { if (l.SelectedItems.Count > 0) onDouble((int)l.SelectedItems[0].UserData); };
            Controls.Add(lb);
            return lb;
        }

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);
            AddCaption(T("Kolejność rud w rafineriach", "Ore order in refineries"));
            var all = AllOres();
            var free = all.Where(d => !order.Contains(d.Id.SubtypeName)).ToList();
            Label(-0.33f, -0.3f, T("Wszystkie rudy w grze:", "All ores in the game:"), 0.72f);
            Label(0.03f, -0.3f, T("Kolejność (pierwsza na górze):", "Order (first at the top):"), 0.72f);
            List(-0.33f, -0.275f, free.Select(Show), selAll, i => selAll = i, i => { selAll = i; Add(free); });
            List(0.03f, -0.275f, order.Select((o, i) =>
            {
                var d = all.FirstOrDefault(z => z.Id.SubtypeName == o);
                return (i + 1) + ". " + (d != null ? Show(d) : o);
            }), selOrder, i => selOrder = i, i => { selOrder = i; Remove(); });

            const float by = 0.21f;
            Button(-0.18f, by, T("Dodaj »", "Add »"), () => Add(free), 0.16f);
            Button(0.075f, by, T("W górę", "Up"), () => MoveSel(-1), 0.1f);
            Button(0.18f, by, T("W dół", "Down"), () => MoveSel(1), 0.1f);
            Button(0.285f, by, T("« Usuń", "« Remove"), Remove, 0.1f);
            Label(-0.33f, by + 0.05f, T("Dwuklik po lewej = dodaj na koniec kolejki, po prawej = usuń. Rudy spoza listy idą po nich, wg ilości.",
                                       "Double-click left = add to the end, right = remove. Ores not on the list follow, by amount."), 0.62f);
            Button(0f, 0.33f, "OK", () => CloseScreen(false), 0.18f, 0.048f);
        }

        void Add(List<MyPhysicalItemDefinition> free)
        {
            if (selAll < 0 || selAll >= free.Count) return;
            order.Add(free[selAll].Id.SubtypeName);
            selAll = Math.Min(selAll, free.Count - 2);
            selOrder = order.Count - 1;
            Save();
        }

        void Remove()
        {
            if (selOrder < 0 || selOrder >= order.Count) return;
            order.RemoveAt(selOrder);
            selOrder = Math.Min(selOrder, order.Count - 1);
            Save();
        }

        void MoveSel(int d)
        {
            int j = selOrder + d;
            if (selOrder < 0 || selOrder >= order.Count || j < 0 || j >= order.Count) return;
            var t = order[selOrder]; order[selOrder] = order[j]; order[j] = t;
            selOrder = j;
            Save();
        }

        void Save()
        {
            m.OrePriority = string.Join(", ", order);
            Plugin.Dirty = true;
            RecreateControls(false);
        }

        protected override void OnClosed()
        {
            Plugin.Cfg?.Save();
            try { onClose?.Invoke(); } catch { }
            base.OnClosed();
        }
    }

    // ====================================================================== limity autocraftingu

    internal class QuotaScreen : DialogBase
    {
        readonly GridProfile g;
        readonly Action onClose;
        static int cat = 0;
        static bool onlySet = false;
        static readonly ItemCat[] Cats = { ItemCat.Component, ItemCat.Tool, ItemCat.Ammo, ItemCat.Bottle, ItemCat.Ingot, ItemCat.Consumable, ItemCat.Other, ItemCat.All };
        Dictionary<string, ItemAmount> have;
        string info = "";

        public QuotaScreen(GridProfile g, Action onClose) : base(new Vector2(0.84f, 0.95f))
        {
            this.g = g;
            this.onClose = onClose;
            EnabledBackgroundFade = true;
            if (Plugin.ActiveProfile == g)
                try { have = Ctx.Collect()?.Items(false); } catch { }
            RecreateControls(true);
        }

        public override string GetFriendlyName() => "DaneHudQuota";

        static string NameOf(MyPhysicalItemDefinition d) => d.DisplayNameText ?? d.Id.SubtypeName;

        List<MyPhysicalItemDefinition> Defs(bool filterSet)
        {
            var c = Cats[Math.Max(0, Math.Min(Cats.Length - 1, cat))];
            var m = g.Magazyn;
            var list = new List<MyPhysicalItemDefinition>();
            try
            {
                foreach (var d in MyDefinitionManager.Static.GetPhysicalItemDefinitions())
                {
                    if (d == null || !d.Public) continue;
                    if (c != ItemCat.All && Ctx.CatOf(d.Id.TypeId.ToString()) != c) continue;
                    if (Magazyn.BlueprintFor(d.Id) == null) continue;
                    if (filterSet)
                    {
                        string key = d.Id.ToString();
                        var q = m.Quotas.FirstOrDefault(z => z.Id == key);
                        if (q == null || (q.Amount <= 0 && !q.Hide && !q.Priority)) continue;
                    }
                    list.Add(d);
                }
            }
            catch { }
            return list.OrderBy(NameOf, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        /// <summary>Wpis limitu (tworzy go, gdy trzeba); pusty wpis bez flag znika.</summary>
        Quota Get(string key, bool create)
        {
            var m = g.Magazyn;
            var q = m.Quotas.FirstOrDefault(z => z.Id == key);
            if (q == null && create) m.Quotas.Add(q = new Quota { Id = key });
            return q;
        }

        void Tidy(string key)
        {
            var q = Get(key, false);
            if (q != null && q.Amount <= 0 && !q.Hide && !q.Priority) g.Magazyn.Quotas.Remove(q);
            Plugin.Dirty = true;
        }

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);
            var m = g.Magazyn;
            AddCaption(T("Limity autocraftingu: ", "Autocrafting limits: ") + g.GridName);
            Label(-0.39f, -0.39f, T("Rodzaj:", "Kind:"), 0.72f);
            Combo(-0.31f, -0.39f, 0.22f, Cats.Select(ElementCfg.CatName).ToList(), cat, v => { cat = v; RecreateControls(false); });
            Button(0.1f, -0.39f, T("Importuj z LCD Isy", "Import from Isy's LCD"), () =>
            {
                Ctx c = null;
                try { c = Ctx.Collect(); } catch { }
                if (c == null) { info = T("Usiądź w gridzie z LCD Isy.", "Sit in the grid with Isy's LCD."); RecreateControls(false); return; }
                int n = Magazyn.ImportFromIsy(c, m, "Autocrafting", out int unknown);
                info = n == 0 ? T("Nie znaleziono LCD „Autocrafting” albo jest pusty.", "No \"Autocrafting\" LCD found, or it is empty.")
                              : T($"Wczytano {n} limitów", $"Imported {n} limits") + (unknown > 0 ? T($", nierozpoznane: {unknown}", $", unrecognized: {unknown}") : "");
                Plugin.Dirty = true;
                RecreateControls(false);
            }, 0.22f);
            Button(0.32f, -0.39f, T("Wyzeruj ten rodzaj", "Clear this kind"), () =>
            {
                var keys = new HashSet<string>(Defs(false).Select(d => d.Id.ToString()));
                m.Quotas.RemoveAll(q => keys.Contains(q.Id));
                Plugin.Dirty = true;
                RecreateControls(false);
            }, 0.2f);
            Check(-0.39f, -0.345f, T("Tylko z limitem", "Only with a limit"), onlySet, v => { onlySet = v; RecreateControls(false); });
            Label(-0.15f, -0.345f, string.IsNullOrEmpty(info)
                ? T("Limit = ile sztuk ma być w gridzie (puste = bez limitu).", "Limit = how many should be in the grid (empty = none).")
                : info, 0.62f);

            // kolumny (przesunięcia od lewej krawędzi wiersza)
            const float oHave = 0.40f, oQueue = 0.49f, oLimit = 0.505f, oPrio = 0.635f, oHide = 0.69f;
            const float hx = -0.375f, hy = -0.3f;
            Label(hx, hy, T("Przedmiot", "Item"), 0.64f);
            HeadRight(hx + oHave, hy, T("Jest", "Have"));
            HeadRight(hx + oQueue, hy, T("W kolejce", "Queued"));
            Label(hx + oLimit, hy, T("Limit", "Limit"), 0.64f);
            Label(hx + oPrio - 0.012f, hy, T("Prio.", "Prio."), 0.64f);
            Label(hx + oHide - 0.012f, hy, T("Ukryj", "Hide"), 0.64f);

            var defs = Defs(onlySet);
            const float width = 0.78f, itemRow = 0.038f;
            float visibleH = 0.66f;
            float contentH = Math.Max(visibleH, defs.Count * itemRow + 0.01f);
            float pw = width - 0.02f;
            var parent = new MyGuiControlParent { Size = new Vector2(pw, contentH) };
            var inv = CultureInfo.InvariantCulture;
            Dictionary<string, double> queued = Plugin.ActiveProfile == g ? Magazyn.I.QueuedNow : null;
            float x0 = -pw / 2f + 0.01f;
            for (int i = 0; i < defs.Count; i++)
            {
                var d = defs[i];
                var key = d.Id.ToString();
                float yy = -contentH / 2f + itemRow / 2f + 0.005f + i * itemRow;
                var q = Get(key, false);
                double hv = 0;
                if (have != null && have.TryGetValue(key, out var h)) hv = h?.Amount ?? 0;
                bool low = q != null && q.Amount > 0 && hv < q.Amount;
                string name = NameOf(d);
                if (name.Length > 34) name = name.Substring(0, 33) + "…";
                parent.Controls.Add(new MyGuiControlLabel
                {
                    Position = new Vector2(x0, yy), Text = name, TextScale = 0.62f,
                    ColorMask = q != null && q.Hide ? new Vector4(0.6f, 0.6f, 0.6f, 1f) : Vector4.One,
                    OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER,
                });
                if (have != null)
                    parent.Controls.Add(new MyGuiControlLabel
                    {
                        Position = new Vector2(x0 + oHave, yy), Text = Fmt.Amount(hv), TextScale = 0.6f,
                        ColorMask = low ? new Vector4(1f, 0.55f, 0.35f, 1f) : Vector4.One,
                        OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_CENTER,
                    });
                if (queued != null && queued.TryGetValue(key, out var qv) && qv > 0)
                    parent.Controls.Add(new MyGuiControlLabel
                    {
                        Position = new Vector2(x0 + oQueue, yy), Text = "+" + Fmt.Amount(qv), TextScale = 0.6f,
                        OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_CENTER,
                    });
                var tb = new MyGuiControlTextbox(new Vector2(x0 + oLimit, yy), q != null && q.Amount > 0 ? q.Amount.ToString("0.##", inv) : "", 12)
                {
                    OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER,
                };
                tb.Size = new Vector2(0.1f, tb.Size.Y);
                tb.TextChanged += t =>
                {
                    double.TryParse((t.Text ?? "").Replace(',', '.').Trim(), NumberStyles.Float, inv, out var v);
                    Get(key, true).Amount = Math.Max(0, v);
                    Tidy(key);
                };
                parent.Controls.Add(tb);
                var cp = new MyGuiControlCheckbox(new Vector2(x0 + oPrio, yy), null, T("Zawsze pierwszy w kolejce (P u Isy)", "Always first in the queue (Isy's P)"),
                    q != null && q.Priority, MyGuiControlCheckboxStyleEnum.Default, MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER);
                cp.IsCheckedChanged += cb => { Get(key, true).Priority = cb.IsChecked; Tidy(key); };
                parent.Controls.Add(cp);
                var ch = new MyGuiControlCheckbox(new Vector2(x0 + oHide, yy), null, T("Limit działa, ale nie pokazuj na HUD (H u Isy)", "Limit works but hide from the HUD (Isy's H)"),
                    q != null && q.Hide, MyGuiControlCheckboxStyleEnum.Default, MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER);
                ch.IsCheckedChanged += cb => { Get(key, true).Hide = cb.IsChecked; Tidy(key); };
                parent.Controls.Add(ch);
            }
            if (defs.Count == 0)
                parent.Controls.Add(new MyGuiControlLabel
                {
                    Position = new Vector2(x0, -contentH / 2f + 0.03f), TextScale = 0.65f,
                    Text = onlySet ? T("Brak limitów w tym rodzaju.", "No limits of this kind.") : T("(pusto)", "(empty)"),
                    OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER,
                });
            var panel = new MyGuiControlScrollablePanel(parent)
            {
                Position = new Vector2(-0.39f, -0.28f),
                Size = new Vector2(width, visibleH),
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP,
                ScrollbarVEnabled = true,
                BackgroundTexture = MyGuiConstants.TEXTURE_SCROLLABLE_LIST,
                ScrolledAreaPadding = new MyGuiBorderThickness(0.005f),
            };
            Controls.Add(panel);
            Button(0f, 0.43f, "OK", () => CloseScreen(false), 0.18f, 0.048f);
        }

        void HeadRight(float x, float y, string text)
        {
            Controls.Add(new MyGuiControlLabel
            {
                Position = new Vector2(x, y), Text = text, TextScale = 0.64f,
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_CENTER,
            });
        }

        protected override void OnClosed()
        {
            Plugin.Cfg?.Save();
            Plugin.Dirty = true;
            try { onClose?.Invoke(); } catch { }
            base.OnClosed();
        }
    }
}
