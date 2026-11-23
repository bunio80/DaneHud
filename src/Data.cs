using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using Ing = Sandbox.ModAPI.Ingame;

namespace DaneHud
{
    internal class ItemAmount
    {
        public string Key;      // Id definicji, np. MyObjectBuilder_Ore/Iron
        public string Subtype;
        public string Name;
        public ItemCat Cat;
        public double Amount;
    }

    internal class AutoEntry
    {
        public string Name;
        public double Current, Wanted;
        public string Op;       // ">", "=", "<"
        public string Extra;    // modyfikatory i [A:...]
    }

    /// <summary>Stan grida, w którym siedzi gracz (kokpit/fotel/zdalne sterowanie). Liczone raz na odświeżenie, leniwie.</summary>
    internal class Ctx
    {
        public static readonly MyDefinitionId Elec = MyResourceDistributorComponent.ElectricityId;
        public static readonly MyDefinitionId H2 = MyDefinitionId.Parse("MyObjectBuilder_GasProperties/Hydrogen");

        public Ing.IMyShipController Controller;
        public IMyCubeGrid Grid;
        public readonly List<IMyCubeGrid> Grids = new List<IMyCubeGrid>();
        public readonly List<MyCubeBlock> Blocks = new List<MyCubeBlock>();
        public readonly HashSet<long> BlockIds = new HashSet<long>();

        Dictionary<string, ItemAmount> items, cargoItems;
        public Dictionary<string, ItemAmount> Items(bool onlyCargo) =>
            onlyCargo ? (cargoItems ?? (cargoItems = CountItems(true))) : (items ?? (items = CountItems(false)));

        public static Ctx Collect()
        {
            var ctrl = MyAPIGateway.Session?.ControlledObject as Ing.IMyShipController;
            var grid = (ctrl as IMyCubeBlock)?.CubeGrid;
            if (ctrl == null || grid == null) return null;

            var c = new Ctx { Controller = ctrl, Grid = grid };
            try { MyAPIGateway.GridGroups.GetGroup(grid, GridLinkTypeEnum.Mechanical, c.Grids); } catch { }
            if (c.Grids.Count == 0) c.Grids.Add(grid);
            foreach (var g in c.Grids)
            {
                var mg = g as MyCubeGrid;
                if (mg == null) continue;
                foreach (var b in mg.GetFatBlocks())
                {
                    if (b == null || b.Closed) continue;
                    c.Blocks.Add(b);
                    c.BlockIds.Add(b.EntityId);
                }
            }
            return c;
        }

        // ------------------------------------------------------------------ przedmioty

        internal static ItemCat CatOf(string typeId)
        {
            switch (typeId)
            {
                case "MyObjectBuilder_Ore": return ItemCat.Ore;
                case "MyObjectBuilder_Ingot": return ItemCat.Ingot;
                case "MyObjectBuilder_Component": return ItemCat.Component;
                case "MyObjectBuilder_AmmoMagazine": return ItemCat.Ammo;
                case "MyObjectBuilder_PhysicalGunObject": return ItemCat.Tool;
                case "MyObjectBuilder_OxygenContainerObject":
                case "MyObjectBuilder_GasContainerObject": return ItemCat.Bottle;
            }
            return ItemCat.Other;
        }

        static readonly Dictionary<MyDefinitionId, string> nameCache = new Dictionary<MyDefinitionId, string>();

        internal static string ItemName(MyDefinitionId id)
        {
            if (nameCache.TryGetValue(id, out var n)) return n;
            try
            {
                var d = MyDefinitionManager.Static.GetPhysicalItemDefinition(id);
                n = d?.DisplayNameText;
            }
            catch { }
            if (string.IsNullOrEmpty(n)) n = id.SubtypeName;
            nameCache[id] = n;
            return n;
        }

        Dictionary<string, ItemAmount> CountItems(bool onlyCargo)
        {
            var res = new Dictionary<string, ItemAmount>();
            foreach (var b in Blocks)
            {
                if (onlyCargo && !(b is Ing.IMyCargoContainer)) continue;
                int n = b.InventoryCount;
                for (int i = 0; i < n; i++)
                {
                    var inv = b.GetInventory(i);
                    if (inv == null) continue;
                    foreach (var it in inv.GetItems())
                    {
                        var id = new MyDefinitionId(it.Content.TypeId, it.Content.SubtypeId);
                        var key = id.ToString();
                        if (!res.TryGetValue(key, out var a))
                        {
                            a = new ItemAmount { Key = key, Subtype = id.SubtypeName, Name = ItemName(id), Cat = CatOf(id.TypeId.ToString()) };
                            res[key] = a;
                        }
                        a.Amount += (double)(float)it.Amount;
                    }
                }
            }
            return res;
        }

        // ------------------------------------------------------------------ kontenery

        IMyGridTerminalSystem gts;
        IMyGridTerminalSystem Gts
        {
            get
            {
                if (gts == null) try { gts = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(Grid); } catch { }
                return gts;
            }
        }

        /// <summary>Kontenery: grupa o dokładnie tej nazwie albo kontenery z tym tekstem w nazwie (puste = wszystkie).</summary>
        public List<MyCubeBlock> Containers(string filter)
        {
            var list = new List<MyCubeBlock>();
            if (!string.IsNullOrWhiteSpace(filter))
            {
                try
                {
                    var grp = Gts?.GetBlockGroupWithName(filter.Trim());
                    if (grp != null)
                    {
                        var tb = new List<Sandbox.ModAPI.Ingame.IMyTerminalBlock>();
                        grp.GetBlocks(tb);
                        foreach (var t in tb)
                            if (t is MyCubeBlock mb && BlockIds.Contains(mb.EntityId) && mb.InventoryCount > 0) list.Add(mb);
                        if (list.Count > 0) return list;
                    }
                }
                catch { }
            }
            foreach (var b in Blocks)
            {
                if (!(b is Ing.IMyCargoContainer)) continue;
                if (!string.IsNullOrWhiteSpace(filter))
                {
                    var name = (b as Ing.IMyTerminalBlock)?.CustomName ?? "";
                    if (name.IndexOf(filter.Trim(), StringComparison.OrdinalIgnoreCase) < 0) continue;
                }
                list.Add(b);
            }
            return list;
        }

        public static void Volume(List<MyCubeBlock> blocks, out double curL, out double maxL)
        {
            curL = 0; maxL = 0;
            foreach (var b in blocks)
            {
                var inv = b.GetInventory(0);
                if (inv == null) continue;
                curL += (double)(float)inv.CurrentVolume * 1000.0;
                maxL += (double)(float)inv.MaxVolume * 1000.0;
            }
        }

        // ------------------------------------------------------------------ zbiorniki

        public void Tanks(string gas, out double stored, out double capacity, out int count)
        {
            stored = 0; capacity = 0; count = 0;
            foreach (var b in Blocks)
            {
                if (!(b is Ing.IMyGasTank t)) continue;
                var def = b.BlockDefinition as MyGasTankDefinition;
                string g = def != null ? def.StoredGasId.SubtypeName : "";
                if (!string.Equals(g, gas, StringComparison.OrdinalIgnoreCase)) continue;
                count++;
                capacity += t.Capacity;
                stored += t.Capacity * t.FilledRatio;
            }
        }

        // ------------------------------------------------------------------ moc i wodór

        public class PowerInfo
        {
            public double ReactorCur, ReactorMax; public int Reactors;
            public double EngineCur, EngineMax; public int Engines;          // silniki wodorowe (generatory prądu)
            public double SolarCur, SolarMax; public int Solars;
            public double WindCur, WindMax; public int Winds;
            public double OtherCur, OtherMax; public int Others;
            public double BattOut, BattIn, BattMaxOut, BattMaxIn, BattStored, BattStoredMax; public int Batteries;
            public double ProdCur => ReactorCur + EngineCur + SolarCur + WindCur + OtherCur + BattOut;
            public double ProdMax => ReactorMax + EngineMax + SolarMax + WindMax + OtherMax + BattMaxOut;

            public double IonCur, IonMax; public int Ions;
            public double AtmoCur, AtmoMax; public int Atmos;
            public double ElecThrOtherCur, ElecThrOtherMax; public int ElecThrOthers;
            public double OtherLoad;                                          // reszta odbiorników prądu

            public double UraniumKg, ReactorFuelRate;                         // kg, kg/s

            public double GenH2Cur, GenH2Max; public int Generators;          // generatory O2/H2 (lód)
            public double ThrH2Cur, ThrH2Max; public int H2Thrusters;
            public double EngH2Cur, EngH2Max;                                 // silniki wodorowe (prąd) zużywają H2
        }

        PowerInfo power;
        public PowerInfo Power => power ?? (power = ComputePower());

        static string TypeName(MyCubeBlock b) => b.BlockDefinition?.Id.TypeId.ToString() ?? "";

        PowerInfo ComputePower()
        {
            var p = new PowerInfo();
            foreach (var b in Blocks)
            {
                if (!b.IsFunctional) continue;
                string type = TypeName(b);
                bool isEngine = type == "MyObjectBuilder_HydrogenEngine";

                // źródła prądu
                if (b is Ing.IMyBatteryBlock bat)
                {
                    p.Batteries++;
                    p.BattOut += bat.CurrentOutput;
                    p.BattIn += bat.CurrentInput;
                    p.BattStored += bat.CurrentStoredPower;
                    p.BattStoredMax += bat.MaxStoredPower;
                    var src = b.Components.Get<MyResourceSourceComponent>();
                    if (src != null && Has(src, Elec)) p.BattMaxOut += src.MaxOutputByType(Elec);
                    var bsink = b.Components.Get<MyResourceSinkComponent>();
                    if (bsink != null && bsink.AcceptedResources.Contains(Elec)) p.BattMaxIn += bsink.MaxRequiredInputByType(Elec);
                }
                else
                {
                    var src = b.Components.Get<MyResourceSourceComponent>();
                    if (src != null && Has(src, Elec))
                    {
                        double cur = src.CurrentOutputByType(Elec), max = src.MaxOutputByType(Elec);
                        if (b is Ing.IMyReactor)
                        {
                            p.Reactors++; p.ReactorCur += cur; p.ReactorMax += max;
                            ReactorFuel(b, cur, p);
                        }
                        else if (isEngine) { p.Engines++; p.EngineCur += cur; p.EngineMax += max; }
                        else if (type == "MyObjectBuilder_SolarPanel") { p.Solars++; p.SolarCur += cur; p.SolarMax += max; }
                        else if (type == "MyObjectBuilder_WindTurbine") { p.Winds++; p.WindCur += cur; p.WindMax += max; }
                        else { p.Others++; p.OtherCur += cur; p.OtherMax += max; }
                    }
                    if (src != null && b is Ing.IMyGasGenerator && Has(src, H2))
                    {
                        p.Generators++;
                        p.GenH2Cur += src.CurrentOutputByType(H2);
                        p.GenH2Max += src.MaxOutputByType(H2);
                    }
                }

                // odbiorcy
                var sink = b.Components.Get<MyResourceSinkComponent>();
                if (sink == null || b is Ing.IMyBatteryBlock) continue;
                if (b is Ing.IMyThrust)
                {
                    var td = b.BlockDefinition as MyThrustDefinition;
                    if (sink.AcceptedResources.Contains(H2))
                    {
                        p.H2Thrusters++;
                        p.ThrH2Cur += sink.CurrentInputByType(H2);
                        p.ThrH2Max += sink.MaxRequiredInputByType(H2);
                    }
                    else if (sink.AcceptedResources.Contains(Elec))
                    {
                        double cur = sink.CurrentInputByType(Elec), max = sink.MaxRequiredInputByType(Elec);
                        string tt = td?.ThrusterType.String ?? "";
                        if (tt.IndexOf("Ion", StringComparison.OrdinalIgnoreCase) >= 0) { p.Ions++; p.IonCur += cur; p.IonMax += max; }
                        else if (tt.IndexOf("Atmo", StringComparison.OrdinalIgnoreCase) >= 0) { p.Atmos++; p.AtmoCur += cur; p.AtmoMax += max; }
                        else { p.ElecThrOthers++; p.ElecThrOtherCur += cur; p.ElecThrOtherMax += max; }
                    }
                    continue;
                }
                if (isEngine && sink.AcceptedResources.Contains(H2))
                {
                    p.EngH2Cur += sink.CurrentInputByType(H2);
                    p.EngH2Max += sink.MaxRequiredInputByType(H2);
                }
                if (sink.AcceptedResources.Contains(Elec))
                    p.OtherLoad += sink.CurrentInputByType(Elec);
            }
            return p;
        }

        static bool Has(MyResourceSourceComponent src, MyDefinitionId id)
        {
            foreach (var t in src.ResourceTypes) if (t == id) return true;
            return false;
        }

        static void ReactorFuel(MyCubeBlock b, double curMw, PowerInfo p)
        {
            try
            {
                var def = b.BlockDefinition as MyReactorDefinition;
                if (def?.FuelInfos == null || def.FuelInfos.Length == 0) return;
                var fi = def.FuelInfos[0];
                var inv = b.GetInventory(0);
                if (inv != null) p.UraniumKg += (double)(float)inv.GetItemAmount(fi.FuelId);
                if (def.MaxPowerOutput > 0) p.ReactorFuelRate += curMw / def.MaxPowerOutput * fi.ConsumptionPerSecond_Items;
            }
            catch { }
        }

        // ------------------------------------------------------------------ ciąg

        public class ThrustInfo
        {
            public readonly double[] Max = new double[6];   // przód, tył, góra, dół, lewo, prawo (siła, która pcha statek w tę stronę)
            public double Current, MaxTotal;
            public double Mass, Gravity;
            public int Count;
        }

        public static readonly string[] DirNames = { "Przód", "Tył", "Góra", "Dół", "Lewo", "Prawo" };

        ThrustInfo thrust;
        public ThrustInfo Thrust => thrust ?? (thrust = ComputeThrust());

        ThrustInfo ComputeThrust()
        {
            var t = new ThrustInfo();
            var m = Controller.WorldMatrix;
            var axes = new[] { m.Forward, m.Backward, m.Up, m.Down, m.Left, m.Right };
            foreach (var b in Blocks)
            {
                if (!(b is Ing.IMyThrust th) || !b.IsFunctional) continue;
                t.Count++;
                Vector3D dir = b.WorldMatrix.Backward;   // siła działa przeciwnie do dyszy
                int best = 0; double bestDot = -2;
                for (int i = 0; i < 6; i++)
                {
                    double d = Vector3D.Dot(dir, axes[i]);
                    if (d > bestDot) { bestDot = d; best = i; }
                }
                double max = th.MaxEffectiveThrust;
                t.Max[best] += max;
                t.MaxTotal += max;
                t.Current += th.CurrentThrust;
            }
            try { t.Mass = Controller.CalculateShipMass().PhysicalMass; } catch { }
            try { t.Gravity = Controller.GetNaturalGravity().Length(); } catch { }
            return t;
        }

        // ------------------------------------------------------------------ rafinerie

        public class RefineryInfo
        {
            public int Count, Working;
            public double MaxSeconds;
            public readonly Dictionary<string, double> Ores = new Dictionary<string, double>();
        }

        RefineryInfo refinery;
        public RefineryInfo Refinery => refinery ?? (refinery = ComputeRefinery());

        static readonly Dictionary<string, MyBlueprintDefinitionBase> bpCache = new Dictionary<string, MyBlueprintDefinitionBase>();

        static MyBlueprintDefinitionBase FindBlueprint(MyRefineryDefinition def, MyDefinitionId ore)
        {
            string key = def.Id + "|" + ore;
            if (bpCache.TryGetValue(key, out var bp)) return bp;
            bp = null;
            try
            {
                foreach (var cls in def.BlueprintClasses)
                {
                    foreach (var x in cls)
                        if (x.Prerequisites != null && x.Prerequisites.Length == 1 && x.Prerequisites[0].Id == ore) { bp = x; break; }
                    if (bp != null) break;
                }
            }
            catch { }
            bpCache[key] = bp;
            return bp;
        }

        RefineryInfo ComputeRefinery()
        {
            var r = new RefineryInfo();
            float mult = 1f;
            try { mult = MySession.Static.RefinerySpeedMultiplier; } catch { }
            foreach (var b in Blocks)
            {
                if (!(b is Ing.IMyRefinery ref_) || !b.IsFunctional) continue;
                var def = b.BlockDefinition as MyRefineryDefinition;
                if (def == null) continue;
                r.Count++;
                if (ref_.IsWorking && ref_.IsProducing) r.Working++;

                float prod = 0f;
                try { if (b.UpgradeValues.TryGetValue("Productivity", out var pv)) prod = pv; } catch { }
                double speed = (def.RefineSpeed + prod) * mult;   // jak MyRefinery.ProcessQueueItems
                var inv = b.GetInventory(0);
                if (inv == null) continue;
                double secs = 0;
                foreach (var it in inv.GetItems())
                {
                    var id = new MyDefinitionId(it.Content.TypeId, it.Content.SubtypeId);
                    if (id.TypeId.ToString() != "MyObjectBuilder_Ore") continue;
                    double amount = (double)(float)it.Amount;
                    r.Ores.TryGetValue(id.SubtypeName, out var have);
                    r.Ores[id.SubtypeName] = have + amount;
                    var bp = FindBlueprint(def, id);
                    if (bp == null || speed <= 0) continue;
                    double per = (double)(float)bp.Prerequisites[0].Amount;
                    if (per <= 0) continue;
                    secs += amount / per * bp.BaseProductionTimeInSeconds / speed;
                }
                if (b is Ing.IMyFunctionalBlock fb && !fb.Enabled) continue;
                if (secs > r.MaxSeconds) r.MaxSeconds = secs;
            }
            return r;
        }

        // ------------------------------------------------------------------ LCD

        public List<Ing.IMyTextPanel> Panels(string keyword)
        {
            var list = new List<Ing.IMyTextPanel>();
            if (string.IsNullOrEmpty(keyword)) return list;
            foreach (var b in Blocks)
                if (b is Ing.IMyTextPanel tp && (tp.CustomName ?? "").Contains(keyword)) list.Add(tp);
            // jak u Isy: kolejność wg numeru po słowie kluczowym ("Autocrafting 1", "Autocrafting 2")
            var rx = new Regex("(" + Regex.Escape(keyword) + @" *)(\d*)");
            return list.OrderBy(p => { var g = rx.Match(p.CustomName ?? "").Groups[2].Value; return g.Length == 0 ? 0 : int.Parse(g); })
                       .ThenBy(p => p.CustomName).ToList();
        }

        static readonly Regex AutoLine = new Regex(@"^\s*(-?)(\S+)\s+(\d+(?:[.,]\d+)?)\s*([=<>])\s*(\d+(?:[.,]\d+)?)(.*)$");

        Dictionary<string, List<AutoEntry>> autoCache = new Dictionary<string, List<AutoEntry>>();

        /// <summary>Wpisy autocraftingu Isy z LCD z danym słowem w nazwie (null = brak takiego LCD).</summary>
        public List<AutoEntry> Autocrafting(string keyword)
        {
            if (autoCache.TryGetValue(keyword ?? "", out var cached)) return cached;
            List<AutoEntry> res = null;
            var panels = Panels(keyword);
            if (panels.Count > 0)
            {
                res = new List<AutoEntry>();
                var seen = new HashSet<string>();
                foreach (var p in panels)
                {
                    string text;
                    try { text = p.GetText() ?? ""; } catch { continue; }
                    foreach (var raw in text.Split('\n'))
                    {
                        var m = AutoLine.Match(raw.TrimEnd('\r'));
                        if (!m.Success || m.Groups[1].Value == "-") continue;
                        string name = m.Groups[2].Value;
                        if (!seen.Add(name)) continue;
                        res.Add(new AutoEntry
                        {
                            Name = name,
                            Current = ParseNum(m.Groups[3].Value),
                            Op = m.Groups[4].Value,
                            Wanted = ParseNum(m.Groups[5].Value),
                            Extra = m.Groups[6].Value.Trim(),
                        });
                    }
                }
            }
            autoCache[keyword ?? ""] = res;
            return res;
        }

        static double ParseNum(string s)
        {
            double.TryParse(s.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v);
            return v;
        }

        static readonly Dictionary<string, string> gameNameCache = new Dictionary<string, string>();

        /// <summary>Nazwa z gry dla nazwy używanej przez Isy (subtype), np. SteelPlate → Steel Plate.</summary>
        internal static string GameName(string subtype)
        {
            if (gameNameCache.TryGetValue(subtype, out var n)) return n;
            n = subtype;
            try
            {
                MyPhysicalItemDefinition best = null;
                foreach (var d in MyDefinitionManager.Static.GetPhysicalItemDefinitions())
                {
                    if (d.Id.SubtypeName != subtype) continue;
                    if (best == null || d.Id.TypeId.ToString() == "MyObjectBuilder_Component") best = d;
                }
                if (best != null && !string.IsNullOrEmpty(best.DisplayNameText)) n = best.DisplayNameText;
            }
            catch { }
            gameNameCache[subtype] = n;
            return n;
        }
    }
}
