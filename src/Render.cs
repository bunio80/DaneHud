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
                case RowKind.Bar: return lh * 0.85f;
                default: return lh;
            }
        }

        public static void Draw(WindowCfg w, List<Row> rows)
        {
            if (rows == null) return;
            string font = w.FontId;
            float scale = w.Scale;
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
                MyGuiManager.DrawString(font, w.Title, new Vector2(x, y), scale * 1.1f,
                    WindowCfg.ParseColor(w.TitleColor, Color.White), MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP, false, width);
                y += lh * 1.35f;
            }

            foreach (var r in rows)
            {
                float h = RowH(r, lh);
                switch (r.Kind)
                {
                    case RowKind.Text:
                        if (r.Center)
                            MyGuiManager.DrawString(font, r.Left ?? "", new Vector2(x + width / 2f, y), scale, r.LeftColor,
                                MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_TOP, false, width);
                        else
                            MyGuiManager.DrawString(font, r.Left ?? "", new Vector2(x, y), scale, r.LeftColor,
                                MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP, false, width);
                        break;

                    case RowKind.Pair:
                    {
                        string rt = r.Right ?? "";
                        float rw = 0f;
                        try { rw = MyGuiManager.MeasureString(font, rt, scale).X; } catch { }
                        MyGuiManager.DrawString(font, rt, new Vector2(x + width, y), scale, r.RightColor,
                            MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_TOP, false, width);
                        float lw = Math.Max(0.01f, width - rw - 0.008f);
                        MyGuiManager.DrawString(font, r.Left ?? "", new Vector2(x, y), scale, r.LeftColor,
                            MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP, false, lw);
                        break;
                    }

                    case RowKind.Bar:
                    {
                        string pct = r.Right ?? "";
                        float pw = 0.05f;
                        try { pw = MyGuiManager.MeasureString(font, "100.0%", scale).X + 0.006f; } catch { }
                        float bw = Math.Max(0.02f, width - pw);
                        float bh = lh * 0.5f;
                        float by = y + (h - bh) / 2f;
                        var dark = new Color(r.BarColor.R / 5, r.BarColor.G / 5, r.BarColor.B / 5, 170);
                        MyGuiManager.DrawSpriteBatch(Blank, new Vector2(x, by), new Vector2(bw, bh), dark,
                            MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP, false, true);
                        if (r.Fraction > 0f)
                            MyGuiManager.DrawSpriteBatch(Blank, new Vector2(x, by), new Vector2(bw * r.Fraction, bh), r.BarColor,
                                MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP, false, true);
                        MyGuiManager.DrawString(font, pct, new Vector2(x + width, y), scale * 0.9f, r.RightColor,
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
                var rows = Plugin.Rows;
                for (int i = 0; i < cfg.Windows.Count && i < rows.Length; i++)
                    if (cfg.Windows[i].Enabled) Render.Draw(cfg.Windows[i], rows[i]);
            }
            catch (Exception e) { Plugin.LogOnce("draw", e); }
            return true;
        }
    }
}
