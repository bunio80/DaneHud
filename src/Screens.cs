using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Sandbox.Definitions;
using Sandbox.Graphics;
using Sandbox.Graphics.GUI;
using VRage.Game;
using VRage.Utils;
using VRageMath;

namespace DaneHud
{
    /// <summary>Wspólne pomocnicze kontrolki.</summary>
    internal abstract class DialogBase : MyGuiScreenBase
    {
        protected DialogBase(Vector2 size, float alpha = 0.97f)
            : base(new Vector2(0.5f, 0.5f), new Vector4(0.08f, 0.1f, 0.12f, alpha), size, false, null, 0.95f, 0.95f, null)
        {
            CloseButtonEnabled = true;
        }

        protected static string T(string pl, string en) => L.T(pl, en);

        protected MyGuiControlLabel Label(float x, float y, string text, float scale = 0.75f)
        {
            var l = new MyGuiControlLabel
            {
                Position = new Vector2(x, y),
                Text = text,
                TextScale = scale,
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER,
            };
            Controls.Add(l);
            return l;
        }

        protected MyGuiControlCheckbox Check(float x, float y, string text, bool value, Action<bool> set)
        {
            var cb = new MyGuiControlCheckbox(new Vector2(x, y), null, null, value,
                MyGuiControlCheckboxStyleEnum.Default, MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER);
            cb.IsCheckedChanged += c => { set(c.IsChecked); Plugin.Dirty = true; };
            Controls.Add(cb);
            Label(x + 0.035f, y, text, 0.72f);
            return cb;
        }

        /// <summary>Przycisk o podanej szerokości (środek w x, y). Długi tekst zmniejsza się, żeby się zmieścił.</summary>
        protected MyGuiControlButton Button(float x, float y, string text, Action click, float width = 0.18f, float height = 0.042f)
        {
            var b = new MyGuiControlButton(
                position: new Vector2(x, y),
                visualStyle: MyGuiControlButtonStyleEnum.Rectangular,
                size: new Vector2(width, height),
                originAlign: MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER,
                text: new StringBuilder(text),
                textScale: 0.75f,
                onButtonClick: _ => click(),
                isAutoscaleEnabled: true,
                isEllipsisEnabled: true);
            Controls.Add(b);
            return b;
        }

        protected MyGuiControlTextbox TextBox(float x, float y, float width, string value, int maxLen, Action<string> set)
        {
            var tb = new MyGuiControlTextbox(new Vector2(x, y), value ?? "", maxLen)
            {
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER,
            };
            tb.Size = new Vector2(width, tb.Size.Y);
            tb.TextChanged += t => { set(t.Text); Plugin.Dirty = true; };
            Controls.Add(tb);
            return tb;
        }

        protected MyGuiControlTextbox Number(float x, float y, float width, double value, Action<double> set)
        {
            return TextBox(x, y, width, value.ToString("0.##", CultureInfo.InvariantCulture), 12, s =>
            {
                if (double.TryParse((s ?? "").Replace(',', '.').Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) set(v);
                else if (string.IsNullOrWhiteSpace(s)) set(0);
            });
        }

        protected MyGuiControlSlider Slider(float x, float y, float width, float min, float max, float value, string fmt, int decimals, Action<float> set)
        {
            var s = new MyGuiControlSlider(new Vector2(x, y), min, max, width, value, null, fmt, decimals, 0.7f, 0.07f,
                "White", null, MyGuiControlSliderStyleEnum.Default, MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER, decimals == 0, true);
            s.Value = value;
            s.ValueChanged += sl => { set(sl.Value); Plugin.Dirty = true; };
            Controls.Add(s);
            return s;
        }

        protected MyGuiControlCombobox Combo(float x, float y, float width, IList<string> items, int selected, Action<int> set)
        {
            var c = new MyGuiControlCombobox(new Vector2(x, y))
            {
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER,
            };
            c.Size = new Vector2(width, c.Size.Y);
            for (int i = 0; i < items.Count; i++) c.AddItem(i, items[i]);
            if (items.Count > 0) c.SelectItemByKey(Math.Max(0, Math.Min(items.Count - 1, selected)));
            c.ItemSelected += () => { set((int)c.GetSelectedKey()); Plugin.Dirty = true; };
            Controls.Add(c);
            return c;
        }
    }

    // ====================================================================== główne okno /data

    internal class MainScreen : DialogBase
    {
        readonly Config cfg;
        static int sel;                 // wybrane okno (zapamiętane między otwarciami)
        static GridProfile editing;     // edytowany profil grida (null = szablon)
        int selElem = -1;
        int addIdx = 2;                 // pozycja w ElementCfg.AddOrder
        int loadIdx = 0;

        public MainScreen(Config cfg) : base(new Vector2(0.95f, 0.97f), 0.96f)
        {
            this.cfg = cfg;
            EnabledBackgroundFade = true;

            // siedzisz w gridzie: edytujesz jego ustawienia (pierwszy raz = kopia szablonu)
            editing = null;
            if (cfg.PerGrid && Plugin.Seated && Plugin.GridId != 0)
                editing = cfg.FindProfile(Plugin.GridId, Plugin.GridName) ?? cfg.CreateProfile(Plugin.GridId, Plugin.GridName);
            RecreateControls(true);
        }

        public override string GetFriendlyName() => "DaneHudMain";

        bool hiddenForLayout;

        /// <summary>Na czas ustawiania położenia okna: menu znika, a gra (HUD i okna) jest widoczna pod spodem.</summary>
        internal void HideForLayout(bool hide)
        {
            hiddenForLayout = hide;
            CanHideOthers = !hide;
            EnabledBackgroundFade = !hide;
        }

        public override bool Draw() => hiddenForLayout || base.Draw();

        List<WindowCfg> Wins => editing != null && cfg.Grids.Contains(editing) ? editing.Windows : cfg.Windows;

        WindowCfg W
        {
            get
            {
                var l = Wins;
                if (l.Count == 0) return null;
                sel = Math.Max(0, Math.Min(l.Count - 1, sel));
                return l[sel];
            }
        }

        void Redraw() { Plugin.Dirty = true; RecreateControls(false); }

        static string ProfileName(GridProfile g) =>
            T("Grid: ", "Grid: ") + (string.IsNullOrEmpty(g.GridName) ? "?" : g.GridName) + (g.GridId == Plugin.GridId && Plugin.Seated ? T("  (tu siedzisz)", "  (you are here)") : "");

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);
            AddCaption("Grid HUD & Logistics  v" + Plugin.Version);
            if (editing != null && !cfg.Grids.Contains(editing)) editing = null;

            // ---------------------------------------------------------------- profil
            const float lx = -0.44f;
            float y = -0.385f;
            var profiles = new List<string> { T("Domyślne (gridy bez własnych ustawień)", "Default (grids without own settings)") };
            profiles.AddRange(cfg.Grids.Select(ProfileName));
            Label(lx, y, T("Ustawienia dla:", "Settings for:"), 0.75f);
            Combo(lx + 0.14f, y, 0.42f, profiles, editing == null ? 0 : cfg.Grids.IndexOf(editing) + 1, v =>
            {
                editing = v == 0 ? null : cfg.Grids[v - 1];
                sel = 0; selElem = -1; Redraw();
            });
            Label(0.16f, y, T("Język:", "Language:"), 0.75f);
            Combo(0.26f, y, 0.19f, new[] { "Polski", "English" }, cfg.Language, v => { cfg.Language = v; Redraw(); });

            y += 0.05f;
            Check(lx, y, T("Osobne okna dla każdego grida", "Separate windows for each grid"), cfg.PerGrid, v => { cfg.PerGrid = v; });
            Button(0.115f, y, T($"Szablony... ({cfg.Templates.Count})", $"Templates... ({cfg.Templates.Count})"),
                () => MyGuiSandbox.AddScreen(new TemplatesScreen(cfg, () => Wins, () => { sel = 0; selElem = -1; Redraw(); })), 0.2f);
            if (editing != null)
            {
                Button(0.335f, y, T("Usuń ustawienia grida", "Delete grid settings"), () =>
                {
                    cfg.Grids.Remove(editing);
                    editing = null; sel = 0; selElem = -1; Redraw();
                }, 0.22f);
            }

            y += 0.05f;
            var sources = new List<string> { T("Domyślne fabryczne (6 okien)", "Factory defaults (6 windows)") };
            if (editing != null) sources.Add(T("Domyślne dla nowych gridów", "Default for new grids"));
            var templates = cfg.Templates.ToList();
            sources.AddRange(templates.Select(t => T("Szablon: ", "Template: ") + t.Name));
            var srcProfiles = cfg.Grids.Where(g => g != editing).ToList();
            sources.AddRange(srcProfiles.Select(ProfileName));
            loadIdx = Math.Min(loadIdx, sources.Count - 1);
            Label(lx, y, T("Wczytaj okna z:", "Load windows from:"), 0.75f);
            Combo(lx + 0.14f, y, 0.48f, sources, loadIdx, v => loadIdx = v);
            Button(0.335f, y, T("Wczytaj (zastąp)", "Load (replace)"), () =>
            {
                List<WindowCfg> src;
                int k = loadIdx;
                if (k == 0) src = WindowCfg.Defaults();
                else
                {
                    k--;
                    if (editing != null && k == 0) src = cfg.Windows;
                    else
                    {
                        if (editing != null) k--;
                        src = k < templates.Count ? templates[k].Windows : srcProfiles[k - templates.Count].Windows;
                    }
                }
                var copy = src.Select(w => w.Clone()).ToList();
                var target = Wins;
                target.Clear();
                target.AddRange(copy);
                sel = 0; selElem = -1; Redraw();
            }, 0.22f);

            // ---------------------------------------------------------------- wybór okna
            y += 0.06f;
            var wins = Wins;
            Label(lx, y, T("Okno:", "Window:"), 0.85f);
            var names = wins.Select((w, i) => (i + 1) + ". " + w.Name + (w.Enabled ? "" : T(" (wył.)", " (off)"))).ToList();
            if (names.Count == 0) names.Add(T("(brak okien)", "(no windows)"));
            Combo(lx + 0.08f, y, 0.34f, names, sel, v => { sel = v; selElem = -1; Redraw(); });
            y += 0.048f;
            Button(-0.372f, y, T("Nowe okno", "New window"), () =>
            {
                Wins.Add(WindowCfg.New(Wins.Count + 1));
                sel = Wins.Count - 1; selElem = -1; Redraw();
            }, 0.135f);
            Button(-0.23f, y, T("Duplikuj", "Duplicate"), () =>
            {
                var w0 = W; if (w0 == null) return;
                var c = w0.Clone(); c.Name += T(" (kopia)", " (copy)");
                Wins.Insert(sel + 1, c);
                sel++; selElem = -1; Redraw();
            }, 0.135f);
            Button(-0.088f, y, T("Usuń okno", "Delete window"), () =>
            {
                if (W == null) return;
                Wins.RemoveAt(sel);
                sel = Math.Max(0, sel - 1); selElem = -1; Redraw();
            }, 0.135f);

            var win = W;
            if (win == null)
            {
                Label(lx, y + 0.06f, T("Brak okien. Kliknij „Nowe okno” albo wczytaj domyślne.", "No windows. Click \"New window\" or load defaults."), 0.7f);
                Button(0f, 0.43f, T("Zapisz", "Save"), () => { cfg.Save(); Plugin.Dirty = true; CloseScreen(false); }, 0.22f, 0.05f);
                return;
            }

            // ---------------------------------------------------------------- ustawienia okna (lewa kolumna)
            const float row = 0.045f;
            y += 0.055f;
            Check(lx, y, T("Okno włączone", "Window enabled"), win.Enabled, v => win.Enabled = v); y += row;
            Label(lx, y, T("Nazwa w menu:", "Name in menu:"), 0.72f);
            TextBox(lx + 0.15f, y, 0.27f, win.Name, 40, v => win.Name = v); y += row;
            Check(lx, y, T("Pokaż tytuł:", "Show title:"), win.ShowTitle, v => win.ShowTitle = v);
            TextBox(lx + 0.15f, y, 0.27f, win.Title, 60, v => win.Title = v); y += row;
            Label(lx, y, T("Czcionka:", "Font:"), 0.72f);
            Combo(lx + 0.15f, y, 0.27f, WindowCfg.FontNames, win.Font, v => win.Font = v); y += row;
            Label(lx, y, T("Styl tekstu:", "Text style:"), 0.72f);
            Combo(lx + 0.15f, y, 0.27f, WindowCfg.StyleNames, win.Style, v => win.Style = v); y += row * 1.15f;

            int idx = sel;
            Button(-0.335f, y, T("Położenie, rozmiar, tło...", "Position, size, background..."), () => MyGuiSandbox.AddScreen(new LayoutScreen(cfg, win, idx, this)), 0.205f);
            if (sel < WindowCfg.DefaultCount)
                Button(-0.125f, y, T("Przywróć domyślne okno ", "Restore default window ") + (sel + 1), () =>
                {
                    Wins[sel] = WindowCfg.Default(sel);
                    selElem = -1; Redraw();
                }, 0.205f);
            y += row * 1.1f;

            Label(lx, y, T("Kolory:", "Colors:"), 0.72f); y += row * 0.9f;
            ColorButton(-0.39f, y, T("Tytuł/nagłówki", "Title/headers"), () => win.TitleColor, v => win.TitleColor = v);
            ColorButton(-0.285f, y, T("Tekst", "Text"), () => win.TextColor, v => win.TextColor = v);
            ColorButton(-0.18f, y, T("Liczby", "Numbers"), () => win.ValueColor, v => win.ValueColor = v);
            ColorButton(-0.075f, y, T("Paski", "Bars"), () => win.BarColor, v => win.BarColor = v); y += row;
            ColorButton(-0.39f, y, T("Ostrzeżenia", "Warnings"), () => win.WarnColor, v => win.WarnColor = v);
            ColorButton(-0.285f, y, T("Tło", "Background"), () => win.BgColor, v => win.BgColor = v);
            ColorButton(-0.18f, y, T("Obrys / cień", "Outline / shadow"), () => win.OutlineColor, v => win.OutlineColor = v); y += row * 1.2f;

            Check(lx, y, T("Ładownie na żywo (wirtualny terminal)", "Live cargo (virtual terminal)"), cfg.LiveInventory, v => cfg.LiveInventory = v); y += row * 0.75f;
            Label(lx + 0.035f, y, T("Serwer przysyła zawartość ładowni jak przy otwartym K.", "The server sends cargo contents as if K was open."), 0.64f); y += row * 0.5f;
            Label(lx + 0.035f, y, T("Wyłączone: dane odświeżą się dopiero po otwarciu terminala.", "Off: data refreshes only after opening the terminal."), 0.64f);

            // ---------------------------------------------------------------- elementy okna (prawa kolumna)
            const float rx = 0.02f;
            float ry = -0.225f;
            Label(rx, ry, T("Elementy okna (od góry do dołu):", "Window elements (top to bottom):"), 0.75f);
            var lb = new MyGuiControlListbox(new Vector2(rx, ry + 0.022f), MyGuiControlListboxStyleEnum.Default)
            {
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP,
                VisibleRowsCount = 10,
                MultiSelect = false,
            };
            lb.Size = new Vector2(0.43f, lb.Size.Y);
            for (int i = 0; i < win.Elements.Count; i++)
            {
                var item = new MyGuiControlListbox.Item(new StringBuilder((i + 1) + ". " + win.Elements[i].Describe()), win.Elements[i].Describe(), null, i);
                lb.Add(item);
                if (i == selElem) lb.SelectSingleItem(item);
            }
            lb.ItemsSelected += l => { if (l.SelectedItems.Count > 0) selElem = (int)l.SelectedItems[0].UserData; };
            lb.ItemDoubleClicked += l => { if (l.SelectedItems.Count > 0) { selElem = (int)l.SelectedItems[0].UserData; OpenElement(); } };
            Controls.Add(lb);

            float by = lb.Position.Y + lb.Size.Y + 0.032f;
            Button(0.075f, by, T("W górę", "Up"), () => Move(-1), 0.105f);
            Button(0.185f, by, T("W dół", "Down"), () => Move(1), 0.105f);
            Button(0.295f, by, T("Opcje...", "Options..."), OpenElement, 0.105f);
            Button(0.405f, by, T("Usuń", "Remove"), () =>
            {
                if (selElem < 0 || selElem >= win.Elements.Count) return;
                win.Elements.RemoveAt(selElem);
                selElem = Math.Min(selElem, win.Elements.Count - 1);
                Redraw();
            }, 0.105f);
            by += 0.055f;

            Label(rx, by, T("Dodaj:", "Add:"), 0.72f);
            var kinds = ElementCfg.AddOrder;
            addIdx = Math.Max(0, Math.Min(kinds.Length - 1, addIdx));
            Combo(rx + 0.065f, by, 0.25f, kinds.Select(ElementCfg.KindName).ToList(), addIdx, v => addIdx = v);
            Button(0.395f, by, T("Dodaj", "Add"), () =>
            {
                var e = ElementCfg.Make(kinds[addIdx]);
                int at = selElem >= 0 && selElem < win.Elements.Count ? selElem + 1 : win.Elements.Count;
                win.Elements.Insert(at, e);
                selElem = at;
                Redraw();
            }, 0.11f);
            by += 0.04f;
            Label(rx, by, T("Dwuklik na elemencie = jego opcje. Nowy element trafia pod zaznaczony.", "Double-click an element = its options. New elements go below the selected one."), 0.62f);
            by += 0.06f;
            if (editing != null)
                Button(0.235f, by, T("Logistyka: sortowanie, rafinerie, autocrafting...", "Logistics: sorting, refineries, autocrafting..."),
                    () => MyGuiSandbox.AddScreen(new MagazynScreen(editing, () => RecreateControls(false))), 0.43f, 0.048f);
            else
                Label(rx, by, T("Logistyka: wybierz u góry ustawienia grida.", "Logistics: pick a grid's settings at the top."), 0.64f);

            Button(0f, 0.43f, T("Zapisz", "Save"), () => { cfg.Save(); Plugin.Dirty = true; CloseScreen(false); }, 0.22f, 0.05f);
        }

        void ColorButton(float x, float y, string name, Func<string> get, Action<string> set)
        {
            Button(x, y, name, () => MyGuiSandbox.AddScreen(new ColorScreen(name, get, set)), 0.1f);
        }

        void Move(int d)
        {
            var w = W; if (w == null) return;
            var list = w.Elements;
            int j = selElem + d;
            if (selElem < 0 || selElem >= list.Count || j < 0 || j >= list.Count) return;
            var t = list[selElem]; list[selElem] = list[j]; list[j] = t;
            selElem = j;
            Redraw();
        }

        void OpenElement()
        {
            var w = W; if (w == null) return;
            var list = w.Elements;
            if (selElem < 0 || selElem >= list.Count) return;
            MyGuiSandbox.AddScreen(new ElementScreen(list[selElem], () => RecreateControls(false)));
        }

        protected override void OnClosed()
        {
            cfg.Save();     // zmiany działają od razu, więc zamknięcie X też zapisuje
            Plugin.Dirty = true;
            base.OnClosed();
        }
    }

    // ====================================================================== położenie i rozmiar (podgląd na żywo)

    internal class LayoutScreen : DialogBase
    {
        readonly Config cfg;
        readonly WindowCfg w;
        readonly int idx;
        readonly WindowCfg backup;
        readonly MainScreen owner;
        bool saved;
        static bool panelTop;            // panel suwaków u góry albo u dołu ekranu (zapamiętane)

        public LayoutScreen(Config cfg, WindowCfg w, int idx, MainScreen owner = null) : base(new Vector2(0.56f, 0.44f), 0.85f)
        {
            this.cfg = cfg;
            this.w = w;
            this.idx = idx;
            this.owner = owner;
            backup = w.Clone();
            EnabledBackgroundFade = false;   // podgląd ma być dobrze widoczny
            CanHideOthers = false;           // widać grę, HUD i pozostałe okna
            owner?.HideForLayout(true);
            RecreateControls(true);
        }

        public override string GetFriendlyName() => "DaneHudLayout";

        public override void RecreateControls(bool constructor)
        {
            m_position = new Vector2(0.5f, panelTop ? 0.25f : 0.75f);
            base.RecreateControls(constructor);
            AddCaption(w.Name + T(": położenie i rozmiar", ": position and size"));
            const float x = -0.25f, sx = -0.03f, sw = 0.27f, row = 0.045f;
            float y = -0.13f;
            Label(x, y, T("Poziomo (0 lewo, 100 prawo)", "Horizontal (0 left, 100 right)")); Slider(sx, y, sw, 0, 100, w.X, "{0} %", 0, v => w.X = (float)Math.Round(v)); y += row;
            Label(x, y, T("Pionowo (0 góra, 100 dół)", "Vertical (0 top, 100 bottom)")); Slider(sx, y, sw, 0, 100, w.Y, "{0} %", 0, v => w.Y = (float)Math.Round(v)); y += row;
            Label(x, y, T("Szerokość okna", "Window width")); Slider(sx, y, sw, 8, 70, w.Width * 100f, "{0}", 0, v => w.Width = (float)Math.Round(v) / 100f); y += row;
            Label(x, y, T("Wielkość czcionki", "Font size")); Slider(sx, y, sw, 30, 130, w.Scale * 100f, "{0}", 0, v => w.Scale = (float)Math.Round(v) / 100f); y += row;
            Label(x, y, T("Odstęp wierszy", "Line spacing")); Slider(sx, y, sw, 60, 160, w.LineSpacing * 100f, "{0} %", 0, v => w.LineSpacing = (float)Math.Round(v) / 100f); y += row;
            Label(x, y, T("Tło (0 = brak)", "Background (0 = none)")); Slider(sx, y, sw, 0, 255, w.BgAlpha, "{0}", 0, v => w.BgAlpha = (int)Math.Round(v)); y += row;
            Label(x, y + 0.005f, T("Okno, inne okna i HUD gry są widoczne. Zmiany działają od razu.", "The window, other windows and the game HUD are visible. Changes apply at once."), 0.6f);

            Button(-0.19f, 0.175f, panelTop ? T("Panel na dół", "Panel down") : T("Panel do góry", "Panel up"), () => { panelTop = !panelTop; RecreateControls(false); }, 0.16f);
            Button(0f, 0.175f, T("Domyślne", "Defaults"), () =>
            {
                var d = WindowCfg.Default(idx);
                w.X = d.X; w.Y = d.Y; w.Width = d.Width; w.Scale = d.Scale; w.LineSpacing = d.LineSpacing; w.BgAlpha = d.BgAlpha;
                RecreateControls(false);
            });
            Button(0.19f, 0.175f, T("Zapisz", "Save"), () => { saved = true; cfg.Save(); CloseScreen(false); }, 0.16f);
        }

        public override bool Draw()
        {
            if (!base.Draw()) return false;
            try
            {
                // pozostałe okna na swoich miejscach, żeby dopasować do nich edytowane
                var shown = Plugin.Shown;
                if (shown != null)
                    for (int i = 0; i < shown.Windows.Count && i < shown.Rows.Length; i++)
                    {
                        var o = shown.Windows[i];
                        if (ReferenceEquals(o, w) || i == idx || !o.Enabled) continue;
                        Render.Draw(o, shown.Rows[i]);
                    }
            }
            catch { }
            try { Render.Draw(w, Plugin.RowsOf(w) ?? RowBuilder.Sample(w)); }
            catch { }
            return true;
        }

        protected override void OnClosed()
        {
            owner?.HideForLayout(false);
            if (!saved)
            {
                w.X = backup.X; w.Y = backup.Y; w.Width = backup.Width; w.Scale = backup.Scale;
                w.LineSpacing = backup.LineSpacing; w.BgAlpha = backup.BgAlpha;
            }
            Plugin.Dirty = true;
            base.OnClosed();
        }
    }

    // ====================================================================== kolor

    internal class ColorScreen : DialogBase
    {
        readonly string name;
        readonly Func<string> get;
        readonly Action<string> set;
        readonly string start;
        Color c;
        bool saved;

        static (string, string)[] Presets => new[]
        {
            (T("Biały", "White"), "FFFFFF"), (T("Zielony", "Green"), "33FF33"), (T("Cyjan", "Cyan"), "2ECCFF"), (T("Pomarańcz", "Orange"), "FF5A1E"),
            (T("Żółty", "Yellow"), "FFD21E"), (T("Czerwony", "Red"), "FF4040"), (T("Szary", "Grey"), "A0A0A0"), (T("Czarny", "Black"), "000000"),
        };

        public ColorScreen(string name, Func<string> get, Action<string> set) : base(new Vector2(0.5f, 0.52f))
        {
            this.name = name; this.get = get; this.set = set;
            start = get();
            c = WindowCfg.ParseColor(start, Color.White);
            EnabledBackgroundFade = false;
            RecreateControls(true);
        }

        public override string GetFriendlyName() => "DaneHudColor";

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);
            AddCaption(T("Kolor: ", "Color: ") + name);
            const float x = -0.22f, sx = -0.15f, sw = 0.3f;
            float y = -0.15f;
            Label(x, y, "R"); Slider(sx, y, sw, 0, 255, c.R, "{0}", 0, v => { c.R = (byte)v; Apply(); }); y += 0.05f;
            Label(x, y, "G"); Slider(sx, y, sw, 0, 255, c.G, "{0}", 0, v => { c.G = (byte)v; Apply(); }); y += 0.05f;
            Label(x, y, "B"); Slider(sx, y, sw, 0, 255, c.B, "{0}", 0, v => { c.B = (byte)v; Apply(); }); y += 0.06f;
            var presets = Presets;
            for (int i = 0; i < presets.Length; i++)
            {
                var p = presets[i];
                Button(-0.165f + (i % 4) * 0.11f, y + (i / 4) * 0.05f, p.Item1, () =>
                {
                    c = WindowCfg.ParseColor(p.Item2, Color.White);
                    Apply();
                    RecreateControls(false);
                }, 0.1f);
            }
            Button(0f, 0.21f, T("Zapisz", "Save"), () => { saved = true; Plugin.Cfg?.Save(); CloseScreen(false); });
        }

        void Apply() { set(WindowCfg.ToHex(c)); Plugin.Dirty = true; }

        public override bool Draw()
        {
            if (!base.Draw()) return false;
            try
            {
                var pos = GetPosition() + new Vector2(0.17f, -0.1f);
                MyGuiManager.DrawSpriteBatch(@"Textures\GUI\Blank.dds", pos, new Vector2(0.06f, 0.08f), c,
                    MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER, false, true);
                MyGuiManager.DrawString("Debug", T("Przykład 123", "Example 123"), pos + new Vector2(0f, 0.07f), 0.7f, c,
                    MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER);
            }
            catch { }
            return true;
        }

        protected override void OnClosed()
        {
            if (!saved) { set(start); Plugin.Dirty = true; }
            base.OnClosed();
        }
    }

    // ====================================================================== opcje elementu

    internal class ElementScreen : DialogBase
    {
        readonly ElementCfg e;
        readonly Action onClose;
        const float x = -0.3f, vx = -0.02f, w = 0.32f, row = 0.048f;
        float y;

        public ElementScreen(ElementCfg e, Action onClose) : base(new Vector2(0.66f, 0.82f))
        {
            this.e = e;
            this.onClose = onClose;
            EnabledBackgroundFade = true;
            RecreateControls(true);
        }

        public override string GetFriendlyName() => "DaneHudElement";

        void Hint(string s) { Label(x, y, s, 0.64f); y += 0.028f; }
        void Gap() => y += row * 0.4f;

        void FilterBox(string label = null)
        {
            Label(x, y, label ?? T("Bloki:", "Blocks:")); TextBox(vx, y, w, e.Filter, 80, v => e.Filter = v); y += row;
            Hint(T("Tekst w nazwie bloku, G:grupa, Typ:rodzaj (np. Typ:Refinery), * = wszystkie.", "Text in block name, G:group, Type:kind (e.g. Type:Refinery), * = all."));
        }

        void LabelBox(string hint = null)
        {
            Label(x, y, T("Podpis:", "Label:")); TextBox(vx, y, w, e.Label, 60, v => e.Label = v); y += row;
            if (hint != null) Hint(hint);
        }

        void LinesBox(string label = null)
        {
            Label(x, y, label ?? T("Ile pozycji (0 = wszystkie):", "How many (0 = all):")); Number(vx, y, 0.1f, e.MaxLines, v => e.MaxLines = Math.Max(0, (int)v)); y += row;
        }

        void C(string text, bool value, Action<bool> set) { Check(x, y, text, value, set); y += row; }

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);
            AddCaption(ElementCfg.KindName(e.Kind));
            y = -0.32f;
            switch (e.Kind)
            {
                case ElementKind.Header:
                    Label(x, y, T("Tekst:", "Text:")); TextBox(vx, y, w, e.Text, 100, v => e.Text = v); y += row;
                    C(T("Wyśrodkuj (Center)", "Center"), e.Center, v => e.Center = v);
                    C(T("Do prawej (Right)", "Align right (Right)"), e.Right, v => e.Right = v);
                    break;

                case ElementKind.Items:
                {
                    var cats = Enum.GetValues(typeof(ItemCat)).Cast<ItemCat>().ToList();
                    Label(x, y, T("Kategoria:", "Category:")); Combo(vx, y, w, cats.Select(ElementCfg.CatName).ToList(), cats.IndexOf(e.Category), v => { e.Category = cats[v]; }); y += row;
                    FilterBox(T("Tylko z bloków:", "Only from blocks:"));
                    Hint(T("Puste = cały grid. Np. G:Magazyn albo nazwa jednego kontenera (InvList).", "Empty = whole grid. E.g. G:Storage or one container name (InvList)."));
                    C(T("Sortuj wg nazwy (inaczej wg ilości)", "Sort by name (otherwise by amount)"), e.SortByName, v => e.SortByName = v);
                    C(T("Licz tylko kontenery (bez rafinerii, montażowni...)", "Count containers only (no refineries, assemblers...)"), e.OnlyCargo, v => e.OnlyCargo = v);
                    Label(x, y, T("Minimum (0 = brak):", "Minimum (0 = none):")); Number(vx, y, 0.12f, e.Quota, v => e.Quota = Math.Max(0, v)); y += row;
                    Hint(T("Poniżej minimum = kolor ostrzeżeń (jak Missing w LCDs 2).", "Below minimum = warning color (like Missing in LCDs 2)."));
                    C(T("Tylko poniżej minimum (Missing)", "Only below minimum (Missing)"), e.OnlyMissing, v => e.OnlyMissing = v);
                    C(T("Paski względem minimum", "Bars against minimum"), e.ShowBars, v => e.ShowBars = v);
                    LinesBox();
                    Button(0f, y + 0.01f, T($"Co pokazać... (ukryte: {e.Hidden.Count})", $"What to show... ({e.Hidden.Count} hidden)"),
                        () => MyGuiSandbox.AddScreen(new ItemPickScreen(e, () => RecreateControls(false))), 0.32f);
                    break;
                }

                case ElementKind.CargoBar:
                    LabelBox();
                    Label(x, y, T("Kontenery:", "Containers:")); TextBox(vx, y, w, e.Text, 60, v => e.Text = v); y += row;
                    Hint(T("Nazwa grupy albo tekst w nazwie kontenera (np. Ores).", "Group name or text in container name (e.g. Ores)."));
                    Hint(T("Puste = wszystkie kontenery grida.", "Empty = all containers of the grid."));
                    break;

                case ElementKind.Tank:
                    Label(x, y, T("Gaz:", "Gas:")); Combo(vx, y, w, new[] { T("Wodór", "Hydrogen"), T("Tlen", "Oxygen") }, e.Gas == "Oxygen" ? 1 : 0, v => e.Gas = v == 1 ? "Oxygen" : "Hydrogen"); y += row;
                    LabelBox();
                    break;

                case ElementKind.Thrust:
                    C(T("Tylko przód i góra (krócej)", "Forward and up only (shorter)"), e.Compact, v => e.Compact = v);
                    Hint(T("Góra pokazuje też udźwig w grawitacji (x = ile razy ciąg > ciężar).", "Up also shows lift in gravity (x = thrust / weight)."));
                    break;

                case ElementKind.Autocrafting:
                {
                    Label(x, y, T("Słowo w nazwie LCD:", "Word in LCD name:")); TextBox(vx, y, w, e.Text, 40, v => e.Text = v); y += row;
                    var parts = new List<string> { T("Całość", "All"), T("Część 1 z 2", "Part 1 of 2"), T("Część 2 z 2", "Part 2 of 2"), T("Część 1 z 3", "Part 1 of 3"), T("Część 2 z 3", "Part 2 of 3"), T("Część 3 z 3", "Part 3 of 3") };
                    int cur = e.Parts <= 1 ? 0 : e.Parts == 2 ? 1 + e.Part : 3 + e.Part;
                    Label(x, y, T("Która część listy:", "Which part of the list:")); Combo(vx, y, w, parts, cur, v =>
                    {
                        if (v == 0) { e.Parts = 1; e.Part = 0; }
                        else if (v <= 2) { e.Parts = 2; e.Part = v - 1; }
                        else { e.Parts = 3; e.Part = v - 3; }
                    }); y += row;
                    C(T("Tylko braki (obecnie < chciane)", "Only missing (current < wanted)"), e.OnlyMissing, v => e.OnlyMissing = v);
                    C(T("Nazwy z gry zamiast nazw skryptu", "Game names instead of script names"), e.GameNames, v => e.GameNames = v);
                    C(T("Limity z Logistyki (pluginu), nie z LCD Isy", "Limits from Logistics (plugin), not Isy's LCD"), e.FromPlugin, v => e.FromPlugin = v);
                    Hint(T("Dane czytane z LCD skryptu Isy (\"Autocrafting\", \"Autocrafting 2\"...).", "Read from Isy's script LCDs (\"Autocrafting\", \"Autocrafting 2\"...)."));
                    Hint(T("Długa lista: dwa okna, część 1 z 2 i część 2 z 2.", "Long list: two windows, part 1 of 2 and part 2 of 2."));
                    break;
                }

                case ElementKind.LcdText:
                    Label(x, y, T("Słowo w nazwie LCD:", "Word in LCD name:")); TextBox(vx, y, w, e.Text, 60, v => e.Text = v); y += row;
                    C(T("Liczby po ':' do prawej", "Numbers after ':' aligned right"), e.AlignNumbers, v => e.AlignNumbers = v);
                    break;

                case ElementKind.Details:
                    FilterBox(T("Blok:", "Block:"));
                    C(T("Bez nazwy bloku (NoN)", "Without block name (NoN)"), e.NoName, v => e.NoName = v);
                    C(T("Pomiń pierwszą linię (X)", "Skip first line (X)"), e.SkipFirst, v => e.SkipFirst = v);
                    Label(x, y, T("Zacznij od linii z:", "Start at line with:")); TextBox(vx, y, w, e.StartText, 60, v => e.StartText = v); y += row;
                    Label(x, y, T("Skończ przed linią z:", "Stop before line with:")); TextBox(vx, y, w, e.StopText, 60, v => e.StopText = v); y += row;
                    LinesBox(T("Ile linii (0 = wszystkie):", "How many lines (0 = all):"));
                    C(T("Liczby po ':' do prawej", "Numbers after ':' aligned right"), e.AlignNumbers, v => e.AlignNumbers = v);
                    C(T("Odświeżaj informacje bloku (mody, np. tarcze)", "Refresh block info (mods, e.g. shields)"), e.RefreshInfo, v => e.RefreshInfo = v);
                    Gap();
                    Hint(T("Przykład z LCDs 2: DetailsNoNX {[A] Shield Controller} {CapHp:} 2", "LCDs 2 example: DetailsNoNX {[A] Shield Controller} {CapHp:} 2"));
                    Hint(T("= blok \"[A] Shield Controller\", bez nazwy, pomiń 1. linię, od \"CapHp:\", 2 linie.", "= block \"[A] Shield Controller\", no name, skip line 1, from \"CapHp:\", 2 lines."));
                    Hint(T("Tekst jak w terminalu (K) w prawym dolnym rogu bloku.", "Same text as the block info in the terminal (K)."));
                    break;

                case ElementKind.CustomData:
                    FilterBox(T("Blok:", "Block:"));
                    C(T("Bez nazwy bloku", "Without block name"), e.NoName, v => e.NoName = v);
                    LinesBox(T("Ile linii (0 = wszystkie):", "How many lines (0 = all):"));
                    C(T("Liczby po ':' do prawej", "Numbers after ':' aligned right"), e.AlignNumbers, v => e.AlignNumbers = v);
                    break;

                case ElementKind.Working:
                    FilterBox();
                    C(T("Tylko problemy (wyłączone, uszkodzone, bezczynne)", "Only problems (off, damaged, idle)"), e.OnlyProblems, v => e.OnlyProblems = v);
                    LabelBox(T("Podpis linii „wszystkie działają” (gdy tylko problemy).", "Label of the \"all working\" line (only-problems mode)."));
                    LinesBox();
                    Hint(T("Stan: rafinerie/montażownie pracuje/bezczynna, konektory, drzwi, tłoki (m),", "State: production working/idle, connectors, doors, pistons (m),"));
                    Hint(T("rotory (°), baterie i napędy skokowe (%), zbiorniki, wentylacje, wieżyczki.", "rotors (°), batteries and jump drives (%), tanks, vents, turrets."));
                    break;

                case ElementKind.Count:
                    FilterBox();
                    LinesBox();
                    Hint(T("Liczba bloków każdego rodzaju; „3 / 5” = 3 działają z 5.", "Count of each block kind; \"3 / 5\" = 3 of 5 working."));
                    break;

                case ElementKind.Damage:
                    FilterBox();
                    Label(x, y, T("Pokaż poniżej (%):", "Show below (%):")); Number(vx, y, 0.1f, e.Threshold, v => e.Threshold = (float)Math.Max(1, Math.Min(100, v))); y += row;
                    LinesBox();
                    C(T("Paski", "Bars"), e.ShowBars, v => e.ShowBars = v);
                    Hint(T("Bloki funkcyjne (bez pancerza), najbardziej uszkodzone na górze.", "Functional blocks (no armor), most damaged first."));
                    break;

                case ElementKind.Ship:
                    C(T("Prędkość", "Speed"), e.ShowSpeed, v => e.ShowSpeed = v);
                    C(T("   w km/h", "   in km/h"), e.Kmh, v => e.Kmh = v);
                    C(T("Przyspieszenie", "Acceleration"), e.ShowAccel, v => e.ShowAccel = v);
                    C(T("Wysokość nad gruntem i morzem", "Altitude above surface and sea level"), e.ShowAlt, v => e.ShowAlt = v);
                    C(T("Grawitacja (naturalna i sztuczna)", "Gravity (natural and artificial)"), e.ShowGrav, v => e.ShowGrav = v);
                    C(T("Masa i ładunek", "Mass and cargo"), e.ShowMass, v => e.ShowMass = v);
                    C(T("Droga i czas hamowania", "Stopping distance and time"), e.ShowStop, v => e.ShowStop = v);
                    C(T("Tłumiki i hamulec ręczny", "Dampeners and handbrake"), e.ShowDamp, v => e.ShowDamp = v);
                    C(T("Pozycja (X Y Z)", "Position (X Y Z)"), e.ShowPos, v => e.ShowPos = v);
                    Hint(T("Hamowanie liczone z ciągu silników przeciwnie do lotu (bez grawitacji).", "Stopping uses thrust against the flight direction (no gravity)."));
                    break;

                case ElementKind.Charge:
                    FilterBox();
                    LabelBox();
                    C(T("Każdy blok osobno", "Each block separately"), e.Each, v => e.Each = v);
                    C(T("Paski", "Bars"), e.ShowBars, v => e.ShowBars = v);
                    LinesBox();
                    Hint(T("Napędy skokowe i baterie. Np. Typ:Jump albo Typ:Battery.", "Jump drives and batteries. E.g. Type:Jump or Type:Battery."));
                    break;

                case ElementKind.Oxygen:
                    FilterBox();
                    C(T("Każda wentylacja osobno", "Each air vent separately"), e.Each, v => e.Each = v);
                    C(T("Paski", "Bars"), e.ShowBars, v => e.ShowBars = v);
                    LinesBox();
                    Hint(T("Ciśnienie w wentylacjach, farmy tlenu, zbiorniki tlenu.", "Air vent pressure, oxygen farms, oxygen tanks."));
                    break;

                case ElementKind.Amount:
                    FilterBox();
                    Label(x, y, T("Tylko przedmioty z:", "Only items with:")); TextBox(vx, y, w, e.Text, 60, v => e.Text = v); y += row;
                    Hint(T("Tekst w nazwie przedmiotu, np. Ammo, Uranium, Ice. Puste = wszystko.", "Text in item name, e.g. Ammo, Uranium, Ice. Empty = all."));
                    C(T("Każdy blok osobno", "Each block separately"), e.Each, v => e.Each = v);
                    LinesBox();
                    break;

                case ElementKind.PowerUsed:
                    FilterBox();
                    LabelBox();
                    C(T("Każdy blok osobno", "Each block separately"), e.Each, v => e.Each = v);
                    C(T("Pasek (bieżący / maks. pobór)", "Bar (current / max use)"), e.ShowBars, v => e.ShowBars = v);
                    LinesBox();
                    break;

                case ElementKind.Clock:
                    Label(x, y, T("Format:", "Format:")); TextBox(vx, y, w, e.Text, 40, v => e.Text = v); y += row;
                    Hint(T("HH:mm  •  HH:mm:ss  •  dd.MM.yyyy  •  dddd HH:mm", "HH:mm  •  HH:mm:ss  •  dd.MM.yyyy  •  dddd HH:mm"));
                    LabelBox(T("Z podpisem: „Godzina:  14:05”. Bez: sama godzina.", "With label: \"Time:  14:05\". Without: time only."));
                    C(T("Wyśrodkuj", "Center"), e.Center, v => e.Center = v);
                    C(T("Do prawej", "Align right"), e.Right, v => e.Right = v);
                    break;

                case ElementKind.Distance:
                    Label(x, y, "GPS:"); TextBox(vx, y, w, e.Text, 120, v => e.Text = v); y += row;
                    Hint(T("Wklej punkt GPS z gry (GPS:Nazwa:X:Y:Z:...).", "Paste a GPS from the game (GPS:Name:X:Y:Z:...)."));
                    LabelBox();
                    Hint(T("Pokazuje odległość i czas dolotu przy obecnej prędkości.", "Shows distance and arrival time at current speed."));
                    break;

                case ElementKind.Property:
                    FilterBox(T("Blok:", "Block:"));
                    Label(x, y, T("Opcja:", "Property:")); TextBox(vx, y, w, e.Property, 60, v => e.Property = v); y += row;
                    Hint(T("Nazwy opcji: OnOff, Override, Velocity, UpperLimit, Radius, ...", "Property ids: OnOff, Override, Velocity, UpperLimit, Radius, ..."));
                    LabelBox();
                    LinesBox();
                    break;

                case ElementKind.Magazyn:
                    LinesBox(T("Ile ostatnich ruchów:", "How many recent moves:"));
                    Hint(T("Stan modułu Logistyka (/data » Logistyka) i co ostatnio przeniósł.", "Logistics module state (/data » Logistics) and its recent moves."));
                    break;

                case ElementKind.RefineryCalc:
                {
                    C(T("Ruda, która jest w bazie (ze wszystkich bloków) + suma czasu", "Ore that is in the base (all blocks) + total time"), e.FromStock, v => { e.FromStock = v; RecreateControls(false); });
                    if (!e.FromStock)
                    {
                        var amounts = new double[] { 1, 10, 100, 1000, 10000, 100000, 1000000 };
                        var names = amounts.Select(v => Fmt.Mass(v)).ToList();
                        int cur = Array.IndexOf(amounts, e.Quota);
                        Label(x, y, T("Ilość rudy:", "Ore amount:")); Combo(vx, y, 0.14f, names, cur < 0 ? 3 : cur, v => { e.Quota = amounts[v]; RecreateControls(false); });
                        Number(vx + 0.16f, y, 0.12f, e.Quota, v => e.Quota = Math.Max(0, v)); y += row;
                    }
                    LinesBox(T("Ile rud (0 = wszystkie):", "How many ores (0 = all):"));

                    Label(x, y, T("Pokazywane rafinerie:", "Refineries shown:")); y += row * 0.9f;
                    var kinds = Ctx.RefineryKinds();
                    for (int k = 0; k < kinds.Count; k++)
                    {
                        string id = kinds[k].Key;
                        Check(x + (k % 2) * 0.3f, y, kinds[k].Value, !e.HiddenKinds.Contains(id), v => { e.HiddenKinds.Remove(id); if (!v) e.HiddenKinds.Add(id); });
                        if (k % 2 == 1 || k == kinds.Count - 1) y += row * 0.85f;
                    }
                    if (kinds.Count == 0) { Hint(T("Brak rafinerii.", "No refineries.")); }

                    Label(x, y, T("Pokazywane rudy:", "Ores shown:")); y += row * 0.9f;
                    var ores = Ctx.AllRefineryOres();
                    for (int k = 0; k < ores.Count; k++)
                    {
                        string ore = ores[k];
                        Check(x + (k % 4) * 0.15f, y, Fmt.Symbol(ore) + "  " + ore, !e.Hidden.Contains(ore), v => { e.Hidden.Remove(ore); if (!v) e.Hidden.Add(ore); });
                        if (k % 4 == 3 || k == ores.Count - 1) y += row * 0.85f;
                    }
                    Button(x + 0.09f, y + 0.005f, T("Wszystkie", "All"), () => { e.Hidden.Clear(); RecreateControls(false); }, 0.16f, 0.036f);
                    Button(x + 0.27f, y + 0.005f, T("Żadna", "None"), () => { e.Hidden.Clear(); e.Hidden.AddRange(ores); RecreateControls(false); }, 0.16f, 0.036f);
                    y += row;
                    Hint(T("Kolumny: sztabki i czas dla 1 rafinerii i dla wszystkich danego rodzaju razem.", "Columns: ingots and time for 1 refinery and for all of that kind together."));
                    break;
                }

                case ElementKind.Queue:
                    FilterBox(T("Montażownie:", "Assemblers:"));
                    LinesBox();
                    Hint(T("Suma kolejek wybranych montażowni.", "Sum of the queues of the selected assemblers."));
                    break;

                default:
                    Label(x, y, T("Ten element nie ma dodatkowych opcji.", "This element has no extra options."), 0.7f); y += row;
                    break;
            }
            Button(0f, 0.36f, "OK", () => CloseScreen(false), 0.18f, 0.048f);
        }

        protected override void OnClosed()
        {
            Plugin.Dirty = true;
            try { onClose?.Invoke(); } catch { }
            base.OnClosed();
        }
    }

    // ====================================================================== wybór przedmiotów

    internal class ItemPickScreen : DialogBase
    {
        readonly ElementCfg e;
        readonly Action onClose;

        public ItemPickScreen(ElementCfg e, Action onClose) : base(new Vector2(0.72f, 0.9f))
        {
            this.e = e;
            this.onClose = onClose;
            EnabledBackgroundFade = true;
            RecreateControls(true);
        }

        public override string GetFriendlyName() => "DaneHudItems";

        List<MyPhysicalItemDefinition> Defs()
        {
            var list = new List<MyPhysicalItemDefinition>();
            try
            {
                foreach (var d in MyDefinitionManager.Static.GetPhysicalItemDefinitions())
                {
                    if (d == null || !d.Public) continue;
                    var cat = Ctx.CatOf(d.Id.TypeId.ToString());
                    if (e.Category != ItemCat.All && cat != e.Category) continue;
                    list.Add(d);
                }
            }
            catch { }
            return list.OrderBy(d => d.DisplayNameText).ToList();
        }

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);
            AddCaption(T("Co pokazać: ", "What to show: ") + ElementCfg.CatName(e.Category));
            var defs = Defs();
            Label(-0.33f, -0.38f, T("Zaznaczone są pokazywane (gdy jest ich więcej niż 0).", "Checked items are shown (when more than 0)."), 0.65f);
            Button(0.15f, -0.38f, T("Wszystkie", "All"), () => { e.Hidden.Clear(); Plugin.Dirty = true; RecreateControls(false); }, 0.1f);
            Button(0.26f, -0.38f, T("Żadne", "None"), () => { e.Hidden = defs.Select(d => d.Id.ToString()).ToList(); Plugin.Dirty = true; RecreateControls(false); }, 0.1f);

            const float width = 0.66f, itemRow = 0.036f;
            const int ncol = 3;
            float colW = (width - 0.03f) / ncol;
            int rows = (defs.Count + ncol - 1) / ncol;
            float visibleH = 0.7f;
            float contentH = Math.Max(visibleH, rows * itemRow + 0.01f);
            var parent = new MyGuiControlParent { Size = new Vector2(width - 0.02f, contentH) };
            for (int i = 0; i < defs.Count; i++)
            {
                var key = defs[i].Id.ToString();
                float x = -(width - 0.02f) / 2f + 0.005f + (i % ncol) * colW;
                float yy = -contentH / 2f + itemRow / 2f + 0.005f + (i / ncol) * itemRow;
                var cb = new MyGuiControlCheckbox(new Vector2(x, yy), null, null, !e.Hidden.Contains(key),
                    MyGuiControlCheckboxStyleEnum.Default, MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER);
                cb.IsCheckedChanged += c =>
                {
                    e.Hidden.RemoveAll(h => h == key);
                    if (!c.IsChecked) e.Hidden.Add(key);
                    Plugin.Dirty = true;
                };
                parent.Controls.Add(cb);
                parent.Controls.Add(new MyGuiControlLabel
                {
                    Position = new Vector2(x + 0.033f, yy),
                    Text = defs[i].DisplayNameText ?? defs[i].Id.SubtypeName,
                    TextScale = 0.62f,
                    OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER,
                });
            }
            var panel = new MyGuiControlScrollablePanel(parent)
            {
                Position = new Vector2(-0.33f, -0.35f),
                Size = new Vector2(width, visibleH),
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP,
                ScrollbarVEnabled = true,
                BackgroundTexture = MyGuiConstants.TEXTURE_SCROLLABLE_LIST,
                ScrolledAreaPadding = new MyGuiBorderThickness(0.005f),
            };
            Controls.Add(panel);
            Button(0f, 0.405f, "OK", () => CloseScreen(false), 0.18f, 0.048f);
        }

        protected override void OnClosed()
        {
            Plugin.Dirty = true;
            try { onClose?.Invoke(); } catch { }
            base.OnClosed();
        }
    }

    // ====================================================================== nazwane szablony okien

    internal class TemplatesScreen : DialogBase
    {
        readonly Config cfg;
        readonly Func<List<WindowCfg>> current;   // okna, które teraz edytujesz w menu
        readonly Action onChange;
        string info = "";

        public TemplatesScreen(Config cfg, Func<List<WindowCfg>> current, Action onChange) : base(new Vector2(0.8f, 0.8f))
        {
            this.cfg = cfg;
            this.current = current;
            this.onChange = onChange;
            EnabledBackgroundFade = true;
            RecreateControls(true);
        }

        public override string GetFriendlyName() => "DaneHudTemplates";

        static List<WindowCfg> Copy(List<WindowCfg> l) => l.Select(w => w.Clone()).ToList();

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);
            AddCaption(T("Szablony okien", "Window templates"));
            Label(-0.37f, -0.32f, string.IsNullOrEmpty(info)
                ? T("Nazwę zmieniasz w polu tekstowym. „Wczytaj” zastępuje okna, które teraz edytujesz.", "Edit the name in the text box. \"Load\" replaces the windows you are editing now.")
                : info, 0.62f);
            Label(-0.37f, -0.27f, T("Nazwa", "Name"), 0.64f);
            Label(-0.15f, -0.27f, T("Okna", "Windows"), 0.64f);

            float y = -0.22f;
            const float row = 0.05f;
            var list = cfg.Templates;
            if (list.Count == 0) Label(-0.37f, y, T("Brak szablonów. Zapisz pierwszy przyciskiem na dole.", "No templates yet. Save the first one with the button below."), 0.66f);
            for (int i = 0; i < list.Count && i < 9; i++)
            {
                var t = list[i];
                TextBox(-0.37f, y, 0.2f, t.Name, 40, s => t.Name = s ?? "");
                Label(-0.15f, y, t.Windows.Count.ToString(), 0.66f);
                Button(-0.05f, y, T("Wczytaj", "Load"), () =>
                {
                    var target = current();
                    target.Clear();
                    target.AddRange(Copy(t.Windows));
                    info = T("Wczytano: ", "Loaded: ") + t.Name;
                    onChange?.Invoke();
                    RecreateControls(false);
                }, 0.1f, 0.036f);
                Button(0.075f, y, T("Nadpisz", "Overwrite"), () =>
                {
                    t.Windows = Copy(current());
                    info = T("Zapisano obecne okna do: ", "Current windows saved to: ") + t.Name;
                    Plugin.Dirty = true;
                    RecreateControls(false);
                }, 0.11f, 0.036f);
                Button(0.2f, y, T("Domyślny", "Default"), () =>
                {
                    cfg.Windows = Copy(t.Windows);
                    info = T("Nowe gridy dostaną okna z: ", "New grids will get the windows from: ") + t.Name;
                    Plugin.Dirty = true;
                    onChange?.Invoke();
                    RecreateControls(false);
                }, 0.11f, 0.036f);
                Button(0.315f, y, T("Usuń", "Delete"), () =>
                {
                    cfg.Templates.Remove(t);
                    info = T("Usunięto: ", "Deleted: ") + t.Name;
                    Plugin.Dirty = true;
                    RecreateControls(false);
                }, 0.09f, 0.036f);
                y += row;
            }
            if (list.Count >= 9) Label(-0.37f, y, T("(maks. 9 szablonów)", "(max. 9 templates)"), 0.62f);

            if (list.Count < 9)
                Button(-0.12f, 0.29f, T("Nowy szablon z obecnych okien", "New template from current windows"), () =>
                {
                    int n = cfg.Templates.Count + 1;
                    string name;
                    do name = T("Szablon ", "Template ") + n++; while (cfg.Templates.Any(x => x.Name == name));
                    cfg.Templates.Add(new WindowTemplate { Name = name, Windows = Copy(current()) });
                    info = T("Zapisano: ", "Saved: ") + name + T(" (nazwę możesz zmienić)", " (you can rename it)");
                    Plugin.Dirty = true;
                    RecreateControls(false);
                }, 0.34f, 0.045f);
            Button(0.22f, 0.29f, "OK", () => CloseScreen(false), 0.16f, 0.045f);
        }

        protected override void OnClosed()
        {
            Plugin.Cfg?.Save();
            Plugin.Dirty = true;
            try { onChange?.Invoke(); } catch { }
            base.OnClosed();
        }
    }
}
