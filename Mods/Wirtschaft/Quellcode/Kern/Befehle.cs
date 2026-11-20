// Wirtschaft-Mod – Konsolenbefehle (F1, "wb ...") und Textberichte
// Die Fenster von Hauptbank und Firmentresor sind der normale Weg. Die Befehle bleiben als Reserve.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Wirtschaft.Kern
{
    public static class Befehle
    {
        public const string Hilfe =
@"Wirtschaft – Befehle (F1, immer mit 'wb' davor). Namen mit Leerzeichen in Anführungszeichen.
Normalerweise reichen die Fenster von Hauptbank und Firmentresor.

  wb übersicht                                   alles auf einen Blick
  wb partei neu <Name> [Startkapital]
  wb regierung <Partei>                          diese Partei bekommt die Steuern
  wb aktiv <Partei>                              diese Partei spielst du gerade
  wb monat <Spieltage>                           wie viele Spieltage ein Monat hat
  wb test <Ingame-Minuten>                       Testmodus: 1 Tag = so viele Ingame-Minuten (0 = aus)
  wb bank neu <Name> [Regierung]                 neue Bank (Stadt) mit eigener Regierung
  wb firma neu <Name> <Besitzer> [Bank]
  wb firma <Name>                                Details
  wb firma <Name> setze <Feld> <Wert>            Felder: umsatz, ausgaben, steuer, besitzer,
                                                 bank, laeuft (ja/nein), mitarbeiter, immobilien,
                                                 fahrzeuge, branche, konto, wert (0 = automatisch)
  wb wert <Firma>                                Firmenwert mit allen Einzelteilen
  wb schilder                                    Schild-Sicherung: Stand prüfen (liste | wiederherstellen | auto an/aus)
  wb schild <x> <y> <z> [leeren | zurueck]       gesicherten Schildtext zeigen, absichtlich leeren, älteren nehmen
  wb kurs <Währung> <Coins>                      Auszahlungskurs, z. B. wb kurs Goldbarren 140
  wb abrechnen <Firma|Immobilie|Partei> <Monate> manuell abrechnen
  wb anteile <Firma> <Verkäufer> <Käufer> <Prozent> <Preis>
  wb immo neu <Name> <Besitzer> <Wert> <Miete/Monat> <Kosten/Monat>
  wb immo <Name> setze <Feld> <Wert>             Felder: wert, miete, kosten, steuer
  wb immo verkauf <Name> <Käufer> <Preis>
  wb überweisen <Von> <An> <Betrag>
  wb tresor <Firma>                              zuletzt angeklickten Tresor verbinden
  wb kredit liste                                alle Kredite
  wb kredit prüfen <Firma|Partei> <Summe> <Monate>   Regeln prüfen, ohne zu buchen
  wb kredit neu <Firma|Partei> <Summe> <Monate> [Zweck]   Kredit vergeben und auszahlen
  wb kredit regeln <MaxMonate> <MaxQuote%> <Zins%>   Obergrenzen fest: 36 Monate, 40 %. Nur senken möglich. Zins Standard 8 %
  wb kredit löschen <Nr>                         Kredit streichen (ohne Buchung)
  wb abm <Firma>                                 erweiterte Verwaltung der Firma anzeigen (Personal, Verträge ...)
  wb abm <Firma> an|aus                          erweiterte Verwaltung ein-/ausschalten (Daten bleiben)
  wb abm <Firma> modul <Personal|Lieferanten|Kunden|Produkte|Projekte|Logistik> an|aus
  wb abm <Firma> löhne                           offene Löhne zahlen (Handbetrieb)
  wb reset person|firma|stadt <Name> ja          Guthaben auf 0 setzen (ohne 'ja' nur Vorschau)";

        public static string Ausfuehren(Bank bank, IList<string> a, int heute)
        {
            try
            {
                if (a.Count == 0 || Ist(a[0], "hilfe", "help", "?")) return Hilfe;
                string cmd = a[0].ToLowerInvariant();

                switch (cmd)
                {
                    case "übersicht": case "uebersicht": case "liste":
                        return Uebersicht(bank, heute);

                    case "abm":
                        {
                            Min(a, 2);
                            var u = bank.Firma(a[1]);
                            if (a.Count == 2)
                            {
                                var sb = new StringBuilder();
                                sb.AppendLine(u.Name + ": erweiterte Verwaltung " + (u.AbmAn ? "AN" : "aus") + ", Module: " + (u.AbmModule.Length > 0 ? u.AbmModule : "keine")
                                    + ", Löhne " + (u.LohnManuell ? "manuell (" + u.LohnOffen + " Monat/e offen)" : "automatisch"));
                                Abm.FirmaBeschreiben(bank, u, sb, heute);
                                return sb.ToString().TrimEnd();
                            }
                            if (Ist(a[2], "an", "aus")) return Abm.AnSetzen(u, Ist(a[2], "an"), heute);
                            if (Ist(a[2], "modul"))
                            {
                                Min(a, 5);
                                string m = Abm.Module.FirstOrDefault(x => Ist(x, a[3]));
                                if (m == null) return "Module: " + string.Join(", ", Abm.Module);
                                return Abm.ModulSetzen(u, m, Ist(a[4], "an", "ja"), heute);
                            }
                            if (Ist(a[2], "löhne", "loehne")) return Abm.LoehneZahlen(bank, u, heute);
                            return "wb abm <Firma> [an|aus | modul <Name> an|aus | löhne]";
                        }

                    case "reset":
                        {
                            Min(a, 3);
                            bool ja = a.Count > 3 && Ist(a[3], "ja");
                            if (Ist(a[1], "stadt", "bank", "region"))
                            {
                                string folgen = bank.StadtResetFolgen(a[2]);
                                if (!ja) return folgen + " Zum Ausführen: wb reset stadt \"" + a[2] + "\" ja";
                                return "Erledigt. " + Betrag(bank.StadtZuruecksetzen(a[2], heute)) + " " + bank.Stand.Waehrung + " entfernt.";
                            }
                            if (Ist(a[1], "firma"))
                            {
                                var u = bank.Firma(a[2]);
                                if (!ja) return "Guthaben von " + u.Name + " (" + Betrag(u.Konto) + ") würde auf 0 gesetzt. Zum Ausführen: wb reset firma \"" + u.Name + "\" ja";
                                return u.Name + ": " + Betrag(bank.FirmaZuruecksetzen(u.Name, heute)) + " entfernt, Guthaben jetzt 0.";
                            }
                            if (Ist(a[1], "person", "partei", "regierung"))
                            {
                                var p = bank.ParteiHolen(a[2]);
                                if (!ja) return "Konto von " + p.Name + " (" + Betrag(p.Konto) + ") würde auf 0 gesetzt. Zum Ausführen: wb reset person \"" + p.Name + "\" ja";
                                return p.Name + ": " + Betrag(bank.ParteiZuruecksetzen(p.Name, heute)) + " entfernt, Konto jetzt 0.";
                            }
                            return "wb reset person|firma|stadt <Name> ja";
                        }

                    case "partei":
                        Min(a, 3);
                        if (!Ist(a[1], "neu")) return "Partei anlegen: wb partei neu <Name> [Startkapital]";
                        {
                            var p = bank.ParteiAnlegen(a[2], a.Count > 3 ? Zahl(a[3]) : 0m);
                            return "Partei " + p.Name + " angelegt, Konto " + Geld(bank, p.Konto) + ".";
                        }

                    case "regierung":
                        Min(a, 2);
                        bank.RegierungFestlegen(a[1]);
                        return a[1] + " ist jetzt die Regierung und bekommt alle Steuern.";

                    case "aktiv":
                        Min(a, 2);
                        bank.AktivSetzen(a[1], heute);
                        return "Aktive Partei: " + bank.Stand.AktivePartei + ". Ihre Firmen zählen ab heute (Tag " + heute + ").";

                    case "test":
                        Min(a, 2);
                        {
                            int m = (int)Zahl(a[1]);
                            if (m < 0 || m > 1440) throw new WirtschaftsFehler("Zwischen 0 und 1440 Minuten.");
                            bank.Stand.TestMinuten = m;
                            bank.ZeitNeuStarten = true;   // Zähler starten ab jetzt neu
                            return m == 0 ? "Testmodus aus: wieder normale Spieltage."
                                          : "Testmodus an: 1 Tag = " + m + " Ingame-Minuten. Mit 'Monat = 1 Tage' wird alle " + m + " Minuten gebucht.";
                        }

                    case "monat":
                        Min(a, 2);
                        bank.TageProMonatSetzen((int)Zahl(a[1]));
                        return "Ein Monat hat jetzt " + bank.Stand.TageProMonat + " Spieltage.";

                    case "bank":
                        Min(a, 3);
                        if (Ist(a[1], "loeschen")) { string bankFolgen = bank.BankLoeschFolgen(a[2]); bank.BankLoeschen(a[2]); return "Bank " + a[2] + " gelöscht. " + bankFolgen; }
                        if (Ist(a[1], "umbenennen") && a.Count > 3) { var bankNeu = bank.BankUmbenennen(a[2], a[3]); return "Bank " + a[2] + " heißt jetzt " + bankNeu.Name + "."; }
                        if (!Ist(a[1], "neu")) return "Bank anlegen: wb bank neu <Name> [Regierung]  |  wb bank loeschen <Name>  |  wb bank umbenennen <alt> <neu>";
                        {
                            var b = bank.BankAnlegen(a[2], a.Count > 3 ? a[3] : null);
                            return "Bank " + b.Name + " angelegt, Regierung " + b.Regierung + ".";
                        }

                    case "firma":
                        Min(a, 2);
                        if (Ist(a[1], "neu"))
                        {
                            Min(a, 4);
                            var u = bank.FirmaAnlegen(a[2], a[3], null, heute, a.Count > 4 ? a[4] : null);
                            return "Firma " + u.Name + " angelegt, Besitzer " + u.Besitzer + ", Bank " + u.Bank + ".";
                        }
                        if (a.Count >= 5 && Ist(a[2], "setze")) return FirmaSetzen(bank, bank.Firma(a[1]), a[3], a[4], heute);
                        return FirmaBericht(bank, bank.Firma(a[1]), heute);

                    case "abrechnen":
                        Min(a, 3);
                        {
                            var erg = bank.ManuellAbrechnen(a[1], (int)Zahl(a[2]), heute);
                            if (erg.Count == 0) return "Nichts abzurechnen.";
                            return string.Join("\n\n", erg.Select(x => RechnungBericht(bank, x)).ToArray());
                        }

                    case "anteile":
                        Min(a, 6);
                        bank.AnteileVerkaufen(a[1], a[2], a[3], Zahl(a[4]), Zahl(a[5]), heute);
                        return a[4] + " % von " + a[1] + " gehen an " + a[3] + ".";

                    case "immo": case "immobilie":
                        Min(a, 2);
                        if (Ist(a[1], "neu"))
                        {
                            Min(a, 7);
                            var i = bank.ImmobilieAnlegen(a[2], a[3], Zahl(a[4]), Zahl(a[5]), Zahl(a[6]), heute);
                            return "Immobilie " + i.Name + " angelegt, Besitzer " + i.Besitzer + ".";
                        }
                        if (Ist(a[1], "verkauf", "verkaufen"))
                        {
                            Min(a, 5);
                            bank.ImmobilieVerkaufen(a[2], a[3], Zahl(a[4]), heute);
                            return a[2] + " gehört jetzt " + bank.ImmobilieHolen(a[2]).Besitzer + ".";
                        }
                        if (a.Count >= 5 && Ist(a[2], "setze")) return ImmoSetzen(bank.ImmobilieHolen(a[1]), a[3], a[4]);
                        return ImmoBericht(bank, bank.ImmobilieHolen(a[1]));

                    case "kurs":
                        if (a.Count < 3)
                            return "Kurse: " + string.Join(", ", bank.Stand.Waehrungen.Select(x => x.Name + " = " + Betrag(x.Kurs)).ToArray());
                        {
                            var w = bank.WaehrungOderNull(a[1]);
                            if (w == null) throw new WirtschaftsFehler("Währung '" + a[1] + "' gibt es nicht.");
                            decimal k = Zahl(a[2]);
                            if (k <= 0) throw new WirtschaftsFehler("Der Kurs muss größer als 0 sein.");
                            w.Kurs = k;
                            return "1 " + w.Name + " = " + Betrag(k) + " Coins Guthaben.";
                        }

                    case "mitarbeiterwert":
                        return "Der Mitarbeiterwert ist jetzt eine feste Staffel: Mitarbeiter 1-5 je 1.000, 6-10 je 2.000, ab dem 11. je 3.000.";

                    case "wert":
                        Min(a, 2);
                        return WertBericht(bank, bank.Firma(a[1]));

                    case "tresor":
                        Min(a, 2);
                        bank.TresorVerbinden(a[1], bank.LetzterFreierTresor);
                        return "Firmentresor ist jetzt mit " + bank.Firma(a[1]).Name + " verbunden.";

                    case "kredit": case "kredite":
                        return Kredit(bank, a, heute);

                    case "überweisen": case "ueberweisen":
                        Min(a, 4);
                        bank.Ueberweisen(a[1], a[2], Zahl(a[3]), heute, "Überweisung");
                        return Geld(bank, Zahl(a[3])) + " von " + a[1] + " an " + a[2] + " überwiesen.";
                }
                return "Unbekannter Befehl '" + a[0] + "'. 'wb hilfe' zeigt alle Befehle.";
            }
            catch (WirtschaftsFehler f) { return "Fehler: " + f.Message; }
        }

        static string Kredit(Bank bank, IList<string> a, int heute)
        {
            string was = a.Count > 1 ? a[1].ToLowerInvariant() : "liste";
            switch (was)
            {
                case "liste":
                    return KreditListe(bank);
                case "prüfen": case "pruefen":
                    {
                        Min(a, 5);
                        decimal summe = Zahl(a[3]); int monate = (int)Zahl(a[4]);
                        string grund = bank.KreditGrund(a[2], summe, monate);
                        decimal rate = Bank.Annuitaet(summe, bank.Stand.KreditZins, monate);
                        return (grund == null ? "Kredit möglich: " : "Kredit NICHT möglich: " + grund + "\nWunsch: ")
                               + Geld(bank, summe) + " über " + monate + " Monate, Rate " + Geld(bank, rate) + " pro Monat (" + Betrag(bank.Stand.KreditZins) + " % Zins). Freie Rate: " + Geld(bank, bank.FreieRate(a[2])) + ".";
                    }
                case "neu":
                    {
                        Min(a, 5);
                        string zweck = a.Count > 5 ? string.Join(" ", a.Skip(5).ToArray()) : "";
                        var k = bank.KreditVergeben(a[2], Zahl(a[3]), (int)Zahl(a[4]), zweck, heute);
                        return "Kredit Nr. " + k.Nr + " an " + k.Nehmer + ": " + Geld(bank, k.Summe) + " ausgezahlt, " + k.Monate + " Monate, Rate " + Geld(bank, k.Rate) + " pro Monat.";
                    }
                case "regeln":
                    {
                        if (a.Count < 5) return "Kreditregeln: höchstens " + bank.Stand.KreditMaxMonate + " Monate, Rate höchstens " + Betrag(bank.Stand.KreditMaxQuote) + " % vom Nettoeinkommen, Zins " + Betrag(bank.Stand.KreditZins) + " % pro Jahr.\nÄndern: wb kredit regeln <MaxMonate> <MaxQuote%> <Zins%>";
                        bank.Stand.KreditMaxMonate = Math.Max(1, (int)Zahl(a[2]));
                        bank.Stand.KreditMaxQuote = Prozent(a[3]);
                        bank.Stand.KreditZins = Prozent(a[4]);
                        return "Kreditregeln gesetzt: " + bank.Stand.KreditMaxMonate + " Monate, " + Betrag(bank.Stand.KreditMaxQuote) + " %, Zins " + Betrag(bank.Stand.KreditZins) + " %. (Fest: nie mehr als " + Stand.KreditLaufzeitGrenze + " Monate und " + Betrag(Stand.KreditQuoteGrenze) + " %.)";
                    }
                case "löschen": case "loeschen":
                    {
                        Min(a, 3);
                        int nr = (int)Zahl(a[2]);
                        int n = bank.Stand.Kredite.RemoveAll(k => k.Nr == nr);
                        return n > 0 ? "Kredit Nr. " + nr + " gestrichen." : "Kredit Nr. " + nr + " gibt es nicht.";
                    }
            }
            return "wb kredit liste | prüfen | neu | regeln | löschen";
        }

        public static string KreditListe(Bank bank)
        {
            var l = bank.Stand.Kredite;
            if (l.Count == 0) return "Keine Kredite.";
            var sb = new StringBuilder("Kredite:\n");
            foreach (var k in l.OrderBy(w => w.Erledigt).ThenBy(w => w.Nr))
                sb.Append("Nr. ").Append(k.Nr).Append("  ").Append(k.Nehmer).Append(": ").Append(Geld(bank, k.Summe))
                  .Append(", Rate ").Append(Geld(bank, k.Rate)).Append(", ").Append(k.Bezahlt).Append("/").Append(k.Monate).Append(" Raten bezahlt, Rest ")
                  .Append(Geld(bank, k.Rest)).Append(k.Erledigt ? "  (abbezahlt)" : "").Append(k.Zweck.Length > 0 ? "  Zweck: " + k.Zweck : "").Append("\n");
            return sb.ToString().TrimEnd();
        }

        /// <summary>Bericht für den KI-Banker (Figuren-Mod): Regeln, Firmen und Personen mit Nettoeinkommen, freier Rate und Kreditrahmen, laufende Kredite.</summary>
        public static string FinanzBericht(Bank bank, string stadt)
        {
            var st = bank.Stand;
            var sb = new StringBuilder();
            sb.AppendLine("KREDITREGELN (verbindlich): Laufzeit höchstens " + st.KreditMaxMonate + " Monate. Die Monatsrate aller Kredite zusammen darf höchstens " + Betrag(st.KreditMaxQuote)
                          + " % vom Nettoeinkommen (nach allen Kosten und Steuern) betragen. Zins " + Betrag(st.KreditZins) + " % pro Jahr, feste Monatsrate.");
            decimal r12 = Bank.Annuitaet(1000m, st.KreditZins, 12), r24 = Bank.Annuitaet(1000m, st.KreditZins, 24), r36 = Bank.Annuitaet(1000m, st.KreditZins, Math.Min(36, st.KreditMaxMonate));
            sb.AppendLine("Monatsrate je 1.000 " + st.Waehrung + " Kredit: 12 Monate " + Betrag(r12) + ", 24 Monate " + Betrag(r24) + ", " + Math.Min(36, st.KreditMaxMonate) + " Monate " + Betrag(r36) + ".");
            int lz = st.KreditMaxMonate;
            var namen = new List<string>();
            namen.AddRange(st.Unternehmen.Select(u => u.Name));
            namen.AddRange(bank.Personen().Select(p => p.Name));
            if (namen.Count == 0) sb.AppendLine("Im Wirtschaftssystem sind noch keine Firmen oder Personen eingetragen. Ohne Daten kannst du keine Kreditzusage geben; der Spieler muss die Firma erst an der Hauptbank anlegen.");
            else
            {
                sb.AppendLine("KUNDEN (echte Daten aus der Bank; Beträge pro Monat):");
                foreach (var u in st.Unternehmen)
                {
                    var v = bank.Vorschau(u);
                    decimal frei = bank.FreieRate(u.Name);
                    string status = Finanzen.InKonkurs(u) ? " KONKURS (Liquidationsverfahren): KEINE Kredite!"
                                  : Finanzen.Ueberschuldet(bank, u) ? " ÜBERSCHULDET: KEINE Kredite!" : "";
                    sb.Append("- Firma ").Append(u.Name).Append(" (").Append(u.Branche).Append(", Bank ").Append(u.Bank).Append(", Besitzer ").Append(u.Besitzer).Append("): ")
                      .Append("Umsatz ").Append(Betrag(u.UmsatzMonat)).Append(", Ausgaben ").Append(Betrag(u.AusgabenMonat)).Append(", Steuer ").Append(Betrag(v.Steuer))
                      .Append(", Nettoeinkommen ").Append(Betrag(v.Einkommen)).Append(", Firmenkonto ").Append(Betrag(u.Konto))
                      .Append(", laufende Raten ").Append(Betrag(bank.LaufendeRaten(u.Name))).Append(", freie Rate ").Append(Betrag(frei))
                      .Append(", Schuldenquote ").Append(Finanzen.QuoteText(bank, u, 0m))
                      .Append(", Kreditrahmen höchstens ").Append(Betrag(Bank.MaxSumme(frei, st.KreditZins, 12))).Append(" (12 M) / ").Append(Betrag(Bank.MaxSumme(frei, st.KreditZins, lz))).Append(" (").Append(lz).Append(" M).").Append(status).AppendLine();
                }
                foreach (var p in bank.Personen())
                {
                    bool f;
                    decimal netto = bank.NettoMonat(p.Name, out f);
                    decimal frei = bank.FreieRate(p.Name);
                    sb.Append("- Person ").Append(p.Name).Append(": Konto ").Append(Betrag(p.Konto)).Append(", Nettoeinkommen ").Append(Betrag(netto))
                      .Append(" (Mieten und Firmenanteile), laufende Raten ").Append(Betrag(bank.LaufendeRaten(p.Name))).Append(", freie Rate ").Append(Betrag(frei))
                      .Append(", Kreditrahmen höchstens ").Append(Betrag(Bank.MaxSumme(frei, st.KreditZins, lz))).Append(" (").Append(lz).Append(" M).").AppendLine();
                }
            }
            var offen = st.Kredite.Where(k => !k.Erledigt).ToList();
            if (offen.Count > 0)
            {
                sb.AppendLine("LAUFENDE KREDITE:");
                foreach (var k in offen)
                    sb.Append("- Nr. ").Append(k.Nr).Append(" ").Append(k.Nehmer).Append(": ").Append(Betrag(k.Summe)).Append(", Rate ").Append(Betrag(k.Rate))
                      .Append(", ").Append(k.Bezahlt).Append("/").Append(k.Monate).Append(" bezahlt, Rest ").Append(Betrag(k.Rest)).Append(k.Zweck.Length > 0 ? ", Zweck " + k.Zweck : "").AppendLine();
            }
            return sb.ToString().Trim();
        }

        static string FirmaSetzen(Bank bank, Unternehmen u, string feld, string wert, int heute)
        {
            switch (feld.ToLowerInvariant())
            {
                case "umsatz": u.UmsatzMonat = Zahl(wert); break;
                case "ausgaben": u.AusgabenMonat = Zahl(wert); break;
                case "steuer": case "steuersatz": u.Steuersatz = Prozent(wert); break;
                case "besitzer": bank.BesitzerSetzen(u, wert, heute); break;
                case "bank": u.Bank = bank.BankhausHolen(wert).Name; break;
                case "mitarbeiter": u.Mitarbeiter = (int)Zahl(wert); break;
                case "immobilien": case "immobilienwert": u.Immobilienwert = Zahl(wert); break;
                case "fahrzeuge": case "fahrzeugwert": u.Fahrzeugwert = Zahl(wert); break;
                case "laeuft": case "läuft": bank.LaeuftSetzen(u, Ist(wert, "ja", "an", "1", "true"), heute); break;
                case "branche": u.Branche = wert; break;
                case "quote": case "ausschüttung": case "ausschuettung": u.Ausschuettungsquote = Prozent(wert); break;
                case "konto": u.Konto = Zahl(wert); break;
                case "wert": u.Wert = Zahl(wert); break;
                default: throw new WirtschaftsFehler("Unbekanntes Feld '" + feld + "'.");
            }
            return u.Name + ": " + feld + " = " + wert;
        }

        static string ImmoSetzen(Immobilie i, string feld, string wert)
        {
            switch (feld.ToLowerInvariant())
            {
                case "wert": i.Wert = Zahl(wert); break;
                case "miete": i.MieteMonat = Zahl(wert); break;
                case "kosten": i.KostenMonat = Zahl(wert); break;
                case "steuer": case "steuersatz": i.Steuersatz = Prozent(wert); break;
                default: throw new WirtschaftsFehler("Unbekanntes Feld '" + feld + "'.");
            }
            return i.Name + ": " + feld + " = " + wert;
        }

        // ---------- Berichte ----------

        public static string Uebersicht(Bank bank, int heute)
        {
            var s = new StringBuilder();
            s.AppendLine("=== Hauptbank – Tag " + heute + " – 1 Monat = " + bank.Stand.TageProMonat + " Spieltage – aktiv: " +
                         (string.IsNullOrEmpty(bank.Stand.AktivePartei) ? "keine" : bank.Stand.AktivePartei) + " ===");
            s.AppendLine("Banken:");
            foreach (var b in bank.Stand.Banken)
                s.AppendLine("  " + b.Name + "  Regierung " + b.Regierung + "  Steuern/Monat " + Geld(bank, bank.SteuernProMonat(b.Name)));
            s.AppendLine("Parteien:");
            foreach (var p in bank.Stand.Parteien)
                s.AppendLine("  " + p.Name + (bank.IstRegierungspartei(p) ? " (Regierung)" : "") + "  Konto " + Geld(bank, p.Konto));
            s.AppendLine("Unternehmen (pro Monat):");
            foreach (var u in bank.Stand.Unternehmen)
            {
                var r = bank.Vorschau(u);
                s.AppendLine("  " + u.Name + "  Bank " + u.Bank + "  Besitzer " + u.Besitzer + "  Umsatz " + Geld(bank, r.Umsatz) + "  Ausgaben " + Geld(bank, r.Ausgaben) +
                             "  Steuer " + Geld(bank, r.Steuer) + "  Einkommen " + Geld(bank, r.Einkommen) +
                             "  Guthaben " + Geld(bank, u.Konto) + (u.Laeuft ? "" : "  (pausiert)"));
            }
            s.AppendLine("Steuereinnahmen aller Regierungen pro Monat: " + Geld(bank, bank.SteuernProMonat(null)));
            return s.ToString().TrimEnd();
        }

        public static string FirmaBericht(Bank bank, Unternehmen u, int heute)
        {
            var r = bank.Vorschau(u);
            var s = new StringBuilder();
            s.AppendLine("=== " + u.Name + " [" + u.Branche + "] – Bank " + u.Bank + " – Besitzer " + u.Besitzer + " ===");
            s.AppendLine("Umsatz/Monat      " + Geld(bank, r.Umsatz));
            s.AppendLine("- Ausgaben        " + Geld(bank, r.Ausgaben));
            s.AppendLine("= Zwischensumme   " + Geld(bank, r.Zwischensumme));
            s.AppendLine("- Steuer " + u.Steuersatz + " %    " + Geld(bank, r.Steuer));
            s.AppendLine("= Einkommen       " + Geld(bank, r.Einkommen) + "  (bleibt in der Firma)");
            s.AppendLine("Guthaben der Firma: " + Geld(bank, u.Konto));
            s.AppendLine("Mitarbeiter " + u.Mitarbeiter + ", Immobilienwert " + Geld(bank, u.Immobilienwert) + ", Fahrzeugwert " + Geld(bank, u.Fahrzeugwert) + ", Firmenwert " + Geld(bank, bank.Firmenwert(u)));
            s.AppendLine("Anteile: " + string.Join(", ", u.Anteile.Select(x => x.Partei + " " + x.Prozent + " %").ToArray()));
            int t = bank.TageBisBuchung(u, heute);
            s.AppendLine(t < 0 ? "Pausiert (läuft nicht)" : "Nächste Buchung in " + t + " Spieltag(en)");
            return s.ToString().TrimEnd();
        }

        public static string ImmoBericht(Bank bank, Immobilie i)
        {
            return "=== " + i.Name + " ===\nBesitzer " + i.Besitzer + "  Wert " + Geld(bank, i.Wert) +
                   "\nMiete/Monat " + Geld(bank, i.MieteMonat) + "  Kosten/Monat " + Geld(bank, i.KostenMonat) + "  Steuer " + i.Steuersatz + " %";
        }

        public static string RechnungBericht(Bank bank, Rechnung r)
        {
            var s = new StringBuilder();
            s.AppendLine("Abrechnung " + r.Name + " (" + r.Monate + " Monat/e)");
            s.AppendLine("  Umsatz " + Geld(bank, r.Umsatz) + " - Ausgaben " + Geld(bank, r.Ausgaben) + " = " + Geld(bank, r.Zwischensumme));
            s.AppendLine("  Steuer an Regierung " + Geld(bank, r.Steuer) + ", Einkommen " + Geld(bank, r.Einkommen));
            foreach (var d in r.Auszahlungen) s.AppendLine("  an " + d.Key + ": " + Geld(bank, d.Value));
            if (r.Ruecklage != 0) s.AppendLine("  in der Firma: " + Geld(bank, r.Ruecklage));
            return s.ToString().TrimEnd();
        }

        // ---------- Hilfen ----------

        static readonly CultureInfo De = DeutscheZahlen();

        static CultureInfo DeutscheZahlen()
        {
            try { return new CultureInfo("de-DE"); }
            catch (Exception)
            {
                var c = (CultureInfo)CultureInfo.InvariantCulture.Clone();
                c.NumberFormat.NumberDecimalSeparator = ",";
                c.NumberFormat.NumberGroupSeparator = ".";
                return c;
            }
        }

        public static string Geld(Bank bank, decimal d) { return d.ToString("N2", De) + " " + bank.Stand.Waehrung; }

        /// <summary>Zahl ohne Währung, z. B. "12.500" oder "12.500,50".</summary>
        /// <summary>Firmenwert mit allen Einzelteilen und dem Branchenvergleich.</summary>
        public static string WertBericht(Bank bank, Unternehmen u)
        {
            var b = bank.Bewerten(u);
            var p = Branchen.VonFirma(u);
            var s = new StringBuilder();
            s.AppendLine("=== Firmenwert " + u.Name + " (" + b.Branche + ") ===");
            s.AppendLine("Umsatz " + Betrag(u.UmsatzMonat) + " x " + Betrag(b.Faktor) + "      " + Betrag(b.Umsatz));
            s.AppendLine("+ Mitarbeiter (" + u.Mitarbeiter + ", Staffel)   " + Betrag(b.Mitarbeiter));
            s.AppendLine("+ Immobilien                " + Betrag(b.Immobilien));
            s.AppendLine("+ Fahrzeuge                 " + Betrag(b.Fahrzeuge));
            s.AppendLine("+ Ertrag (" + Bank.Ertragsmonate + " Monatsgewinne)  " + Betrag(b.Ertrag));
            s.AppendLine("= Firmenwert                " + Betrag(b.Summe) + (b.Fest ? "  (fest eingetragen)" : ""));
            s.AppendLine("Umsatzrendite " + Betrag(Rendite(u)) + " %, in der Branche üblich " + Betrag(p.MargeVon) + "-" + Betrag(p.MargeBis) + " %");
            return s.ToString().TrimEnd();
        }

        /// <summary>Umsatzrendite vor Steuer in Prozent, auf eine Stelle gerundet.</summary>
        public static decimal Rendite(Unternehmen u)
        {
            if (u.UmsatzMonat <= 0) return 0m;
            return Math.Round((u.UmsatzMonat - u.AusgabenMonat) * 100m / u.UmsatzMonat, 1);
        }

        public static string Betrag(decimal d)
        {
            return d == Math.Truncate(d) ? d.ToString("N0", De) : d.ToString("N2", De);
        }

        static bool Ist(string s, params string[] werte)
        {
            return werte.Any(w => string.Equals(s, w, StringComparison.OrdinalIgnoreCase));
        }

        static void Min(IList<string> a, int n)
        {
            if (a.Count < n) throw new WirtschaftsFehler("Zu wenige Angaben. 'wb hilfe' zeigt, was der Befehl braucht.");
        }

        /// <summary>Nimmt "1500", "1.500", "1500,50", "1500.50" und "20%".</summary>
        public static bool VersucheZahl(string s, out decimal d)
        {
            s = (s ?? "").Trim().TrimEnd('%').Replace(" ", "");
            if (s.Length == 0) { d = 0m; return true; }
            if (s.Contains(",") && s.Contains(".")) s = s.Replace(".", "").Replace(",", ".");
            else if (s.Contains(",")) s = s.Replace(",", ".");
            else if (s.Count(c => c == '.') > 1 || (s.Contains(".") && s.Length - s.LastIndexOf('.') == 4)) s = s.Replace(".", "");
            return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out d);
        }

        public static decimal Zahl(string s)
        {
            decimal d;
            if (!VersucheZahl(s, out d)) throw new WirtschaftsFehler("'" + s + "' ist keine Zahl.");
            return d;
        }

        public static decimal Prozent(string s)
        {
            var d = Zahl(s);
            if (d < 0 || d > 100) throw new WirtschaftsFehler("Prozent müssen zwischen 0 und 100 liegen.");
            return d;
        }
    }
}
