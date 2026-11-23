using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;
using VRageMath;
using Ing = Sandbox.ModAPI.Ingame;

namespace DaneHud
{
    /// <summary>Wyszukiwanie bloków i dane dla poleceń z Automatic LCDs 2 (Details, Working, Count, Damage, Charge...).</summary>
    internal partial class Ctx
    {
        public static string NameOf(MyCubeBlock b) =>
            (b as Ing.IMyTerminalBlock)?.CustomName ?? b.DisplayNameText ?? b.BlockDefinition?.DisplayNameText ?? "?";

        public static string TypeOf(MyCubeBlock b) => b.BlockDefinition?.DisplayNameText ?? b.BlockDefinition?.Id.SubtypeName ?? "?";

        static string TypeIdOf(MyCubeBlock b)
        {
            var t = b.BlockDefinition?.Id.TypeId.ToString() ?? "";
            return t.StartsWith("MyObjectBuilder_") ? t.Substring(16) : t;
        }

        readonly Dictionary<string, List<MyCubeBlock>> matchCache = new Dictionary<string, List<MyCubeBlock>>();

        /// <summary>
        /// Bloki wg filtra jak w Automatic LCDs 2: tekst w nazwie, "G:grupa", "Typ:rodzaj" (np. Typ:Refinery, Typ:Jump),
        /// puste albo "*" = wszystkie bloki terminala. Tylko grid, w którym siedzisz (z rotorami i tłokami).
        /// </summary>
        public List<MyCubeBlock> Match(string filter)
        {
            string f = (filter ?? "").Trim();
            if (matchCache.TryGetValue(f, out var cached)) return cached;
            var res = new List<MyCubeBlock>();
            string pre = "";
            int colon = f.IndexOf(':');
            if (colon >= 1 && colon <= 4)
            {
                pre = f.Substring(0, colon).ToLowerInvariant();
                if (pre == "g" || pre == "t" || pre == "c" || pre == "tg" || pre == "gt" || pre == "cg" || pre == "gc" || pre == "typ" || pre == "type")
                    f = f.Substring(colon + 1).Trim();
                else pre = "";
            }

            if (pre.Contains("g") && f.Length > 0)
            {
                try
                {
                    var groups = new List<Ing.IMyBlockGroup>();
                    Gts?.GetBlockGroups(groups);
                    foreach (var g in groups)
                    {
                        if (!string.Equals(g.Name, f, StringComparison.OrdinalIgnoreCase)) continue;
                        var tb = new List<Ing.IMyTerminalBlock>();
                        g.GetBlocks(tb);
                        foreach (var t in tb)
                            if (t is MyCubeBlock mb && BlockIds.Contains(mb.EntityId) && !res.Contains(mb)) res.Add(mb);
                    }
                }
                catch { }
            }
            else
            {
                bool all = f.Length == 0 || f == "*";
                bool type = pre == "typ" || pre == "type";
                foreach (var b in Blocks)
                {
                    if (!(b is Ing.IMyTerminalBlock)) continue;
                    if (!all)
                    {
                        if (type)
                        {
                            if (TypeOf(b).IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0 &&
                                TypeIdOf(b).IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        }
                        else if (NameOf(b).IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    }
                    res.Add(b);
                }
            }
            res = res.OrderBy(NameOf, StringComparer.OrdinalIgnoreCase).ToList();
            matchCache[(filter ?? "").Trim()] = res;
            return res;
        }

        // ------------------------------------------------------------------ przedmioty w wybranych blokach

        readonly Dictionary<string, Dictionary<string, ItemAmount>> filteredItems = new Dictionary<string, Dictionary<string, ItemAmount>>();

        public Dictionary<string, ItemAmount> ItemsIn(string filter, bool onlyCargo)
        {
            if (string.IsNullOrWhiteSpace(filter) || filter.Trim() == "*") return Items(onlyCargo);
            string key = filter.Trim() + "|" + onlyCargo;
            if (filteredItems.TryGetValue(key, out var r)) return r;
            r = CountItems(Match(filter).Where(b => !onlyCargo || b is Ing.IMyCargoContainer));
            filteredItems[key] = r;
            return r;
        }

        // ------------------------------------------------------------------ informacje bloku

        static readonly Regex ColorTag = new Regex(@"\[/?Color[^\]]*\]", RegexOptions.IgnoreCase);

        /// <summary>DetailedInfo + CustomInfo bez znaczników koloru (jak Details w Automatic LCDs 2).</summary>
        public static string[] InfoLines(MyCubeBlock b, bool refresh)
        {
            var tb = b as Ing.IMyTerminalBlock;
            if (tb == null) return new string[0];
            if (refresh)
                try { (b as IMyTerminalBlock)?.RefreshCustomInfo(); } catch { }
            string text = "";
            try { text = ColorTag.Replace(tb.DetailedInfo ?? "", "") + "\n" + ColorTag.Replace(tb.CustomInfo ?? "", ""); } catch { }
            return text.Replace("\r", "").Split('\n');
        }

        // ------------------------------------------------------------------ stan bloku (Working)

        public static string State(MyCubeBlock b, out bool bad)
        {
            bad = false;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (!b.IsFunctional) { bad = true; return L.T("USZKODZONY", "DAMAGED"); }
            var fb = b as Ing.IMyFunctionalBlock;
            if (fb != null && !fb.Enabled) { bad = true; return L.T("wył.", "off"); }

            switch (b)
            {
                case Ing.IMyShipConnector c:
                    switch (c.Status)
                    {
                        case Ing.MyShipConnectorStatus.Connected: return L.T("połączony", "connected");
                        case Ing.MyShipConnectorStatus.Connectable: return L.T("gotowy", "ready");
                        default: return L.T("wolny", "free");
                    }
                case Ing.IMyDoor d:
                    switch (d.Status)
                    {
                        case Ing.DoorStatus.Open: return L.T("otwarte", "open");
                        case Ing.DoorStatus.Opening: return L.T("otwiera", "opening");
                        case Ing.DoorStatus.Closing: return L.T("zamyka", "closing");
                        default: return L.T("zamknięte", "closed");
                    }
                case Ing.IMyPistonBase p: return p.CurrentPosition.ToString("0.0", inv) + " m";
                case Ing.IMyMotorStator r: return MathHelper.ToDegrees(r.Angle).ToString("0", inv) + "°";
                case Ing.IMyBatteryBlock bat:
                {
                    string mode = bat.ChargeMode == Ing.ChargeMode.Recharge ? L.T(" ładuj", " charge") : bat.ChargeMode == Ing.ChargeMode.Discharge ? L.T(" rozład.", " discharge") : "";
                    return Fmt.Pct(bat.MaxStoredPower > 0 ? bat.CurrentStoredPower / bat.MaxStoredPower : 0) + mode;
                }
                case Ing.IMyJumpDrive j: return Fmt.Pct(j.MaxStoredPower > 0 ? j.CurrentStoredPower / j.MaxStoredPower : 0);
                case Ing.IMyGasTank t: return Fmt.Pct(t.FilledRatio) + (t.Stockpile ? L.T(" zbieraj", " stockpile") : "");
                case SpaceEngineers.Game.ModAPI.Ingame.IMyLandingGear g: return g.IsLocked ? L.T("zablokowane", "locked") : L.T("wolne", "unlocked");
                case SpaceEngineers.Game.ModAPI.Ingame.IMyAirVent v: return Fmt.Pct(v.GetOxygenLevel()) + (v.Depressurize ? L.T(" wypompuj", " depress.") : "");
                case Ing.IMyLargeTurretBase tur: return tur.HasTarget ? L.T("cel!", "target!") : L.T("czuwa", "idle");
                case Ing.IMyProductionBlock pb:
                    if (pb.IsProducing) return L.T("pracuje", "working");
                    bad = true;
                    return pb.IsQueueEmpty ? L.T("bezczynna", "idle") : L.T("czeka", "waiting");
                case Ing.IMyGasGenerator _:
                case Ing.IMyShipToolBase _:
                    return b.IsWorking ? L.T("działa", "on") : L.T("nie działa", "not working");
            }
            if (!b.IsWorking) { bad = true; return L.T("nie działa", "not working"); }
            return L.T("działa", "on");
        }

        public static float IntegrityOf(MyCubeBlock b)
        {
            var s = b.SlimBlock;
            if (s == null || s.MaxIntegrity <= 0) return 1f;
            return s.Integrity / s.MaxIntegrity;
        }

        public static double PowerIn(MyCubeBlock b, out double max)
        {
            max = 0;
            var sink = b.Components.Get<MyResourceSinkComponent>();
            if (sink == null || !sink.AcceptedResources.Contains(Elec)) return 0;
            max = sink.MaxRequiredInputByType(Elec);
            return sink.CurrentInputByType(Elec);
        }

        // ------------------------------------------------------------------ opcja bloku (Prop)

        public static string PropertyValue(MyCubeBlock b, string id)
        {
            var tb = b as Ing.IMyTerminalBlock;
            if (tb == null || string.IsNullOrEmpty(id)) return "-";
            var p = tb.GetProperty(id);
            if (p == null) return L.T("brak opcji", "no such property");
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            switch (p.TypeName)
            {
                case "Boolean": return tb.GetValue<bool>(id) ? L.T("tak", "on") : L.T("nie", "off");
                case "Single": return tb.GetValue<float>(id).ToString("0.##", inv);
                case "Int64": return tb.GetValue<long>(id).ToString(inv);
                case "Color": { var c = tb.GetValue<Color>(id); return $"{c.R},{c.G},{c.B}"; }
                case "StringBuilder": return tb.GetValue<StringBuilder>(id)?.ToString() ?? "";
                case "String": return tb.GetValue<string>(id) ?? "";
            }
            return p.TypeName;
        }

        // ------------------------------------------------------------------ zadokowane

        public List<string> DockedGrids(out int lockedGears)
        {
            lockedGears = 0;
            var names = new List<string>();
            foreach (var b in Blocks)
            {
                if (b is Ing.IMyShipConnector c && c.Status == Ing.MyShipConnectorStatus.Connected && c.OtherConnector != null)
                {
                    var n = c.OtherConnector.CubeGrid?.CustomName ?? "?";
                    if (!names.Contains(n)) names.Add(n);
                }
                else if (b is SpaceEngineers.Game.ModAPI.Ingame.IMyLandingGear g && g.IsLocked) lockedGears++;
            }
            return names;
        }

        // ------------------------------------------------------------------ kolejka montażowni

        public List<KeyValuePair<string, double>> Queue(string filter)
        {
            var sum = new Dictionary<string, double>();
            var q = new List<Ing.MyProductionItem>();
            foreach (var b in Match(filter))
            {
                if (!(b is Ing.IMyAssembler a)) continue;
                q.Clear();
                try { a.GetQueue(q); } catch { continue; }
                foreach (var it in q)
                {
                    string name = BlueprintName(it.BlueprintId);
                    sum.TryGetValue(name, out var have);
                    sum[name] = have + (double)it.Amount;
                }
            }
            return sum.OrderByDescending(kv => kv.Value).ToList();
        }

        static readonly Dictionary<MyDefinitionId, string> bpNames = new Dictionary<MyDefinitionId, string>();

        static string BlueprintName(MyDefinitionId id)
        {
            if (bpNames.TryGetValue(id, out var n)) return n;
            try
            {
                var bp = MyDefinitionManager.Static.GetBlueprintDefinition(id);
                if (bp?.Results != null && bp.Results.Length > 0) n = ItemName(bp.Results[0].Id);
                else n = bp?.DisplayNameText;
            }
            catch { }
            if (string.IsNullOrEmpty(n)) n = id.SubtypeName;
            bpNames[id] = n;
            return n;
        }

        // ------------------------------------------------------------------ lot

        public class ShipInfo
        {
            public double Speed, Accel, NatG, ArtG, StopDist = -1, StopTime = -1;
            public double? AltSurface, AltSea;
            public float BaseMass, TotalMass, PhysMass;
            public bool Dampeners, Handbrake;
            public Vector3D Pos;
        }

        static Vector3D lastVel;
        static DateTime lastVelTime;
        static long lastVelCtrl;

        ShipInfo ship;
        public ShipInfo Ship => ship ?? (ship = ComputeShip());

        ShipInfo ComputeShip()
        {
            var s = new ShipInfo();
            var c = Controller;
            Vector3D vel = Vector3D.Zero;
            try { vel = c.GetShipVelocities().LinearVelocity; s.Speed = vel.Length(); } catch { }
            var now = DateTime.UtcNow;
            if (lastVelCtrl == c.EntityId)
            {
                double dt = (now - lastVelTime).TotalSeconds;
                if (dt > 0.05 && dt < 5) s.Accel = (vel - lastVel).Length() / dt;
            }
            lastVel = vel; lastVelTime = now; lastVelCtrl = c.EntityId;

            try { s.NatG = c.GetNaturalGravity().Length(); } catch { }
            try { s.ArtG = c.GetArtificialGravity().Length(); } catch { }
            try { if (c.TryGetPlanetElevation(Ing.MyPlanetElevation.Surface, out var a)) s.AltSurface = a; } catch { }
            try { if (c.TryGetPlanetElevation(Ing.MyPlanetElevation.Sealevel, out var a)) s.AltSea = a; } catch { }
            try { var m = c.CalculateShipMass(); s.BaseMass = m.BaseMass; s.TotalMass = m.TotalMass; s.PhysMass = m.PhysicalMass; } catch { }
            try { s.Dampeners = c.DampenersOverride; s.Handbrake = c.HandBrake; } catch { }
            try { s.Pos = c.GetPosition(); } catch { }

            // droga hamowania: ciąg silników skierowany przeciwnie do prędkości
            if (s.Speed > 0.1 && s.PhysMass > 0)
            {
                var back = -vel / s.Speed;
                double f = 0;
                foreach (var b in Blocks)
                {
                    if (!(b is Ing.IMyThrust th) || !b.IsWorking) continue;
                    double d = Vector3D.Dot(b.WorldMatrix.Backward, back);
                    if (d > 0) f += d * th.MaxEffectiveThrust;
                }
                if (f > 0)
                {
                    double a = f / s.PhysMass;
                    s.StopTime = s.Speed / a;
                    s.StopDist = s.Speed * s.Speed / (2 * a);
                }
            }
            return s;
        }

        // ------------------------------------------------------------------ GPS

        static readonly Regex GpsRx = new Regex(@"GPS:([^:]*):(-?[\d.]+):(-?[\d.]+):(-?[\d.]+)", RegexOptions.IgnoreCase);

        public static bool ParseGps(string s, out string name, out Vector3D pos)
        {
            name = ""; pos = Vector3D.Zero;
            var m = GpsRx.Match(s ?? "");
            if (!m.Success) return false;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            name = m.Groups[1].Value;
            return double.TryParse(m.Groups[2].Value, System.Globalization.NumberStyles.Float, inv, out pos.X)
                && double.TryParse(m.Groups[3].Value, System.Globalization.NumberStyles.Float, inv, out pos.Y)
                && double.TryParse(m.Groups[4].Value, System.Globalization.NumberStyles.Float, inv, out pos.Z);
        }
    }
}
