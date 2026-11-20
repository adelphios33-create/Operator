// Figuren-Mod – Brücke zur Wirtschaft-Mod (Kredite, Firmen- und Personendaten für den KI-Banker)
// Die Wirtschaft-Mod ist eine eigene DLL. Damit Figuren auch ohne sie baut und läuft, wird sie per Reflection
// angesprochen: Typ "Wirtschaft.Spiel.WirtschaftMod", statische Methoden FinanzBericht, RatenText, KreditPruefen, KreditVergeben.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Figuren.Spiel
{
    public static class WirtschaftBruecke
    {
        static Type typ;
        static bool gesucht;

        static Type Typ
        {
            get
            {
                if (typ != null || gesucht) return typ;
                gesucht = true;
                try
                {
                    foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var t = a.GetType("Wirtschaft.Spiel.WirtschaftMod", false);
                        if (t != null) { typ = t; break; }
                    }
                }
                catch (Exception e) { Log.Warning("[Figuren] Wirtschaft-Mod nicht gefunden: " + e.Message); }
                if (typ == null) Log.Out("[Figuren] Wirtschaft-Mod nicht geladen: Banker ohne Kreditdaten.");
                return typ;
            }
        }

        public static bool Da { get { return Typ != null; } }

        static string Rufen(string methode, Type[] typen, object[] werte)
        {
            var t = Typ;
            if (t == null) return "";
            try
            {
                var m = t.GetMethod(methode, BindingFlags.Public | BindingFlags.Static, null, typen, null);
                if (m == null) { Log.Warning("[Figuren] Wirtschaft-Mod hat keine Methode " + methode + " (ältere Version? Wirtschaft neu bauen)."); return ""; }
                return (m.Invoke(null, werte) as string) ?? "";
            }
            catch (TargetInvocationException e) { return "NEIN: " + (e.InnerException != null ? e.InnerException.Message : e.Message); }
            catch (Exception e) { return "NEIN: " + e.Message; }
        }

        /// <summary>Spielstand der Wirtschaft-Mod (Objekt "Stand" mit Banken, Unternehmen, Parteien) oder null. Nur lesen.</summary>
        public static object StandHolen()
        {
            var t = Typ;
            if (t == null) return null;
            try
            {
                var pb = t.GetProperty("Bank", BindingFlags.Public | BindingFlags.Static);
                object bank = pb == null ? null : pb.GetValue(null, null);
                if (bank == null) return null;
                var ps = bank.GetType().GetProperty("Stand", BindingFlags.Public | BindingFlags.Instance);
                if (ps != null) return ps.GetValue(bank, null);
                var fs = bank.GetType().GetField("Stand", BindingFlags.Public | BindingFlags.Instance);
                return fs == null ? null : fs.GetValue(bank);
            }
            catch (Exception e) { Log.Warning("[Figuren] Wirtschaft-Stand nicht lesbar: " + e.Message); return null; }
        }

        public static string FinanzBericht(string stadt) { return Rufen("FinanzBericht", new[] { typeof(string) }, new object[] { stadt ?? "" }); }
        public static string RatenText(decimal summe) { return Rufen("RatenText", new[] { typeof(decimal) }, new object[] { summe }); }
        public static string KreditPruefen(string nehmer, decimal summe, int monate) { return Rufen("KreditPruefen", new[] { typeof(string), typeof(decimal), typeof(int) }, new object[] { nehmer, summe, monate }); }
        public static string KreditVergeben(string nehmer, decimal summe, int monate, string zweck) { return Rufen("KreditVergeben", new[] { typeof(string), typeof(decimal), typeof(int), typeof(string) }, new object[] { nehmer, summe, monate, zweck ?? "" }); }
    }

    /// <summary>Kreditangebote, die der KI-Banker in einer Antwort vorgeschlagen hat. Der Spieler bestätigt sie im Fenster per Knopf.</summary>
    public static class KreditAngebote
    {
        public class Angebot
        {
            public string Nehmer = "";
            public decimal Summe;
            public int Monate;
            public string Zweck = "";
            public string Pruefung = "";   // "OK: ..." oder "NEIN: ..."
            public bool Ok { get { return Pruefung.StartsWith("OK"); } }
        }

        static readonly Dictionary<string, Angebot> offen = new Dictionary<string, Angebot>(StringComparer.OrdinalIgnoreCase);

        static readonly Regex Marke = new Regex(@"\[\s*KREDIT\s*[:|]?\s*([^|\]]+?)\s*\|\s*([0-9][0-9.,\s]*)\s*(?:Dukes|Coins)?\s*\|\s*([0-9]+)\s*(?:Monate?)?\s*(?:\|\s*([^\]]*?))?\s*\]", RegexOptions.IgnoreCase);

        public static Angebot Offen(string rolle)
        {
            Angebot a;
            return rolle != null && offen.TryGetValue(rolle, out a) ? a : null;
        }

        public static void Verwerfen(string rolle) { if (rolle != null) offen.Remove(rolle); }

        static decimal Zahl(string s)
        {
            s = (s ?? "").Replace(" ", "").Trim();
            if (s.Contains(",") && s.Contains(".")) s = s.Replace(".", "").Replace(",", ".");
            else if (s.Contains(",")) s = s.Replace(",", ".");
            else if (s.Count(c => c == '.') > 1 || (s.Contains(".") && s.Length - s.LastIndexOf('.') == 4)) s = s.Replace(".", "");
            decimal d;
            return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out d) ? d : 0m;
        }

        /// <summary>Sucht die Kreditmarke in der Antwort, entfernt sie aus dem Text und merkt sich das Angebot (geprüft gegen die Regeln).</summary>
        public static string Herausziehen(string rolle, string antwort)
        {
            if (string.IsNullOrEmpty(antwort)) return antwort;
            var m = Marke.Match(antwort);
            if (!m.Success) return antwort;
            var a = new Angebot
            {
                Nehmer = m.Groups[1].Value.Trim().Trim('"', '„', '“'),
                Summe = Zahl(m.Groups[2].Value),
                Monate = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture),
                Zweck = m.Groups[4].Success ? m.Groups[4].Value.Trim() : ""
            };
            a.Pruefung = a.Summe > 0 ? WirtschaftBruecke.KreditPruefen(a.Nehmer, a.Summe, a.Monate) : "NEIN: Keine gültige Summe.";
            if (a.Pruefung.Length == 0) a.Pruefung = "NEIN: Wirtschaft-Mod nicht erreichbar.";
            offen[rolle] = a;
            return Marke.Replace(antwort, "").Trim();
        }

        /// <summary>Kredit wirklich vergeben (Knopf im Fenster). Das Ergebnis kommt als fester Fakt ins Gedächtnis des Bankers.</summary>
        public static string Abschliessen(string rolle)
        {
            var a = Offen(rolle);
            if (a == null) return "Kein offenes Kreditangebot.";
            string erg = WirtschaftBruecke.KreditVergeben(a.Nehmer, a.Summe, a.Monate, a.Zweck);
            if (erg.Length == 0) erg = "NEIN: Wirtschaft-Mod nicht erreichbar.";
            if (erg.StartsWith("OK"))
            {
                offen.Remove(rolle);
                Ki.Merken(rolle, "Kredit vergeben: " + erg.Substring(3).Trim() + (a.Zweck.Length > 0 ? " Zweck: " + a.Zweck + "." : ""));
                FigurenMod.Speichern();
            }
            return erg;
        }

        public static string Text(Angebot a)
        {
            return "Kreditangebot: " + a.Summe.ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".") + " Coins an " + a.Nehmer + ", " + a.Monate + " Monate. "
                   + (a.Ok ? a.Pruefung.Substring(3).Trim() : "Abgelehnt: " + a.Pruefung.Replace("NEIN:", "").Trim());
        }
    }
}
