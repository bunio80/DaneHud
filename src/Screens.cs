using System;
using System.Collections.Generic;
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

        protected MyGuiControlButton Button(float x, float y, string text, Action click, bool small = false)
        {
            var b = new MyGuiControlButton
            {
                Position = new Vector2(x, y),
                Text = text,
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER,
            };
            if (small) b.VisualStyle = MyGuiControlButtonStyleEnum.Small;
            b.ButtonClicked += _ => click();
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
            c.SelectItemByKey(Math.Max(0, Math.Min(items.Count - 1, selected)));
            c.ItemSelected += () => { set((int)c.GetSelectedKey()); Plugin.Dirty = true; };
            Controls.Add(c);
            return c;
        }
    }

    // ====================================================================== główne okno /dane

    internal class MainScreen : DialogBase
    {
        readonly Config cfg;
        static int sel;          // wybrane okno (zapamiętane między otwarciami)
        int selElem = -1;
        int addKind = (int)ElementKind.Items;

        public MainScreen(Config cfg) : base(new Vector2(0.95f, 0.97f), 0.96f)
        {
            this.cfg = cfg;
            EnabledBackgroundFade = true;
            RecreateControls(true);
        }

        public override string GetFriendlyName() => "DaneHudMain";

        WindowCfg W => cfg.Windows[Math.Max(0, Math.Min(cfg.Windows.Count - 1, sel))];

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);
            AddCaption("Dane na HUD  (DaneHud v" + Plugin.Version + ")");

            // --- wybór okna: 2 rzędy po 3
            for (int i = 0; i < Config.WindowCount; i++)
            {
                int idx = i;
                var w = cfg.Windows[i];
                string t = (i == sel ? "» " : "") + (i + 1) + ". " + w.Name + (w.Enabled ? "" : " (wył.)");
                Button(-0.3f + (i % 3) * 0.3f, -0.385f + (i / 3) * 0.05f, t, () => { sel = idx; selElem = -1; RecreateControls(false); });
            }

            var win = W;
            const float lx = -0.44f, row = 0.047f;
            float y = -0.28f;
            Label(lx, y, $"Okno {sel + 1}", 0.9f); y += row;
            Check(lx, y, "Okno włączone", win.Enabled, v => win.Enabled = v); y += row;
            Label(lx, y, "Nazwa w menu:", 0.72f);
            TextBox(lx + 0.15f, y, 0.27f, win.Name, 40, v => win.Name = v); y += row;
            Check(lx, y, "Pokaż tytuł:", win.ShowTitle, v => win.ShowTitle = v);
            TextBox(lx + 0.15f, y, 0.27f, win.Title, 60, v => win.Title = v); y += row;
            Label(lx, y, "Czcionka:", 0.72f);
            Combo(lx + 0.15f, y, 0.27f, WindowCfg.FontNames, win.Font, v => win.Font = v); y += row * 1.15f;

            Button(lx + 0.21f, y, "Położenie, rozmiar, tło...", () => MyGuiSandbox.AddScreen(new LayoutScreen(cfg, sel))); y += row * 1.2f;

            Label(lx, y, "Kolory:", 0.72f); y += row;
            ColorButton(lx + 0.105f, y, "Tytuł/nagłówki", () => win.TitleColor, v => win.TitleColor = v);
            ColorButton(lx + 0.315f, y, "Tekst", () => win.TextColor, v => win.TextColor = v); y += row * 1.1f;
            ColorButton(lx + 0.105f, y, "Liczby", () => win.ValueColor, v => win.ValueColor = v);
            ColorButton(lx + 0.315f, y, "Paski", () => win.BarColor, v => win.BarColor = v); y += row * 1.1f;
            ColorButton(lx + 0.105f, y, "Ostrzeżenia", () => win.WarnColor, v => win.WarnColor = v);
            ColorButton(lx + 0.315f, y, "Tło", () => win.BgColor, v => win.BgColor = v); y += row * 1.3f;

            Button(lx + 0.21f, y, "Przywróć domyślne okno " + (sel + 1), () =>
            {
                cfg.Windows[sel] = WindowCfg.Default(sel);
                selElem = -1; Plugin.Dirty = true; RecreateControls(false);
            }); y += row * 1.3f;

            Check(lx, y, "Ładownie na żywo (wirtualny terminal)", cfg.LiveInventory, v => cfg.LiveInventory = v); y += row * 0.75f;
            Label(lx + 0.035f, y, "Serwer przysyła zawartość ładowni jak przy otwartym K.", 0.6f); y += row * 0.55f;
            Label(lx + 0.035f, y, "Wyłączone: dane odświeżą się dopiero po otwarciu terminala.", 0.6f);

            // --- elementy okna
            const float rx = 0.02f;
            Label(rx, -0.28f, "Elementy okna (od góry do dołu):", 0.75f);
            var lb = new MyGuiControlListbox(new Vector2(rx, -0.255f), MyGuiControlListboxStyleEnum.Default)
            {
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP,
                VisibleRowsCount = 12,
                MultiSelect = false,
            };
            lb.Size = new Vector2(0.42f, lb.Size.Y);
            for (int i = 0; i < win.Elements.Count; i++)
            {
                var item = new MyGuiControlListbox.Item(new StringBuilder((i + 1) + ". " + win.Elements[i].Describe()), null, null, i);
                lb.Add(item);
                if (i == selElem) lb.SelectSingleItem(item);
            }
            lb.ItemsSelected += l => { if (l.SelectedItems.Count > 0) selElem = (int)l.SelectedItems[0].UserData; };
            lb.ItemDoubleClicked += l => { if (l.SelectedItems.Count > 0) { selElem = (int)l.SelectedItems[0].UserData; OpenElement(); } };
            Controls.Add(lb);

            float by = lb.Position.Y + lb.Size.Y + 0.035f;
            Button(rx + 0.105f, by, "W górę", () => Move(-1));
            Button(rx + 0.315f, by, "W dół", () => Move(1)); by += 0.052f;
            Button(rx + 0.105f, by, "Opcje...", OpenElement);
            Button(rx + 0.315f, by, "Usuń", () =>
            {
                if (selElem < 0 || selElem >= win.Elements.Count) return;
                win.Elements.RemoveAt(selElem);
                selElem = Math.Min(selElem, win.Elements.Count - 1);
                Plugin.Dirty = true; RecreateControls(false);
            }); by += 0.06f;

            Label(rx, by, "Dodaj:", 0.72f);
            var kinds = Enum.GetValues(typeof(ElementKind)).Cast<ElementKind>().ToList();
            Combo(rx + 0.07f, by, 0.25f, kinds.Select(ElementCfg.KindName).ToList(), kinds.IndexOf((ElementKind)addKind), v => addKind = (int)kinds[v]);
            Button(rx + 0.375f, by, "Dodaj", () =>
            {
                var e = ElementCfg.Make((ElementKind)addKind, addKind == (int)ElementKind.Header ? "--==Tekst==--" : "");
                if (e.Kind == ElementKind.LcdText) e.Text = "LCD";
                int at = selElem >= 0 && selElem < win.Elements.Count ? selElem + 1 : win.Elements.Count;
                win.Elements.Insert(at, e);
                selElem = at;
                Plugin.Dirty = true; RecreateControls(false);
            }, true);

            Button(0f, 0.43f, "Zapisz", () => { cfg.Save(); Plugin.Dirty = true; CloseScreen(false); });
        }

        void ColorButton(float x, float y, string name, Func<string> get, Action<string> set)
        {
            Button(x, y, name, () => MyGuiSandbox.AddScreen(new ColorScreen(name, get, set)));
        }

        void Move(int d)
        {
            var list = W.Elements;
            int j = selElem + d;
            if (selElem < 0 || selElem >= list.Count || j < 0 || j >= list.Count) return;
            var t = list[selElem]; list[selElem] = list[j]; list[j] = t;
            selElem = j;
            Plugin.Dirty = true; RecreateControls(false);
        }

        void OpenElement()
        {
            var list = W.Elements;
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
        readonly int idx;
        readonly WindowCfg backup;
        bool saved;

        public LayoutScreen(Config cfg, int idx) : base(new Vector2(0.56f, 0.5f), 0.9f)
        {
            this.cfg = cfg;
            this.idx = idx;
            backup = cfg.Windows[idx].Clone();
            EnabledBackgroundFade = false;   // podgląd ma być dobrze widoczny
            RecreateControls(true);
        }

        public override string GetFriendlyName() => "DaneHudLayout";

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);
            var w = cfg.Windows[idx];
            AddCaption("Okno " + (idx + 1) + ": położenie i rozmiar");
            const float x = -0.25f, sx = -0.03f, sw = 0.27f, row = 0.05f;
            float y = -0.15f;
            Label(x, y, "Poziomo (0 lewo, 100 prawo)"); Slider(sx, y, sw, 0, 100, w.X, "{0} %", 0, v => w.X = (float)Math.Round(v)); y += row;
            Label(x, y, "Pionowo (0 góra, 100 dół)"); Slider(sx, y, sw, 0, 100, w.Y, "{0} %", 0, v => w.Y = (float)Math.Round(v)); y += row;
            Label(x, y, "Szerokość okna"); Slider(sx, y, sw, 8, 70, w.Width * 100f, "{0}", 0, v => w.Width = (float)Math.Round(v) / 100f); y += row;
            Label(x, y, "Wielkość czcionki"); Slider(sx, y, sw, 30, 130, w.Scale * 100f, "{0}", 0, v => w.Scale = (float)Math.Round(v) / 100f); y += row;
            Label(x, y, "Odstęp wierszy"); Slider(sx, y, sw, 70, 160, w.LineSpacing * 100f, "{0} %", 0, v => w.LineSpacing = (float)Math.Round(v) / 100f); y += row;
            Label(x, y, "Tło (0 = brak)"); Slider(sx, y, sw, 0, 255, w.BgAlpha, "{0}", 0, v => w.BgAlpha = (int)Math.Round(v)); y += row;
            Label(x, y + 0.01f, "Podgląd okna jest na ekranie. Zmiany działają od razu.", 0.6f);

            Button(-0.12f, 0.2f, "Domyślne", () =>
            {
                var d = WindowCfg.Default(idx);
                w.X = d.X; w.Y = d.Y; w.Width = d.Width; w.Scale = d.Scale; w.LineSpacing = d.LineSpacing; w.BgAlpha = d.BgAlpha;
                RecreateControls(false);
            });
            Button(0.12f, 0.2f, "Zapisz", () => { saved = true; cfg.Save(); CloseScreen(false); });
        }

        public override bool Draw()
        {
            if (!base.Draw()) return false;
            try
            {
                var w = cfg.Windows[idx];
                var rows = Plugin.Seated && Plugin.Rows[idx] != null ? Plugin.Rows[idx] : RowBuilder.Sample(w);
                Render.Draw(w, rows);
            }
            catch { }
            return true;
        }

        protected override void OnClosed()
        {
            if (!saved)
            {
                var w = cfg.Windows[idx];
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
        MyGuiControlSlider sr, sg, sb;

        static readonly (string, string)[] Presets =
        {
            ("Biały", "FFFFFF"), ("Zielony", "33FF33"), ("Cyjan", "2ECCFF"), ("Pomarańcz", "FF5A1E"),
            ("Żółty", "FFD21E"), ("Czerwony", "FF4040"), ("Szary", "A0A0A0"), ("Czarny", "000000"),
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
            AddCaption("Kolor: " + name);
            const float x = -0.22f, sx = -0.15f, sw = 0.3f;
            float y = -0.15f;
            Label(x, y, "R"); sr = Slider(sx, y, sw, 0, 255, c.R, "{0}", 0, v => { c.R = (byte)v; Apply(); }); y += 0.05f;
            Label(x, y, "G"); sg = Slider(sx, y, sw, 0, 255, c.G, "{0}", 0, v => { c.G = (byte)v; Apply(); }); y += 0.05f;
            Label(x, y, "B"); sb = Slider(sx, y, sw, 0, 255, c.B, "{0}", 0, v => { c.B = (byte)v; Apply(); }); y += 0.06f;
            for (int i = 0; i < Presets.Length; i++)
            {
                var p = Presets[i];
                Button(-0.165f + (i % 4) * 0.11f, y + (i / 4) * 0.05f, p.Item1, () =>
                {
                    c = WindowCfg.ParseColor(p.Item2, Color.White);
                    Apply();
                    RecreateControls(false);
                }, true);
            }
            Button(0f, 0.21f, "Zapisz", () => { saved = true; Plugin.Cfg?.Save(); CloseScreen(false); });
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
                MyGuiManager.DrawString("Debug", "Przykład 123", pos + new Vector2(0f, 0.07f), 0.7f, c,
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

        public ElementScreen(ElementCfg e, Action onClose) : base(new Vector2(0.62f, 0.6f))
        {
            this.e = e;
            this.onClose = onClose;
            EnabledBackgroundFade = true;
            RecreateControls(true);
        }

        public override string GetFriendlyName() => "DaneHudElement";

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);
            AddCaption(ElementCfg.KindName(e.Kind));
            const float x = -0.28f, vx = -0.05f, w = 0.32f, row = 0.055f;
            float y = -0.19f;
            switch (e.Kind)
            {
                case ElementKind.Header:
                    Label(x, y, "Tekst:"); TextBox(vx, y, w, e.Text, 100, v => e.Text = v); y += row;
                    Check(x, y, "Wyśrodkuj", e.Center, v => e.Center = v); y += row;
                    break;

                case ElementKind.Items:
                {
                    var cats = Enum.GetValues(typeof(ItemCat)).Cast<ItemCat>().ToList();
                    Label(x, y, "Kategoria:"); Combo(vx, y, w, cats.Select(ElementCfg.CatName).ToList(), cats.IndexOf(e.Category), v => { e.Category = cats[v]; }); y += row;
                    Check(x, y, "Sortuj wg nazwy (inaczej wg ilości)", e.SortByName, v => e.SortByName = v); y += row;
                    Check(x, y, "Licz tylko kontenery (bez rafinerii, montażowni...)", e.OnlyCargo, v => e.OnlyCargo = v); y += row;
                    Button(0f, y + 0.01f, $"Co pokazać... (ukryte: {e.Hidden.Count})", () => MyGuiSandbox.AddScreen(new ItemPickScreen(e, () => RecreateControls(false)))); y += row;
                    break;
                }

                case ElementKind.CargoBar:
                    Label(x, y, "Podpis:"); TextBox(vx, y, w, e.Label, 40, v => e.Label = v); y += row;
                    Label(x, y, "Kontenery:"); TextBox(vx, y, w, e.Text, 60, v => e.Text = v); y += row;
                    Label(x, y, "Nazwa grupy albo tekst w nazwie kontenera (np. Ores).", 0.62f); y += 0.03f;
                    Label(x, y, "Puste = wszystkie kontenery grida.", 0.62f); y += row;
                    break;

                case ElementKind.Tank:
                    Label(x, y, "Gaz:"); Combo(vx, y, w, new[] { "Wodór", "Tlen" }, e.Gas == "Oxygen" ? 1 : 0, v => e.Gas = v == 1 ? "Oxygen" : "Hydrogen"); y += row;
                    Label(x, y, "Podpis:"); TextBox(vx, y, w, e.Label, 40, v => e.Label = v); y += row;
                    break;

                case ElementKind.Thrust:
                    Check(x, y, "Tylko przód i góra (krócej)", e.Compact, v => e.Compact = v); y += row;
                    Label(x, y, "Góra pokazuje też udźwig w grawitacji (x = ile razy ciąg > ciężar).", 0.62f); y += row;
                    break;

                case ElementKind.Autocrafting:
                {
                    Label(x, y, "Słowo w nazwie LCD:"); TextBox(vx, y, w, e.Text, 40, v => e.Text = v); y += row;
                    var parts = new List<string> { "Całość", "Część 1 z 2", "Część 2 z 2", "Część 1 z 3", "Część 2 z 3", "Część 3 z 3" };
                    int cur = e.Parts <= 1 ? 0 : e.Parts == 2 ? 1 + e.Part : 3 + e.Part;
                    Label(x, y, "Która część listy:"); Combo(vx, y, w, parts, cur, v =>
                    {
                        if (v == 0) { e.Parts = 1; e.Part = 0; }
                        else if (v <= 2) { e.Parts = 2; e.Part = v - 1; }
                        else { e.Parts = 3; e.Part = v - 3; }
                    }); y += row;
                    Check(x, y, "Tylko braki (obecnie < chciane)", e.OnlyMissing, v => e.OnlyMissing = v); y += row;
                    Check(x, y, "Nazwy z gry zamiast nazw skryptu", e.GameNames, v => e.GameNames = v); y += row;
                    Label(x, y, "Dane czytane z LCD skryptu Isy (\"Autocrafting\", \"Autocrafting 2\"...).", 0.62f); y += 0.03f;
                    Label(x, y, "Okno 5 = część 1 z 2, okno 6 = część 2 z 2.", 0.62f); y += row;
                    break;
                }

                case ElementKind.LcdText:
                    Label(x, y, "Słowo w nazwie LCD:"); TextBox(vx, y, w, e.Text, 60, v => e.Text = v); y += row;
                    Check(x, y, "Liczby po ':' do prawej", e.AlignNumbers, v => e.AlignNumbers = v); y += row;
                    break;

                default:
                    Label(x, y, "Ten element nie ma dodatkowych opcji.", 0.7f); y += row;
                    break;
            }
            Button(0f, 0.24f, "OK", () => CloseScreen(false));
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
            AddCaption("Co pokazać: " + ElementCfg.CatName(e.Category));
            var defs = Defs();
            Label(-0.33f, -0.38f, "Zaznaczone są pokazywane (gdy jest ich więcej niż 0).", 0.65f);
            Button(0.13f, -0.38f, "Wszystkie", () => { e.Hidden.Clear(); Plugin.Dirty = true; RecreateControls(false); }, true);
            Button(0.25f, -0.38f, "Żadne", () => { e.Hidden = defs.Select(d => d.Id.ToString()).ToList(); Plugin.Dirty = true; RecreateControls(false); }, true);

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
            Button(0f, 0.405f, "OK", () => CloseScreen(false));
        }

        protected override void OnClosed()
        {
            Plugin.Dirty = true;
            try { onClose?.Invoke(); } catch { }
            base.OnClosed();
        }
    }
}
