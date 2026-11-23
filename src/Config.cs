using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using VRage.Utils;
using VRageMath;

namespace DaneHud
{
    /// <summary>Rodzaj elementu (wiersza lub grupy wierszy) w oknie.</summary>
    public enum ElementKind
    {
        Header,          // nagłówek / dowolny tekst
        Blank,           // pusty wiersz
        Items,           // lista przedmiotów jednej kategorii (rudy, sztabki, komponenty...)
        CargoBar,        // zapełnienie kontenerów (wg nazwy albo grupy)
        Tank,            // zbiorniki wodoru / tlenu
        PowerSummary,    // moc: bieżąca / maks.
        PowerTime,       // czas zasilania (baterie, paliwo reaktorów)
        PowerDetails,    // reaktory, silniki H2, słoneczne, wiatrowe, baterie
        PowerBalance,    // prąd: produkcja kontra silniki jonowe/atmosferyczne
        HydrogenBalance, // wodór: generatory kontra silniki
        Thrust,          // ciąg w każdą stronę, przyspieszenie, udźwig
        Refineries,      // jedna linia: liczba rafinerii, czas do końca, rudy
        Autocrafting,    // lista z LCD "Autocrafting" skryptu Isy
        LcdText,         // tekst dowolnego LCD
    }

    public enum ItemCat { Ore, Ingot, Component, Ammo, Tool, Bottle, Other, All }

    public class ElementCfg
    {
        public ElementKind Kind;
        public string Text = "";          // nagłówek / filtr kontenerów / słowo w nazwie LCD
        public string Label = "";         // podpis wiersza
        public ItemCat Category = ItemCat.Ore;
        public List<string> Hidden = new List<string>();   // ukryte przedmioty (Id definicji)
        public bool SortByName = false;
        public bool Center = false;
        public string Gas = "Hydrogen";
        public int Part = 0;              // autocrafting: która część (0..Parts-1)
        public int Parts = 1;             // autocrafting: na ile części dzielić listę
        public bool OnlyMissing = false;  // autocrafting: tylko braki
        public bool GameNames = false;    // autocrafting: nazwy z gry zamiast nazw skryptu
        public bool AlignNumbers = true;  // tekst LCD: liczby po ":" do prawej
        public bool Compact = false;      // ciąg: tylko przód i góra
        public bool OnlyCargo = false;    // listy: liczyć tylko kontenery (bez rafinerii, montażowni...)

        public static ElementCfg Make(ElementKind k, string text = "", string label = "")
        {
            var e = new ElementCfg { Kind = k, Text = text, Label = label };
            switch (k)
            {
                case ElementKind.Header: e.Center = true; break;
                case ElementKind.Autocrafting: if (string.IsNullOrEmpty(e.Text)) e.Text = "Autocrafting"; break;
                case ElementKind.Tank: if (string.IsNullOrEmpty(e.Label)) e.Label = "Wodór"; break;
                case ElementKind.CargoBar: if (string.IsNullOrEmpty(e.Label)) e.Label = "Ładownia"; break;
            }
            return e;
        }

        public static string KindName(ElementKind k)
        {
            switch (k)
            {
                case ElementKind.Header: return "Nagłówek / tekst";
                case ElementKind.Blank: return "Pusty wiersz";
                case ElementKind.Items: return "Lista przedmiotów";
                case ElementKind.CargoBar: return "Zapełnienie ładowni";
                case ElementKind.Tank: return "Zbiorniki (wodór/tlen)";
                case ElementKind.PowerSummary: return "Moc: podsumowanie";
                case ElementKind.PowerTime: return "Moc: czas zasilania";
                case ElementKind.PowerDetails: return "Moc: szczegóły źródeł";
                case ElementKind.PowerBalance: return "Prąd: produkcja / silniki";
                case ElementKind.HydrogenBalance: return "Wodór: produkcja / silniki";
                case ElementKind.Thrust: return "Ciąg silników";
                case ElementKind.Refineries: return "Rafinerie (jedna linia)";
                case ElementKind.Autocrafting: return "Autocrafting Isy";
                case ElementKind.LcdText: return "Tekst z LCD";
            }
            return k.ToString();
        }

        public static string CatName(ItemCat c)
        {
            switch (c)
            {
                case ItemCat.Ore: return "Rudy";
                case ItemCat.Ingot: return "Sztabki";
                case ItemCat.Component: return "Komponenty";
                case ItemCat.Ammo: return "Amunicja";
                case ItemCat.Tool: return "Narzędzia i broń";
                case ItemCat.Bottle: return "Butle";
                case ItemCat.Other: return "Inne";
                case ItemCat.All: return "Wszystko";
            }
            return c.ToString();
        }

        /// <summary>Opis elementu na liście w menu.</summary>
        public string Describe()
        {
            string s = KindName(Kind);
            switch (Kind)
            {
                case ElementKind.Header: return "Tekst: " + Text;
                case ElementKind.Items: return s + ": " + CatName(Category) + (Hidden.Count > 0 ? $" (ukryte {Hidden.Count})" : "");
                case ElementKind.CargoBar: return s + ": " + Label + (string.IsNullOrEmpty(Text) ? " (wszystkie)" : " [" + Text + "]");
                case ElementKind.Tank: return s + ": " + (Gas == "Oxygen" ? "tlen" : "wodór");
                case ElementKind.Autocrafting: return s + (Parts > 1 ? $" część {Part + 1}/{Parts}" : " (całość)");
                case ElementKind.LcdText: return s + ": [" + Text + "]";
            }
            return s;
        }
    }

    public class WindowCfg
    {
        public string Name = "Okno";
        public bool Enabled = true;
        public bool ShowTitle = false;
        public string Title = "";
        public float X = 0f;           // % (0 = lewa krawędź ekranu, 100 = prawa)
        public float Y = 0f;           // % (0 = góra, 100 = dół)
        public float Width = 0.22f;    // szerokość okna (jednostki GUI)
        public float Scale = 0.62f;    // wielkość czcionki
        public float LineSpacing = 1f; // odstęp wierszy (mnożnik)
        public int Font = 1;           // 0 = zwykła, 1 = z cieniem, 2 = stała szerokość
        public int BgAlpha = 0;        // tło: 0 = brak, 255 = pełne
        public string TitleColor = "FFFFFF";
        public string TextColor = "FFFFFF";
        public string ValueColor = "FFFFFF";
        public string BarColor = "FFFFFF";
        public string WarnColor = "FF4040";
        public string BgColor = "000000";
        public List<ElementCfg> Elements = new List<ElementCfg>();

        internal static readonly string[] FontIds = { "White", "Debug", "Monospace" };
        internal static readonly string[] FontNames = { "Zwykła", "Z cieniem", "Stała szerokość" };
        internal string FontId => FontIds[Math.Max(0, Math.Min(FontIds.Length - 1, Font))];

        internal static Color ParseColor(string hex, Color fallback)
        {
            try
            {
                if (string.IsNullOrEmpty(hex) || hex.Length < 6) return fallback;
                uint v = Convert.ToUInt32(hex.Substring(0, 6), 16);
                return new Color((int)(v >> 16) & 255, (int)(v >> 8) & 255, (int)v & 255);
            }
            catch { return fallback; }
        }

        internal static string ToHex(Color c) => $"{c.R:X2}{c.G:X2}{c.B:X2}";

        public WindowCfg Clone()
        {
            var w = (WindowCfg)MemberwiseClone();
            w.Elements = new List<ElementCfg>();
            foreach (var e in Elements)
            {
                var c = (ElementCfg)e.MemberwiseCloneHelper();
                w.Elements.Add(c);
            }
            return w;
        }

        public void CopyFrom(WindowCfg o)
        {
            Name = o.Name; Enabled = o.Enabled; ShowTitle = o.ShowTitle; Title = o.Title;
            X = o.X; Y = o.Y; Width = o.Width; Scale = o.Scale; LineSpacing = o.LineSpacing; Font = o.Font; BgAlpha = o.BgAlpha;
            TitleColor = o.TitleColor; TextColor = o.TextColor; ValueColor = o.ValueColor; BarColor = o.BarColor;
            WarnColor = o.WarnColor; BgColor = o.BgColor;
            Elements = o.Clone().Elements;
        }

        /// <summary>Domyślne okna 1–6 ułożone jak na screenie użytkownika (HudLcd + Automatic LCDs 2 + Isy).</summary>
        public static WindowCfg Default(int i)
        {
            var w = new WindowCfg();
            switch (i)
            {
                case 0:
                    w.Name = "Rudy"; w.X = 0f; w.Y = 80f;
                    w.Elements.Add(ElementCfg.Make(ElementKind.Header, "--==Rudy==--"));
                    w.Elements.Add(new ElementCfg { Kind = ElementKind.Items, Category = ItemCat.Ore });
                    break;
                case 1:
                    w.Name = "Ładownie"; w.X = 0f; w.Y = 0f;
                    w.TitleColor = w.TextColor = w.ValueColor = w.BarColor = "FF5A1E";
                    w.Elements.Add(ElementCfg.Make(ElementKind.Header, "--==Rudy==--"));
                    w.Elements.Add(ElementCfg.Make(ElementKind.CargoBar, "Ores", "Zajęte"));
                    w.Elements.Add(ElementCfg.Make(ElementKind.Header, "--==Sztabki==--"));
                    w.Elements.Add(ElementCfg.Make(ElementKind.CargoBar, "Ingots", "Zajęte"));
                    w.Elements.Add(ElementCfg.Make(ElementKind.Header, "--==Komponenty==--"));
                    w.Elements.Add(ElementCfg.Make(ElementKind.CargoBar, "Components", "Zajęte"));
                    w.Elements.Add(ElementCfg.Make(ElementKind.Blank));
                    w.Elements.Add(new ElementCfg { Kind = ElementKind.Tank, Gas = "Hydrogen", Label = "Wodór" });
                    break;
                case 2:
                    w.Name = "Moc"; w.X = 22f; w.Y = 0f; w.Width = 0.26f;
                    w.TitleColor = w.TextColor = w.ValueColor = w.BarColor = "2ECCFF";
                    w.Elements.Add(ElementCfg.Make(ElementKind.PowerSummary));
                    w.Elements.Add(ElementCfg.Make(ElementKind.Blank));
                    w.Elements.Add(ElementCfg.Make(ElementKind.PowerTime));
                    w.Elements.Add(ElementCfg.Make(ElementKind.Blank));
                    w.Elements.Add(ElementCfg.Make(ElementKind.PowerDetails));
                    w.Elements.Add(ElementCfg.Make(ElementKind.Blank));
                    w.Elements.Add(ElementCfg.Make(ElementKind.PowerBalance));
                    w.Elements.Add(ElementCfg.Make(ElementKind.HydrogenBalance));
                    w.Elements.Add(ElementCfg.Make(ElementKind.Thrust));
                    break;
                case 3:
                    w.Name = "Sztabki"; w.X = 0f; w.Y = 42f;
                    w.Elements.Add(ElementCfg.Make(ElementKind.Header, "--==Sztabki==--"));
                    w.Elements.Add(new ElementCfg { Kind = ElementKind.Items, Category = ItemCat.Ingot });
                    w.Elements.Add(ElementCfg.Make(ElementKind.Blank));
                    w.Elements.Add(ElementCfg.Make(ElementKind.Refineries));
                    break;
                case 4:
                case 5:
                    w.Name = i == 4 ? "Autocrafting 1" : "Autocrafting 2";
                    w.X = i == 4 ? 76f : 100f; w.Y = 0f; w.Width = 0.24f;
                    w.ShowTitle = i == 4; w.Title = "Autocrafting Isy";
                    w.TitleColor = w.TextColor = w.ValueColor = "33FF33";
                    w.Elements.Add(new ElementCfg { Kind = ElementKind.Autocrafting, Text = "Autocrafting", Part = i - 4, Parts = 2 });
                    break;
            }
            return w;
        }
    }

    internal static class CloneExt
    {
        internal static object MemberwiseCloneHelper(this ElementCfg e)
        {
            var c = new ElementCfg
            {
                Kind = e.Kind, Text = e.Text, Label = e.Label, Category = e.Category, Hidden = new List<string>(e.Hidden ?? new List<string>()),
                SortByName = e.SortByName, Center = e.Center, Gas = e.Gas, Part = e.Part, Parts = e.Parts, OnlyMissing = e.OnlyMissing,
                GameNames = e.GameNames, AlignNumbers = e.AlignNumbers, Compact = e.Compact, OnlyCargo = e.OnlyCargo,
            };
            return c;
        }
    }

    public class Config
    {
        public const int WindowCount = 6;

        public bool Visible = true;            // wszystkie okna (/dane ukryj, /dane pokaz)
        public bool LiveInventory = true;      // "wirtualny terminal": serwer przysyła zawartość ładowni na bieżąco
        public int RefreshTicks = 30;          // co ile klatek liczyć dane (30 = pół sekundy)
        public List<WindowCfg> Windows = new List<WindowCfg>();   // bez domyślnych w inicjalizatorze (XmlSerializer by doklejał)

        void Fix()
        {
            if (Windows == null) Windows = new List<WindowCfg>();
            while (Windows.Count < WindowCount) Windows.Add(WindowCfg.Default(Windows.Count));
            if (Windows.Count > WindowCount) Windows.RemoveRange(WindowCount, Windows.Count - WindowCount);
            foreach (var w in Windows)
            {
                if (w.Elements == null) w.Elements = new List<ElementCfg>();
                foreach (var e in w.Elements)
                {
                    if (e.Hidden == null) e.Hidden = new List<string>();
                    if (e.Parts < 1) e.Parts = 1;
                    if (e.Part >= e.Parts) e.Part = e.Parts - 1;
                    if (e.Part < 0) e.Part = 0;
                }
                if (w.Scale < 0.2f) w.Scale = 0.62f;
                if (w.Width < 0.05f) w.Width = 0.22f;
                if (w.LineSpacing < 0.5f) w.LineSpacing = 1f;
            }
            if (RefreshTicks < 10) RefreshTicks = 30;
        }

        static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SpaceEngineers", "Plugins", "DaneHud.xml");

        public static Config Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    using (var f = File.OpenRead(FilePath))
                    {
                        var c = (Config)new XmlSerializer(typeof(Config)).Deserialize(f);
                        if (c != null) { c.Fix(); return c; }
                    }
            }
            catch (Exception e) { MyLog.Default.WriteLine("DaneHud: config load failed: " + e.Message); }
            var fresh = new Config();
            fresh.Fix();
            return fresh;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                using (var f = File.Create(FilePath))
                    new XmlSerializer(typeof(Config)).Serialize(f, this);
            }
            catch (Exception e) { MyLog.Default.WriteLine("DaneHud: config save failed: " + e.Message); }
        }
    }
}
