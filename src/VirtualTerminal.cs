using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Sandbox.Game.Gui;
using VRage.Library.Collections;
using VRage.Utils;

namespace DaneHud
{
    /// <summary>
    /// Serwer wysyła klientowi zawartość ładowni tylko, gdy klient zgłasza otwarty terminal na tym gridzie
    /// (MyEntityInventoryStateGroup.GetGroupPriority: kontekst Inventory + ContextEntity w systemie terminala grida).
    /// Klient zgłasza to w MySpaceClientState.WriteInternal: 2 bity rodzaju kontekstu + 64 bity Id bloku.
    /// Gdy siedzisz w kokpicie, a prawdziwy terminal jest zamknięty, plugin zgłasza "Inventory" na kokpicie —
    /// dokładnie to samo, co gra wysyła przy otwartym K na tym kokpicie. Nic więcej się nie zmienia.
    /// </summary>
    internal static class VirtualTerminal
    {
        internal static long EntityId;      // 0 = wyłączone
        internal static bool Applied;
        const int ContextInventory = 2;     // MyContextKind dla strony Inventory (GetContextByPage: strona 0 -> 2)

        internal static void Apply()
        {
            try
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "SpaceEngineers.Game");
                Type t = null;
                if (asm != null)
                {
                    Type[] types;
                    try { types = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types.Where(x => x != null).ToArray(); }
                    t = types.FirstOrDefault(x => x.Name == "MySpaceClientState");
                }
                var m = t?.GetMethod("WriteInternal", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (m == null) { MyLog.Default.WriteLine("DaneHud: MySpaceClientState.WriteInternal not found — virtual terminal off"); return; }
                var h = new Harmony("DaneHud.VirtualTerminal");
                h.Patch(m, new HarmonyMethod(typeof(VirtualTerminal).GetMethod(nameof(Prefix), BindingFlags.Static | BindingFlags.NonPublic)));
                Applied = true;
                MyLog.Default.WriteLine("DaneHud: virtual terminal patch ok (" + t.FullName + ")");
            }
            catch (Exception e) { MyLog.Default.WriteLine("DaneHud: virtual terminal patch failed: " + e); }
        }

        static bool Prefix(BitStream stream, bool __runOriginal)
        {
            if (!__runOriginal) return false;          // inny plugin już zapisał kontekst w tej wiadomości
            long id = EntityId;
            if (id == 0) return true;
            try
            {
                // prawdziwy terminal na stronie Inventory / Control Panel / Production — zostaw grze
                if (MyGuiScreenTerminal.IsOpen)
                {
                    int page = (int)MyGuiScreenTerminal.GetCurrentScreen();
                    if (page >= 0 && page <= 2) return true;
                }
                stream.WriteInt32(ContextInventory, 2);
                stream.WriteInt64(id, 64);
                return false;
            }
            catch { return true; }
        }
    }
}
