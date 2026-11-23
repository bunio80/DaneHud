using System;
using System.Collections.Generic;
using System.Linq;
using VRageMath;

namespace DaneHud
{
    internal enum RowKind { Text, Pair, Bar, Space }

    internal struct Row
    {
        public RowKind Kind;
        public string Left, Right;
        public Color LeftColor, RightColor, BarColor;
        public float Fraction;
        public bool Center;
    }

    /// <summary>Zamienia elementy okna na wiersze do narysowania.</summary>
    internal class RowBuilder
    {
        readonly WindowCfg w;
        readonly List<Row> rows = new List<Row>();
        readonly Color text, value, bar, warn, title, good;

        RowBuilder(WindowCfg w)
        {
            this.w = w;
            text = WindowCfg.ParseColor(w.TextColor, Color.White);
            value = WindowCfg.ParseColor(w.ValueColor, Color.White);
            bar = WindowCfg.ParseColor(w.BarColor, Color.White);
            warn = WindowCfg.ParseColor(w.WarnColor, new Color(255, 64, 64));
            title = WindowCfg.ParseColor(w.TitleColor, Color.White);
            good = value;
        }

        void Text(string s, Color c, bool center = false) => rows.Add(new Row { Kind = RowKind.Text, Left = s, LeftColor = c, Center = center });
        void Pair(string l, string r) => Pair(l, r, value);
        void Pair(string l, string r, Color rc) => rows.Add(new Row { Kind = RowKind.Pair, Left = l, Right = r, LeftColor = text, RightColor = rc });
        void Bar(float f, Color? c = null) => rows.Add(new Row
        {
            Kind = RowKind.Bar, Fraction = Math.Max(0f, Math.Min(1f, float.IsNaN(f) ? 0f : f)),
            Right = Fmt.Pct(float.IsNaN(f) ? 0 : f), BarColor = c ?? bar, RightColor = value,
        });
        void Space() => rows.Add(new Row { Kind = RowKind.Space });

        public static List<Row> Build(WindowCfg w, Ctx ctx)
        {
            var b = new RowBuilder(w);
            foreach (var e in w.Elements)
            {
                try { b.Element(e, ctx); }
                catch (Exception ex) { b.Text(ElementCfg.KindName(e.Kind) + ": błąd", b.warn); Plugin.Log("element " + e.Kind + ": " + ex); }
            }
            return b.rows;
        }

        void Element(ElementCfg e, Ctx c)
        {
            switch (e.Kind)
            {
                case ElementKind.Header: Text(e.Text ?? "", title, e.Center); break;
                case ElementKind.Blank: Space(); break;
                case ElementKind.Items: Items(e, c); break;
                case ElementKind.CargoBar: Cargo(e, c); break;
                case ElementKind.Tank: Tank(e, c); break;
                case ElementKind.PowerSummary: PowerSummary(c); break;
                case ElementKind.PowerTime: PowerTime(c); break;
                case ElementKind.PowerDetails: PowerDetails(c); break;
                case ElementKind.PowerBalance: PowerBalance(c); break;
                case ElementKind.HydrogenBalance: HydrogenBalance(c); break;
                case ElementKind.Thrust: Thrust(e, c); break;
                case ElementKind.Refineries: Refineries(c); break;
                case ElementKind.Autocrafting: Autocrafting(e, c); break;
                case ElementKind.LcdText: Lcd(e, c); break;
            }
        }

        // ------------------------------------------------------------------

        void Items(ElementCfg e, Ctx c)
        {
            var hidden = new HashSet<string>(e.Hidden ?? new List<string>());
            IEnumerable<ItemAmount> list = c.Items(e.OnlyCargo).Values
                .Where(i => (e.Category == ItemCat.All || i.Cat == e.Category) && i.Amount > 0.0001 && !hidden.Contains(i.Key));
            list = e.SortByName ? list.OrderBy(i => i.Name) : list.OrderByDescending(i => i.Amount);
            int n = 0;
            foreach (var i in list) { Pair(i.Name + ":", Fmt.Amount(i.Amount)); n++; }
            if (n == 0) Text("(brak)", text);
        }

        void Cargo(ElementCfg e, Ctx c)
        {
            var blocks = c.Containers(e.Text);
            string label = string.IsNullOrEmpty(e.Label) ? "Ładownia" : e.Label;
            if (blocks.Count == 0) { Pair(label + ":", "brak kontenerów", warn); return; }
            Ctx.Volume(blocks, out var cur, out var max);
            float f = max > 0 ? (float)(cur / max) : 0f;
            Pair(label + ":", Fmt.Volume(cur) + " / " + Fmt.Volume(max));
            Bar(f, f >= 0.95f ? warn : (Color?)null);
        }

        void Tank(ElementCfg e, Ctx c)
        {
            string gas = e.Gas == "Oxygen" ? "Oxygen" : "Hydrogen";
            string label = string.IsNullOrEmpty(e.Label) ? (gas == "Oxygen" ? "Tlen" : "Wodór") : e.Label;
            c.Tanks(gas, out var stored, out var cap, out var n);
            if (n == 0) { Pair(label + ":", "brak zbiorników", warn); return; }
            float f = cap > 0 ? (float)(stored / cap) : 0f;
            Pair(label + ":", Fmt.Volume(stored) + " / " + Fmt.Volume(cap));
            Bar(f, f <= 0.1f ? warn : (Color?)null);
        }

        void PowerSummary(Ctx c)
        {
            var p = c.Power;
            Pair("Moc:", Fmt.Power(p.ProdCur) + " / " + Fmt.Power(p.ProdMax), p.ProdMax > 0 && p.ProdCur >= p.ProdMax * 0.95 ? warn : value);
        }

        void PowerTime(Ctx c)
        {
            var p = c.Power;
            double net = p.BattOut - p.BattIn;
            if (p.Batteries > 0)
            {
                if (net > 0.0001) Pair("Czas baterii:", Fmt.Time(p.BattStored / net * 3600.0), p.BattStored / net < 0.5 ? warn : value);
                else if (net < -0.0001) Pair("Baterie:", "ładowanie, pełne za " + Fmt.Time((p.BattStoredMax - p.BattStored) / -net * 3600.0));
                else Pair("Baterie:", "bez poboru");
            }
            if (p.Reactors > 0)
            {
                string fuel = Fmt.Amount(p.UraniumKg) + " kg";
                if (p.ReactorFuelRate > 1e-9) Pair("Paliwo reaktorów:", fuel + " = " + Fmt.Time(p.UraniumKg / p.ReactorFuelRate));
                else Pair("Paliwo reaktorów:", fuel);
            }
            if (p.Batteries == 0 && p.Reactors == 0) Text("Czas zasilania: brak baterii i reaktorów", text);
        }

        void PowerDetails(Ctx c)
        {
            var p = c.Power;
            if (p.Reactors > 0) Pair($"Reaktory ({p.Reactors}):", Fmt.Power(p.ReactorCur) + " / " + Fmt.Power(p.ReactorMax));
            if (p.Engines > 0) Pair($"Silniki H2 ({p.Engines}):", Fmt.Power(p.EngineCur) + " / " + Fmt.Power(p.EngineMax));
            if (p.Solars > 0) Pair($"Panele słoneczne ({p.Solars}):", Fmt.Power(p.SolarCur) + " / " + Fmt.Power(p.SolarMax));
            if (p.Winds > 0) Pair($"Turbiny wiatrowe ({p.Winds}):", Fmt.Power(p.WindCur) + " / " + Fmt.Power(p.WindMax));
            if (p.Others > 0) Pair($"Inne źródła ({p.Others}):", Fmt.Power(p.OtherCur) + " / " + Fmt.Power(p.OtherMax));
            if (p.Batteries > 0)
            {
                Pair($"Baterie ({p.Batteries}):", "WE " + Fmt.Power(p.BattIn) + " / WY " + Fmt.Power(p.BattOut));
                Pair("   Zapas:", Fmt.Energy(p.BattStored) + " / " + Fmt.Energy(p.BattStoredMax));
                Pair("   Wyjście:", Fmt.Power(p.BattOut) + " / " + Fmt.Power(p.BattMaxOut));
                Pair("   Wejście:", Fmt.Power(p.BattIn) + " / " + Fmt.Power(p.BattMaxIn));
            }
            Pair("Razem:", Fmt.Power(p.ProdCur) + " / " + Fmt.Power(p.ProdMax));
        }

        void PowerBalance(Ctx c)
        {
            var p = c.Power;
            double prodNoBatt = p.ProdCur - p.BattOut;
            double maxNoBatt = p.ProdMax - p.BattMaxOut;
            Pair("Produkcja prądu:", Fmt.Power(prodNoBatt) + " / " + Fmt.Power(maxNoBatt));
            if (p.BattOut > 0.0001) Pair("Z baterii:", Fmt.Power(p.BattOut));
            if (p.Ions > 0) Pair($"Silniki jonowe ({p.Ions}):", Fmt.Power(p.IonCur) + " / " + Fmt.Power(p.IonMax));
            if (p.Atmos > 0) Pair($"Silniki atmosf. ({p.Atmos}):", Fmt.Power(p.AtmoCur) + " / " + Fmt.Power(p.AtmoMax));
            if (p.ElecThrOthers > 0) Pair($"Inne silniki ({p.ElecThrOthers}):", Fmt.Power(p.ElecThrOtherCur) + " / " + Fmt.Power(p.ElecThrOtherMax));
            Pair("Reszta odbiorników:", Fmt.Power(p.OtherLoad));
            if (p.BattIn > 0.0001) Pair("Ładowanie baterii:", Fmt.Power(p.BattIn));
            double thrMax = p.IonMax + p.AtmoMax + p.ElecThrOtherMax;
            if (thrMax > 0)
            {
                // czy produkcja (bez baterii) uciągnie silniki na pełnej mocy razem z resztą
                double need = thrMax + p.OtherLoad;
                double avail = maxNoBatt + p.BattMaxOut;
                Pair("Pełny ciąg potrzebuje:", Fmt.Power(need) + " / " + Fmt.Power(avail), need > avail ? warn : value);
            }
        }

        void HydrogenBalance(Ctx c)
        {
            var p = c.Power;
            c.Tanks("Hydrogen", out var stored, out var cap, out var tanks);
            if (p.Generators == 0 && p.H2Thrusters == 0 && p.Engines == 0 && tanks == 0) return;
            if (p.Generators > 0) Pair($"Wodór z generatorów ({p.Generators}):", Fmt.Rate(p.GenH2Cur) + " / " + Fmt.Rate(p.GenH2Max));
            if (p.H2Thrusters > 0) Pair($"Silniki wodorowe ({p.H2Thrusters}):", Fmt.Rate(p.ThrH2Cur) + " / " + Fmt.Rate(p.ThrH2Max));
            if (p.Engines > 0) Pair("Generatory prądu H2:", Fmt.Rate(p.EngH2Cur) + " / " + Fmt.Rate(p.EngH2Max));
            double use = p.ThrH2Cur + p.EngH2Cur;
            double bal = p.GenH2Cur - use;
            if (p.Generators > 0 || use > 0) Pair("Bilans wodoru:", (bal >= 0 ? "+" : "") + Fmt.Rate(bal), bal < -0.001 ? warn : good);
            if (tanks > 0)
            {
                Pair($"Zbiorniki H2 ({tanks}):", Fmt.Pct(cap > 0 ? stored / cap : 0) + "  " + Fmt.Volume(stored));
                if (bal < -0.001) Pair("Wodoru starczy na:", Fmt.Time(stored / -bal), stored / -bal < 300 ? warn : value);
                double maxUse = p.ThrH2Max + p.EngH2Max - p.GenH2Max;
                if (maxUse > 0.001 && p.H2Thrusters > 0) Pair("Na pełnym ciągu:", Fmt.Time(stored / maxUse));
            }
        }

        void Thrust(ElementCfg e, Ctx c)
        {
            var t = c.Thrust;
            if (t.Count == 0) return;
            Pair("Masa statku:", Fmt.Mass(t.Mass));
            Pair("Ciąg teraz:", Fmt.Force(t.Current) + " / " + Fmt.Force(t.MaxTotal));
            int[] order = e.Compact ? new[] { 0, 2 } : new[] { 0, 1, 2, 3, 4, 5 };
            foreach (int i in order)
            {
                double f = t.Max[i];
                string s = Fmt.Force(f);
                Color col = value;
                if (t.Mass > 0)
                {
                    double a = f / t.Mass;
                    s += "  " + a.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " m/s²";
                    if (i == 2 && t.Gravity > 0.05)
                    {
                        double lift = f / (t.Mass * t.Gravity);
                        s += "  " + lift.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "x";
                        if (lift < 1.0) col = warn;
                    }
                }
                Pair(Ctx.DirNames[i] + ":", s, col);
            }
            if (t.Gravity > 0.05)
                Pair("Grawitacja:", (t.Gravity / 9.81).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " g");
        }

        void Refineries(Ctx c)
        {
            var r = c.Refinery;
            if (r.Count == 0) { Text("Rafinerie: brak", text); return; }
            string head = (r.Working < r.Count && r.Working > 0 ? $"{r.Working}/{r.Count}" : r.Count.ToString()) + "x Rafinerie";
            if (r.Ores.Count == 0) { Pair(head, "puste", warn); return; }
            string ores = string.Join(" ", r.Ores.OrderByDescending(kv => kv.Value).Select(kv => Fmt.Symbol(kv.Key)));
            Pair(head + "  " + Fmt.TimeShort(r.MaxSeconds), ores);
        }

        void Autocrafting(ElementCfg e, Ctx c)
        {
            var list = c.Autocrafting(e.Text);
            if (list == null) { Text("Brak LCD z '" + e.Text + "' w nazwie", warn); return; }
            IEnumerable<AutoEntry> src = list;
            if (e.OnlyMissing) src = src.Where(a => a.Current < a.Wanted);
            var all = src.ToList();
            int parts = Math.Max(1, e.Parts);
            int per = (all.Count + parts - 1) / parts;
            int start = Math.Min(all.Count, Math.Max(0, e.Part) * per);
            int end = Math.Min(all.Count, start + per);
            if (all.Count == 0 && e.Part == 0) { Text(e.OnlyMissing ? "Wszystko jest" : "(pusto)", text); return; }
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            for (int i = start; i < end; i++)
            {
                var a = all[i];
                string name = e.GameNames ? Ctx.GameName(a.Name) : a.Name;
                // dokładne liczby jak na LCD Isy
                string right = a.Current.ToString("0.##", inv) + " " + a.Op + " " + a.Wanted.ToString("0.##", inv);
                if (!string.IsNullOrEmpty(a.Extra)) right += " " + a.Extra;
                Pair(name, right, a.Current < a.Wanted ? warn : value);
            }
        }

        void Lcd(ElementCfg e, Ctx c)
        {
            var panels = c.Panels(e.Text);
            if (panels.Count == 0) { Text("Brak LCD z '" + e.Text + "' w nazwie", warn); return; }
            foreach (var p in panels)
            {
                string t;
                try { t = p.GetText() ?? ""; } catch { continue; }
                foreach (var raw in t.TrimEnd('\n', '\r', ' ').Split('\n'))
                {
                    var line = raw.TrimEnd('\r');
                    int k = line.IndexOf(':');
                    if (e.AlignNumbers && k > 0 && k < line.Length - 1 && line.Substring(k + 1).Trim().Length > 0)
                        Pair(line.Substring(0, k + 1).Trim(), line.Substring(k + 1).Trim());
                    else if (line.Trim().Length == 0) Space();
                    else Text(line, text);
                }
            }
        }

        // ------------------------------------------------------------------ podgląd bez danych

        /// <summary>Przykładowe wiersze do podglądu, gdy nie siedzisz w gridzie.</summary>
        public static List<Row> Sample(WindowCfg w)
        {
            var b = new RowBuilder(w);
            foreach (var e in w.Elements)
            {
                switch (e.Kind)
                {
                    case ElementKind.Header: b.Text(e.Text ?? "", b.title, e.Center); break;
                    case ElementKind.Blank: b.Space(); break;
                    case ElementKind.Items:
                        b.Pair("Przykład 1:", "13.9 k"); b.Pair("Przykład 2:", "1.9 M"); b.Pair("Przykład 3:", "916.1 k"); break;
                    case ElementKind.CargoBar: b.Pair((e.Label ?? "Ładownia") + ":", "10.5 ML / 33.8 ML"); b.Bar(0.31f); break;
                    case ElementKind.Tank: b.Pair((e.Label ?? "Wodór") + ":", "210 ML / 210 ML"); b.Bar(1f); break;
                    case ElementKind.PowerSummary: b.Pair("Moc:", "134.3 MW / 2.7 GW"); break;
                    case ElementKind.PowerTime: b.Pair("Czas baterii:", "1h 54m 47s"); break;
                    case ElementKind.PowerDetails: b.Pair("Reaktory (4):", "66.5 MW / 2.5 GW"); b.Pair("Baterie (12):", "WE 67 MW / WY 66.9 MW"); b.Pair("Razem:", "115.7 MW / 2.7 GW"); break;
                    case ElementKind.PowerBalance: b.Pair("Produkcja prądu:", "66.5 MW / 2.5 GW"); b.Pair("Silniki jonowe (24):", "12 MW / 80 MW"); break;
                    case ElementKind.HydrogenBalance: b.Pair("Silniki wodorowe (8):", "1.2 kL/s / 9.6 kL/s"); b.Pair("Bilans wodoru:", "-1.2 kL/s", b.warn); break;
                    case ElementKind.Thrust: b.Pair("Masa statku:", "1.2 kt"); b.Pair("Góra:", "24 MN  20 m/s²  2.04x"); break;
                    case ElementKind.Refineries: b.Pair("10x Rafinerie  5h 12m", "U Ag Si Ni"); break;
                    case ElementKind.Autocrafting:
                        b.Pair("SteelPlate", "200372 > 200000"); b.Pair("Motor", "1200 < 5000", b.warn); b.Pair("Computer", "5000 = 5000"); break;
                    case ElementKind.LcdText: b.Text("(tekst z LCD '" + e.Text + "')", b.text); break;
                }
            }
            return b.rows;
        }
    }
}
