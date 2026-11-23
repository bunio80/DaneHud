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
        public readonly HashSet<string> Unreachable = new HashSet<string>();   // bloki bez połączenia konwejerem z magazynem
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
            var script = RefineryScript(c);
            ScriptName = script != null ? Ctx.NameOf((MyCubeBlock)script) : "";
            if (script != null)
            {
                // rafinerie obsługuje skrypt PB 24/7 — plugin tylko podaje mu ustawienia
                if (cycle % 20 == 7) WriteScriptConfig(script, m);
            }
            else if (m.Refineries) Refineries(w, m);
            if (m.Ice && cycle % 6 == 1) Ice(w, m);
            if (m.Uranium && cycle % 6 == 3) Uranium(w, m);
            if (m.Specials && cycle % 4 == 2) Specials(w, m);
            if (m.Sort) Sort(w);
            if (cycle % 6 == 4) Learn(w, m);
            if (m.Autocraft && cycle % 10 == 5) Autocraft(c, w, m);
            if (cycle % 120 == 0) { Prune(cooldown); Prune(queueCooldown); }
            if (cycle % 240 == 0) Unreachable.Clear();
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

        // ------------------------------------------------------------------ skrypt PB rafinerii (24/7 na serwerze)

        internal const string ScriptTag = "[DaneHud-Rafinerie]";
        public string ScriptName = "";
        public string PrimaryOre = "";

        /// <summary>Działający PB z naszym skryptem rafinerii (znacznik w CustomData).</summary>
        internal static Ing.IMyProgrammableBlock RefineryScript(Ctx c)
        {
            if (c == null) return null;
            foreach (var b in c.Blocks)
                if (b is Ing.IMyProgrammableBlock pb && b.IsFunctional && pb.Enabled && (pb.CustomData ?? "").Contains(ScriptTag)) return pb;
            return null;
        }

        internal static string ScriptConfig(MagazynCfg m) =>
            ScriptTag + "\npriority=" + (m.OrePriority ?? "") + "\nores=" + (m.OreKw ?? "") + "\ningots=" + (m.IngotKw ?? "") +
            "\nskip=" + (m.LockedKw ?? "") + "\n";

        static void WriteScriptConfig(Ing.IMyProgrammableBlock pb, MagazynCfg m)
        {
            string cfg = ScriptConfig(m);
            if ((pb.CustomData ?? "").Replace("\r", "") == cfg) return;
            try { pb.CustomData = cfg; } catch { }
        }

        internal static string ScriptText() => RafinerieScript.Text;

        /// <summary>
        /// Wgrywa skrypt rafinerii do PB: najpierw PB, który już go ma, potem PB z „Rafinerie”/„Refiner” w nazwie,
        /// potem pusty PB. Nigdy nie nadpisuje innego skryptu (np. Isy).
        /// </summary>
        internal static string InstallScript(Ctx c, MagazynCfg m)
        {
            if (c == null) return T("Usiądź w gridzie bazy.", "Sit in the base grid.");
            var pbs = c.Blocks.OfType<Sandbox.ModAPI.IMyProgrammableBlock>().Where(p => ((MyCubeBlock)p).IsFunctional).ToList();
            var pb = pbs.FirstOrDefault(p => (p.CustomData ?? "").Contains(ScriptTag))
                  ?? pbs.FirstOrDefault(p => (p.CustomName ?? "").IndexOf("Rafinerie", StringComparison.OrdinalIgnoreCase) >= 0
                                          || (p.CustomName ?? "").IndexOf("Refiner", StringComparison.OrdinalIgnoreCase) >= 0)
                  ?? pbs.FirstOrDefault(p => string.IsNullOrWhiteSpace(p.ProgramData));
            if (pb == null) return T("Brak wolnego PB. Postaw PB albo nazwij go „PB Rafinerie”.", "No free PB. Build one or name it \"PB Refineries\".");
            string text = ScriptText();
            if (string.IsNullOrEmpty(text)) return T("Błąd: brak skryptu w pluginie.", "Error: script missing in plugin.");
            try
            {
                pb.CustomData = ScriptConfig(m);
                pb.ProgramData = text;
                pb.Enabled = true;
                return T("Skrypt wgrany do: ", "Script installed in: ") + pb.CustomName;
            }
            catch (Exception e) { Plugin.Log("install script: " + e); return T("Nie udało się: ", "Failed: ") + e.Message; }
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
                    if ((pb.CustomData ?? "").Contains(ScriptTag)) continue;   // nasz skrypt rafinerii to nie Isy
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
            public Func<MyDefinitionId, bool> Keep;   // przedmioty, które mają zostać w bloku (paliwo, lód, ruda w rafinerii)
            public double Fill => (double)I.MaxVolume > 0 ? (double)I.CurrentVolume / (double)I.MaxVolume : 1;
        }

        // ------------------------------------------------------------------ kontenery specjalne (format Isy)

        internal const string SpecialHeader = "Special Container modes:";

        class Rule
        {
            public MyDefinitionId Id;
            public double Amount;
            public char Mode;       // N = trzymaj tyle i oddawaj nadmiar, M = minimum, L = tylko oddawaj nadmiar, A = wszystko
        }

        class Special
        {
            public readonly Inv Inv;
            public readonly int Priority;
            public readonly List<Rule> Rules = new List<Rule>();
            public readonly bool HasHeader;

            static readonly System.Text.RegularExpressions.Regex PrioRx =
                new System.Text.RegularExpressions.Regex(@"\[p(\d+|max|min)\]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            public Special(Inv inv)
            {
                Inv = inv;
                var pm = PrioRx.Match(inv.Name);
                string p = pm.Success ? pm.Groups[1].Value.ToLowerInvariant() : "";
                Priority = p == "max" ? int.MinValue : p == "min" ? int.MaxValue : int.TryParse(p, out var n) ? n : 1000000;
                string cd = (inv.Block as Ing.IMyTerminalBlock)?.CustomData ?? "";
                HasHeader = cd.Contains(SpecialHeader);
                bool body = !HasHeader;       // bez nagłówka Isy: czytamy wszystkie linie przedmiot=ilość
                foreach (var raw in cd.Replace("\r", "").Split('\n'))
                {
                    var line = raw.Trim();
                    if (!body) { if (line.Contains(SpecialHeader)) body = true; continue; }
                    if (line.Length == 0 || line.StartsWith("-")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim(), val = line.Substring(eq + 1).Trim();
                    if (!key.StartsWith("MyObjectBuilder_")) key = "MyObjectBuilder_" + key;
                    if (!MyDefinitionId.TryParse(key, out var id)) continue;
                    var r = new Rule { Id = id, Mode = 'N' };
                    if (val.Equals("All", StringComparison.OrdinalIgnoreCase)) r.Mode = 'A';
                    else
                    {
                        char last = val.Length > 0 ? char.ToUpperInvariant(val[val.Length - 1]) : ' ';
                        if (last == 'M' || last == 'L') { r.Mode = last; val = val.Substring(0, val.Length - 1); }
                        if (!double.TryParse(val.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out r.Amount)) continue;
                        if (r.Amount <= 0) continue;
                    }
                    Rules.Add(r);
                }
            }

            public Rule RuleFor(MyDefinitionId id) => Rules.FirstOrDefault(r => r.Id == id);
        }

        /// <summary>Szablon CustomData jak u Isy: lista przedmiotów z =0 do uzupełnienia.</summary>
        static string SpecialTemplate(string existing)
        {
            var sb = new System.Text.StringBuilder();
            existing = (existing ?? "").TrimEnd();
            if (existing.Length > 0) sb.Append(existing).Append("\n\n");
            sb.Append("@").Append(SpecialHeader).Append("\n\n");
            sb.Append("- Normal: stores wanted amount, removes excess. Usage: item=100\n");
            sb.Append("- Minimum: stores wanted amount, ignores excess. Usage: item=100M\n");
            sb.Append("- Limiter: doesn't store items, only removes excess. Usage: item=100L\n");
            sb.Append("- All: stores all items it can get until it's full. Usage: item=All\n\n");
            var cats = new[] { ItemCat.Component, ItemCat.Ingot, ItemCat.Ore, ItemCat.Ammo, ItemCat.Tool, ItemCat.Bottle };
            try
            {
                foreach (var cat in cats)
                    foreach (var d in MyDefinitionManager.Static.GetPhysicalItemDefinitions()
                                 .Where(d => d.Public && Ctx.CatOf(d.Id.TypeId.ToString()) == cat).OrderBy(d => d.Id.SubtypeName))
                        sb.Append(d.Id.TypeId.ToString().Replace("MyObjectBuilder_", "")).Append('/').Append(d.Id.SubtypeName).Append("=0\n");
            }
            catch { }
            return sb.ToString().TrimEnd('\n');
        }

        void Specials(World w, MagazynCfg m)
        {
            for (int si = 0; si < w.Specials.Count; si++)
            {
                if (budget <= 0) return;
                var sp = w.Specials[si];
                var tb = sp.Inv.Block as Sandbox.ModAPI.IMyTerminalBlock;
                if (!sp.HasHeader && sp.Rules.Count == 0 && tb != null)
                {
                    // nowy kontener specjalny: wpisz listę przedmiotów do CustomData (jak Isy)
                    try { tb.CustomData = SpecialTemplate(tb.CustomData); Note(T("szablon CustomData: ", "CustomData template: ") + sp.Inv.Name); } catch { }
                    continue;
                }

                // 1) wyrzuć to, czego ma nie być albo jest za dużo
                foreach (var item in sp.Inv.I.GetItems().ToList())
                {
                    if (budget <= 0) return;
                    var id = new MyDefinitionId(item.Content.TypeId, item.Content.SubtypeId);
                    var r = sp.RuleFor(id);
                    double have = (double)sp.Inv.I.GetItemAmount(id);
                    double excess = r == null ? have : (r.Mode == 'N' || r.Mode == 'L') ? have - r.Amount : 0;
                    if (excess <= (IsFractional(id) ? 0.01 : 0.5)) continue;
                    var dst = (w.Targets(Ctx.CatOf(id.TypeId.ToString())) ?? w.Cargo).FirstOrDefault(x => x.Fill < 0.99);
                    if (dst != null) Move(sp.Inv.I, item, dst.I, excess, T("nadmiar ze specjalnego", "special excess"));
                }

                // 2) dołóż brakujące
                foreach (var r in sp.Rules)
                {
                    if (budget <= 0) return;
                    if (r.Mode == 'L') continue;
                    double have = (double)sp.Inv.I.GetItemAmount(r.Id);
                    double need = r.Mode == 'A' ? 1e12 : r.Amount - have;
                    if (need <= (IsFractional(r.Id) ? 0.01 : 0.5)) continue;
                    var cat = Ctx.CatOf(r.Id.TypeId.ToString());
                    var from = new List<Inv>();
                    var typed = w.Targets(cat);
                    if (typed != null) from.AddRange(typed);
                    from.AddRange(w.Cargo.Where(x => !from.Contains(x)));
                    from.AddRange(w.Sources.Where(x => x.Output && !from.Contains(x)));
                    if (m.SpecialSteal) from.AddRange(w.Specials.Skip(si + 1).Where(o => o.Priority > sp.Priority).Select(o => o.Inv));
                    Fill(from, sp.Inv.I, r.Id, need, T("specjalny", "special"));
                }
            }
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
            public readonly List<MyCubeBlock> AllAssemblers = new List<MyCubeBlock>();
            public readonly List<Special> Specials = new List<Special>();
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

                    // kontener specjalny (jak u Isy): zawartość wg CustomData, sortowanie go nie opróżnia
                    bool production = b is Ing.IMyProductionBlock || b is Ing.IMyReactor || b is Ing.IMyGasGenerator;
                    if (!production && !string.IsNullOrEmpty(m.SpecialKw) && name.IndexOf(m.SpecialKw, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Specials.Add(new Special(new Inv { Block = b, I = inv0, Name = name }));
                        continue;
                    }

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
                    else if (b is Ing.IMyShipConnector || b is Ing.IMyShipDrill || b is Ing.IMyShipGrinder || b is Ing.IMyCollector || b is Ing.IMyShipController)
                        Sources.Add(new Inv { Block = b, I = inv0, Name = name });

                    var fb = b as Ing.IMyFunctionalBlock;
                    bool on = fb == null || fb.Enabled;
                    if (b is Ing.IMyRefinery)
                    {
                        if (b.InventoryCount > 1 && b.GetInventory(1) is MyInventory o) Sources.Add(new Inv { Block = b, I = o, Name = name, Output = true });
                        // wejście: wynieś to, czego rafineria nie przerobi
                        var ri = inv0;
                        Sources.Add(new Inv { Block = b, I = inv0, Name = name, Keep = id => ri.CheckConstraint(id) });
                        if (on) Refineries.Add(b);
                    }
                    else if (b is Ing.IMyAssembler)
                    {
                        bool kit = b.BlockDefinition.Id.TypeId.ToString() == "MyObjectBuilder_SurvivalKit";
                        var asm = (Ing.IMyAssembler)b;
                        if (b.InventoryCount > 1 && b.GetInventory(1) is MyInventory o) Sources.Add(new Inv { Block = b, I = o, Name = name, Output = true });
                        // sprzątanie montażowni jak u Isy: bezczynna, pusta kolejka — sztabki wracają do magazynu
                        if (asm.IsQueueEmpty && !asm.IsProducing && asm.Mode == Ing.MyAssemblerMode.Assembly)
                            Sources.Add(new Inv { Block = b, I = inv0, Name = name });
                        if (!kit) AllAssemblers.Add(b);
                        bool learning = name.IndexOf("!learn", StringComparison.OrdinalIgnoreCase) >= 0;
                        if (on && !kit && !learning) Assemblers.Add(b);
                    }
                    else if (b is Ing.IMyGasGenerator)
                    {
                        if (MakesGas(b))
                        {
                            // zostaje lód i butle do napełnienia, reszta wychodzi
                            Sources.Add(new Inv { Block = b, I = inv0, Name = name, Keep = id => id == IceId || IsBottle(id) });
                            if (on) Generators.Add(b);
                        }
                    }
                    else if (b is Ing.IMyReactor)
                    {
                        var rdef = b.BlockDefinition as MyReactorDefinition;
                        var fuel = rdef?.FuelInfos != null && rdef.FuelInfos.Length > 0 ? rdef.FuelInfos[0].FuelId : default(MyDefinitionId);
                        Sources.Add(new Inv { Block = b, I = inv0, Name = name, Keep = id => id == fuel });
                        if (on) Reactors.Add(b);
                    }
                }
                Specials.Sort((a, b2) => a.Priority.CompareTo(b2.Priority));
            }

            /// <summary>Sztabki w kontenerach (subtype → ilość).</summary>
            public Dictionary<string, double> IngotsIn()
            {
                var d = new Dictionary<string, double>();
                foreach (var x in Cargo)
                    foreach (var it in x.I.GetItems())
                        if (it.Content.TypeId.ToString() == "MyObjectBuilder_Ingot")
                        {
                            d.TryGetValue(it.Content.SubtypeName, out var v); d[it.Content.SubtypeName] = v + (double)it.Amount;
                        }
                return d;
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

            static bool IsBottle(MyDefinitionId id)
            {
                string t = id.TypeId.ToString();
                return t == "MyObjectBuilder_OxygenContainerObject" || t == "MyObjectBuilder_GasContainerObject";
            }

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
                if (!((IngInv.IMyInventory)src).CanTransferItemTo(dst, ItemType(id)))
                {
                    // gra (i serwer) nie pozwala przenosić między blokami bez połączenia konwejerem
                    Unreachable.Add(Ctx.NameOf((MyCubeBlock)src.Entity));
                    cooldown[key] = DateTime.UtcNow.AddSeconds(30);
                    return 0;
                }
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
                    if (src.Keep != null && src.Keep(id)) continue;
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
            // Algorytm z Isy's Inventory Manager (z poprawką użytkownika v4), przeniesiony 1:1:
            //  Φ  lista rud: najpierw fixedRefiningList (kolejność z ustawień), potem rudy, których sztabek jest najmniej
            //  ˤ  pełna rafineria (98%) bez pierwszej dostępnej rudy z listy — cała zawartość wraca do magazynu
            //  ά  każda ruda wg listy po równo: ceil(suma / liczba rafinerii), nadmiar > 10% wraca
            //  ˏ  pierwsza ruda z listy, którą rafineria ma, idzie na początek jej kolejki
            var storage = w.Storage(ItemCat.Ore);
            var ins = refs.Select(r => r.GetInventory(0) as MyInventory).Where(i => i != null).ToList();
            if (ins.Count == 0) return;
            Func<MyInventory, bool> hasSpace = i => (double)i.CurrentVolume < (double)i.MaxVolume * 0.98;   // ư(inventoryFullBuffer)

            // ile jest każdej rudy (magazyn + rafinerie) i sztabek
            var total = new Dictionary<MyDefinitionId, double>();
            var ingots = new Dictionary<string, double>();
            foreach (var x in storage.Select(z => z.I).Concat(ins))
                foreach (var it in x.GetItems())
                {
                    var id = new MyDefinitionId(it.Content.TypeId, it.Content.SubtypeId);
                    if (id.TypeId.ToString() != "MyObjectBuilder_Ore") continue;
                    total.TryGetValue(id, out var t); total[id] = t + (double)it.Amount;
                }
            foreach (var kv in w.IngotsIn())
            {
                ingots.TryGetValue(kv.Key, out var t); ingots[kv.Key] = t + kv.Value;
            }

            // Φ: kolejność rud
            var prio = (m.OrePriority ?? "").Split(',').Select(z => z.Trim()).Where(z => z.Length > 0).ToList();
            var allOres = AllOreIds();
            var order = new List<MyDefinitionId>();
            foreach (var name in prio)
            {
                var id = allOres.FirstOrDefault(o => string.Equals(o.SubtypeName, name, StringComparison.OrdinalIgnoreCase));
                if (id.TypeId.IsNull == false && !order.Contains(id)) order.Add(id);
            }
            foreach (var id in allOres.Where(o => !order.Contains(o)).OrderBy(o => ingots.TryGetValue(o.SubtypeName, out var v) ? v : 0))
                order.Add(id);
            order = order.Where(o => ins.Any(i => i.CheckConstraint(o))).ToList();
            Func<MyDefinitionId, double> amount = id => total.TryGetValue(id, out var v) ? v : 0;

            // ˤ: pierwsza ruda z listy, której jest > 100 — pełna rafineria bez niej oddaje wszystko do magazynu
            var top = order.FirstOrDefault(o => amount(o) > 100);
            PrimaryOre = top.TypeId.IsNull ? "" : Ctx.ItemName(top);
            if (!top.TypeId.IsNull)
                foreach (var inv in ins)
                {
                    if (budget <= 0) break;
                    if (hasSpace(inv) || (double)inv.GetItemAmount(top) > 0) continue;
                    foreach (var item in inv.GetItems().ToList())
                    {
                        var dst = storage.FirstOrDefault(z => z.Fill < 0.99);
                        if (dst == null || budget <= 0) break;
                        Move(inv, item, dst.I, (double)item.Amount, T("miejsce dla ", "room for ") + Ctx.ItemName(top));
                    }
                }

            // ά: każda ruda wg listy po równo, nadmiar ponad 10% wraca
            foreach (var ore in order)
            {
                double sum = amount(ore);
                if (sum <= 0) continue;
                double share = Math.Ceiling(sum / ins.Count);
                foreach (var inv in ins)
                {
                    if (budget <= 0) return;
                    double need = share - (double)inv.GetItemAmount(ore);
                    if (need < -1 - share * 0.1)
                    {
                        var item = inv.GetItems().FirstOrDefault(z => z.Content.TypeId == ore.TypeId && z.Content.SubtypeId == ore.SubtypeId);
                        var dst = storage.FirstOrDefault(z => z.Fill < 0.99);
                        if (dst != null && item.Amount > 0) Move(inv, item, dst.I, -need, T("nadmiar rudy", "ore excess"));
                        continue;
                    }
                    if (!hasSpace(inv)) continue;
                    // plugin wysyła żądania przez sieć: nie dosypuj po kilka kg (Isy na serwerze może)
                    if (need < Math.Max(50, share * 0.05)) continue;
                    foreach (var src in storage)
                    {
                        if (need <= 1 || budget <= 0) break;
                        foreach (var item in src.I.GetItems().Where(z => z.Content.TypeId == ore.TypeId && z.Content.SubtypeId == ore.SubtypeId).ToList())
                        {
                            need -= Move(src.I, item, inv, need, T("równy podział rudy", "ore balancing"));
                            if (need <= 1) break;
                        }
                    }
                }
            }

            // ˏ: pierwsza ruda z listy, którą rafineria ma, na początek kolejki
            foreach (var inv in ins)
            {
                var list = inv.GetItems();
                if (list.Count < 2) continue;
                int idx = -1;
                foreach (var ore in order)
                {
                    idx = list.FindIndex(z => z.Content.TypeId == ore.TypeId && z.Content.SubtypeId == ore.SubtypeId);
                    if (idx >= 0) break;
                }
                if (idx <= 0) continue;
                string key = "order:" + inv.Entity.EntityId;
                if (cooldown.TryGetValue(key, out var until) && until > DateTime.UtcNow) continue;
                try
                {
                    MyInventory.TransferByUser(inv, inv, list[idx].ItemId, 0, null);
                    cooldown[key] = DateTime.UtcNow.AddSeconds(4);
                    Note(Ctx.ItemName(new MyDefinitionId(list[idx].Content.TypeId, list[idx].Content.SubtypeId)) + T(" na początek kolejki: ", " to the front: ") + Ctx.NameOf((MyCubeBlock)inv.Entity));
                }
                catch (Exception e) { Plugin.LogOnce("order", e); }
            }
        }

        static List<MyDefinitionId> oreIds;

        /// <summary>Wszystkie rudy z definicji gry i modów.</summary>
        static List<MyDefinitionId> AllOreIds()
        {
            if (oreIds != null) return oreIds;
            try
            {
                oreIds = MyDefinitionManager.Static.GetPhysicalItemDefinitions()
                    .Where(d => d != null && d.Public && d.Id.TypeId.ToString() == "MyObjectBuilder_Ore").Select(d => d.Id).ToList();
            }
            catch { oreIds = new List<MyDefinitionId>(); }
            return oreIds;
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

        /// <summary>
        /// Nauka jak u Isy: „!learn” w nazwie montażowni — to, co wrzucisz do jej kolejki, trafia do limitów
        /// (limit = ilość z kolejki), kolejka jest czyszczona, słowo znika z nazwy. „!learnMany” zostaje i uczy dalej.
        /// </summary>
        void Learn(World w, MagazynCfg m)
        {
            string one = string.IsNullOrEmpty(m.LearnKw) ? "!learn" : m.LearnKw;
            string many = string.IsNullOrEmpty(m.LearnManyKw) ? "!learnMany" : m.LearnManyKw;
            var q = new List<Ing.MyProductionItem>();
            foreach (var a in w.AllAssemblers)
            {
                var asm = (Ing.IMyAssembler)a;
                string name = asm.CustomName ?? "";
                bool isMany = name.IndexOf(many, StringComparison.OrdinalIgnoreCase) >= 0;
                bool isOne = !isMany && name.IndexOf(one, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!isOne && !isMany) continue;
                q.Clear();
                try { asm.GetQueue(q); } catch { continue; }
                if (q.Count == 0) continue;
                foreach (var it in q)
                {
                    MyBlueprintDefinitionBase bp = null;
                    try { bp = MyDefinitionManager.Static.GetBlueprintDefinition(it.BlueprintId); } catch { }
                    if (bp?.Results == null || bp.Results.Length == 0) continue;
                    var res = bp.Results[0];
                    string key = res.Id.ToString();
                    double amount = Math.Round((double)it.Amount * (double)res.Amount);
                    if (amount <= 0) continue;
                    var quota = m.Quotas.FirstOrDefault(z => z.Id == key);
                    if (quota == null) m.Quotas.Add(quota = new Quota { Id = key });
                    quota.Amount = amount;
                    Note(T("nauka: ", "learned: ") + Ctx.ItemName(res.Id) + " = " + Fmt.Amount(amount));
                }
                try { asm.ClearQueue(); } catch { }
                if (isOne)
                    try
                    {
                        int i = name.IndexOf(one, StringComparison.OrdinalIgnoreCase);
                        asm.CustomName = (name.Substring(0, i) + name.Substring(i + one.Length)).Replace("  ", " ").Trim();
                    }
                    catch { }
                Plugin.Cfg?.Save();
            }
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
