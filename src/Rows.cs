using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Sandbox.Game.Entities;
using VRageMath;
using Ing = Sandbox.ModAPI.Ingame;

namespace DaneHud
{
    internal enum RowKind { Text, Pair, Bar, Space }

    internal struct Row
    {
        public RowKind Kind;
        public string Left, Right;
        public Color LeftColor, RightColor, BarColor;
        public float Fraction;
        public bool Center, AlignRight;
    }

    /// <summary>Zamienia elementy okna na wiersze do narysowania.</summary>
    internal class RowBuilder
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        readonly List<Row> rows = new List<Row>();
        readonly Color text, value, bar, warn, title, good;

        RowBuilder(WindowCfg w)
        {
            text = WindowCfg.ParseColor(w.TextColor, Color.White);
            value = WindowCfg.ParseColor(w.ValueColor, Color.White);
            bar = WindowCfg.ParseColor(w.BarColor, Color.White);
            warn = WindowCfg.ParseColor(w.WarnColor, new Color(255, 64, 64));
            title = WindowCfg.ParseColor(w.TitleColor, Color.White);
            good = value;
        }

        static string T(string pl, string en) => L.T(pl, en);

        void Text(string s, Color c, bool center = false, bool right = false) =>
            rows.Add(new Row { Kind = RowKind.Text, Left = s, LeftColor = c, Center = center, AlignRight = right });
        void Pair(string l, string r) => Pair(l, r, value);
        void Pair(string l, string r, Color rc) => rows.Add(new Row { Kind = RowKind.Pair, Left = l, Right = r, LeftColor = text, RightColor = rc });
        void Bar(double f, Color? c = null)
        {
            float v = double.IsNaN(f) || double.IsInfinity(f) ? 0f : (float)f;
            rows.Add(new Row { Kind = RowKind.Bar, Fraction = Math.Max(0f, Math.Min(1f, v)), Right = Fmt.Pct(v), BarColor = c ?? bar, RightColor = value });
        }
        void Space() => rows.Add(new Row { Kind = RowKind.Space });

        /// <summary>Linia "klucz: wartość" do prawej albo cały tekst.</summary>
        void Line(string line, bool align, Color c)
        {
            int k = line.IndexOf(':');
            if (align && k > 0 && k < line.Length - 1 && line.Substring(k + 1).Trim().Length > 0)
                Pair(line.Substring(0, k + 1).Trim(), line.Substring(k + 1).Trim());
            else Text(line, c);
        }

        static int Limit(ElementCfg e, int count) => e.MaxLines > 0 ? Math.Min(e.MaxLines, count) : count;

        public static List<Row> Build(WindowCfg w, Ctx ctx)
        {
            var b = new RowBuilder(w);
            foreach (var e in w.Elements)
            {
                try { b.Element(e, ctx); }
                catch (Exception ex) { b.Text(ElementCfg.KindName(e.Kind) + T(": błąd", ": error"), b.warn); Plugin.LogOnce("element " + e.Kind, ex); }
            }
            return b.rows;
        }

        void Element(ElementCfg e, Ctx c)
        {
            switch (e.Kind)
            {
                case ElementKind.Header: Text(e.Text ?? "", title, e.Center, e.Right); break;
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
                case ElementKind.Details: Details(e, c); break;
                case ElementKind.CustomData: CustomData(e, c); break;
                case ElementKind.Working: Working(e, c); break;
                case ElementKind.Count: Count(e, c); break;
                case ElementKind.Damage: Damage(e, c); break;
                case ElementKind.Ship: Ship(e, c); break;
                case ElementKind.Docked: Docked(c); break;
                case ElementKind.Charge: Charge(e, c); break;
                case ElementKind.Oxygen: Oxygen(e, c); break;
                case ElementKind.Amount: Amount(e, c); break;
                case ElementKind.PowerUsed: PowerUsed(e, c); break;
                case ElementKind.Clock: Clock(e); break;
                case ElementKind.Distance: Distance(e, c); break;
                case ElementKind.Property: Property(e, c); break;
                case ElementKind.Queue: Queue(e, c); break;
                case ElementKind.Magazyn: MagazynState(e); break;
            }
        }

        // ------------------------------------------------------------------ ładownie i przedmioty

        void Items(ElementCfg e, Ctx c)
        {
            var hidden = new HashSet<string>(e.Hidden ?? new List<string>());
            IEnumerable<ItemAmount> list = c.ItemsIn(e.Filter, e.OnlyCargo).Values
                .Where(i => (e.Category == ItemCat.All || i.Cat == e.Category) && i.Amount > 0.0001 && !hidden.Contains(i.Key));
            if (e.OnlyMissing && e.Quota > 0) list = list.Where(i => i.Amount < e.Quota);
            list = e.SortByName ? list.OrderBy(i => i.Name) : list.OrderByDescending(i => i.Amount);
            int n = 0;
            foreach (var i in list)
            {
                bool low = e.Quota > 0 && i.Amount < e.Quota;
                Pair(i.Name + ":", Fmt.Amount(i.Amount) + (e.Quota > 0 && e.ShowBars ? " / " + Fmt.Amount(e.Quota) : ""), low ? warn : value);
                if (e.ShowBars && e.Quota > 0) Bar(i.Amount / e.Quota, low ? warn : (Color?)null);
                n++;
                if (e.MaxLines > 0 && n >= e.MaxLines) break;
            }
            if (n == 0) Text(e.OnlyMissing ? T("(niczego nie brakuje)", "(nothing missing)") : T("(brak)", "(none)"), text);
        }

        void Cargo(ElementCfg e, Ctx c)
        {
            var blocks = c.Containers(e.Text);
            string label = string.IsNullOrEmpty(e.Label) ? T("Ładownia", "Cargo") : e.Label;
            if (blocks.Count == 0) { Pair(label + ":", T("brak kontenerów", "no containers"), warn); return; }
            Ctx.Volume(blocks, out var cur, out var max);
            double f = max > 0 ? cur / max : 0;
            Pair(label + ":", Fmt.Volume(cur) + " / " + Fmt.Volume(max));
            Bar(f, f >= 0.95 ? warn : (Color?)null);
        }

        void Tank(ElementCfg e, Ctx c)
        {
            string gas = e.Gas == "Oxygen" ? "Oxygen" : "Hydrogen";
            string label = string.IsNullOrEmpty(e.Label) ? (gas == "Oxygen" ? T("Tlen", "Oxygen") : T("Wodór", "Hydrogen")) : e.Label;
            c.Tanks(gas, out var stored, out var cap, out var n);
            if (n == 0) { Pair(label + ":", T("brak zbiorników", "no tanks"), warn); return; }
            double f = cap > 0 ? stored / cap : 0;
            Pair(label + ":", Fmt.Volume(stored) + " / " + Fmt.Volume(cap));
            Bar(f, f <= 0.1 ? warn : (Color?)null);
        }

        void Amount(ElementCfg e, Ctx c)
        {
            // zawartość wybranych bloków (amunicja w wieżyczkach, uran w reaktorach, lód w generatorach)
            string itemFilter = (e.Text ?? "").Trim();
            var blocks = c.Match(e.Filter).Where(b => b.InventoryCount > 0).ToList();
            if (blocks.Count == 0) { Text(T("Brak bloków: ", "No blocks: ") + e.Filter, warn); return; }
            var sum = new Dictionary<string, double>();
            int shown = 0;
            foreach (var b in blocks)
            {
                double total = 0;
                for (int i = 0; i < b.InventoryCount; i++)
                {
                    var inv = b.GetInventory(i);
                    if (inv == null) continue;
                    foreach (var it in inv.GetItems())
                    {
                        var id = new VRage.Game.MyDefinitionId(it.Content.TypeId, it.Content.SubtypeId);
                        string name = Ctx.ItemName(id);
                        if (itemFilter.Length > 0 && name.IndexOf(itemFilter, StringComparison.OrdinalIgnoreCase) < 0 &&
                            id.SubtypeName.IndexOf(itemFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        double a = (double)(float)it.Amount;
                        total += a;
                        sum.TryGetValue(name, out var have);
                        sum[name] = have + a;
                    }
                }
                if (e.Each && (e.MaxLines <= 0 || shown < e.MaxLines))
                {
                    Pair(Ctx.NameOf(b) + ":", Fmt.Amount(total), total <= 0 ? warn : value);
                    shown++;
                }
            }
            if (!e.Each)
            {
                if (sum.Count == 0) Pair(T("Zawartość:", "Contents:"), T("pusto", "empty"), warn);
                foreach (var kv in sum.OrderByDescending(k => k.Value)) Pair(kv.Key + ":", Fmt.Amount(kv.Value));
            }
        }

        // ------------------------------------------------------------------ moc

        void PowerSummary(Ctx c)
        {
            var p = c.Power;
            Pair(T("Moc:", "Power:"), Fmt.Power(p.ProdCur) + " / " + Fmt.Power(p.ProdMax), p.ProdMax > 0 && p.ProdCur >= p.ProdMax * 0.95 ? warn : value);
        }

        void PowerTime(Ctx c)
        {
            var p = c.Power;
            double net = p.BattOut - p.BattIn;
            if (p.Batteries > 0)
            {
                if (net > 0.0001) Pair(T("Czas baterii:", "Battery time:"), Fmt.Time(p.BattStored / net * 3600.0), p.BattStored / net < 0.5 ? warn : value);
                else if (net < -0.0001) Pair(T("Baterie:", "Batteries:"), T("ładowanie, pełne za ", "charging, full in ") + Fmt.Time((p.BattStoredMax - p.BattStored) / -net * 3600.0));
                else Pair(T("Baterie:", "Batteries:"), T("bez poboru", "idle"));
            }
            if (p.Reactors > 0)
            {
                string fuel = Fmt.Mass(p.UraniumKg);
                if (p.ReactorFuelRate > 1e-9) Pair(T("Paliwo reaktorów:", "Reactor fuel:"), fuel + " = " + Fmt.Time(p.UraniumKg / p.ReactorFuelRate));
                else Pair(T("Paliwo reaktorów:", "Reactor fuel:"), fuel);
            }
            if (p.Batteries == 0 && p.Reactors == 0) Text(T("Czas zasilania: brak baterii i reaktorów", "Power time: no batteries or reactors"), text);
        }

        void PowerDetails(Ctx c)
        {
            var p = c.Power;
            if (p.Reactors > 0) Pair(T($"Reaktory ({p.Reactors}):", $"Reactors ({p.Reactors}):"), Fmt.Power(p.ReactorCur) + " / " + Fmt.Power(p.ReactorMax));
            if (p.Engines > 0) Pair(T($"Silniki H2 ({p.Engines}):", $"H2 engines ({p.Engines}):"), Fmt.Power(p.EngineCur) + " / " + Fmt.Power(p.EngineMax));
            if (p.Solars > 0) Pair(T($"Panele słoneczne ({p.Solars}):", $"Solar panels ({p.Solars}):"), Fmt.Power(p.SolarCur) + " / " + Fmt.Power(p.SolarMax));
            if (p.Winds > 0) Pair(T($"Turbiny wiatrowe ({p.Winds}):", $"Wind turbines ({p.Winds}):"), Fmt.Power(p.WindCur) + " / " + Fmt.Power(p.WindMax));
            if (p.Others > 0) Pair(T($"Inne źródła ({p.Others}):", $"Other sources ({p.Others}):"), Fmt.Power(p.OtherCur) + " / " + Fmt.Power(p.OtherMax));
            if (p.Batteries > 0)
            {
                Pair(T($"Baterie ({p.Batteries}):", $"Batteries ({p.Batteries}):"), T("WE ", "IN ") + Fmt.Power(p.BattIn) + T(" / WY ", " / OUT ") + Fmt.Power(p.BattOut));
                Pair(T("   Zapas:", "   Stored:"), Fmt.Energy(p.BattStored) + " / " + Fmt.Energy(p.BattStoredMax));
                Pair(T("   Wyjście:", "   Output:"), Fmt.Power(p.BattOut) + " / " + Fmt.Power(p.BattMaxOut));
                Pair(T("   Wejście:", "   Input:"), Fmt.Power(p.BattIn) + " / " + Fmt.Power(p.BattMaxIn));
            }
            Pair(T("Razem:", "Total:"), Fmt.Power(p.ProdCur) + " / " + Fmt.Power(p.ProdMax));
        }

        void PowerBalance(Ctx c)
        {
            var p = c.Power;
            double prodNoBatt = p.ProdCur - p.BattOut;
            double maxNoBatt = p.ProdMax - p.BattMaxOut;
            Pair(T("Produkcja prądu:", "Power output:"), Fmt.Power(prodNoBatt) + " / " + Fmt.Power(maxNoBatt));
            if (p.BattOut > 0.0001) Pair(T("Z baterii:", "From batteries:"), Fmt.Power(p.BattOut));
            if (p.Ions > 0) Pair(T($"Silniki jonowe ({p.Ions}):", $"Ion thrusters ({p.Ions}):"), Fmt.Power(p.IonCur) + " / " + Fmt.Power(p.IonMax));
            if (p.Atmos > 0) Pair(T($"Silniki atmosf. ({p.Atmos}):", $"Atmo thrusters ({p.Atmos}):"), Fmt.Power(p.AtmoCur) + " / " + Fmt.Power(p.AtmoMax));
            if (p.ElecThrOthers > 0) Pair(T($"Inne silniki ({p.ElecThrOthers}):", $"Other thrusters ({p.ElecThrOthers}):"), Fmt.Power(p.ElecThrOtherCur) + " / " + Fmt.Power(p.ElecThrOtherMax));
            Pair(T("Reszta odbiorników:", "Other consumers:"), Fmt.Power(p.OtherLoad));
            if (p.BattIn > 0.0001) Pair(T("Ładowanie baterii:", "Battery charging:"), Fmt.Power(p.BattIn));
            double thrMax = p.IonMax + p.AtmoMax + p.ElecThrOtherMax;
            if (thrMax > 0)
            {
                double need = thrMax + p.OtherLoad;
                double avail = maxNoBatt + p.BattMaxOut;
                Pair(T("Pełny ciąg potrzebuje:", "Full thrust needs:"), Fmt.Power(need) + " / " + Fmt.Power(avail), need > avail ? warn : value);
            }
        }

        void HydrogenBalance(Ctx c)
        {
            var p = c.Power;
            c.Tanks("Hydrogen", out var stored, out var cap, out var tanks);
            if (p.Generators == 0 && p.H2Thrusters == 0 && p.Engines == 0 && tanks == 0) return;
            if (p.Generators > 0) Pair(T($"Generatory H2 ({p.Generators}):", $"H2 generators ({p.Generators}):"), Fmt.Rate(p.GenH2Cur) + " / " + Fmt.Rate(p.GenH2Max));
            if (p.H2Thrusters > 0) Pair(T($"Silniki wodorowe ({p.H2Thrusters}):", $"H2 thrusters ({p.H2Thrusters}):"), Fmt.Rate(p.ThrH2Cur) + " / " + Fmt.Rate(p.ThrH2Max));
            if (p.Engines > 0) Pair(T("Silniki H2 (prąd):", "H2 engines (power):"), Fmt.Rate(p.EngH2Cur));
            double use = p.ThrH2Cur + p.EngH2Cur;
            double bal = p.GenH2Cur - use;
            if (p.Generators > 0 || use > 0) Pair(T("Bilans wodoru:", "H2 balance:"), (bal >= 0 ? "+" : "") + Fmt.Rate(bal), bal < -0.001 ? warn : good);
            if (tanks > 0)
            {
                Pair(T($"Zbiorniki H2 ({tanks}):", $"H2 tanks ({tanks}):"), Fmt.Pct(cap > 0 ? stored / cap : 0) + "  " + Fmt.Volume(stored));
                if (bal < -0.001) Pair(T("Wodoru starczy na:", "H2 lasts:"), Fmt.Time(stored / -bal), stored / -bal < 300 ? warn : value);
                double maxUse = p.ThrH2Max + p.EngH2Cur - p.GenH2Max;
                if (maxUse > 0.001 && p.H2Thrusters > 0) Pair(T("Na pełnym ciągu:", "At full thrust:"), Fmt.Time(stored / maxUse));
            }
        }

        void PowerUsed(ElementCfg e, Ctx c)
        {
            var blocks = c.Match(e.Filter);
            double cur = 0, max = 0;
            var each = new List<KeyValuePair<string, double>>();
            foreach (var b in blocks)
            {
                double m;
                double v = Ctx.PowerIn(b, out m);
                if (m <= 0 && v <= 0) continue;
                cur += v; max += m;
                each.Add(new KeyValuePair<string, double>(Ctx.NameOf(b), v));
            }
            string label = string.IsNullOrEmpty(e.Label) ? T("Pobór", "Used") + " {" + e.Filter + "}:" : e.Label;
            Pair(label, Fmt.Power(cur) + " / " + Fmt.Power(max));
            if (e.ShowBars) Bar(max > 0 ? cur / max : 0);
            if (e.Each)
            {
                var sorted = each.OrderByDescending(k => k.Value).ToList();
                for (int i = 0; i < Limit(e, sorted.Count); i++) Pair("  " + sorted[i].Key + ":", Fmt.Power(sorted[i].Value));
            }
        }

        void Charge(ElementCfg e, Ctx c)
        {
            double cur = 0, max = 0;
            int n = 0, shown = 0;
            foreach (var b in c.Match(e.Filter))
            {
                double bc, bm;
                if (b is Ing.IMyJumpDrive j) { bc = j.CurrentStoredPower; bm = j.MaxStoredPower; }
                else if (b is Ing.IMyBatteryBlock bat) { bc = bat.CurrentStoredPower; bm = bat.MaxStoredPower; }
                else continue;
                n++; cur += bc; max += bm;
                if (e.Each && (e.MaxLines <= 0 || shown < e.MaxLines))
                {
                    Pair(Ctx.NameOf(b) + ":", Fmt.Pct(bm > 0 ? bc / bm : 0));
                    if (e.ShowBars) Bar(bm > 0 ? bc / bm : 0);
                    shown++;
                }
            }
            if (n == 0) { Text(T("Brak napędów/baterii: ", "No jump drives/batteries: ") + e.Filter, warn); return; }
            if (!e.Each)
            {
                string label = string.IsNullOrEmpty(e.Label) ? T($"Naładowanie ({n}):", $"Charge ({n}):") : e.Label;
                Pair(label, Fmt.Energy(cur) + " / " + Fmt.Energy(max));
                if (e.ShowBars) Bar(max > 0 ? cur / max : 0);
                else Pair("", Fmt.Pct(max > 0 ? cur / max : 0));
            }
        }

        // ------------------------------------------------------------------ silniki i lot

        void Thrust(ElementCfg e, Ctx c)
        {
            var t = c.Thrust;
            if (t.Count == 0) return;
            Pair(T("Masa statku:", "Ship mass:"), Fmt.Mass(t.Mass));
            Pair(T("Ciąg teraz:", "Thrust now:"), Fmt.Force(t.Current) + " / " + Fmt.Force(t.MaxTotal));
            int[] order = e.Compact ? new[] { 0, 2 } : new[] { 0, 1, 2, 3, 4, 5 };
            var dirs = Ctx.DirNames;
            foreach (int i in order)
            {
                double f = t.Max[i];
                string s = Fmt.Force(f);
                Color col = value;
                if (t.Mass > 0)
                {
                    double a = f / t.Mass;
                    s += "  " + a.ToString("0.0", Inv) + " m/s²";
                    if (i == 2 && t.Gravity > 0.05)
                    {
                        double lift = f / (t.Mass * t.Gravity);
                        s += "  " + lift.ToString("0.00", Inv) + "x";
                        if (lift < 1.0) col = warn;
                    }
                }
                Pair(dirs[i] + ":", s, col);
            }
            if (t.Gravity > 0.05)
                Pair(T("Grawitacja:", "Gravity:"), (t.Gravity / 9.81).ToString("0.00", Inv) + " g");
        }

        void Ship(ElementCfg e, Ctx c)
        {
            var s = c.Ship;
            if (e.ShowSpeed)
                Pair(T("Prędkość:", "Speed:"), e.Kmh ? (s.Speed * 3.6).ToString("0", Inv) + " km/h" : s.Speed.ToString("0.0", Inv) + " m/s");
            if (e.ShowAccel) Pair(T("Przyspieszenie:", "Acceleration:"), s.Accel.ToString("0.0", Inv) + " m/s²");
            if (e.ShowAlt)
            {
                if (s.AltSurface.HasValue) Pair(T("Wysokość (grunt):", "Altitude (surface):"), Fmt.Distance(s.AltSurface.Value));
                if (s.AltSea.HasValue) Pair(T("Wysokość (morze):", "Altitude (sea level):"), Fmt.Distance(s.AltSea.Value));
            }
            if (e.ShowGrav)
            {
                if (s.NatG > 0.01) Pair(T("Grawitacja:", "Gravity:"), (s.NatG / 9.81).ToString("0.00", Inv) + " g");
                if (s.ArtG > 0.01) Pair(T("Sztuczna grawitacja:", "Artificial gravity:"), (s.ArtG / 9.81).ToString("0.00", Inv) + " g");
            }
            if (e.ShowMass)
            {
                Pair(T("Masa:", "Mass:"), Fmt.Mass(s.PhysMass));
                if (s.TotalMass - s.BaseMass > 1) Pair(T("   ładunek:", "   cargo:"), Fmt.Mass(s.TotalMass - s.BaseMass));
            }
            if (e.ShowStop && s.Speed > 0.1)
            {
                if (s.StopDist >= 0) Pair(T("Hamowanie:", "Stopping:"), Fmt.Distance(s.StopDist) + "  " + Fmt.Time(s.StopTime));
                else Pair(T("Hamowanie:", "Stopping:"), T("brak ciągu do tyłu", "no braking thrust"), warn);
            }
            if (e.ShowDamp)
            {
                Pair(T("Tłumiki:", "Dampeners:"), s.Dampeners ? T("wł.", "on") : T("wył.", "off"), s.Dampeners ? value : warn);
                if (s.Handbrake) Pair(T("Hamulec ręczny:", "Handbrake:"), T("zaciągnięty", "on"), warn);
            }
            if (e.ShowPos)
                Pair(T("Pozycja:", "Position:"), $"{s.Pos.X:0} {s.Pos.Y:0} {s.Pos.Z:0}");
        }

        void Docked(Ctx c)
        {
            var list = c.DockedGrids(out int gears);
            if (list.Count == 0) Pair(T("Zadokowane:", "Docked:"), T("nic", "nothing"));
            else
            {
                Pair(T("Zadokowane:", "Docked:"), list.Count.ToString(Inv));
                foreach (var n in list) Text("  " + n, value);
            }
            if (gears > 0) Pair(T("Podwozia zablokowane:", "Landing gears locked:"), gears.ToString(Inv));
        }

        void Distance(ElementCfg e, Ctx c)
        {
            if (!Ctx.ParseGps(e.Text, out var name, out var pos)) { Text(T("Zły GPS: ", "Bad GPS: ") + e.Text, warn); return; }
            var s = c.Ship;
            double d = Vector3D.Distance(s.Pos, pos);
            string label = string.IsNullOrEmpty(e.Label) ? (string.IsNullOrEmpty(name) ? "GPS" : name) + ":" : e.Label;
            string r = Fmt.Distance(d);
            if (s.Speed > 1) r += "  " + Fmt.Time(d / s.Speed);
            Pair(label, r);
        }

        // ------------------------------------------------------------------ tlen

        void Oxygen(ElementCfg e, Ctx c)
        {
            var blocks = c.Match(e.Filter);
            int vents = 0; double level = 0; int farms = 0; double farmOut = 0; int shown = 0;
            foreach (var b in blocks)
            {
                if (b is SpaceEngineers.Game.ModAPI.Ingame.IMyAirVent v)
                {
                    vents++;
                    double l = v.GetOxygenLevel();
                    level += l;
                    if (e.Each && (e.MaxLines <= 0 || shown < e.MaxLines))
                    {
                        Pair(Ctx.NameOf(b) + ":", v.CanPressurize ? Fmt.Pct(l) : T("nieszczelne", "leaking"), v.CanPressurize ? value : warn);
                        if (e.ShowBars) Bar(l);
                        shown++;
                    }
                }
                else if (b is SpaceEngineers.Game.ModAPI.Ingame.IMyOxygenFarm f) { farms++; farmOut += f.GetOutput(); }
            }
            if (vents > 0 && !e.Each)
            {
                Pair(T($"Ciśnienie ({vents} went.):", $"Pressure ({vents} vents):"), Fmt.Pct(level / vents));
                if (e.ShowBars) Bar(level / vents);
            }
            if (farms > 0) Pair(T($"Farmy tlenu ({farms}):", $"Oxygen farms ({farms}):"), Fmt.Pct(farmOut / farms));
            c.Tanks("Oxygen", out var st, out var cap, out var tanks);
            if (tanks > 0)
            {
                Pair(T($"Zbiorniki O2 ({tanks}):", $"O2 tanks ({tanks}):"), Fmt.Volume(st) + " / " + Fmt.Volume(cap));
                if (e.ShowBars) Bar(cap > 0 ? st / cap : 0);
            }
            if (vents == 0 && farms == 0 && tanks == 0) Text(T("Brak wentylacji i zbiorników tlenu", "No air vents or oxygen tanks"), text);
        }

        // ------------------------------------------------------------------ produkcja

        void Refineries(Ctx c)
        {
            var r = c.Refinery;
            if (r.Count == 0) { Text(T("Rafinerie: brak", "Refineries: none"), text); return; }
            string head = (r.Working < r.Count && r.Working > 0 ? $"{r.Working}/{r.Count}" : r.Count.ToString()) + T("x Rafinerie", "x Refineries");
            if (r.Ores.Count == 0) { Pair(head, T("puste", "empty"), warn); return; }
            string ores = string.Join(" ", r.Ores.OrderByDescending(kv => kv.Value).Select(kv => Fmt.Symbol(kv.Key)));
            Pair(head + "  " + Fmt.TimeShort(r.MaxSeconds), ores);
        }

        void Queue(ElementCfg e, Ctx c)
        {
            var q = c.Queue(e.Filter);
            if (q.Count == 0) { Pair(T("Kolejka:", "Queue:"), T("pusta", "empty")); return; }
            for (int i = 0; i < Limit(e, q.Count); i++) Pair(q[i].Key + ":", Fmt.Amount(q[i].Value));
            if (e.MaxLines > 0 && q.Count > e.MaxLines) Text(T($"  ... i jeszcze {q.Count - e.MaxLines}", $"  ... and {q.Count - e.MaxLines} more"), text);
        }

        /// <summary>Wpisy autocraftingu z limitów Magazynu (pluginu) — format jak u Isy.</summary>
        static List<AutoEntry> PluginAuto(Ctx c)
        {
            var m = Plugin.ActiveProfile?.Magazyn;
            if (m == null || m.Quotas == null || m.Quotas.Count == 0) return null;
            var items = c.Items(false);
            var res = new List<AutoEntry>();
            foreach (var q in m.Quotas.Where(x => x.Amount > 0))
            {
                if (!VRage.Game.MyDefinitionId.TryParse(q.Id, out var id)) continue;
                items.TryGetValue(q.Id, out var have);
                double cur = have?.Amount ?? 0;
                Magazyn.I.QueuedNow.TryGetValue(q.Id, out var queued);
                res.Add(new AutoEntry
                {
                    Name = Ctx.ItemName(id), Current = Math.Floor(cur), Wanted = q.Amount,
                    Op = cur > q.Amount ? ">" : cur < q.Amount ? "<" : "=",
                    Extra = queued > 0 ? "+" + Fmt.Amount(queued) : "",
                });
            }
            return res.OrderBy(a => a.Name).ToList();
        }

        void MagazynState(ElementCfg e)
        {
            var m = Plugin.ActiveProfile?.Magazyn;
            var g = Magazyn.I;
            if (m == null || !m.Enabled) { Pair(T("Magazyn:", "Storage:"), T("wyłączony", "off")); return; }
            Pair(T("Magazyn:", "Storage:"), g.State, string.IsNullOrEmpty(g.Warning) ? value : warn);
            if (!string.IsNullOrEmpty(g.Warning)) Text(g.Warning, warn);
            Pair(T("Przeniesienia / kolejka:", "Moves / queued:"), g.Moved + " / " + g.Queued);
            if (!string.IsNullOrEmpty(g.ScriptName)) Pair(T("Rafinerie:", "Refineries:"), T("skrypt PB ", "PB script ") + g.ScriptName);
            else if (!string.IsNullOrEmpty(g.PrimaryOre)) Pair(T("Rafinerie najpierw:", "Refining first:"), g.PrimaryOre);
            if (g.Unreachable.Count > 0)
                Text(T("Bez konwejera: ", "No conveyor: ") + string.Join(", ", g.Unreachable.Take(4)) + (g.Unreachable.Count > 4 ? " ..." : ""), warn);
            for (int i = 0; i < Limit(e, g.Recent.Count); i++) Text("  " + g.Recent[i], text);
        }

        void Autocrafting(ElementCfg e, Ctx c)
        {
            if (e.FromPlugin)
            {
                var own = PluginAuto(c);
                if (own == null) { Text(T("Brak limitów w Magazynie (/data » Magazyn)", "No limits in Storage (/data » Storage)"), warn); return; }
                AutoRows(e, own);
                return;
            }
            var list = c.Autocrafting(e.Text);
            if (list == null) { Text(T("Brak LCD z '", "No LCD with '") + e.Text + T("' w nazwie", "' in name"), warn); return; }
            AutoRows(e, list);
        }

        void AutoRows(ElementCfg e, List<AutoEntry> list)
        {
            IEnumerable<AutoEntry> src = list;
            if (e.OnlyMissing) src = src.Where(a => a.Current < a.Wanted);
            var all = src.ToList();
            int parts = Math.Max(1, e.Parts);
            int per = (all.Count + parts - 1) / parts;
            int start = Math.Min(all.Count, Math.Max(0, e.Part) * per);
            int end = Math.Min(all.Count, start + per);
            if (all.Count == 0 && e.Part == 0) { Text(e.OnlyMissing ? T("Wszystko jest", "Nothing missing") : T("(pusto)", "(empty)"), text); return; }
            for (int i = start; i < end; i++)
            {
                var a = all[i];
                string name = e.GameNames ? Ctx.GameName(a.Name) : a.Name;
                string right = a.Current.ToString("0.##", Inv) + " " + a.Op + " " + a.Wanted.ToString("0.##", Inv);
                if (!string.IsNullOrEmpty(a.Extra)) right += " " + a.Extra;
                Pair(name, right, a.Current < a.Wanted ? warn : value);
            }
        }

        // ------------------------------------------------------------------ bloki

        void Details(ElementCfg e, Ctx c)
        {
            var blocks = c.Match(e.Filter);
            if (string.IsNullOrWhiteSpace(e.Filter) || e.Filter.Trim() == "*") { Text(T("Details: podaj nazwę bloku", "Details: enter a block name"), warn); return; }
            if (blocks.Count == 0) { Text(T("Brak bloku: ", "No block: ") + e.Filter, warn); return; }
            bool first = true;
            foreach (var b in blocks)
            {
                if (!e.NoName)
                {
                    if (!first) Space();
                    Text(Ctx.NameOf(b), title);
                }
                first = false;
                var lines = Ctx.InfoLines(b, e.RefreshInfo);
                bool started = string.IsNullOrEmpty(e.StartText);
                int n = 0;
                for (int i = e.SkipFirst ? 1 : 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (line.Trim().Length == 0) continue;
                    if (!started)
                    {
                        if (line.IndexOf(e.StartText, StringComparison.Ordinal) < 0) continue;
                        started = true;
                    }
                    if (!string.IsNullOrEmpty(e.StopText) && line.IndexOf(e.StopText, StringComparison.Ordinal) >= 0) break;
                    Line(line.Trim(), e.AlignNumbers, text);
                    n++;
                    if (e.MaxLines > 0 && n >= e.MaxLines) break;
                }
            }
        }

        void CustomData(ElementCfg e, Ctx c)
        {
            var blocks = c.Match(e.Filter);
            if (blocks.Count == 0) { Text(T("Brak bloku: ", "No block: ") + e.Filter, warn); return; }
            foreach (var b in blocks)
            {
                if (!e.NoName) Text(Ctx.NameOf(b), title);
                string data = (b as Ing.IMyTerminalBlock)?.CustomData ?? "";
                int n = 0;
                foreach (var raw in data.Replace("\r", "").TrimEnd('\n', ' ').Split('\n'))
                {
                    if (raw.Trim().Length == 0) { Space(); continue; }
                    Line(raw, e.AlignNumbers, text);
                    if (e.MaxLines > 0 && ++n >= e.MaxLines) break;
                }
            }
        }

        void Working(ElementCfg e, Ctx c)
        {
            var blocks = c.Match(e.Filter);
            if (blocks.Count == 0) { Text(T("Brak bloków: ", "No blocks: ") + e.Filter, warn); return; }
            int shown = 0, problems = 0;
            foreach (var b in blocks)
            {
                string st = Ctx.State(b, out bool bad);
                if (bad) problems++;
                if (e.OnlyProblems && !bad) continue;
                if (e.MaxLines > 0 && shown >= e.MaxLines) continue;
                Pair(Ctx.NameOf(b) + ":", st, bad ? warn : value);
                shown++;
            }
            if (e.OnlyProblems && problems == 0)
                Pair(string.IsNullOrEmpty(e.Label) ? "{" + e.Filter + "}:" : e.Label, T($"wszystkie działają ({blocks.Count})", $"all working ({blocks.Count})"));
        }

        void Count(ElementCfg e, Ctx c)
        {
            var groups = c.Match(e.Filter).GroupBy(Ctx.TypeOf).OrderBy(g => g.Key);
            int n = 0;
            foreach (var g in groups)
            {
                int total = g.Count();
                int on = g.Count(b => b.IsWorking);
                string r = on == total ? total.ToString(Inv) : on + " / " + total;
                Pair(g.Key + ":", r, on < total ? warn : value);
                if (e.MaxLines > 0 && ++n >= e.MaxLines) break;
            }
            if (n == 0) Text(T("Brak bloków: ", "No blocks: ") + e.Filter, warn);
        }

        void Damage(ElementCfg e, Ctx c)
        {
            float limit = Math.Max(1f, Math.Min(100f, e.Threshold)) / 100f;
            var damaged = new List<KeyValuePair<MyCubeBlock, float>>();
            foreach (var b in c.Match(e.Filter))
            {
                float r = Ctx.IntegrityOf(b);
                if (r < limit - 0.0001f) damaged.Add(new KeyValuePair<MyCubeBlock, float>(b, r));
            }
            if (damaged.Count == 0) { Pair(T("Uszkodzenia:", "Damage:"), T("brak", "none")); return; }
            Pair(T("Uszkodzone bloki:", "Damaged blocks:"), damaged.Count.ToString(Inv), warn);
            var sorted = damaged.OrderBy(k => k.Value).ToList();
            for (int i = 0; i < Limit(e, sorted.Count); i++)
            {
                Pair("  " + Ctx.NameOf(sorted[i].Key) + ":", Fmt.Pct(sorted[i].Value), warn);
                if (e.ShowBars) Bar(sorted[i].Value, warn);
            }
        }

        void Property(ElementCfg e, Ctx c)
        {
            var blocks = c.Match(e.Filter);
            if (blocks.Count == 0) { Text(T("Brak bloku: ", "No block: ") + e.Filter, warn); return; }
            for (int i = 0; i < Limit(e, blocks.Count); i++)
            {
                string label = string.IsNullOrEmpty(e.Label) ? Ctx.NameOf(blocks[i]) + ":" : (blocks.Count > 1 ? e.Label + " " + (i + 1) + ":" : e.Label);
                Pair(label, Ctx.PropertyValue(blocks[i], e.Property));
            }
        }

        void Clock(ElementCfg e)
        {
            string s;
            try { s = DateTime.Now.ToString(string.IsNullOrEmpty(e.Text) ? "HH:mm" : e.Text, L.En ? CultureInfo.GetCultureInfo("en-GB") : CultureInfo.GetCultureInfo("pl-PL")); }
            catch { s = DateTime.Now.ToString("HH:mm"); }
            if (string.IsNullOrEmpty(e.Label)) Text(s, value, e.Center, e.Right);
            else Pair(e.Label, s);
        }

        void Lcd(ElementCfg e, Ctx c)
        {
            var panels = c.Panels(e.Text);
            if (panels.Count == 0) { Text(T("Brak LCD z '", "No LCD with '") + e.Text + T("' w nazwie", "' in name"), warn); return; }
            foreach (var p in panels)
            {
                string t;
                try { t = p.GetText() ?? ""; } catch { continue; }
                foreach (var raw in t.TrimEnd('\n', '\r', ' ').Split('\n'))
                {
                    var line = raw.TrimEnd('\r');
                    if (line.Trim().Length == 0) Space();
                    else Line(line, e.AlignNumbers, text);
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
                    case ElementKind.Header: b.Text(e.Text ?? "", b.title, e.Center, e.Right); break;
                    case ElementKind.Blank: b.Space(); break;
                    case ElementKind.Items:
                        b.Pair(T("Przykład 1:", "Example 1:"), "13.9 k"); b.Pair(T("Przykład 2:", "Example 2:"), "1.9 M"); break;
                    case ElementKind.CargoBar: b.Pair((string.IsNullOrEmpty(e.Label) ? T("Ładownia", "Cargo") : e.Label) + ":", "10.5 ML / 33.8 ML"); b.Bar(0.31f); break;
                    case ElementKind.Tank: b.Pair((string.IsNullOrEmpty(e.Label) ? T("Wodór", "Hydrogen") : e.Label) + ":", "210 ML / 210 ML"); b.Bar(1f); break;
                    case ElementKind.PowerSummary: b.Pair(T("Moc:", "Power:"), "134.3 MW / 2.7 GW"); break;
                    case ElementKind.PowerTime: b.Pair(T("Czas baterii:", "Battery time:"), "1h 54m 47s"); break;
                    case ElementKind.PowerDetails: b.Pair(T("Reaktory (4):", "Reactors (4):"), "66.5 MW / 2.5 GW"); b.Pair(T("Razem:", "Total:"), "115.7 MW / 2.7 GW"); break;
                    case ElementKind.PowerBalance: b.Pair(T("Produkcja prądu:", "Power output:"), "66.5 MW / 2.5 GW"); b.Pair(T("Silniki jonowe (24):", "Ion thrusters (24):"), "12 MW / 80 MW"); break;
                    case ElementKind.HydrogenBalance: b.Pair(T("Bilans wodoru:", "H2 balance:"), "-1.2 kL/s", b.warn); break;
                    case ElementKind.Thrust: b.Pair(T("Masa statku:", "Ship mass:"), "1.2 kt"); b.Pair(Ctx.DirNames[2] + ":", "24 MN  20 m/s²  2.04x"); break;
                    case ElementKind.Refineries: b.Pair(T("10x Rafinerie  5h 12m", "10x Refineries  5h 12m"), "U Ag Si Ni"); break;
                    case ElementKind.Autocrafting:
                        b.Pair("SteelPlate", "200372 > 200000"); b.Pair("Motor", "1200 < 5000", b.warn); break;
                    case ElementKind.Ship: b.Pair(T("Prędkość:", "Speed:"), "98.4 m/s"); b.Pair(T("Hamowanie:", "Stopping:"), "1.2 km  24s"); break;
                    default: b.Text("[" + ElementCfg.KindName(e.Kind) + "]", b.text); break;
                }
            }
            return b.rows;
        }
    }
}
