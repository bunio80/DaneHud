using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using Ing = Sandbox.ModAPI.Ingame;
using IngInv = VRage.Game.ModAPI.Ingame;

namespace DaneHud
{
    /// <summary>
    /// Magazyn: części skryptu Isy (sortowanie, równy podział rudy, lód, uran, autocrafting) wykonywane przez plugin.
    /// Działa tylko, gdy siedzisz w gridzie z włączonym magazynem. Przenosi przedmioty tak jak gracz w terminalu
    /// (MyInventory.TransferByUser » żądanie do serwera), kolejkuje montażownie przez AddQueueItem (też żądanie do serwera).
    /// </summary>
    internal class Magazyn
    {
        public static readonly Magazyn I = new Magazyn();

        public string State = "";
        public string Warning = "";
        public int Moved, Queued;
        public readonly List<string> Recent = new List<string>();
        public readonly Dictionary<string, double> QueuedNow = new Dictionary<string, double>();   // klucz przedmiotu » w kolejce

        int cycle;
        int budget;
        readonly Dictionary<string, DateTime> cooldown = new Dictionary<string, DateTime>();
        readonly Dictionary<string, DateTime> queueCooldown = new Dictionary<string, DateTime>();
        readonly HashSet<long> conveyorOff = new HashSet<long>();      // rafinerie, którym wyłączyliśmy pobieranie z konwejera

        static string T(string pl, string en) => L.T(pl, en);

        void Note(string s)
        {
            Recent.Insert(0, DateTime.Now.ToString("HH:mm:ss ") + s);
            if (Recent.Count > 12) Recent.RemoveAt(Recent.Count - 1);
        }

        // ------------------------------------------------------------------ wywoływane z Plugin.Refresh (co ~0,5 s)

        public void Run(Ctx c, MagazynCfg m)
        {
            cycle++;
            budget = Math.Max(1, m.MovesPerSecond / 2);
            Warning = "";
            if (IsyRunning(c))
            {
                State = T("wstrzymany", "paused");
                Warning = T("Działa PB Isy — wyłącz go, inaczej będą się kłócić.", "Isy's PB is running — turn it off or they will fight.");
                return;
            }
            State = T("działa", "running");
            var w = new World(c, m);
            if (m.Refineries) Refineries(w, m);
            if (m.Ice && cycle % 6 == 1) Ice(w, m);
            if (m.Uranium && cycle % 6 == 3) Uranium(w, m);
            if (m.Sort) Sort(w);
            if (m.Autocraft && cycle % 10 == 5) Autocraft(c, w, m);
            if (cycle % 120 == 0) { Prune(cooldown); Prune(queueCooldown); }
        }

        /// <summary>Wstałeś z fotela albo wyłączyłeś magazyn: rafinerie znowu same pobierają rudę (działa też, gdy nie grasz).</summary>
        public void Release()
        {
            if (conveyorOff.Count == 0) return;
            foreach (var id in conveyorOff)
                if (MyEntities.TryGetEntityById(id, out var e) && e is Ing.IMyRefinery r && !e.Closed && !r.UseConveyorSystem)
                    try { r.UseConveyorSystem = true; } catch { }
            conveyorOff.Clear();
            State = "";
        }

        static void Prune(Dictionary<string, DateTime> d)
        {
            var now = DateTime.UtcNow;
            foreach (var k in d.Where(kv => kv.Value < now).Select(kv => kv.Key).ToList()) d.Remove(k);
        }

        static bool IsyRunning(Ctx c)
        {
            foreach (var b in c.Blocks)
                if (b is Ing.IMyProgrammableBlock pb && pb.Enabled && b.IsWorking)
                {
                    var n = pb.CustomName ?? "";
                    if (n.IndexOf("Isy", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("IIM", StringComparison.Ordinal) >= 0) return true;
                }
            return false;
        }

        // ------------------------------------------------------------------ obraz grida

        class Inv
        {
            public MyCubeBlock Block;
            public MyInventory I;
            public string Name;
            public bool Output;            // wyjście rafinerii/montażowni — przenosić dopiero większe ilości
            public double Fill => (double)I.MaxVolume > 0 ? (double)I.CurrentVolume / (double)I.MaxVolume : 1;
        }

        class World
        {
            public readonly Dictionary<ItemCat, List<Inv>> Typed = new Dictionary<ItemCat, List<Inv>>();
            public readonly List<Inv> Cargo = new List<Inv>();           // wszystkie kontenery (bez zablokowanych)
            public readonly List<Inv> Sources = new List<Inv>();         // skąd sortować
            public readonly List<MyCubeBlock> Refineries = new List<MyCubeBlock>();
            public readonly List<MyCubeBlock> Generators = new List<MyCubeBlock>();
            public readonly List<MyCubeBlock> Reactors = new List<MyCubeBlock>();
            public readonly List<MyCubeBlock> Assemblers = new List<MyCubeBlock>();
            readonly string[] locked;
            readonly Dictionary<ItemCat, string> kw;

            public World(Ctx c, MagazynCfg m)
            {
                locked = (m.LockedKw ?? "").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
                kw = new Dictionary<ItemCat, string>
                {
                    { ItemCat.Ore, m.OreKw }, { ItemCat.Ingot, m.IngotKw }, { ItemCat.Component, m.ComponentKw },
                    { ItemCat.Tool, m.ToolKw }, { ItemCat.Ammo, m.AmmoKw }, { ItemCat.Bottle, m.BottleKw },
                };
                foreach (var k in kw.Keys) Typed[k] = new List<Inv>();

                foreach (var b in c.Blocks.OrderBy(Ctx.NameOf, StringComparer.OrdinalIgnoreCase))
                {
                    if (b.InventoryCount == 0 || !b.IsFunctional) continue;
                    string name = Ctx.NameOf(b);
                    if (IsLocked(name)) continue;
                    var inv0 = b.GetInventory(0) as MyInventory;
                    if (inv0 == null) continue;

                    if (b is Ing.IMyCargoContainer)
                    {
                        var x = new Inv { Block = b, I = inv0, Name = name };
                        Cargo.Add(x);
                        bool typed = false;
                        foreach (var kv in kw)
                            if (!string.IsNullOrEmpty(kv.Value) && name.IndexOf(kv.Value, StringComparison.OrdinalIgnoreCase) >= 0) { Typed[kv.Key].Add(x); typed = true; }
                        Sources.Add(x);   // też kontenery typowe — źle odłożone przedmioty wyjdą z nich
                        if (!typed) { }
                    }
                    else if (b is Ing.IMyShipConnector || b is Ing.IMyShipDrill || b is Ing.IMyShipGrinder || b is Ing.IMyCollector)
                        Sources.Add(new Inv { Block = b, I = inv0, Name = name });

                    var fb = b as Ing.IMyFunctionalBlock;
                    bool on = fb == null || fb.Enabled;
                    if (b is Ing.IMyRefinery)
                    {
                        if (b.InventoryCount > 1 && b.GetInventory(1) is MyInventory o) Sources.Add(new Inv { Block = b, I = o, Name = name, Output = true });
                        if (on) Refineries.Add(b);
                    }
                    else if (b is Ing.IMyAssembler && b.BlockDefinition.Id.TypeId.ToString() != "MyObjectBuilder_SurvivalKit")
                    {
                        if (b.InventoryCount > 1 && b.GetInventory(1) is MyInventory o) Sources.Add(new Inv { Block = b, I = o, Name = name, Output = true });
                        if (on) Assemblers.Add(b);
                    }
                    else if (b is Ing.IMyGasGenerator && on && MakesGas(b)) Generators.Add(b);
                    else if (b is Ing.IMyReactor && on) Reactors.Add(b);
                }
            }

            public bool IsLocked(string name)
            {
                foreach (var k in locked) if (name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                return false;
            }

            public bool IsTyped(Inv x, ItemCat cat) => Typed.TryGetValue(cat, out var l) && l.Contains(x);

            /// <summary>Gdzie odkładać daną kategorię: kontenery z jej słowem, a gdy ich nie ma — null (nie sortujemy).</summary>
            public List<Inv> Targets(ItemCat cat) => Typed.TryGetValue(cat, out var l) && l.Count > 0 ? l : null;

            /// <summary>Skąd brać rudę/lód/sztabki: kontenery typowe, a gdy ich nie ma — wszystkie kontenery.</summary>
            public List<Inv> Storage(ItemCat cat) => Targets(cat) ?? Cargo;

            static bool MakesGas(MyCubeBlock b)
            {
                var src = b.Components.Get<MyResourceSourceComponent>();
                if (src == null) return false;
                foreach (var t in src.ResourceTypes)
                    if (t.SubtypeName == "Hydrogen" || t.SubtypeName == "Oxygen") return true;
                return false;
            }
        }

        // ------------------------------------------------------------------ przenoszenie

        static IngInv.MyItemType ItemType(MyDefinitionId id) => new IngInv.MyItemType(id.TypeId.ToString(), id.SubtypeName);

        /// <summary>Przenosi do `amount` sztuk przedmiotu; zwraca ile poszło (0 = nic).</summary>
        double Move(MyInventory src, MyPhysicalInventoryItem item, MyInventory dst, double amount, string why)
        {
            if (budget <= 0 || amount <= 0 || src == dst) return 0;
            var id = new MyDefinitionId(item.Content.TypeId, item.Content.SubtypeId);
            string key = src.Entity.EntityId + ":" + item.ItemId + ">" + dst.Entity.EntityId;
            if (cooldown.TryGetValue(key, out var until) && until > DateTime.UtcNow) return 0;
            try
            {
                if (!((IngInv.IMyInventory)src).CanTransferItemTo(dst, ItemType(id))) return 0;
                double fits = (double)dst.ComputeAmountThatFits(id, 0f, 0f);
                double a = Math.Min(Math.Min(amount, (double)item.Amount), fits);
                if (!IsFractional(id)) a = Math.Floor(a);
                if (a < (IsFractional(id) ? 0.01 : 1)) return 0;
                MyInventory.TransferByUser(src, dst, item.ItemId, -1, (MyFixedPoint)a);
                budget--;
                Moved++;
                cooldown[key] = DateTime.UtcNow.AddSeconds(4);
                Note($"{Fmt.Amount(a)} {Ctx.ItemName(id)} » {Ctx.NameOf((MyCubeBlock)dst.Entity)} ({why})");
                return a;
            }
            catch (Exception e) { Plugin.LogOnce("move", e); return 0; }
        }

        static bool IsFractional(MyDefinitionId id)
        {
            string t = id.TypeId.ToString();
            return t == "MyObjectBuilder_Ore" || t == "MyObjectBuilder_Ingot";
        }

        // ------------------------------------------------------------------ sortowanie

        void Sort(World w)
        {
            foreach (var src in w.Sources)
            {
                if (budget <= 0) return;
                foreach (var item in src.I.GetItems().ToList())
                {
                    if (budget <= 0) return;
                    var id = new MyDefinitionId(item.Content.TypeId, item.Content.SubtypeId);
                    var cat = Ctx.CatOf(id.TypeId.ToString());
                    var targets = w.Targets(cat);
                    if (targets == null || w.IsTyped(src, cat)) continue;
                    // z wyjść produkcji co kilka sekund większymi porcjami, nie po 0,4 kg co pół sekundy
                    if (src.Output && src.Fill < 0.5 && (double)item.Amount < (IsFractional(id) ? 100 : 20)) continue;
                    foreach (var dst in targets)
                    {
                        if (dst.Fill > 0.995) continue;
                        if (Move(src.I, item, dst.I, (double)item.Amount, T("sortowanie", "sorting")) > 0) break;
                    }
                }
            }
        }

        // ------------------------------------------------------------------ rafinerie: każda ruda po równo

        void Refineries(World w, MagazynCfg m)
        {
            var refs = w.Refineries;
            if (refs.Count == 0) return;

            // rafinerie mają brać rudę tylko od nas (jak w poprawionym Isy) — inaczej gra wrzuca całą rudę do pierwszej
            foreach (var r in refs)
            {
                conveyorOff.Add(r.EntityId);   // po wstaniu z fotela włączymy pobieranie wszystkim (także wyłączonym wcześniej przez Isy)
                if (r is Ing.IMyRefinery rr && rr.UseConveyorSystem)
                    try { rr.UseConveyorSystem = false; } catch { }
            }

            if (cycle % 4 != 0) return;
            var storage = w.Storage(ItemCat.Ore);
            var totals = new Dictionary<MyDefinitionId, double>();
            foreach (var s in storage)
                foreach (var it in s.I.GetItems())
                {
                    var id = new MyDefinitionId(it.Content.TypeId, it.Content.SubtypeId);
                    if (id.TypeId.ToString() != "MyObjectBuilder_Ore") continue;
                    totals.TryGetValue(id, out var t); totals[id] = t + (double)it.Amount;
                }
            var ins = refs.Select(r => r.GetInventory(0) as MyInventory).ToList();
            foreach (var inv in ins)
                foreach (var it in inv.GetItems())
                {
                    var id = new MyDefinitionId(it.Content.TypeId, it.Content.SubtypeId);
                    totals.TryGetValue(id, out var t); totals[id] = t + (double)it.Amount;
                }

            var prio = (m.OrePriority ?? "").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            var ores = totals.Keys
                .OrderBy(id => { int i = prio.FindIndex(p => string.Equals(p, id.SubtypeName, StringComparison.OrdinalIgnoreCase)); return i < 0 ? 999 : i; })
                .ThenByDescending(id => totals[id]).ToList();

            foreach (var ore in ores)
            {
                var able = ins.Where(i => i.CheckConstraint(ore)).ToList();
                if (able.Count == 0) continue;
                double target = totals[ore] / able.Count;
                // nadmiar wraca do magazynu (np. gra sama wrzuciła rudę do jednej rafinerii)
                foreach (var inv in able)
                {
                    double have = (double)inv.GetItemAmount(ore);
                    if (have > target * 1.25 && have - target > 500)
                    {
                        var item = inv.GetItems().FirstOrDefault(x => x.Content.TypeId == ore.TypeId && x.Content.SubtypeId == ore.SubtypeId);
                        var dst = storage.FirstOrDefault(s => s.Fill < 0.99);
                        if (dst != null && item.Amount > 0) Move(inv, item, dst.I, have - target, T("nadmiar rudy", "ore excess"));
                    }
                }
                // brak — dokładamy z magazynu
                foreach (var inv in able)
                {
                    double have = (double)inv.GetItemAmount(ore);
                    double need = target - have;
                    if (need < Math.Max(50, target * 0.15)) continue;
                    // pełna rafineria: dosypujemy dopiero, gdy zużyje 10% (nie po 30 kg co chwilę)
                    if ((double)inv.MaxVolume > 0 && (double)inv.CurrentVolume / (double)inv.MaxVolume > 0.9) continue;
                    foreach (var s in storage)
                    {
                        if (need <= 0 || budget <= 0) break;
                        foreach (var item in s.I.GetItems().Where(x => x.Content.TypeId == ore.TypeId && x.Content.SubtypeId == ore.SubtypeId).ToList())
                        {
                            double moved = Move(s.I, item, inv, need, T("równy podział rudy", "ore balancing"));
                            need -= moved;
                            if (need <= 0) break;
                        }
                    }
                }
            }
        }

        // ------------------------------------------------------------------ lód i uran

        static readonly MyDefinitionId IceId = MyDefinitionId.Parse("MyObjectBuilder_Ore/Ice");

        void Ice(World w, MagazynCfg m)
        {
            if (w.Generators.Count == 0) return;
            float vol = 0.00037f;
            try { vol = MyDefinitionManager.Static.GetPhysicalItemDefinition(IceId).Volume; } catch { }
            var storage = w.Storage(ItemCat.Ore);
            foreach (var g in w.Generators)
            {
                var inv = g.GetInventory(0) as MyInventory;
                if (inv == null || !inv.CheckConstraint(IceId)) continue;
                double targetKg = (double)inv.MaxVolume * Math.Max(5, Math.Min(100, m.IceFillPct)) / 100.0 / vol;
                double have = (double)inv.GetItemAmount(IceId);
                if (have >= targetKg * 0.75) continue;
                Fill(storage, inv, IceId, targetKg - have, T("lód", "ice"));
            }
        }

        void Uranium(World w, MagazynCfg m)
        {
            if (w.Reactors.Count == 0) return;
            foreach (var r in w.Reactors)
            {
                var def = r.BlockDefinition as MyReactorDefinition;
                if (def?.FuelInfos == null || def.FuelInfos.Length == 0) continue;
                var fuel = def.FuelInfos[0].FuelId;
                var inv = r.GetInventory(0) as MyInventory;
                if (inv == null) continue;
                double target = r.CubeGrid.GridSizeEnum == MyCubeSize.Large ? m.UraniumLarge : m.UraniumSmall;
                double have = (double)inv.GetItemAmount(fuel);
                if (target <= 0 || have >= target * 0.75) continue;
                Fill(w.Storage(ItemCat.Ingot), inv, fuel, target - have, T("paliwo reaktora", "reactor fuel"));
            }
        }

        void Fill(List<Inv> storage, MyInventory dst, MyDefinitionId id, double amount, string why)
        {
            foreach (var s in storage)
            {
                if (amount <= 0 || budget <= 0) return;
                foreach (var item in s.I.GetItems().Where(x => x.Content.TypeId == id.TypeId && x.Content.SubtypeId == id.SubtypeId).ToList())
                {
                    amount -= Move(s.I, item, dst, amount, why);
                    if (amount <= 0) return;
                }
            }
        }

        // ------------------------------------------------------------------ autocrafting

        internal static MyBlueprintDefinitionBase BlueprintFor(MyDefinitionId item)
        {
            try { if (MyDefinitionManager.Static.TryGetBlueprintDefinitionByResultId(item, out var bp)) return bp; } catch { }
            return null;
        }

        void Autocraft(Ctx c, World w, MagazynCfg m)
        {
            if (m.Quotas == null || m.Quotas.Count == 0 || w.Assemblers.Count == 0) return;
            var items = c.Items(false);

            // co już jest w kolejkach montażowni
            var inQueue = new Dictionary<MyDefinitionId, double>();
            var qlen = new Dictionary<MyCubeBlock, double>();
            var q = new List<Ing.MyProductionItem>();
            foreach (var a in w.Assemblers)
            {
                var asm = (Ing.IMyAssembler)a;
                q.Clear();
                try { asm.GetQueue(q); } catch { continue; }
                double len = 0;
                foreach (var it in q)
                {
                    inQueue.TryGetValue(it.BlueprintId, out var v);
                    inQueue[it.BlueprintId] = v + (double)it.Amount;
                    len += (double)it.Amount;
                }
                qlen[a] = len;
            }

            QueuedNow.Clear();
            foreach (var quota in m.Quotas)
            {
                if (quota.Amount <= 0) continue;
                MyDefinitionId id;
                if (!MyDefinitionId.TryParse(quota.Id, out id)) continue;
                var bp = BlueprintFor(id);
                if (bp == null) continue;
                double perRun = 1;
                try { perRun = Math.Max(0.0001, (double)bp.Results.First(r => r.Id == id).Amount); } catch { }
                inQueue.TryGetValue(bp.Id, out var queuedRuns);
                double queued = queuedRuns * perRun;
                QueuedNow[quota.Id] = queued;
                items.TryGetValue(quota.Id, out var have);
                double missing = quota.Amount - (have?.Amount ?? 0) - queued;
                if (missing < perRun) continue;
                if (queueCooldown.TryGetValue(quota.Id, out var until) && until > DateTime.UtcNow) continue;

                var asm = w.Assemblers
                    .Where(a => ((Ing.IMyAssembler)a).Mode == Ing.MyAssemblerMode.Assembly && !w.IsLocked(Ctx.NameOf(a)))
                    .Where(a => { try { return ((Ing.IMyAssembler)a).CanUseBlueprint(bp.Id); } catch { return false; } })
                    .OrderBy(a => qlen.TryGetValue(a, out var l) ? l : 0).FirstOrDefault();
                if (asm == null) continue;
                double runs = Math.Ceiling(missing / perRun);
                try
                {
                    ((Ing.IMyAssembler)asm).AddQueueItem(bp.Id, (MyFixedPoint)runs);
                    qlen[asm] = (qlen.TryGetValue(asm, out var l0) ? l0 : 0) + runs;
                    queueCooldown[quota.Id] = DateTime.UtcNow.AddSeconds(20);
                    Queued++;
                    Note($"{T("kolejka", "queue")}: {Fmt.Amount(runs * perRun)} {Ctx.ItemName(id)} » {Ctx.NameOf(asm)}");
                }
                catch (Exception e) { Plugin.LogOnce("queue", e); }
            }
        }

        // ------------------------------------------------------------------ import limitów z LCD Isy

        /// <summary>Limity z LCD „Autocrafting” skryptu Isy (wartości „chciane”). Zwraca liczbę rozpoznanych pozycji.</summary>
        internal static int ImportFromIsy(Ctx c, MagazynCfg m, string keyword, out int unknown)
        {
            unknown = 0;
            var list = c?.Autocrafting(string.IsNullOrEmpty(keyword) ? "Autocrafting" : keyword);
            if (list == null) return 0;
            var defs = MyDefinitionManager.Static.GetPhysicalItemDefinitions().Where(d => d.Public && BlueprintFor(d.Id) != null).ToList();
            int n = 0;
            foreach (var e in list)
            {
                var d = defs.FirstOrDefault(x => x.Id.SubtypeName == e.Name)
                     ?? defs.FirstOrDefault(x => string.Equals(x.DisplayNameText, e.Name, StringComparison.OrdinalIgnoreCase));
                if (d == null)
                {
                    var pre = defs.Where(x => x.Id.SubtypeName.StartsWith(e.Name, StringComparison.Ordinal)).ToList();
                    if (pre.Count == 1) d = pre[0];
                }
                if (d == null) { unknown++; continue; }
                string key = d.Id.ToString();
                var q = m.Quotas.FirstOrDefault(x => x.Id == key);
                if (e.Wanted <= 0) { if (q != null) m.Quotas.Remove(q); n++; continue; }
                if (q == null) m.Quotas.Add(q = new Quota { Id = key });
                q.Amount = e.Wanted;
                n++;
            }
            return n;
        }
    }
}
