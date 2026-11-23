using System;
using System.Globalization;

namespace DaneHud
{
    /// <summary>Formatowanie liczb jak w Automatic LCDs 2 ("13.9 k", "2.7 GW", "1h 54m 47s").</summary>
    internal static class Fmt
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static string N(double v) => v.ToString(v >= 100 ? "0.#" : "0.#", Inv);

        static string Scaled(double v, string[] units, double step = 1000.0)
        {
            int i = 0;
            double a = Math.Abs(v);
            while (a >= step && i < units.Length - 1) { v /= step; a /= step; i++; }
            return N(v) + units[i];
        }

        public static string Amount(double v)
        {
            if (double.IsNaN(v)) return "-";
            if (Math.Abs(v) < 1000)
                return Math.Abs(v - Math.Round(v)) < 0.005 ? Math.Round(v).ToString("0", Inv) : v.ToString(Math.Abs(v) < 10 ? "0.##" : "0.#", Inv);
            return Scaled(v, new[] { "", " k", " M", " G", " T" });
        }

        /// <summary>Objętość w litrach.</summary>
        public static string Volume(double liters) => Scaled(liters, new[] { " L", " kL", " ML", " GL" });

        /// <summary>Moc w MW.</summary>
        public static string Power(double mw)
        {
            if (Math.Abs(mw) < 1.0) return N(mw * 1000.0) + " kW";
            return Scaled(mw, new[] { " MW", " GW", " TW" });
        }

        /// <summary>Energia w MWh.</summary>
        public static string Energy(double mwh)
        {
            if (Math.Abs(mwh) < 1.0) return N(mwh * 1000.0) + " kWh";
            return Scaled(mwh, new[] { " MWh", " GWh", " TWh" });
        }

        /// <summary>Siła w N.</summary>
        public static string Force(double n) => Scaled(n, new[] { " N", " kN", " MN", " GN" });

        /// <summary>Masa w kg.</summary>
        public static string Mass(double kg) => Math.Abs(kg) < 1000 ? N(kg) + " kg" : Scaled(kg / 1000.0, new[] { " t", " kt", " Mt" });

        /// <summary>Przepływ gazu w L/s.</summary>
        public static string Rate(double lps) => Scaled(lps, new[] { " L/s", " kL/s", " ML/s" });

        public static string Pct(double f) => (f * 100.0).ToString("0.0", Inv) + "%";

        public static string Time(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) return "-";
            long s = (long)Math.Round(seconds);
            long d = s / 86400; s %= 86400;
            long h = s / 3600; s %= 3600;
            long m = s / 60; s %= 60;
            if (d > 0) return $"{d}d {h}h {m}m";
            if (h > 0) return $"{h}h {m}m {s}s";
            if (m > 0) return $"{m}m {s}s";
            return $"{s}s";
        }

        /// <summary>Krótszy czas do jednej linii rafinerii ("5h 12m").</summary>
        public static string TimeShort(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) return "-";
            long s = (long)Math.Round(seconds);
            long d = s / 86400; s %= 86400;
            long h = s / 3600; s %= 3600;
            long m = s / 60; s %= 60;
            if (d > 0) return $"{d}d {h}h";
            if (h > 0) return $"{h}h {m}m";
            if (m > 0) return $"{m}m {s}s";
            return $"{s}s";
        }

        public static string Symbol(string ore)
        {
            switch (ore)
            {
                case "Iron": return "Fe";
                case "Nickel": return "Ni";
                case "Cobalt": return "Co";
                case "Magnesium": return "Mg";
                case "Silicon": return "Si";
                case "Silver": return "Ag";
                case "Gold": return "Au";
                case "Platinum": return "Pt";
                case "Uranium": return "U";
                case "Stone": return "Kam";
                case "Scrap": return "Złom";
                case "Ice": return "Lód";
                case "Organic": return "Org";
            }
            return ore.Length <= 3 ? ore : ore.Substring(0, 3);
        }
    }
}
