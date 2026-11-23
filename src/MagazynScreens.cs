using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Sandbox.Definitions;
using Sandbox.Graphics.GUI;
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
            Label(x + 0.035f, y, T("Najpierw rudy:", "Ores first:"), 0.7f);
            TextBox(x + 0.2f, y, 0.35f, m.OrePriority, 200, v => m.OrePriority = v); y += row;

            // --- lód, uran
            Check(x, y, T("Lód do generatorów H2/O2, wypełnienie %:", "Ice into H2/O2 generators, fill %:"), m.Ice, v => m.Ice = v);
            Number(x + 0.45f, y, 0.08f, m.IceFillPct, v => m.IceFillPct = (int)Math.Max(5, Math.Min(100, v))); y += row;
            Check(x, y, T("Uran do reaktorów: duże / małe:", "Uranium into reactors: large / small:"), m.Uranium, v => m.Uranium = v);
            Number(x + 0.45f, y, 0.08f, m.UraniumLarge, v => m.UraniumLarge = Math.Max(0, v));
            Number(x + 0.54f, y, 0.08f, m.UraniumSmall, v => m.UraniumSmall = Math.Max(0, v)); y += row;

            // --- autocrafting
            Check(x, y, T("Autocrafting wg limitów", "Autocrafting by limits"), m.Autocraft, v => m.Autocraft = v);
            int set = m.Quotas.Count(q => q.Amount > 0);
            Button(0.2f, y, T($"Limity autocraftingu... ({set})", $"Autocrafting limits... ({set})"), () => MyGuiSandbox.AddScreen(new QuotaScreen(g, () => RecreateControls(false))), 0.3f); y += row;

            Label(x, y, T("Przeniesień na sekundę:", "Moves per second:"), 0.7f);
            Number(x + 0.2f, y, 0.08f, m.MovesPerSecond, v => m.MovesPerSecond = (int)Math.Max(2, Math.Min(60, v))); y += row * 1.1f;

            // --- stan
            var mg = Magazyn.I;
            bool here = Plugin.ActiveProfile == g;
            Label(x, y, T("Stan: ", "State: ") + (!m.Enabled ? T("wyłączony", "off") : !here ? T("usiądź w tym gridzie, żeby ruszył", "sit in this grid to start it") : mg.State)
                + "   " + T("przeniesienia: ", "moves: ") + mg.Moved + "   " + T("kolejka: ", "queued: ") + mg.Queued, 0.7f); y += row * 0.8f;
            if (!string.IsNullOrEmpty(mg.Warning)) { var l = Label(x, y, mg.Warning, 0.7f); l.ColorMask = new Vector4(1f, 0.35f, 0.35f, 1f); y += row * 0.8f; }
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

    // ====================================================================== limity autocraftingu

    internal class QuotaScreen : DialogBase
    {
        readonly GridProfile g;
        readonly Action onClose;
        static int cat = 0;
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

        List<MyPhysicalItemDefinition> Defs()
        {
            var c = Cats[Math.Max(0, Math.Min(Cats.Length - 1, cat))];
            var list = new List<MyPhysicalItemDefinition>();
            try
            {
                foreach (var d in MyDefinitionManager.Static.GetPhysicalItemDefinitions())
                {
                    if (d == null || !d.Public) continue;
                    if (c != ItemCat.All && Ctx.CatOf(d.Id.TypeId.ToString()) != c) continue;
                    if (Magazyn.BlueprintFor(d.Id) == null) continue;
                    list.Add(d);
                }
            }
            catch { }
            return list.OrderBy(d => d.DisplayNameText).ToList();
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
                var keys = new HashSet<string>(Defs().Select(d => d.Id.ToString()));
                m.Quotas.RemoveAll(q => keys.Contains(q.Id));
                RecreateControls(false);
            }, 0.2f);
            Label(-0.39f, -0.345f, string.IsNullOrEmpty(info)
                ? T("Wpisz, ile sztuk ma być w gridzie (0 = bez limitu). Liczba po prawej = ile jest teraz.", "Enter how many should be in the grid (0 = no limit). The number on the right = how many now.")
                : info, 0.64f);

            var defs = Defs();
            const float width = 0.78f, itemRow = 0.04f;
            const int ncol = 2;
            float colW = (width - 0.03f) / ncol;
            int rows = (defs.Count + ncol - 1) / ncol;
            float visibleH = 0.7f;
            float contentH = Math.Max(visibleH, rows * itemRow + 0.01f);
            var parent = new MyGuiControlParent { Size = new Vector2(width - 0.02f, contentH) };
            var inv = CultureInfo.InvariantCulture;
            for (int i = 0; i < defs.Count; i++)
            {
                var d = defs[i];
                var key = d.Id.ToString();
                float x0 = -(width - 0.02f) / 2f + 0.005f + (i % ncol) * colW;
                float yy = -contentH / 2f + itemRow / 2f + 0.005f + (i / ncol) * itemRow;
                parent.Controls.Add(new MyGuiControlLabel
                {
                    Position = new Vector2(x0, yy),
                    Text = d.DisplayNameText ?? d.Id.SubtypeName,
                    TextScale = 0.62f,
                    OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER,
                });
                var q = m.Quotas.FirstOrDefault(z => z.Id == key);
                var tb = new MyGuiControlTextbox(new Vector2(x0 + 0.19f, yy), q != null && q.Amount > 0 ? q.Amount.ToString("0.##", inv) : "", 12)
                {
                    OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER,
                };
                tb.Size = new Vector2(0.09f, tb.Size.Y);
                tb.TextChanged += t =>
                {
                    double.TryParse((t.Text ?? "").Replace(',', '.').Trim(), NumberStyles.Float, inv, out var v);
                    var qq = m.Quotas.FirstOrDefault(z => z.Id == key);
                    if (v <= 0) { if (qq != null) m.Quotas.Remove(qq); }
                    else { if (qq == null) m.Quotas.Add(qq = new Quota { Id = key }); qq.Amount = v; }
                    Plugin.Dirty = true;
                };
                parent.Controls.Add(tb);
                if (have != null)
                {
                    have.TryGetValue(key, out var h);
                    parent.Controls.Add(new MyGuiControlLabel
                    {
                        Position = new Vector2(x0 + 0.29f, yy),
                        Text = Fmt.Amount(h?.Amount ?? 0),
                        TextScale = 0.6f,
                        OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER,
                    });
                }
            }
            var panel = new MyGuiControlScrollablePanel(parent)
            {
                Position = new Vector2(-0.39f, -0.32f),
                Size = new Vector2(width, visibleH),
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP,
                ScrollbarVEnabled = true,
                BackgroundTexture = MyGuiConstants.TEXTURE_SCROLLABLE_LIST,
                ScrolledAreaPadding = new MyGuiBorderThickness(0.005f),
            };
            Controls.Add(panel);
            Button(0f, 0.43f, "OK", () => CloseScreen(false), 0.18f, 0.048f);
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
