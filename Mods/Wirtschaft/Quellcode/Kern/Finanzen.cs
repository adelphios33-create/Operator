// Wirtschaft-Mod – Schulden, Schuldenquote, Konkurs und Firma löschen
// Reiner C#-Code ohne Spiel-Abhängigkeiten. Die Kredite selbst (vergeben, Raten abbuchen) macht Bank.cs;
// hier kommen nur Auswertung, Sondertilgung, Konkursverfahren und das Löschen einer Firma dazu.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Wirtschaft.Kern
{
    public static class Finanzen
    {
        static bool Gleich(string a, string b) { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }

        // ---------- Anzeigen ----------

        /// <summary>Offene Kredite einer Firma.</summary>
        public static List<Kredit> Offene(Bank bank, Unternehmen u)
        {
            return bank.Stand.Kredite.Where(k => !k.Erledigt && Gleich(k.Nehmer, u.Name)).ToList();
        }

        public static decimal Schulden(Bank bank, Unternehmen u)
        {
            decimal s = 0m;
            foreach (var k in Offene(bank, u)) s += k.Rest;
            return s;
        }

        public static decimal RatenProMonat(Bank bank, Unternehmen u)
        {
            decimal s = 0m;
            foreach (var k in Offene(bank, u)) s += k.Rate;
            return s;
        }

        /// <summary>Schuldenquote in Prozent: Schulden ÷ Firmenwert. -1 = Schulden, aber kein Firmenwert.</summary>
        public static decimal Quote(Bank bank, Unternehmen u, decimal zusaetzlich)
        {
            decimal schulden = Schulden(bank, u) + zusaetzlich;
            decimal wert = bank.Firmenwert(u);
            if (schulden <= 0) return 0m;
            if (wert <= 0) return -1m;
            return Bank.Runden(schulden / wert * 100m);
        }

        public static string QuoteText(Bank bank, Unternehmen u, decimal zusaetzlich)
        {
            decimal q = Quote(bank, u, zusaetzlich);
            if (q < 0) return "– (kein Firmenwert)";
            string t = Befehle.Betrag(q) + " %";
            if (q == 0) return t + "  (schuldenfrei)";
            if (q <= 30) return t + "  (gesund)";
            if (q <= 60) return t + "  (in Ordnung)";
            if (q <= 100) return t + "  (hoch)";
            return t + "  (überschuldet)";
        }

        /// <summary>Wie viele Monate es mit der festen Rate noch dauert (nach Sondertilgungen kürzer).</summary>
        public static int RestMonate(Kredit k)
        {
            decimal rest = k.Rest;
            int grenze = Math.Max(0, k.Monate - k.Bezahlt);
            int n = 0;
            while (rest > 0.01m && n < grenze)
            {
                decimal z = Math.Ceiling(rest * k.Zins / 1200m);
                decimal t = k.Rate - z;
                if (t <= 0) return grenze;
                rest -= t;
                n++;
            }
            return n;
        }

        // ---------- Sondertilgung ----------

        /// <summary>Sondertilgung aus dem Firmenguthaben. Betrag 0 = ganz zurückzahlen. Die Rate bleibt, die Laufzeit wird kürzer.</summary>
        public static decimal Tilgen(Bank bank, Unternehmen u, Kredit k, decimal betrag, int heute)
        {
            if (k == null || k.Erledigt || !Gleich(k.Nehmer, u.Name)) throw new WirtschaftsFehler("Kein Kredit ausgewählt.");
            betrag = Math.Floor(betrag);   // ganze Coins
            if (betrag <= 0 || betrag > k.Rest) betrag = k.Rest;
            if (u.Konto < betrag)
                throw new WirtschaftsFehler("Nicht genug Guthaben: " + Befehle.Betrag(u.Konto) + " von " + Befehle.Betrag(betrag) + ".");
            u.Konto -= betrag;
            k.Rest = Bank.Runden(k.Rest - betrag);
            Buchen(u.Verlauf, heute, "Sondertilgung Kredit Nr. " + k.Nr, -betrag);
            Buchen(bank.Stand.Kassenbuch, heute, "Sondertilgung Kredit Nr. " + k.Nr + " von " + u.Name, -betrag);
            if (k.Rest <= 0.01m)
            {
                k.Rest = 0m;
                k.Erledigt = true;
                Buchen(u.Verlauf, heute, "Kredit Nr. " + k.Nr + " abbezahlt", 0m);
            }
            return betrag;
        }

        // ---------- Konkurs ----------

        /// <summary>Kredite plus Minus auf dem Firmenkonto.</summary>
        public static decimal Gesamtschulden(Bank bank, Unternehmen u) { return Schulden(bank, u) + Math.Max(0m, -u.Konto); }

        public static bool InKonkurs(Unternehmen u) { return u != null && u.KonkursUrteil > 0; }

        /// <summary>Überschuldet und zahlungsunfähig: Konto im Minus und Schulden mindestens so hoch wie der Firmenwert.</summary>
        public static bool Ueberschuldet(Bank bank, Unternehmen u)
        {
            decimal s = Gesamtschulden(bank, u);
            return u.Konto < 0 && s > 0 && s >= bank.Firmenwert(u);
        }

        public static string KonkursText(Unternehmen u, int heute)
        {
            if (!InKonkurs(u)) return "";
            int rest = u.KonkursUrteil - heute;
            return "KONKURS! Liquidationsverfahren seit Tag " + u.KonkursSeit + ". Urteil "
                 + (rest <= 0 ? "heute" : "in " + rest + (rest == 1 ? " Spieltag" : " Spieltagen")) + " (Tag " + u.KonkursUrteil + ").";
        }

        /// <summary>
        /// Täglich: Überschuldete Firmen gehen in Konkurs (Firma wird eingefroren, Gericht entscheidet nach 1 Monat).
        /// Ist die Firma bis dahin saniert (Konto wieder im Plus oder Schulden unter dem Firmenwert), wird das Verfahren eingestellt.
        /// Gibt die Firmen zurück, deren Urteil gefallen ist: sie werden danach liquidiert.
        /// </summary>
        public static List<Unternehmen> KonkursPruefen(Bank bank, int heute, List<string> meldungen)
        {
            var urteil = new List<Unternehmen>();
            foreach (var u in bank.Stand.Unternehmen.ToList())
            {
                bool ueber = Ueberschuldet(bank, u);
                if (!InKonkurs(u))
                {
                    if (!ueber) continue;
                    u.KonkursSeit = heute;
                    u.KonkursUrteil = heute + Math.Max(1, bank.Stand.TageProMonat);
                    u.Laeuft = false;
                    Buchen(u.Verlauf, heute, "KONKURS: Liquidationsverfahren eröffnet", 0m);
                    Buchen(bank.Stand.Kassenbuch, heute, "Konkurs " + u.Name + " (Schulden " + Befehle.Betrag(Gesamtschulden(bank, u)) + ")", 0m);
                    meldungen.Add("KONKURS: " + u.Name + " ist überschuldet. Gerichtsurteil an Tag " + u.KonkursUrteil + ".");
                }
                else if (!ueber)
                {
                    u.KonkursSeit = 0;
                    u.KonkursUrteil = 0;
                    Buchen(u.Verlauf, heute, "Konkursverfahren eingestellt (saniert)", 0m);
                    meldungen.Add("Verfahren gegen " + u.Name + " eingestellt: saniert. Firma wieder einschalten nicht vergessen.");
                }
                else
                {
                    u.Laeuft = false;
                    if (heute >= u.KonkursUrteil) urteil.Add(u);
                }
            }
            return urteil;
        }

        // ---------- Firma löschen (liquidieren) ----------

        /// <summary>
        /// Löscht eine Firma. Zuerst werden offene Kredite aus dem Guthaben zurückgezahlt (was fehlt, schreibt die Bank ab).
        /// Der Rest geht nach Anteilen an die Eigentümer. Den Geldtresor vorher lösen (macht der Aufrufer).
        /// </summary>
        public static string FirmaLoeschen(Bank bank, Unternehmen u, int heute)
        {
            if (u == null || !bank.Stand.Unternehmen.Contains(u)) throw new WirtschaftsFehler("Diese Firma gibt es nicht mehr.");
            decimal schulden = Schulden(bank, u);
            decimal konto = u.Konto;
            decimal getilgt = Math.Min(Math.Max(konto, 0m), schulden);
            konto -= getilgt;
            decimal abgeschrieben = schulden - getilgt;
            bank.Stand.Kredite.RemoveAll(k => !k.Erledigt && Gleich(k.Nehmer, u.Name));

            var teile = new List<string>();
            if (getilgt > 0) teile.Add("Kredite getilgt: " + Befehle.Betrag(getilgt));
            if (abgeschrieben > 0) teile.Add("abgeschrieben: " + Befehle.Betrag(abgeschrieben));

            if (konto > 0)
            {
                decimal verteilt = 0m;
                foreach (var a in u.Anteile)
                {
                    var p = bank.ParteiOderNull(a.Partei);
                    if (p == null) continue;
                    decimal b = Math.Floor(konto * a.Prozent / 100m);   // ganze Coins, Rest an den Besitzer
                    p.Konto += b;
                    verteilt += b;
                    Buchen(bank.Stand.Kassenbuch, heute, "Auflösung " + u.Name + " an " + p.Name, b);
                }
                decimal rest = konto - verteilt;
                var besitzer = bank.ParteiOderNull(u.Besitzer);
                if (rest != 0 && besitzer != null) { besitzer.Konto += rest; verteilt += rest; }
                teile.Add("an Eigentümer: " + Befehle.Betrag(verteilt));
            }

            bank.Stand.Unternehmen.Remove(u);
            // Erweiterte Verwaltung anderer Firmen: Verknüpfung mit dieser Firma lösen, der Eintrag bleibt als externer Partner
            foreach (var f in bank.Stand.Unternehmen)
                foreach (var e in f.Abm.Where(w => w.Art == "Lieferant" && Gleich(w.Hol("Firma"), u.Name))) e.Setz("Firma", "");
            Buchen(bank.Stand.Kassenbuch, heute, "Firma " + u.Name + " gelöscht", 0m);
            return "Firma " + u.Name + " gelöscht" + (teile.Count > 0 ? " (" + string.Join(", ", teile.ToArray()) + ")." : ".");
        }

        const int MaxVerlauf = 200;
        static void Buchen(List<Buchung> liste, int tag, string text, decimal betrag)
        {
            liste.Add(new Buchung { Tag = tag, Text = text, Betrag = betrag });
            if (liste.Count > MaxVerlauf) liste.RemoveRange(0, liste.Count - MaxVerlauf);
        }
    }
}
