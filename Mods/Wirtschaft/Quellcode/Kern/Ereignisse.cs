// Wirtschaft-Mod – Dynamische Sonderereignisse
// Reiner C#-Code ohne Spiel-Abhängigkeiten.
//
// Ablauf: Nach jeder Monatsbuchung einer Firma (Bank.Tageswechsel setzt ZuletztAbgerechnet weiter) wird
// genau einmal geprüft, ob ein Sonderereignis passt. Die geprüfte Periode wird gespeichert, damit nach einem
// Neustart nichts doppelt ausgelöst wird. Ob etwas passiert, hängt von der Liquidität (Firmenkonto) im Verhältnis
// zu Mindestliquidität, Zielbereich und Obergrenze ab, von Häufigkeit und Zufall. Es gibt keine Garantie:
// Eine Firma kann monatelang ohne Ereignis laufen und auch unter der Mindestliquidität bleiben.
//
// Gebucht wird nur über das Firmenkonto (wie jede andere Buchung) mit Eintrag im Verlauf. Umsatz, Ausgaben,
// Mitarbeiter, Immobilien, Firmenwert und Börse bleiben unverändert. Den Geldtresor gleicht der Geldtresor-Takt an.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Wirtschaft.Kern
{
    public class SonderGrenzen
    {
        public decimal Min, ZielVon, ZielBis, Max;
        public bool Auto;
    }

    public static class Ereignisse
    {
        const int MaxProtokoll = 60;
        static readonly Random zufall = new Random();

        public static string HaeufigkeitName(int s) { return s <= 0 ? "selten" : (s == 1 ? "normal" : "häufig"); }
        public static string IntensitaetName(int s) { return s <= 0 ? "schwach" : (s == 1 ? "normal" : "stark"); }

        /// <summary>Grundchance pro Buchungsperiode in Prozent (vor Zuschlägen durch die Liquiditätslage).</summary>
        public static int Grundchance(int haeufigkeit) { return haeufigkeit <= 0 ? 12 : (haeufigkeit == 1 ? 25 : 45); }

        static decimal IntensitaetFaktor(int s) { return s <= 0 ? 0.6m : (s == 1 ? 1.0m : 1.5m); }

        static decimal Auf100(decimal d) { return Math.Max(100m, Math.Round(d / 100m, MidpointRounding.AwayFromZero) * 100m); }

        // ---------- Grenzen ----------

        /// <summary>Vorschlag aus den Firmendaten: Mindestliquidität etwa ein Monat Ausgaben (mindestens 500),
        /// Ziel 1,6- bis 2-fach, Obergrenze 3,2-fach. Kleiner KFZ-Händler (Ausgaben 2.500): 2.500 / 4.000–5.000 / 8.000.</summary>
        public static SonderGrenzen Vorschlag(Unternehmen u)
        {
            decimal basis = Math.Max(Math.Max(u.AusgabenMonat, u.UmsatzMonat * 0.4m), Math.Max(u.Mitarbeiter * 300m, 500m));
            decimal min = Auf100(basis);
            return new SonderGrenzen { Min = min, ZielVon = Auf100(min * 1.6m), ZielBis = Auf100(min * 2m), Max = Auf100(min * 3.2m), Auto = true };
        }

        /// <summary>Die gültigen Grenzen: eigene Werte, wo gesetzt (größer 0), sonst Vorschlag.</summary>
        public static SonderGrenzen Grenzen(Unternehmen u)
        {
            var v = Vorschlag(u);
            var g = new SonderGrenzen
            {
                Min = u.SonderMin > 0 ? u.SonderMin : v.Min,
                ZielVon = u.SonderZielVon > 0 ? u.SonderZielVon : v.ZielVon,
                ZielBis = u.SonderZielBis > 0 ? u.SonderZielBis : v.ZielBis,
                Max = u.SonderMax > 0 ? u.SonderMax : v.Max,
                Auto = u.SonderMin <= 0 && u.SonderZielVon <= 0 && u.SonderZielBis <= 0 && u.SonderMax <= 0
            };
            return g;
        }

        /// <summary>Prüft eigene Grenzen. null = in Ordnung, sonst der Grund.</summary>
        public static string GrenzenPruefen(decimal min, decimal zielVon, decimal zielBis, decimal max)
        {
            if (min < 0 || zielVon < 0 || zielBis < 0 || max < 0) return "Keine negativen Werte.";
            if (!(min < zielVon)) return "Mindestliquidität muss kleiner als der Zielbereich sein.";
            if (zielVon > zielBis) return "Zielbereich: 'von' darf nicht größer als 'bis' sein.";
            if (!(zielBis < max)) return "Die Obergrenze muss über dem Zielbereich liegen.";
            return null;
        }

        /// <summary>Lage der Liquidität: -2 unter Min, -1 unter Ziel, 0 im Ziel, 1 über Ziel, 2 an/über der Obergrenze.</summary>
        public static int Lage(Unternehmen u, SonderGrenzen g)
        {
            decimal l = u.Konto;
            if (l <= g.Min) return -2;
            if (l < g.ZielVon) return -1;
            if (l >= g.Max) return 2;
            if (l > g.ZielBis) return 1;
            return 0;
        }

        public static string LageName(int lage)
        {
            switch (lage)
            {
                case -2: return "unter Mindestliquidität";
                case -1: return "unter dem Ziel";
                case 1: return "über dem Ziel";
                case 2: return "an der Obergrenze";
                default: return "im Zielbereich";
            }
        }

        // ---------- Beträge ----------

        /// <summary>Typische Ereignisgröße der Firma: aus Umsatz, Einkommen und Mitarbeitern, mit Intensität.</summary>
        public static decimal Grundgroesse(Bank bank, Unternehmen u)
        {
            decimal einkommen = 0m;
            try { einkommen = Math.Abs(bank.Vorschau(u).Einkommen); } catch (Exception) { }
            decimal g = Math.Max(Math.Max(u.UmsatzMonat * 0.35m, einkommen * 0.8m), Math.Max(u.Mitarbeiter * 250m, 200m));
            decimal schwankung = Branchen.VonFirma(u).Schwankung;
            if (schwankung <= 0) schwankung = 1m;
            return Math.Ceiling(g * IntensitaetFaktor(u.SonderIntensitaet) * schwankung);
        }

        static decimal ZufallZwischen(decimal von, decimal bis)
        {
            if (bis <= von) return von;
            return von + (bis - von) * (decimal)zufall.NextDouble();
        }

        /// <summary>Betrag für ein automatisches Ereignis. Über der Obergrenze zieht er Richtung Zielbereich, unter dem Minimum
        /// Richtung Ziel, sonst eine normale Größe. Immer gedeckelt auf das Doppelte der Grundgröße (passend zur Firmengröße)
        /// und so, dass ein negatives Ereignis nie unter die Mindestliquidität und ein positives nie über die Obergrenze führt.</summary>
        static decimal AutoBetrag(Bank bank, Unternehmen u, SonderVorlage v, SonderGrenzen g, int lage)
        {
            decimal groesse = Grundgroesse(bank, u);
            decimal b = groesse * ZufallZwischen(v.FaktorVon, v.FaktorBis);
            decimal mitte = (g.ZielVon + g.ZielBis) / 2m;
            if (!v.Positiv && lage >= 1) b = Math.Max(b, (u.Konto - mitte) * ZufallZwischen(0.6m, 1.0m));
            if (v.Positiv && lage <= -1) b = Math.Max(b, (mitte - u.Konto) * ZufallZwischen(0.4m, 0.9m));
            b = Math.Min(b, groesse * 2m);
            if (v.Positiv) b = Math.Min(b, g.Max - u.Konto);
            else b = Math.Min(b, u.Konto - g.Min);
            return Math.Floor(b);
        }

        // ---------- Buchen ----------

        static SonderEreignis Buchen(Unternehmen u, Branchenprofil p, SonderVorlage v, decimal betrag, int heute, bool manuell)
        {
            var e = new SonderEreignis
            {
                Tag = heute, Periode = u.ZuletztAbgerechnet, Titel = v.Titel, Text = v.Text, Kategorie = p.Name,
                Betrag = betrag, Vorher = u.Konto, Manuell = manuell
            };
            u.Konto += betrag;
            e.Nachher = u.Konto;
            u.Sonderprotokoll.Add(e);
            if (u.Sonderprotokoll.Count > MaxProtokoll) u.Sonderprotokoll.RemoveRange(0, u.Sonderprotokoll.Count - MaxProtokoll);
            u.Verlauf.Add(new Buchung { Tag = heute, Text = "Sonderereignis" + (manuell ? " (manuell)" : "") + ": " + v.Titel, Betrag = betrag });
            if (u.Verlauf.Count > 200) u.Verlauf.RemoveRange(0, u.Verlauf.Count - 200);
            return e;
        }

        public static string Meldung(Unternehmen u, SonderEreignis e)
        {
            return u.Name + ": " + e.Titel + " (" + (e.Betrag > 0 ? "+" : "") + Befehle.Betrag(e.Betrag) + "). " + e.Text;
        }

        /// <summary>Einmal pro Buchungsperiode prüfen. Gibt das gebuchte Ereignis zurück oder null.</summary>
        public static SonderEreignis Pruefen(Bank bank, Unternehmen u, int heute)
        {
            if (u.SonderPeriode == u.ZuletztAbgerechnet) return null;   // diese Periode schon geprüft
            u.SonderPeriode = u.ZuletztAbgerechnet;
            if (!u.SonderAn || !u.Laeuft || Finanzen.InKonkurs(u)) return null;

            var g = Grenzen(u);
            int lage = Lage(u, g);
            if (lage < 0) { u.SonderUnten++; u.SonderOben = 0; }
            else if (lage > 0) { u.SonderOben++; u.SonderUnten = 0; }
            else { u.SonderUnten = 0; u.SonderOben = 0; }

            // Chance: Grundchance plus Zuschlag je nach Lage und Dauer
            int chance = Grundchance(u.SonderHaeufigkeit);
            if (lage == 2) chance += 45;
            else if (lage == 1) chance += 15;
            else if (lage == -2) chance += 30;
            else if (lage == -1) chance += 10;
            if (u.SonderOben >= 2 || u.SonderUnten >= 2) chance += 15;
            if (chance > 90) chance = 90;
            if (zufall.Next(100) >= chance) return null;

            // Richtung: oben eher negativ, unten eher positiv, im Ziel ausgeglichen
            int positivProzent;
            switch (lage)
            {
                case 2: positivProzent = 10; break;
                case 1: positivProzent = 35; break;
                case -1: positivProzent = 65; break;
                case -2: positivProzent = 85; break;
                default: positivProzent = 50; break;
            }
            bool positiv = zufall.Next(100) < positivProzent;
            if (positiv && !u.SonderPositiv) return null;
            if (!positiv && !u.SonderNegativ) return null;

            var profil = Branchen.VonFirma(u);
            var liste = profil.Liste(positiv);
            if (liste.Count == 0) return null;
            var vorlage = liste[zufall.Next(liste.Count)];
            decimal betrag = AutoBetrag(bank, u, vorlage, g, lage);
            if (betrag < 1m) return null;   // kein plausibles Ereignis möglich (z. B. schon an der Grenze)
            return Buchen(u, profil, vorlage, positiv ? betrag : -betrag, heute, false);
        }

        /// <summary>Nach dem Tageswechsel aufrufen: prüft alle Firmen, deren Monatsbuchung neu ist.</summary>
        public static List<string> Monatslauf(Bank bank, int heute)
        {
            var meldungen = new List<string>();
            foreach (var u in bank.Stand.Unternehmen.ToList())
            {
                var e = Pruefen(bank, u, heute);
                // Meldung nur für Favoriten, und nur wenn die Meldungen insgesamt an sind. Das Protokoll speichert trotzdem alles.
                if (e != null && u.Favorit && bank.Stand.SonderBenachrichtigung) meldungen.Add(Meldung(u, e));
            }
            return meldungen;
        }

        /// <summary>Manuelles Ereignis. betrag = 0: Höhe passend zur Firmengröße. Darf bewusst außerhalb der Grenzen liegen;
        /// ein negatives Ereignis nimmt aber höchstens das vorhandene Guthaben.</summary>
        public static SonderEreignis Manuell(Bank bank, Unternehmen u, bool positiv, decimal betrag, int heute)
        {
            if (u == null) throw new WirtschaftsFehler("Keine Firma gewählt.");
            var profil = Branchen.VonFirma(u);
            var liste = profil.Liste(positiv);
            if (liste.Count == 0) throw new WirtschaftsFehler("Das Profil " + profil.Name + " hat keine " + (positiv ? "positiven" : "negativen") + " Ereignisse.");
            var vorlage = liste[zufall.Next(liste.Count)];
            decimal b = Math.Floor(Math.Abs(betrag));
            if (b <= 0) b = Math.Floor(Grundgroesse(bank, u) * ZufallZwischen(vorlage.FaktorVon, vorlage.FaktorBis));
            if (!positiv) b = Math.Min(b, Math.Floor(Math.Max(0m, u.Konto)));
            if (b < 1m) throw new WirtschaftsFehler("Kein Betrag möglich (für ein negatives Ereignis braucht die Firma Guthaben).");
            return Buchen(u, profil, vorlage, positiv ? b : -b, heute, true);
        }

        /// <summary>Ein- oder ausschalten. Beim Einschalten zählt erst die nächste Monatsbuchung (kein sofortiges Ereignis).</summary>
        public static void Schalten(Unternehmen u, bool an)
        {
            if (an && !u.SonderAn) u.SonderPeriode = u.ZuletztAbgerechnet;
            u.SonderAn = an;
        }

        // ---------- Anzeige ----------

        public static decimal Summe(Unternehmen u, bool plus)
        {
            return u.Sonderprotokoll.Where(e => plus ? e.Betrag > 0 : e.Betrag < 0).Sum(e => Math.Abs(e.Betrag));
        }

        public static string Protokoll(Unternehmen u, int anzahl)
        {
            if (u.Sonderprotokoll.Count == 0) return "Noch keine Sonderereignisse.";
            var zeilen = new List<string>();
            for (int i = u.Sonderprotokoll.Count - 1; i >= 0 && zeilen.Count < anzahl; i--)
            {
                var e = u.Sonderprotokoll[i];
                zeilen.Add("Tag " + e.Tag + "  " + (e.Betrag > 0 ? "+" : "") + Befehle.Betrag(e.Betrag) + "  " + e.Titel
                           + "  (" + Befehle.Betrag(e.Vorher) + " -> " + Befehle.Betrag(e.Nachher) + ", " + (e.Manuell ? "manuell" : "auto") + ")");
            }
            return string.Join("\n", zeilen.ToArray());
        }
    }
}
