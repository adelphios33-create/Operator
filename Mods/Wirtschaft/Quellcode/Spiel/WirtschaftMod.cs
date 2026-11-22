// Wirtschaft-Mod – Einstieg ins Spiel
// Geprüft gegen 7 Days to Die V 1.4 (b8): IModApi, ModEvents (GameStartDone, GameUpdate, WorldShuttingDown),
// GameIO.GetSaveGameDir, GameUtils.WorldTimeToDays, World.worldTime, Log.
using System;
using System.Collections.Generic;
using System.IO;
using Wirtschaft.Kern;

namespace Wirtschaft.Spiel
{
    public class WirtschaftMod : IModApi
    {
        /// <summary>Die Bank des aktuell geladenen Spielstands. Alle Blöcke und Befehle greifen darauf zu.</summary>
        public static Bank Bank { get; private set; }

        static int letzterTag = -1;
        static string ordner = "";

        public void InitMod(Mod _modInstance)
        {
            ModEvents.GameStartDone.RegisterHandler(SpielGestartet);
            ModEvents.GameUpdate.RegisterHandler(Aktualisieren);
            ModEvents.WorldShuttingDown.RegisterHandler(SpielBeendet);
            Log.Out("[Wirtschaft] Mod geladen.");
        }

        public static int Heute
        {
            get
            {
                var welt = GameManager.Instance == null ? null : GameManager.Instance.World;
                if (welt == null) return 0;
                // Testmodus: 1 "Tag" = TestMinuten Ingame-Minuten (24000 Ticks = 24 Ingame-Stunden)
                if (Bank != null && Bank.Stand.TestMinuten > 0)
                    return 1 + (int)(welt.worldTime / (ulong)Math.Max(1, Bank.Stand.TestMinuten * 24000 / 1440));
                return GameUtils.WorldTimeToDays(welt.worldTime);
            }
        }

        static void SpielGestartet()
        {
            try
            {
                ordner = GameIO.GetSaveGameDir();
                Bank = new Bank(Speicher.Laden(ordner));
                letzterTag = Heute;
                Bank.Heute = letzterTag;
                Abm.heuteFuerPruefen = letzterTag;
                // einmalig: Spieltage aus dem früheren Testmodus (in der Zukunft) auf heute setzen
                if (Bank.Stand.AbmTagStand < 1 && Bank.Stand.TestMinuten == 0)
                {
                    int n = Abm.TageKorrigieren(Bank, letzterTag);
                    if (n > 0) Log.Out("[Wirtschaft] " + n + " Vertrags-/Einstellungstage aus dem Testmodus auf heute (Tag " + letzterTag + ") gesetzt.");
                    Bank.Stand.AbmTagStand = 1;
                }
                if (Bank.ZeitNeuStarten) Bank.AlleAbHeute(letzterTag);   // älterer Spielstand: keine Nachzahlung auf einen Schlag
                Geldtresor.Start(Bank);   // Geldtresor (Banking): Kurse einmalig umstellen
                Schilder.Laden(ordner);   // Schild-Sicherung
                Speichern();
                Log.Out("[Wirtschaft] Stand geladen: " + Bank.Stand.Unternehmen.Count + " Firmen, " + Bank.Stand.Parteien.Count + " Parteien.");
            }
            catch (Exception e)
            {
                Log.Error("[Wirtschaft] Laden fehlgeschlagen, starte leer: " + e.Message);
                // Kaputte Datei aufheben, damit sie nicht überschrieben wird.
                try
                {
                    string pfad = Path.Combine(ordner, Speicher.Dateiname);
                    if (File.Exists(pfad)) File.Copy(pfad, pfad + ".defekt", true);
                }
                catch (Exception) { }
                Bank = new Bank(new Stand());
                Geldtresor.Start(Bank);
                try { Schilder.Laden(ordner); } catch (Exception) { }
                letzterTag = Heute;
            }
        }

        /// <summary>Läuft jeden Frame, rechnet aber nur beim Tageswechsel.</summary>
        static void Aktualisieren()
        {
            if (Bank == null) return;
            Geldtresor.Takt();   // Geldtresor (Banking): Inhalt und Guthaben abgleichen
            Schilder.Takt();     // Schild-Sicherung: leere Schilder mit gesichertem Text wiederherstellen
            if (Bank.ZeitNeuStarten) { Bank.AlleAbHeute(Heute); letzterTag = Heute; Speichern(); return; }
            int heute = Heute;
            if (heute == letzterTag || heute <= 0) return;
            letzterTag = heute;

            try
            {
                var ergebnisse = Bank.Tageswechsel(heute);
                if (ergebnisse.Count > 0)
                {
                    Speichern();
                    Log.Out("[Wirtschaft] Tag " + heute + ": " + ergebnisse.Count + " Abrechnungen für " + Bank.Stand.AktivePartei + ".");
                }
            }
            catch (Exception e)
            {
                Log.Error("[Wirtschaft] Tagesabrechnung fehlgeschlagen: " + e.Message);
            }

            // Dynamische Sonderereignisse: höchstens eines pro Firma und Buchungsperiode
            try
            {
                var ereignisse = Ereignisse.Monatslauf(Bank, heute);
                if (ereignisse.Count > 0)
                {
                    Speichern();
                    foreach (var m in ereignisse) Log.Out("[Wirtschaft] Sonderereignis: " + m);
                    try
                    {
                        var spieler = GameManager.Instance.World.GetPrimaryPlayer();
                        if (spieler != null) GameManager.ShowTooltip(spieler, string.Join("  ", ereignisse.ToArray()), false);
                    }
                    catch (Exception) { }
                }
            }
            catch (Exception e)
            {
                Log.Error("[Wirtschaft] Sonderereignisse fehlgeschlagen: " + e.Message);
            }

            // Konkurs: überschuldete Firmen, Gerichtsurteil, Liquidation
            try
            {
                var meldungen = new List<string>();
                foreach (var u in Finanzen.KonkursPruefen(Bank, heute, meldungen))
                {
                    Geldtresor.Loesen(Bank, u);
                    meldungen.Add("URTEIL: " + Finanzen.FirmaLoeschen(Bank, u, heute) + " Liquidation nach Konkurs.");
                }
                if (meldungen.Count > 0)
                {
                    Speichern();
                    foreach (var m in meldungen) Log.Out("[Wirtschaft] " + m);
                    try
                    {
                        var spieler = GameManager.Instance.World.GetPrimaryPlayer();
                        if (spieler != null) GameManager.ShowTooltip(spieler, string.Join("  ", meldungen.ToArray()), false);
                    }
                    catch (Exception) { }
                }
            }
            catch (Exception e)
            {
                Log.Error("[Wirtschaft] Konkursprüfung fehlgeschlagen: " + e.Message);
            }
        }

        static void SpielBeendet()
        {
            Speichern();
            try { Schilder.Beenden(); } catch (Exception) { }
            Bank = null;
            letzterTag = -1;
        }

        // ---------- Schnittstelle für andere Mods (Figuren: KI-Banker). Aufruf per Reflection, daher nur einfache Typen. ----------

        /// <summary>Kreditregeln, Kunden mit Einkommen und Kreditrahmen, laufende Kredite. Leer, wenn keine Welt geladen ist.</summary>
        public static string FinanzBericht(string stadt)
        {
            if (Bank == null) return "";
            try { return Befehle.FinanzBericht(Bank, stadt ?? ""); }
            catch (Exception e) { return "Finanzdaten nicht lesbar: " + e.Message; }
        }

        /// <summary>Erweiterte Unternehmensdaten (Advanced Business Management) für die KI.
        /// "" = alle Firmen mit erweiterter Verwaltung (für den Game Operator), "partner:Firmenname" = eigene Daten und
        /// Geschäftsbeziehungen dieser Firma (für eine KI-Figur, die sie führt). Leer, wenn es nichts gibt.</summary>
        public static string AbmBericht(string filter)
        {
            if (Bank == null) return "";
            try
            {
                filter = filter ?? "";
                if (filter.StartsWith("partner:", StringComparison.OrdinalIgnoreCase)) return Abm.PartnerBericht(Bank, filter.Substring(8), Heute);
                return Abm.Bericht(Bank, Heute);
            }
            catch (Exception e) { Log.Warning("[Wirtschaft] AbmBericht: " + e.Message); return ""; }
        }

        /// <summary>Monatsraten für einen Betrag, z. B. für ein Immobilienangebot.</summary>
        public static string RatenText(decimal summe)
        {
            if (Bank == null) return "";
            var st = Bank.Stand;
            int lz = st.KreditMaxMonate;
            return "Rate bei voller Finanzierung: " + Befehle.Betrag(global::Wirtschaft.Kern.Bank.Annuitaet(summe, st.KreditZins, Math.Min(12, lz))) + " (12 M), "
                   + Befehle.Betrag(global::Wirtschaft.Kern.Bank.Annuitaet(summe, st.KreditZins, Math.Min(24, lz))) + " (24 M), "
                   + Befehle.Betrag(global::Wirtschaft.Kern.Bank.Annuitaet(summe, st.KreditZins, lz)) + " (" + lz + " M)";
        }

        /// <summary>Prüft einen Kredit. Rückgabe beginnt mit "OK:" oder "NEIN:".</summary>
        public static string KreditPruefen(string nehmer, decimal summe, int monate)
        {
            if (Bank == null) return "NEIN: Wirtschaft nicht geladen.";
            try
            {
                string grund = Bank.KreditGrund(nehmer, summe, monate);
                decimal rate = global::Wirtschaft.Kern.Bank.Annuitaet(summe, Bank.Stand.KreditZins, monate);
                return grund == null ? "OK: Rate " + Befehle.Betrag(rate) + " pro Monat." : "NEIN: " + grund;
            }
            catch (Exception e) { return "NEIN: " + e.Message; }
        }

        /// <summary>Vergibt den Kredit und zahlt aus. Rückgabe beginnt mit "OK:" oder "NEIN:".</summary>
        public static string KreditVergeben(string nehmer, decimal summe, int monate, string zweck)
        {
            if (Bank == null) return "NEIN: Wirtschaft nicht geladen.";
            try
            {
                var k = Bank.KreditVergeben(nehmer, summe, monate, zweck, Heute);
                Speichern();
                return "OK: Kredit Nr. " + k.Nr + " an " + k.Nehmer + " über " + Befehle.Betrag(k.Summe) + " " + Bank.Stand.Waehrung + ", " + k.Monate + " Monate, Rate " + Befehle.Betrag(k.Rate) + " pro Monat. Betrag ist auf dem Konto von " + k.Nehmer + ".";
            }
            catch (Exception e) { return "NEIN: " + e.Message; }
        }

        public static void Speichern()
        {
            if (Bank == null || string.IsNullOrEmpty(ordner)) return;
            try { Speicher.Speichern(Bank.Stand, ordner); }
            catch (Exception e) { Log.Error("[Wirtschaft] Speichern fehlgeschlagen: " + e.Message); }
        }
    }
}
