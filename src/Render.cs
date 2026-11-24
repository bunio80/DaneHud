using System;
using System.Collections.Generic;
using Sandbox.Game.Gui;
using Sandbox.Graphics;
using Sandbox.Graphics.GUI;
using Sandbox.ModAPI;
using VRage.Utils;
using VRageMath;

namespace DaneHud
{
    /// <summary>Rysowanie jednego okna na ekranie.</summary>
    internal static class Render
    {
        const string Blank = @"Textures\GUI\Blank.dds";
        const float Pad = 0.005f;

        /// <summary>Lewa krawędź PRAWDZIWEGO ekranu (współrzędne GUI 0..1 dotyczą środkowego pola 4:3).</summary>
        static float ScreenLeft
        {
            get
            {
                try { return MyGuiManager.ComputeFullscreenGuiCoordinate(MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP, 6, 0).X; }
                catch { return 0.005f; }
            }
        }

        static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;

        internal static float LineH(WindowCfg w) => 0.034f * w.Scale * Math.Max(0.5f, w.LineSpacing);

        static float RowH(Row r, float lh)
        {
            switch (r.Kind)
            {
                case RowKind.Space: return lh * 0.6f;
                case RowKind.Bar: return lh * 0.95f;
                default: return lh;
            }
        }

        static readonly Dictionary<string, string> fitCache = new Dictionary<string, string>();

        /// <summary>Skraca tekst z lewej kolumny, żeby nie wchodził na liczby po prawej.</summary>
        static string Fit(string font, string s, float scale, float maxW)
        {
            if (s.Length < 4) return s;
            string key = font + "|" + scale.ToString("0.000") + "|" + maxW.ToString("0.000") + "|" + s;
            if (fitCache.TryGetValue(key, out var r)) return r;
            r = s;
            try
            {
                if (MyGuiManager.MeasureString(font, s, scale).X > maxW)
                {
                    int lo = 1, hi = s.Length - 1;
                    while (lo < hi)
                    {
                        int mid = (lo + hi + 1) / 2;
                        if (MyGuiManager.MeasureString(font, s.Substring(0, mid) + "..", scale).X <= maxW) lo = mid; else hi = mid - 1;
                    }
                    r = s.Substring(0, lo).TrimEnd() + "..";
                }
            }
            catch { }
            if (fitCache.Count > 2000) fitCache.Clear();
            fitCache[key] = r;
            return r;
        }

        // ------------------------------------------------------------------ styl tekstu (gra nie ma pogrubionych czcionek)

        static int style;          // 0 zwykły, 1 pogrubiony, 2 bardzo gruby, 3 obrys, 4 pogrubiony + obrys, 5 cień
        static Color outline;
        static Vector2 px;         // przesunięcie o piksel (w jednostkach GUI)

        static readonly Vector2[] Ring =
        {
            new Vector2(-1, -1), new Vector2(0, -1), new Vector2(1, -1), new Vector2(-1, 0),
            new Vector2(1, 0), new Vector2(-1, 1), new Vector2(0, 1), new Vector2(1, 1),
        };

        static void Text(string font, string s, Vector2 p, float scale, Color c, MyGuiDrawAlignEnum a, bool full, float maxW)
        {
            if (string.IsNullOrEmpty(s)) return;
            var oc = outline; oc.A = c.A;
            var dx = new Vector2(px.X, 0f);
            switch (style)
            {
                case 1:
                    MyGuiManager.DrawString(font, s, p + dx, scale, c, a, full, maxW);
                    break;
                case 2:
                    MyGuiManager.DrawString(font, s, p + dx, scale, c, a, full, maxW);
                    MyGuiManager.DrawString(font, s, p + new Vector2(0f, px.Y), scale, c, a, full, maxW);
                    MyGuiManager.DrawString(font, s, p + px, scale, c, a, full, maxW);
                    break;
                case 3:
                    foreach (var r in Ring) MyGuiManager.DrawString(font, s, p + r * px, scale, oc, a, full, maxW);
                    break;
                case 4:
                    foreach (var r in Ring)
                    {
                        MyGuiManager.DrawString(font, s, p + r * px, scale, oc, a, full, maxW);
                        if (r.X > 0) MyGuiManager.DrawString(font, s, p + dx + r * px, scale, oc, a, full, maxW);
                    }
                    MyGuiManager.DrawString(font, s, p + dx, scale, c, a, full, maxW);
                    break;
                case 5:
                    MyGuiManager.DrawString(font, s, p + px * 1.5f, scale, oc, a, full, maxW);
                    break;
            }
            MyGuiManager.DrawString(font, s, p, scale, c, a, full, maxW);
        }

        public static void Draw(WindowCfg w, List<Row> rows)
        {
            if (rows == null) return;
            string font = w.FontId;
            float scale = w.Scale;
            style = w.Style;
            outline = WindowCfg.ParseColor(w.OutlineColor, Color.Black);
            try { px = MyGuiManager.GetNormalizedSizeFromScreenSize(Vector2.One) * Math.Max(1f, (float)Math.Round(scale * 1.6f)); }
            catch { px = new Vector2(0.0006f, 0.001f); }
            float lh = LineH(w);
            float width = Clamp(w.Width, 0.05f, 1.2f);

            bool title = w.ShowTitle && !string.IsNullOrEmpty(w.Title);
            float height = (title ? lh * 1.35f : 0f);
            foreach (var r in rows) height += RowH(r, lh);
            height += Pad * 2;

            float left = ScreenLeft, right = 1f - left;
            float x = left + Math.Max(0f, right - left - width) * Clamp(w.X, 0f, 100f) / 100f;
            float top = 0.005f, bottom = 0.995f;
            float y = top + Math.Max(0f, bottom - top - height) * Clamp(w.Y, 0f, 100f) / 100f;

            if (w.BgAlpha > 0)
            {
                var bg = WindowCfg.ParseColor(w.BgColor, Color.Black);
                bg.A = (byte)Math.Max(0, Math.Min(255, w.BgAlpha));
                MyGuiManager.DrawSpriteBatch(Blank, new Vector2(x - Pad, y), new Vector2(width + Pad * 2, height),
                    bg, MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP, false, true);
            }
            y += Pad;

            if (title)
            {
                Text(font, w.Title, new Vector2(x, y), scale * 1.1f,
                    WindowCfg.ParseColor(w.TitleColor, Color.White), MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP, false, width);
                y += lh * 1.35f;
            }

            foreach (var r in rows)
            {
                float h = RowH(r, lh);
                switch (r.Kind)
                {
                    case RowKind.Text:
                        if (r.AlignRight)
                            Text(font, r.Left ?? "", new Vector2(x + width, y), scale, r.LeftColor,
                                MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_TOP, false, width);
                        else if (r.Center)
                            Text(font, r.Left ?? "", new Vector2(x + width / 2f, y), scale, r.LeftColor,
                                MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_TOP, false, width);
                        else
                            Text(font, r.Left ?? "", new Vector2(x, y), scale, r.LeftColor,
                                MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP, false, width);
                        break;

                    case RowKind.Pair:
                    {
                        string rt = r.Right ?? "";
                        float rw = 0f;
                        try { rw = MyGuiManager.MeasureString(font, rt, scale).X; } catch { }
                        Text(font, rt, new Vector2(x + width, y), scale, r.RightColor,
                            MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_TOP, false, width);
                        float lw = Math.Max(0.01f, width - rw - 0.008f);
                        Text(font, Fit(font, r.Left ?? "", scale, lw), new Vector2(x, y), scale, r.LeftColor,
                            MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP, false, lw);
                        break;
                    }

                    case RowKind.Cols:
                    {
                        var cols = r.Cols ?? new string[0];
                        int n = cols.Length;
                        if (n == 0) break;
                        float firstW = n > 1 ? width * (r.First > 0 ? r.First : 0.16f) : width;
                        Text(font, Fit(font, cols[0] ?? "", scale, firstW), new Vector2(x, y), scale, r.LeftColor,
                            MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP, false, firstW);
                        float colW = n > 1 ? (width - firstW) / (n - 1) : 0f;
                        for (int ci = 1; ci < n; ci++)
                        {
                            float rx = x + firstW + colW * ci;
                            Text(font, Fit(font, cols[ci] ?? "", scale, colW - 0.006f), new Vector2(rx, y), scale, r.RightColor,
                                MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_TOP, false, colW);
                        }
                        break;
                    }

                    case RowKind.Bar:
                    {
                        string pct = r.Right ?? "";
                        float pw = 0.05f;
                        try { pw = MyGuiManager.MeasureString(font, "100.0%", scale).X + 0.006f; } catch { }
                        float bw = Math.Max(0.02f, width - pw);
                        float bh = lh * 0.5f;
                        float by = y + (h - bh) / 2f + lh * 0.1f;   // odstęp od wiersza nad paskiem
                        var dark = new Color(r.BarColor.R / 5, r.BarColor.G / 5, r.BarColor.B / 5, 170);
                        MyGuiManager.DrawSpriteBatch(Blank, new Vector2(x, by), new Vector2(bw, bh), dark,
                            MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP, false, true);
                        if (r.Fraction > 0f)
                            MyGuiManager.DrawSpriteBatch(Blank, new Vector2(x, by), new Vector2(bw * r.Fraction, bh), r.BarColor,
                                MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP, false, true);
                        Text(font, pct, new Vector2(x + width, y + lh * 0.1f), scale * 0.9f, r.RightColor,
                            MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_TOP, false, pw);
                        break;
                    }
                }
                y += h;
            }
        }
    }

    /// <summary>Przezroczysta nakładka bez fokusu, rysuje okna, gdy siedzisz w gridzie. Nie przejmuje klawiatury ani myszy.</summary>
    internal class Overlay : MyGuiScreenBase
    {
        public Overlay() : base(Vector2.Zero, null, Vector2.One, false, null, 0f, 0f, null)
        {
            CanHaveFocus = false;
            m_drawEvenWithoutFocus = true;
            typeof(MyGuiScreenBase).GetField("m_drawMouseCursor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(this, false);
            EnabledBackgroundFade = false;
            CanHideOthers = false;
            CloseButtonEnabled = false;
        }

        public override string GetFriendlyName() => "DaneHudOverlay";

        public override bool Draw()
        {
            try
            {
                var cfg = Plugin.Cfg;
                if (cfg == null || !cfg.Visible || !Plugin.Seated) return true;
                if (!MyHud.IsVisible) return true;
                if (MyAPIGateway.Gui != null && MyAPIGateway.Gui.IsCursorVisible) return true;
                var shown = Plugin.Shown;
                if (shown == null) return true;
                for (int i = 0; i < shown.Windows.Count && i < shown.Rows.Length; i++)
                    if (shown.Windows[i].Enabled) Render.Draw(shown.Windows[i], shown.Rows[i]);
            }
            catch (Exception e) { Plugin.LogOnce("draw", e); }
            return true;
        }
    }
}
