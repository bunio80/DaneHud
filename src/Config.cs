using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using VRage.Utils;
using VRageMath;

namespace DaneHud
{
    /// <summary>Rodzaj elementu (wiersza lub grupy wierszy) w oknie. Zapisywany w XML po nazwie, więc kolejność można zmieniać.</summary>
    public enum ElementKind
    {
        Header,          // tekst (Echo / Center / Right)
        Blank,           // pusty wiersz
        Items,           // lista przedmiotów (Inventory / Missing)
        CargoBar,        // zapełnienie kontenerów (Cargo)
        Tank,            // zbiorniki wodoru / tlenu (Tanks)
        PowerSummary,    // moc: bieżąca / maks. (Power)
        PowerTime,       // czas zasilania (PowerTime)
        PowerDetails,    // reaktory, silniki H2, słoneczne, wiatrowe, baterie (PowerSummary)
        PowerBalance,    // prąd: produkcja kontra silniki jonowe/atmosferyczne
        HydrogenBalance, // wodór: generatory kontra silniki
        Thrust,          // ciąg w każdą stronę, przyspieszenie, udźwig
        Refineries,      // jedna linia: liczba rafinerii, czas do końca, rudy
        Autocrafting,    // lista z LCD "Autocrafting" skryptu Isy
        LcdText,         // tekst dowolnego LCD (TextLCD)
        Details,         // tekst informacji bloku z terminala (Details / DetailsNoN / ...X)
        CustomData,      // CustomData bloku
        Working,         // stan bloków (Working)
        Count,           // liczba bloków wg typu (Count / EnabledCount / ProdCount)
        Damage,          // uszkodzone bloki (Damage)
        Ship,            // prędkość, wysokość, grawitacja, masa, droga hamowania, tłumiki, pozycja
        Docked,          // podłączone gridy przez konektory (Docked)
        Charge,          // naładowanie napędów skokowych i baterii (Charge)
        Oxygen,          // ciśnienie w wentylacjach, farmy tlenu (Oxygen)
        Amount,          // zawartość bloków: amunicja, uran, lód (Amount)
        PowerUsed,       // pobór prądu wybranych bloków (PowerUsed)
        Clock,           // godzina / data (Time / Date)
        Distance,        // odległość do punktu GPS (Distance / Pos)
        Property,        // wartość opcji bloku z terminala (Prop)
        Queue,           // kolejka montażowni
    }

    public enum ItemCat { Ore, Ingot, Component, Ammo, Tool, Bottle, Consumable, Other, All }

    public class ElementCfg
    {
        public ElementKind Kind;
        public string Text = "";          // tekst / filtr kontenerów / słowo w nazwie LCD / GPS / format
        public string Label = "";         // podpis wiersza (puste = domyślny w danym języku)
        public ItemCat Category = ItemCat.Ore;
        public List<string> Hidden = new List<string>();   // ukryte przedmioty (Id definicji)
        public bool SortByName = false;
        public bool Center = false;
        public bool Right = false;        // tekst: do prawej
        public string Gas = "Hydrogen";
        public int Part = 0;              // autocrafting: która część (0..Parts-1)
        public int Parts = 1;             // autocrafting: na ile części dzielić listę
        public bool OnlyMissing = false;  // autocrafting / przedmioty: tylko braki
        public bool GameNames = false;    // autocrafting: nazwy z gry zamiast nazw skryptu
        public bool AlignNumbers = true;  // tekst LCD: liczby po ":" do prawej
        public bool Compact = false;      // ciąg: tylko przód i góra
        public bool OnlyCargo = false;    // listy: liczyć tylko kontenery

        // --- wersja 2: polecenia z Automatic LCDs 2
        public string Filter = "";        // bloki: tekst w nazwie, G:grupa, Typ:rodzaj, * = wszystkie
        public bool NoName = false;       // Details: bez nazwy bloku (NoN)
        public bool SkipFirst = false;    // Details: pomiń pierwszą linię (X)
        public string StartText = "";     // Details: zacznij od linii z tym tekstem
        public string StopText = "";      // Details: skończ przed linią z tym tekstem
        public int MaxLines = 0;          // ile linii / bloków (0 = wszystkie)
        public bool RefreshInfo = true;   // Details: odśwież informacje bloku (mody, np. tarcze)
        public double Quota = 0;          // przedmioty: minimum (poniżej = ostrzeżenie)
        public bool ShowBars = false;     // paski (przedmioty wg minimum, ładowanie, ciśnienie)
        public bool OnlyProblems = false; // stan bloków: tylko wyłączone / uszkodzone / bezczynne
        public bool Each = false;         // każdy blok osobno (zamiast sumy)
        public float Threshold = 100f;    // uszkodzenia: pokaż poniżej tylu %
        public string Property = "";      // Prop: nazwa opcji bloku
        public bool ShowSpeed = true, ShowAccel = false, ShowAlt = true, ShowGrav = true, ShowMass = true,
                    ShowStop = true, ShowDamp = true, ShowPos = false, Kmh = false;

        public static ElementCfg Make(ElementKind k, string text = "", string label = "")
        {
            var e = new ElementCfg { Kind = k, Text = text, Label = label };
            switch (k)
            {
                case ElementKind.Header: e.Center = true; if (string.IsNullOrEmpty(e.Text)) e.Text = L.T("--==Tekst==--", "--==Text==--"); break;
                case ElementKind.Autocrafting: if (string.IsNullOrEmpty(e.Text)) e.Text = "Autocrafting"; break;
                case ElementKind.LcdText: if (string.IsNullOrEmpty(e.Text)) e.Text = "LCD"; break;
                case ElementKind.Details: e.Filter = "[A] Shield Controller"; e.NoName = true; e.SkipFirst = true; e.StartText = "CapHp:"; e.MaxLines = 2; break;
                case ElementKind.CustomData: e.Filter = "LCD"; break;
                case ElementKind.Working: e.Filter = L.T("Typ:", "Type:") + "Refinery"; break;
                case ElementKind.Count: e.Filter = "*"; break;
                case ElementKind.Damage: e.Filter = "*"; e.MaxLines = 10; break;
                case ElementKind.Charge: e.Filter = L.T("Typ:", "Type:") + "Jump"; e.ShowBars = true; break;
                case ElementKind.Oxygen: e.Filter = "*"; break;
                case ElementKind.Amount: e.Filter = L.T("Typ:", "Type:") + "Turret"; break;
                case ElementKind.PowerUsed: e.Filter = L.T("Typ:", "Type:") + "Drill"; break;
                case ElementKind.Clock: e.Text = "HH:mm"; break;
                case ElementKind.Distance: e.Text = "GPS:Baza:0:0:0:"; break;
                case ElementKind.Property: e.Filter = "Connector"; e.Property = "OnOff"; break;
                case ElementKind.Queue: e.Filter = "*"; e.MaxLines = 10; break;
            }
            return e;
        }

        /// <summary>Kolejność w liście "Dodaj" w menu.</summary>
        public static readonly ElementKind[] AddOrder =
        {
            ElementKind.Header, ElementKind.Blank, ElementKind.Items, ElementKind.CargoBar, ElementKind.Tank, ElementKind.Amount,
            ElementKind.PowerSummary, ElementKind.PowerTime, ElementKind.PowerDetails, ElementKind.PowerBalance, ElementKind.PowerUsed,
            ElementKind.Charge, ElementKind.HydrogenBalance, ElementKind.Oxygen, ElementKind.Thrust, ElementKind.Ship, ElementKind.Docked,
            ElementKind.Refineries, ElementKind.Queue, ElementKind.Autocrafting, ElementKind.Working, ElementKind.Count, ElementKind.Damage,
            ElementKind.Details, ElementKind.CustomData, ElementKind.Property, ElementKind.LcdText, ElementKind.Clock, ElementKind.Distance,
        };

        public static string KindName(ElementKind k)
        {
            switch (k)
            {
                case ElementKind.Header: return L.T("Tekst / nagłówek", "Text / header");
                case ElementKind.Blank: return L.T("Pusty wiersz", "Blank line");
                case ElementKind.Items: return L.T("Przedmioty (Inventory)", "Items (Inventory)");
                case ElementKind.CargoBar: return L.T("Zapełnienie ładowni (Cargo)", "Cargo fill (Cargo)");
                case ElementKind.Tank: return L.T("Zbiorniki H2/O2 (Tanks)", "H2/O2 tanks (Tanks)");
                case ElementKind.PowerSummary: return L.T("Moc: podsumowanie (Power)", "Power: summary (Power)");
                case ElementKind.PowerTime: return L.T("Moc: czas zasilania (PowerTime)", "Power: time left (PowerTime)");
                case ElementKind.PowerDetails: return L.T("Moc: szczegóły źródeł", "Power: source details");
                case ElementKind.PowerBalance: return L.T("Prąd: produkcja / silniki", "Power: production / thrusters");
                case ElementKind.HydrogenBalance: return L.T("Wodór: produkcja / silniki", "Hydrogen: production / thrusters");
                case ElementKind.Thrust: return L.T("Ciąg silników", "Thrust");
                case ElementKind.Refineries: return L.T("Rafinerie (jedna linia)", "Refineries (one line)");
                case ElementKind.Autocrafting: return L.T("Autocrafting Isy", "Isy's autocrafting");
                case ElementKind.LcdText: return L.T("Tekst z LCD (TextLCD)", "Text from LCD (TextLCD)");
                case ElementKind.Details: return L.T("Informacje bloku (Details)", "Block info (Details)");
                case ElementKind.CustomData: return L.T("Dane własne bloku (CustomData)", "Block CustomData");
                case ElementKind.Working: return L.T("Stan bloków (Working)", "Block status (Working)");
                case ElementKind.Count: return L.T("Liczba bloków (Count)", "Block count (Count)");
                case ElementKind.Damage: return L.T("Uszkodzenia (Damage)", "Damage");
                case ElementKind.Ship: return L.T("Lot: prędkość, wysokość, masa...", "Flight: speed, altitude, mass...");
                case ElementKind.Docked: return L.T("Zadokowane (Docked)", "Docked");
                case ElementKind.Charge: return L.T("Naładowanie (Charge)", "Charge");
                case ElementKind.Oxygen: return L.T("Tlen i ciśnienie (Oxygen)", "Oxygen and pressure");
                case ElementKind.Amount: return L.T("Zawartość bloków (Amount)", "Block contents (Amount)");
                case ElementKind.PowerUsed: return L.T("Pobór prądu bloków (PowerUsed)", "Block power use (PowerUsed)");
                case ElementKind.Clock: return L.T("Godzina / data (Time)", "Time / date");
                case ElementKind.Distance: return L.T("Odległość do GPS (Distance)", "Distance to GPS");
                case ElementKind.Property: return L.T("Opcja bloku (Prop)", "Block property (Prop)");
                case ElementKind.Queue: return L.T("Kolejka montażowni", "Assembler queue");
            }
            return k.ToString();
        }

        public static string CatName(ItemCat c)
        {
            switch (c)
            {
                case ItemCat.Ore: return L.T("Rudy", "Ores");
                case ItemCat.Ingot: return L.T("Sztabki", "Ingots");
                case ItemCat.Component: return L.T("Komponenty", "Components");
                case ItemCat.Ammo: return L.T("Amunicja", "Ammo");
                case ItemCat.Tool: return L.T("Narzędzia i broń", "Tools and weapons");
                case ItemCat.Bottle: return L.T("Butle", "Bottles");
                case ItemCat.Consumable: return L.T("Jedzenie i apteczki", "Food and consumables");
                case ItemCat.Other: return L.T("Inne", "Other");
                case ItemCat.All: return L.T("Wszystko", "Everything");
            }
            return c.ToString();
        }

        /// <summary>Opis elementu na liście w menu.</summary>
        public string Describe()
        {
            string s = KindName(Kind);
            string f = string.IsNullOrEmpty(Filter) ? "" : " {" + Filter + "}";
            switch (Kind)
            {
                case ElementKind.Header: return L.T("Tekst: ", "Text: ") + Text;
                case ElementKind.Items: return s + ": " + CatName(Category) + f + (Hidden.Count > 0 ? L.T($" (ukryte {Hidden.Count})", $" ({Hidden.Count} hidden)") : "");
                case ElementKind.CargoBar: return s + ": " + Label + (string.IsNullOrEmpty(Text) ? L.T(" (wszystkie)", " (all)") : " [" + Text + "]");
                case ElementKind.Tank: return s + ": " + (Gas == "Oxygen" ? L.T("tlen", "oxygen") : L.T("wodór", "hydrogen"));
                case ElementKind.Autocrafting: return s + (Parts > 1 ? L.T($" część {Part + 1}/{Parts}", $" part {Part + 1}/{Parts}") : L.T(" (całość)", " (all)"));
                case ElementKind.LcdText: return s + ": [" + Text + "]";
                case ElementKind.Details: return s + f + (string.IsNullOrEmpty(StartText) ? "" : " {" + StartText + "}") + (MaxLines > 0 ? " " + MaxLines : "");
                case ElementKind.Property: return s + f + " " + Property;
                case ElementKind.Clock:
                case ElementKind.Distance: return s + ": " + Text;
                case ElementKind.CustomData:
                case ElementKind.Working:
                case ElementKind.Count:
                case ElementKind.Damage:
                case ElementKind.Charge:
                case ElementKind.Oxygen:
                case ElementKind.Amount:
                case ElementKind.PowerUsed:
                case ElementKind.Queue: return s + f;
            }
            return s;
        }

        public ElementCfg Clone() => Config.DeepClone(this);
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
        internal static string[] FontNames => new[] { L.T("Zwykła", "Normal"), L.T("Z cieniem", "Shadow"), L.T("Stała szerokość", "Monospace") };
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

        public WindowCfg Clone() => Config.DeepClone(this);

        /// <summary>Nowe, puste okno dodane z menu.</summary>
        public static WindowCfg New(int number)
        {
            var w = new WindowCfg { Name = L.T("Okno ", "Window ") + number, X = 50f, Y = 50f };
            w.Elements.Add(ElementCfg.Make(ElementKind.Header, L.T("--==Nowe okno==--", "--==New window==--")));
            return w;
        }

        public const int DefaultCount = 6;

        /// <summary>Domyślne okna 1–6 ułożone jak na screenie użytkownika (HudLcd + Automatic LCDs 2 + Isy).</summary>
        public static WindowCfg Default(int i)
        {
            var w = new WindowCfg();
            string used = L.T("Zajęte", "Used");
            switch (i)
            {
                case 0:
                    w.Name = L.T("Rudy", "Ores"); w.X = 0f; w.Y = 80f;
                    w.Elements.Add(ElementCfg.Make(ElementKind.Header, L.T("--==Rudy==--", "--==Ores==--")));
                    w.Elements.Add(new ElementCfg { Kind = ElementKind.Items, Category = ItemCat.Ore });
                    break;
                case 1:
                    w.Name = L.T("Ładownie", "Cargo"); w.X = 0f; w.Y = 0f;
                    w.TitleColor = w.TextColor = w.ValueColor = w.BarColor = "FF5A1E";
                    w.Elements.Add(ElementCfg.Make(ElementKind.Header, L.T("--==Rudy==--", "--==Ores==--")));
                    w.Elements.Add(ElementCfg.Make(ElementKind.CargoBar, "Ores", used));
                    w.Elements.Add(ElementCfg.Make(ElementKind.Header, L.T("--==Sztabki==--", "--==Ingots==--")));
                    w.Elements.Add(ElementCfg.Make(ElementKind.CargoBar, "Ingots", used));
                    w.Elements.Add(ElementCfg.Make(ElementKind.Header, L.T("--==Komponenty==--", "--==Components==--")));
                    w.Elements.Add(ElementCfg.Make(ElementKind.CargoBar, "Components", used));
                    w.Elements.Add(ElementCfg.Make(ElementKind.Blank));
                    w.Elements.Add(new ElementCfg { Kind = ElementKind.Tank, Gas = "Hydrogen", Label = L.T("Wodór", "Hydrogen") });
                    break;
                case 2:
                    w.Name = L.T("Moc", "Power"); w.X = 22f; w.Y = 0f; w.Width = 0.26f;
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
                    w.Name = L.T("Sztabki", "Ingots"); w.X = 0f; w.Y = 42f;
                    w.Elements.Add(ElementCfg.Make(ElementKind.Header, L.T("--==Sztabki==--", "--==Ingots==--")));
                    w.Elements.Add(new ElementCfg { Kind = ElementKind.Items, Category = ItemCat.Ingot });
                    w.Elements.Add(ElementCfg.Make(ElementKind.Blank));
                    w.Elements.Add(ElementCfg.Make(ElementKind.Refineries));
                    break;
                case 4:
                case 5:
                    w.Name = "Autocrafting " + (i - 3);
                    w.X = i == 4 ? 76f : 100f; w.Y = 0f; w.Width = 0.24f;
                    w.ShowTitle = i == 4; w.Title = L.T("Autocrafting Isy", "Isy's autocrafting");
                    w.TitleColor = w.TextColor = w.ValueColor = "33FF33";
                    w.Elements.Add(new ElementCfg { Kind = ElementKind.Autocrafting, Text = "Autocrafting", Part = i - 4, Parts = 2 });
                    break;
                default:
                    return New(i + 1);
            }
            return w;
        }

        public static List<WindowCfg> Defaults()
        {
            var list = new List<WindowCfg>();
            for (int i = 0; i < DefaultCount; i++) list.Add(Default(i));
            return list;
        }
    }

    /// <summary>Ustawienia okien jednego grida (statek bojowy, koparka, baza...).</summary>
    public class GridProfile
    {
        public long GridId;
        public string GridName = "";
        public DateTime LastUsed = DateTime.Now;
        public List<WindowCfg> Windows = new List<WindowCfg>();
    }

    public class Config
    {
        public int Language = 0;               // 0 = polski, 1 = English
        public bool Visible = true;            // wszystkie okna (/dane ukryj, /dane pokaz)
        public bool LiveInventory = true;      // "wirtualny terminal": serwer przysyła zawartość ładowni na bieżąco
        public bool PerGrid = true;            // osobne okna dla każdego grida
        public bool Initialized = false;
        public int RefreshTicks = 30;          // co ile klatek liczyć dane (30 = pół sekundy)
        public List<WindowCfg> Windows = new List<WindowCfg>();      // szablon: okna gridów bez własnego profilu
        public List<GridProfile> Grids = new List<GridProfile>();    // profile gridów

        static void FixWindows(List<WindowCfg> list)
        {
            foreach (var w in list)
            {
                if (w.Elements == null) w.Elements = new List<ElementCfg>();
                foreach (var e in w.Elements)
                {
                    if (e.Hidden == null) e.Hidden = new List<string>();
                    if (e.Parts < 1) e.Parts = 1;
                    if (e.Part >= e.Parts) e.Part = e.Parts - 1;
                    if (e.Part < 0) e.Part = 0;
                    if (e.Filter == null) e.Filter = "";
                    if (e.StartText == null) e.StartText = "";
                    if (e.StopText == null) e.StopText = "";
                    if (e.Property == null) e.Property = "";
                    if (e.Text == null) e.Text = "";
                    if (e.Label == null) e.Label = "";
                }
                if (w.Scale < 0.2f) w.Scale = 0.62f;
                if (w.Width < 0.05f) w.Width = 0.22f;
                if (w.LineSpacing < 0.5f) w.LineSpacing = 1f;
            }
        }

        void Fix()
        {
            if (Windows == null) Windows = new List<WindowCfg>();
            if (Grids == null) Grids = new List<GridProfile>();
            if (!Initialized && Windows.Count == 0) Windows.AddRange(WindowCfg.Defaults());
            Initialized = true;
            FixWindows(Windows);
            Grids.RemoveAll(g => g == null);
            foreach (var g in Grids)
            {
                if (g.Windows == null) g.Windows = new List<WindowCfg>();
                if (g.GridName == null) g.GridName = "";
                FixWindows(g.Windows);
            }
            if (RefreshTicks < 10) RefreshTicks = 30;
            if (Language < 0 || Language > 1) Language = 0;
        }

        /// <summary>Profil grida: najpierw po Id, potem po nazwie (grid wklejony na nowo / po restarcie serwera).</summary>
        public GridProfile FindProfile(long gridId, string gridName)
        {
            foreach (var g in Grids) if (g.GridId == gridId && gridId != 0) return g;
            if (string.IsNullOrEmpty(gridName)) return null;
            GridProfile byName = null;
            foreach (var g in Grids)
                if (string.Equals(g.GridName, gridName, StringComparison.Ordinal))
                {
                    if (byName != null) return byName;   // kilka o tej nazwie: pierwszy
                    byName = g;
                }
            if (byName != null) byName.GridId = gridId;
            return byName;
        }

        public GridProfile CreateProfile(long gridId, string gridName)
        {
            var g = new GridProfile { GridId = gridId, GridName = gridName ?? "" };
            foreach (var w in Windows) g.Windows.Add(w.Clone());
            Grids.Add(g);
            return g;
        }

        internal static T DeepClone<T>(T o)
        {
            var ser = new XmlSerializer(typeof(T));
            using (var ms = new MemoryStream())
            {
                ser.Serialize(ms, o);
                ms.Position = 0;
                return (T)ser.Deserialize(ms);
            }
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
                // najpierw do pliku tymczasowego: nagłe wyłączenie nie zostawi pustego pliku
                string tmp = FilePath + ".tmp";
                using (var f = File.Create(tmp))
                    new XmlSerializer(typeof(Config)).Serialize(f, this);
                if (File.Exists(FilePath)) File.Copy(FilePath, FilePath + ".bak", true);
                File.Copy(tmp, FilePath, true);
                File.Delete(tmp);
            }
            catch (Exception e) { MyLog.Default.WriteLine("DaneHud: config save failed: " + e.Message); }
        }
    }
}
