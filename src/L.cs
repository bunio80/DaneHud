namespace DaneHud
{
    /// <summary>Dwa języki: polski (domyślny) i angielski. Wybór w /dane (Language w DaneHud.xml: 0 = PL, 1 = EN).</summary>
    internal static class L
    {
        internal static bool En => Plugin.Cfg != null && Plugin.Cfg.Language == 1;

        internal static string T(string pl, string en) => En ? en : pl;
    }
}
