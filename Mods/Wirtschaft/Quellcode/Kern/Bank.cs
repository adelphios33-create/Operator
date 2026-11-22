// Wirtschaft-Mod – Rechenlogik
// Monatsrechnung pro Firma: Umsatz - Ausgaben = Zwischensumme - Steuer = Einkommen.
// Steuer geht an die Regierung, Einkommen an die Anteilseigner (Standard: 100 % Besitzer).
using System;
using System.Collections.Generic;
using System.Linq;

namespace Wirtschaft.Kern
{
    public class WirtschaftsFehler : Exception
    {
        public WirtschaftsFehler(string text) : base(text) { }
    }

    /// <summary>Rechnung für einen oder mehrere Monate.</summary>
    public class Rechnung
    {
        public string Name = "";
        public int Monate;
        public decimal Umsatz, Ausgaben, Zwischensumme, Steuer, Einkommen, Ausgeschuettet, Ruecklage;
        public List<KeyValuePair<string, decimal>> Auszahlungen = new List<KeyValuePair<string, decimal>>();
    }

    /// <summary>Firmenwert in Einzelteilen: Umsatz × Branchenfaktor + Mitarbeiter-Staffel + Immobilien + Fahrzeuge + Ertragswert.</summary>
    public class Bewertung
    {
        public string Branche = "";
        public decimal Faktor, Umsatz, Mitarbeiter, Immobilien, Fahrzeuge, Ertrag, Summe;
        public bool Fest;
    }

    public class Bank
    {
        public Stand Stand { get; private set; }

        /// <summary>Firmenwert: so viele Monatsgewinne (nach Steuer) zählen als Ertragswert. Bei Verlust wird genauso viel abgezogen.</summary>
        public const int Ertragsmonate = 6;

        public Bank(Stand stand)
        {
            Stand = stand ?? new Stand();
            if (Stand.TageProMonat < 1) Stand.TageProMonat = 7;
            // Ältere Spielstände (v1) kannten noch keinen Besitzer: größten Anteilseigner nehmen.
            foreach (var u in Stand.Unternehmen)
                if (string.IsNullOrEmpty(u.Besitzer) && u.Anteile.Count > 0)
                    u.Besitzer = u.Anteile.OrderByDescending(w => w.Prozent).First().Partei;
            // Ältere Spielstände (v2) kannten nur eine Bank: Firmen ohne Bank kommen in "Hauptbank".
            bool ohneBank = Stand.Unternehmen.Any(w => string.IsNullOrEmpty(w.Bank)) || Stand.Immobilien.Any(w => string.IsNullOrEmpty(w.Bank));
            if (ohneBank)
            {
                if (Stand.Banken.Count == 0)
                {
                    var alteRegierung = Stand.Parteien.FirstOrDefault(w => w.IstRegierung);
                    Stand.Banken.Add(new Bankhaus { Name = "Hauptbank", Regierung = alteRegierung == null ? "" : alteRegierung.Name });
                }
                string erste = Stand.Banken[0].Name;
                foreach (var u in Stand.Unternehmen) if (string.IsNullOrEmpty(u.Bank)) u.Bank = erste;
                foreach (var i in Stand.Immobilien) if (string.IsNullOrEmpty(i.Bank)) i.Bank = erste;
            }
            // Auszahlungs-Währungen: Kurs in Dukes, abgeleitet aus den Handelswerten des Spiels.
            if (Stand.Waehrungen.Count == 0)
            {
                Stand.Waehrungen.Add(new Zahlungsmittel { Item = "casinoCoin", Name = "Coins", Kurs = 1m });
                Stand.Waehrungen.Add(new Zahlungsmittel { Item = "oldCash", Name = "Old Cash", Kurs = 20m });
                Stand.Waehrungen.Add(new Zahlungsmittel { Item = "resourceSilverNugget", Name = "Silberbarren", Kurs = 500m });
                Stand.Waehrungen.Add(new Zahlungsmittel { Item = "resourceGoldNugget", Name = "Goldbarren", Kurs = 5000m });
            }
            // Regierungen und Personen trennen: "Regierung ..." und alles, was einer Bank als Regierung dient, ist ein Staat.
            // Steht statt der Standard-Regierung einer Bank eine Person (z. B. Hauptfigur), bekommt die Bank wieder ihre eigene Regierung.
            if (Stand.StaatStand < 1)
            {
                foreach (var p in Stand.Parteien)
                    if (p.Name.StartsWith("Regierung ", StringComparison.OrdinalIgnoreCase) || string.Equals(p.Name, "Regierung", StringComparison.OrdinalIgnoreCase))
                        p.IstStaat = true;
                foreach (var bk in Stand.Banken)
                {
                    var rp = Stand.Parteien.FirstOrDefault(w => Gleich(w.Name, bk.Regierung));
                    if (rp == null || rp.IstStaat) continue;
                    var eigene = Stand.Parteien.FirstOrDefault(w => w.IstStaat && Gleich(w.Name, "Regierung " + bk.Name));
                    if (eigene != null) bk.Regierung = eigene.Name;   // Person war an Stelle der eigenen Regierung eingetragen
                    else rp.IstStaat = true;                           // eigener Name (z. B. "Springfield Town"): bleibt Regierung
                }
                Stand.StaatStand = 1;
            }
            // Die Währung heißt überall "Coins".
            if (string.IsNullOrEmpty(Stand.Waehrung) || string.Equals(Stand.Waehrung, "Dukes", StringComparison.OrdinalIgnoreCase)) Stand.Waehrung = "Coins";
            foreach (var zm in Stand.Waehrungen) if (string.Equals(zm.Name, "Dukes", StringComparison.OrdinalIgnoreCase)) zm.Name = "Coins";
            ZeitNeuStarten = Stand.Version < 4;
            Stand.Version = 4;
        }

        /// <summary>True, wenn ein älterer Spielstand geladen wurde: alle Firmen zählen ab heute neu.</summary>
        public bool ZeitNeuStarten;

        /// <summary>Aktueller Spieltag (vom Spiel gesetzt), für Vorschauen mit Kundenverträgen.</summary>
        public int Heute;

        public void AlleAbHeute(int heute)
        {
            foreach (var u in Stand.Unternehmen) u.ZuletztAbgerechnet = heute;
            foreach (var i in Stand.Immobilien) i.ZuletztAbgerechnet = heute;
            foreach (var k in Stand.Kredite) k.ZuletztAbgerechnet = heute;
            Abm.TageKorrigieren(this, heute);
            ZeitNeuStarten = false;
        }

        // ---------- Laufen lassen (wen du gerade spielst) ----------

        public void LaeuftSetzen(Unternehmen u, bool an, int heute)
        {
            if (an && !u.Laeuft) u.ZuletztAbgerechnet = heute;
            u.Laeuft = an;
        }

        public void StadtLaeuftSetzen(string bank, bool an, int heute)
        {
            foreach (var u in FirmenDerBank(bank)) LaeuftSetzen(u, an, heute);
        }

        // ---------- Auszahlen ----------

        public Zahlungsmittel WaehrungOderNull(string name)
        {
            return Stand.Waehrungen.FirstOrDefault(w => Gleich(w.Name, name) || Gleich(w.Item, name));
        }

        /// <summary>Wie viele Stück für einen Betrag ausgezahlt werden und was das kostet. Prüft das Guthaben.</summary>
        public static int Stueckzahl(decimal guthaben, decimal betrag, Zahlungsmittel w, out decimal kosten)
        {
            if (w == null || w.Kurs <= 0) throw new WirtschaftsFehler("Für diese Währung ist kein Kurs eingestellt.");
            if (betrag <= 0) throw new WirtschaftsFehler("Bitte einen Betrag eintippen.");
            if (betrag > guthaben) throw new WirtschaftsFehler("Nicht genug Guthaben (" + Befehle.Betrag(guthaben) + ").");
            decimal stueck = Math.Floor(betrag / w.Kurs);
            if (stueck < 1) throw new WirtschaftsFehler("Zu wenig für 1 " + w.Name + " (1 = " + Befehle.Betrag(w.Kurs) + ").");
            if (stueck > 1000000) throw new WirtschaftsFehler("Höchstens 1.000.000 Stück auf einmal.");
            kosten = stueck * w.Kurs;
            return (int)stueck;
        }

        // ---------- Banken (Städte) ----------

        public Bankhaus BankhausOderNull(string name)
        {
            return Stand.Banken.FirstOrDefault(w => Gleich(w.Name, name));
        }

        public Bankhaus BankhausHolen(string name)
        {
            var b = BankhausOderNull(name);
            if (b == null) throw new WirtschaftsFehler("Bank '" + name + "' gibt es nicht.");
            return b;
        }

        /// <summary>Neue Bank (Stadt). Ohne Regierungsname heißt sie "Regierung &lt;Bankname&gt;"; die Partei wird bei Bedarf angelegt.</summary>
        public Bankhaus BankAnlegen(string name, string regierung)
        {
            NameGueltig(name);
            if (BankhausOderNull(name) != null) throw new WirtschaftsFehler("Bank '" + name + "' gibt es schon.");
            string reg = string.IsNullOrWhiteSpace(regierung) ? "Regierung " + name.Trim() : regierung.Trim();
            var p = ParteiOderNull(reg);
            if (p != null && !IstRegierungspartei(p))
                throw new WirtschaftsFehler("'" + p.Name + "' ist eine Person, keine Regierung. Anderen Namen nehmen oder das Feld leer lassen.");
            if (p == null) { p = new Partei { Name = reg }; Stand.Parteien.Add(p); }
            p.IstRegierung = true; p.IstStaat = true;
            var b = new Bankhaus { Name = name.Trim(), Regierung = p.Name };
            Stand.Banken.Add(b);
            return b;
        }

        public Bankhaus BankAmStandort(string position)
        {
            if (string.IsNullOrEmpty(position)) return null;
            return Stand.Banken.FirstOrDefault(w => w.Standorte.Contains(position));
        }

        public void StandortVerbinden(string bank, string position)
        {
            if (string.IsNullOrEmpty(position)) throw new WirtschaftsFehler("Kein Hauptbank-Block ausgewählt.");
            var b = BankhausHolen(bank);
            foreach (var x in Stand.Banken) x.Standorte.Remove(position);
            b.Standorte.Add(position);
        }

        public void StandortEntfernen(string position)
        {
            foreach (var x in Stand.Banken) x.Standorte.Remove(position);
        }

        public void RegierungSetzen(string bank, string partei)
        {
            var b = BankhausHolen(bank);
            var p = ParteiHolen(partei);
            if (!IstRegierungspartei(p)) throw new WirtschaftsFehler("'" + p.Name + "' ist eine Person, keine Regierung. Lege unter Personen mit 'Regierung anlegen' eine Regierung an.");
            b.Regierung = p.Name;
        }

        /// <summary>Neue, freie Regierung (noch keiner Bank zugeordnet).</summary>
        public Partei RegierungAnlegen(string name)
        {
            NameGueltig(name);
            name = name.Trim();
            if (ParteiOderNull(name) != null || FirmaOderNull(name) != null || ImmobilieOderNull(name) != null)
                throw new WirtschaftsFehler("Den Namen '" + name + "' gibt es schon (Person, Regierung, Firma oder Immobilie).");
            var p = new Partei { Name = name, IstRegierung = true, IstStaat = true };
            Stand.Parteien.Add(p);
            return p;
        }

        /// <summary>Benennt eine Bank um. Firmen, Immobilien und Kredite der Bank ziehen mit.</summary>
        public Bankhaus BankUmbenennen(string alt, string neu)
        {
            var b = BankhausHolen(alt);
            NameGueltig(neu);
            neu = neu.Trim();
            if (string.Equals(b.Name, neu, StringComparison.Ordinal)) throw new WirtschaftsFehler("Der Name ist schon so.");
            if (Stand.Banken.Any(w => !ReferenceEquals(w, b) && Gleich(w.Name, neu))) throw new WirtschaftsFehler("Eine Bank '" + neu + "' gibt es schon.");
            string vorher = b.Name;
            b.Name = neu;
            foreach (var u in Stand.Unternehmen) if (Gleich(u.Bank, vorher)) u.Bank = neu;
            foreach (var i in Stand.Immobilien) if (Gleich(i.Bank, vorher)) i.Bank = neu;
            foreach (var k in Stand.Kredite) if (Gleich(k.Bank, vorher)) k.Bank = neu;
            return b;
        }

        /// <summary>Die Bank, die beim Löschen die Firmen, Immobilien und Kredite übernimmt (die nächste in der Liste).</summary>
        Bankhaus UebernahmeBank(Bankhaus b)
        {
            int i = Stand.Banken.IndexOf(b);
            if (Stand.Banken.Count < 2) return null;
            return Stand.Banken[(i + 1) % Stand.Banken.Count];
        }

        /// <summary>Was beim Löschen einer Bank passiert, als Satz für die Rückfrage. Wirft einen Fehler, wenn es nicht geht.</summary>
        public string BankLoeschFolgen(string name)
        {
            var b = BankhausHolen(name);
            int firmen = FirmenDerBank(b.Name).Count;
            int immo = Stand.Immobilien.Count(w => Gleich(w.Bank, b.Name));
            int kredite = Stand.Kredite.Count(w => Gleich(w.Bank, b.Name));
            var ziel = UebernahmeBank(b);
            var s = new List<string>();
            if (firmen > 0) s.Add(firmen + " Firma/Firmen");
            if (immo > 0) s.Add(immo + " Immobilie(n)");
            if (kredite > 0) s.Add(kredite + " Kredit(e)");
            if (s.Count > 0 && ziel == null)
                throw new WirtschaftsFehler("Das ist die letzte Bank und es hängen noch " + string.Join(", ", s.ToArray()) + " dran. Lege erst eine zweite Bank an.");
            string text = "Bank " + b.Name + " wird gelöscht";
            if (s.Count > 0) text += "; " + string.Join(", ", s.ToArray()) + " wechseln zur Bank " + ziel.Name;
            if (b.Standorte.Count > 0) text += "; " + b.Standorte.Count + " Hauptbank-Block/Blöcke sind dann ohne Bank (neu verbinden)";
            if (!string.IsNullOrEmpty(b.Regierung)) text += "; die Regierung " + b.Regierung + " bleibt bestehen";
            return text + ".";
        }

        public void BankLoeschen(string name)
        {
            BankLoeschFolgen(name);   // wirft, wenn es nicht geht
            var b = BankhausHolen(name);
            var ziel = UebernahmeBank(b);
            string alt = b.Name;
            if (ziel != null)
            {
                foreach (var u in Stand.Unternehmen) if (Gleich(u.Bank, alt)) u.Bank = ziel.Name;
                foreach (var i in Stand.Immobilien) if (Gleich(i.Bank, alt)) i.Bank = ziel.Name;
                foreach (var k in Stand.Kredite) if (Gleich(k.Bank, alt)) k.Bank = ziel.Name;
            }
            Stand.Banken.Remove(b);
        }

        /// <summary>Städte, die diese Regierung beliefert.</summary>
        public List<string> StaedteVon(string regierung)
        {
            return Stand.Banken.Where(w => Gleich(w.Regierung, regierung)).Select(w => w.Name).ToList();
        }

        /// <summary>Die Regierung, die die Steuern einer Bank bekommt.</summary>
        public Partei RegierungVon(string bank)
        {
            var b = BankhausOderNull(bank);
            if (b == null && Stand.Banken.Count > 0) b = Stand.Banken[0];
            if (b != null)
            {
                if (string.IsNullOrEmpty(b.Regierung)) b.Regierung = "Regierung " + b.Name;
                var p = ParteiOderNull(b.Regierung);
                if (p != null && !IstRegierungspartei(p)) { b.Regierung = "Regierung " + b.Name; p = ParteiOderNull(b.Regierung); }   // eine Person ist nie Regierung
                if (p == null) { p = new Partei { Name = b.Regierung }; Stand.Parteien.Add(p); }
                p.IstRegierung = true; p.IstStaat = true;
                return p;
            }
            return Regierung;
        }

        /// <summary>Regierung ist, wer als Regierung (Staat) angelegt ist. Personen sind nie eine Regierung. (Ohne Banken: altes Kennzeichen.)</summary>
        public bool IstRegierungspartei(Partei p)
        {
            if (p.IstStaat) return true;
            return Stand.Banken.Count == 0 && p.IstRegierung;
        }

        public List<Unternehmen> FirmenDerBank(string bank)
        {
            return Stand.Unternehmen.Where(w => Gleich(w.Bank, bank)).ToList();
        }

        // ---------- Suchen ----------

        static bool Gleich(string a, string b) { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }

        public Partei ParteiOderNull(string name) { return Stand.Parteien.FirstOrDefault(w => Gleich(w.Name, name)); }
        public Unternehmen FirmaOderNull(string name) { return Stand.Unternehmen.FirstOrDefault(w => Gleich(w.Name, name)); }
        public Immobilie ImmobilieOderNull(string name) { return Stand.Immobilien.FirstOrDefault(w => Gleich(w.Name, name)); }

        public Partei ParteiHolen(string name)
        {
            var p = ParteiOderNull(name);
            if (p == null) throw new WirtschaftsFehler("Partei '" + name + "' gibt es nicht.");
            return p;
        }
        public Unternehmen Firma(string name)
        {
            var u = FirmaOderNull(name);
            if (u == null) throw new WirtschaftsFehler("Firma '" + name + "' gibt es nicht.");
            return u;
        }
        public Immobilie ImmobilieHolen(string name)
        {
            var i = ImmobilieOderNull(name);
            if (i == null) throw new WirtschaftsFehler("Immobilie '" + name + "' gibt es nicht.");
            return i;
        }

        /// <summary>Die Regierung. Gibt es noch keine, wird "Regierung" angelegt.</summary>
        public Partei Regierung
        {
            get
            {
                var r = Stand.Parteien.FirstOrDefault(w => w.IstRegierung);
                if (r == null)
                {
                    r = ParteiOderNull("Regierung");
                    if (r == null) { r = new Partei { Name = "Regierung" }; Stand.Parteien.Insert(0, r); }
                    r.IstRegierung = true; r.IstStaat = true;
                }
                return r;
            }
        }

        // ---------- Anlegen und Ändern ----------

        public Partei ParteiAnlegen(string name, decimal startkapital)
        {
            NameGueltig(name);
            if (ParteiOderNull(name) != null) throw new WirtschaftsFehler("Partei '" + name + "' gibt es schon.");
            var p = new Partei { Name = name.Trim(), Konto = Runden(startkapital) };
            Stand.Parteien.Add(p);
            if (string.IsNullOrEmpty(Stand.AktivePartei)) Stand.AktivePartei = p.Name;
            return p;
        }

        /// <summary>Benennt eine Person (oder Regierung) um. Alle Verweise (Firmen, Immobilien, Anteile, Kredite,
        /// Bank-Regierung, aktive Person) ziehen mit. Der Name muss unter Personen, Firmen und Immobilien frei sein.</summary>
        public Partei ParteiUmbenennen(string alt, string neu)
        {
            var p = ParteiHolen(alt);
            NameGueltig(neu);
            neu = neu.Trim();
            if (string.Equals(p.Name, neu, StringComparison.Ordinal)) throw new WirtschaftsFehler("Der Name ist schon so.");
            var andere = Stand.Parteien.FirstOrDefault(w => !ReferenceEquals(w, p) && Gleich(w.Name, neu));
            if (andere != null || FirmaOderNull(neu) != null || ImmobilieOderNull(neu) != null)
                throw new WirtschaftsFehler("Den Namen '" + neu + "' gibt es schon (Person, Firma oder Immobilie).");
            string vorher = p.Name;
            p.Name = neu;
            foreach (var u in Stand.Unternehmen)
            {
                if (Gleich(u.Besitzer, vorher)) u.Besitzer = neu;
                foreach (var a in u.Anteile) if (Gleich(a.Partei, vorher)) a.Partei = neu;
            }
            foreach (var i in Stand.Immobilien) if (Gleich(i.Besitzer, vorher)) i.Besitzer = neu;
            foreach (var k in Stand.Kredite) if (Gleich(k.Nehmer, vorher)) k.Nehmer = neu;
            foreach (var b in Stand.Banken) if (Gleich(b.Regierung, vorher)) b.Regierung = neu;
            if (Gleich(Stand.AktivePartei, vorher)) Stand.AktivePartei = neu;
            // Erweiterte Verwaltung: Lohnkonten und Kunden ziehen mit
            foreach (var u in Stand.Unternehmen)
                foreach (var e in u.Abm)
                {
                    if (e.Art == "Mitarbeiter" && Gleich(e.Hol("Konto"), vorher)) e.Setz("Konto", neu);
                    if (e.Art == "Vertrag" && Gleich(e.Hol("Kunde"), vorher)) e.Setz("Kunde", neu);
                }
            return p;
        }

        /// <summary>Auffangperson: bekommt Firmen, Anteile, Immobilien und Guthaben, wenn jemand gelöscht wird, und ist Standardbesitzer neuer Firmen.</summary>
        public const string BuergerName = "Bürger";

        public bool IstBuerger(string name) { return Gleich(name, BuergerName); }

        /// <summary>Die Person "Bürger", wird bei Bedarf angelegt.</summary>
        public Partei BuergerHolen()
        {
            var p = ParteiOderNull(BuergerName);
            if (p == null)
            {
                p = new Partei { Name = BuergerName };
                Stand.Parteien.Add(p);
                if (string.IsNullOrEmpty(Stand.AktivePartei)) Stand.AktivePartei = p.Name;
            }
            return p;
        }

        /// <summary>Was beim Löschen passieren würde, als Satz für die Rückfrage. Wirft einen Fehler bei "Bürger".</summary>
        public string LoeschFolgen(string name)
        {
            var p = ParteiHolen(name);
            if (IstBuerger(p.Name)) throw new WirtschaftsFehler("'" + BuergerName + "' ist die Auffangperson und kann nicht gelöscht werden. Du kannst sie aber umbenennen.");
            var teile = new List<string>();
            if (p.Konto != 0m) teile.Add("Guthaben " + Befehle.Betrag(p.Konto));
            int firmen = Stand.Unternehmen.Count(u => Gleich(u.Besitzer, p.Name));
            if (firmen > 0) teile.Add(firmen + " Firma/Firmen");
            int anteile = Stand.Unternehmen.Count(u => u.Anteile.Any(a => Gleich(a.Partei, p.Name) && a.Prozent > 0m));
            if (anteile > 0) teile.Add("Anteile an " + anteile + " Firma/Firmen");
            int immo = Stand.Immobilien.Count(i => Gleich(i.Besitzer, p.Name));
            if (immo > 0) teile.Add(immo + " Immobilie(n)");
            int kredite = Stand.Kredite.Count(k => !k.Erledigt && Gleich(k.Nehmer, p.Name));
            if (kredite > 0) teile.Add(kredite + " offene(r) Kredit(e)");
            var staedte = Stand.Banken.Where(b => Gleich(b.Regierung, p.Name)).Select(b => b.Name).ToList();
            string text = p.Name + ": " + (teile.Count == 0 ? "nichts mehr dran" : string.Join(", ", teile.ToArray()));
            if (staedte.Count > 0) text += ", Regierung von " + string.Join(", ", staedte.ToArray()) + " (bekommt eine neue Regierung)";
            if (teile.Count > 0 || staedte.Count > 0) text += ". Alles geht an " + BuergerName + ".";
            else text += ".";
            return text;
        }

        /// <summary>Löscht eine Person. Firmen, Anteile, Immobilien, Kredite und Guthaben gehen an "Bürger"; Städte, die sie regierte,
        /// bekommen beim nächsten Gebrauch eine neue Regierung. Ein Geldtresor muss vorher getrennt sein (macht das Fenster).</summary>
        public void ParteiLoeschen(string name)
        {
            var p = ParteiHolen(name);
            LoeschFolgen(name);   // wirft bei Bürger
            var b = BuergerHolen();
            string alt = p.Name;

            b.Konto += p.Konto;
            p.Konto = 0m;
            foreach (var u in Stand.Unternehmen)
            {
                if (Gleich(u.Besitzer, alt)) u.Besitzer = b.Name;
                var eigene = u.Anteile.Where(a => Gleich(a.Partei, alt)).ToList();
                if (eigene.Count == 0) continue;
                decimal summe = eigene.Sum(a => a.Prozent);
                u.Anteile.RemoveAll(a => Gleich(a.Partei, alt));
                var ziel = u.Anteile.FirstOrDefault(a => Gleich(a.Partei, b.Name));
                if (ziel == null) u.Anteile.Add(new Anteil { Partei = b.Name, Prozent = summe });
                else ziel.Prozent += summe;
            }
            foreach (var i in Stand.Immobilien) if (Gleich(i.Besitzer, alt)) i.Besitzer = b.Name;
            foreach (var k in Stand.Kredite) if (Gleich(k.Nehmer, alt)) k.Nehmer = b.Name;
            foreach (var bk in Stand.Banken) if (Gleich(bk.Regierung, alt)) bk.Regierung = "";
            // Erweiterte Verwaltung: Lohnkonto fällt weg (der Mitarbeiter bleibt, sein Lohn geht dann an niemanden)
            foreach (var u in Stand.Unternehmen)
                foreach (var e in u.Abm.Where(w => w.Art == "Mitarbeiter" && Gleich(w.Hol("Konto"), alt)))
                {
                    e.Setz("Konto", "");
                    e.Setz("Notiz", (e.Hol("Notiz") + " | Lohnkonto " + alt + " gelöscht").Trim(' ', '|'));
                }

            Stand.Parteien.Remove(p);
            if (Gleich(Stand.AktivePartei, alt))
            {
                var next = Stand.Parteien.FirstOrDefault(w => !IstRegierungspartei(w)) ?? b;
                Stand.AktivePartei = next.Name;
            }
        }

        public void RegierungFestlegen(string name)
        {
            var neu = ParteiHolen(name);
            foreach (var p in Stand.Parteien) p.IstRegierung = false;
            neu.IstRegierung = true; neu.IstStaat = true;
        }

        public Unternehmen FirmaAnlegen(string name, string besitzer, string branche, int heute, string bank)
        {
            NameGueltig(name);
            if (FirmaOderNull(name) != null) throw new WirtschaftsFehler("Firma '" + name + "' gibt es schon.");
            var b = string.IsNullOrWhiteSpace(besitzer) ? BuergerHolen() : ParteiHolen(besitzer);
            string bankName = "";
            if (!string.IsNullOrEmpty(bank)) bankName = BankhausHolen(bank).Name;
            else if (Stand.Banken.Count > 0) bankName = Stand.Banken[0].Name;
            var u = new Unternehmen
            {
                Name = name.Trim(),
                Bank = bankName,
                Besitzer = b.Name,
                Branche = string.IsNullOrEmpty(branche) ? "Sonstiges" : branche,
                ZuletztAbgerechnet = heute
            };
            u.Anteile.Add(new Anteil { Partei = b.Name, Prozent = 100m });
            Stand.Unternehmen.Add(u);
            return u;
        }

        /// <summary>Neuer Besitzer. Gehörte die Firma bisher allein dem alten Besitzer, wandern die Anteile mit.</summary>
        public void BesitzerSetzen(Unternehmen u, string partei, int heute)
        {
            var p = ParteiHolen(partei);
            if (u.Anteile.Count == 1 && Gleich(u.Anteile[0].Partei, u.Besitzer)) u.Anteile[0].Partei = p.Name;
            u.Besitzer = p.Name;
            u.ZuletztAbgerechnet = heute;
        }

        /// <summary>Gründer festlegen: Gründer 1 hält "prozent1" %, Gründer 2 (optional) den Rest. Ersetzt alle bisherigen Eigentümer.</summary>
        public void GruenderSetzen(Unternehmen u, string gruender1, decimal prozent1, string gruender2, int heute)
        {
            var g1 = ParteiHolen(gruender1);
            if (prozent1 != Math.Floor(prozent1) || prozent1 < 1 || prozent1 > 100) throw new WirtschaftsFehler("Der Anteil von Gründer 1 muss zwischen 1 und 100 % liegen.");
            Partei g2 = string.IsNullOrEmpty(gruender2) ? null : ParteiHolen(gruender2);
            if (g2 != null && Gleich(g1.Name, g2.Name)) throw new WirtschaftsFehler("Gründer 1 und Gründer 2 sind dieselbe Person.");
            if (g2 == null && prozent1 < 100) throw new WirtschaftsFehler("Ohne Gründer 2 muss Gründer 1 100 % halten.");
            u.Anteile.Clear();
            if (g2 == null || prozent1 >= 100) u.Anteile.Add(new Anteil { Partei = g1.Name, Prozent = 100m });
            else
            {
                u.Anteile.Add(new Anteil { Partei = g1.Name, Prozent = prozent1 });
                u.Anteile.Add(new Anteil { Partei = g2.Name, Prozent = 100m - prozent1 });
            }
            u.Besitzer = g1.Name;
            Buchen(u.Verlauf, heute, "Gründer: " + string.Join(", ", u.Anteile.Select(w => w.Partei + " " + w.Prozent + " %").ToArray()), 0m);
        }

        public void TageProMonatSetzen(int tage)
        {
            if (tage < 1 || tage > 365) throw new WirtschaftsFehler("Ein Monat muss zwischen 1 und 365 Spieltagen haben.");
            Stand.TageProMonat = tage;
        }

        public Immobilie ImmobilieAnlegen(string name, string besitzer, decimal wert, decimal miete, decimal kosten, int heute)
        {
            NameGueltig(name);
            if (ImmobilieOderNull(name) != null) throw new WirtschaftsFehler("Immobilie '" + name + "' gibt es schon.");
            var i = new Immobilie
            {
                Name = name.Trim(), Besitzer = ParteiHolen(besitzer).Name, Wert = wert,
                MieteMonat = miete, KostenMonat = kosten, ZuletztAbgerechnet = heute
            };
            Stand.Immobilien.Add(i);
            return i;
        }

        // ---------- Firmentresor ----------

        /// <summary>Position des zuletzt angeklickten, noch freien Firmentresors (wird nicht gespeichert).</summary>
        public string LetzterFreierTresor = "";

        public Unternehmen FirmaAmTresor(string position)
        {
            if (string.IsNullOrEmpty(position)) return null;
            return Stand.Unternehmen.FirstOrDefault(w => w.Tresor == position);
        }

        public void TresorVerbinden(string firma, string position)
        {
            if (string.IsNullOrEmpty(position))
                throw new WirtschaftsFehler("Klick zuerst den Firmentresor an, den du verbinden willst.");
            var u = Firma(firma);
            var alt = FirmaAmTresor(position);
            if (alt != null && alt != u) alt.Tresor = "";
            var reg = RegierungAmTresor(position);
            if (reg != null) { reg.RegTresor = ""; reg.RegZuletzt = 0; }
            u.Tresor = position;
            LetzterFreierTresor = "";
        }

        // ---------- Regierung am Firmentresor ----------

        /// <summary>Monatliche Gehaltskosten pro Staatsbediensteten.</summary>
        public const decimal RegGehalt = 250m;

        public Partei RegierungAmTresor(string position)
        {
            if (string.IsNullOrEmpty(position)) return null;
            return Stand.Parteien.FirstOrDefault(w => w.RegTresor == position && IstRegierungspartei(w));
        }

        /// <summary>Alle Regierungen (Parteien, die einer Bank als Regierung zugeordnet sind).</summary>
        public List<Partei> Regierungen() { return Stand.Parteien.Where(w => IstRegierungspartei(w)).ToList(); }

        public void RegierungTresorVerbinden(string regierung, string position)
        {
            if (string.IsNullOrEmpty(position))
                throw new WirtschaftsFehler("Klick zuerst den Firmentresor an, den du verbinden willst.");
            var p = ParteiHolen(regierung);
            if (!IstRegierungspartei(p)) throw new WirtschaftsFehler(p.Name + " ist keine Regierung.");
            var firma = FirmaAmTresor(position);
            if (firma != null) firma.Tresor = "";
            var alt = RegierungAmTresor(position);
            if (alt != null && alt != p) { alt.RegTresor = ""; alt.RegZuletzt = 0; }
            p.RegTresor = position;
            p.RegZuletzt = 0;   // zählt ab dem nächsten Spieltag
            LetzterFreierTresor = "";
        }

        /// <summary>Staatsausgaben pro Monat: Gehälter plus sonstige Ausgaben.</summary>
        public decimal RegierungAusgaben(Partei p) { return p.RegMitarbeiter * RegGehalt + p.RegAusgaben; }

        /// <summary>Steuereinnahmen pro Monat aus allen Banken, die diese Regierung beliefern.</summary>
        public decimal RegierungSteuern(Partei p)
        {
            decimal s = 0m;
            foreach (var b in Stand.Banken.Where(w => Gleich(w.Regierung, p.Name))) s += SteuernProMonat(b.Name);
            return s;
        }

        // ---------- Aktive Partei und Zeit ----------

        public bool IstAktiv(string partei) { return Gleich(partei, Stand.AktivePartei); }

        /// <summary>Wechselt die aktive Partei. Ihre Firmen und Immobilien zählen ab heute neu.</summary>
        public void AktivSetzen(string name, int heute)
        {
            var p = ParteiHolen(name);
            Stand.AktivePartei = p.Name;
            foreach (var u in Stand.Unternehmen.Where(w => Gleich(w.Besitzer, p.Name))) u.ZuletztAbgerechnet = heute;
            foreach (var i in Stand.Immobilien.Where(w => Gleich(w.Besitzer, p.Name))) i.ZuletztAbgerechnet = heute;
        }

        /// <summary>Spieltage bis zur nächsten Monatsbuchung, oder -1 wenn die Firma eingefroren ist.</summary>
        public int TageBisBuchung(Unternehmen u, int heute)
        {
            if (!u.Laeuft) return -1;
            int rest = u.ZuletztAbgerechnet + Stand.TageProMonat - heute;
            return rest < 0 ? 0 : rest;
        }

        /// <summary>Vom Spiel bei jedem neuen Spieltag aufgerufen. Bucht volle Monate der aktiven Partei.</summary>
        public List<Rechnung> Tageswechsel(int heute)
        {
            Heute = heute;
            Abm.heuteFuerPruefen = heute;
            var ergebnisse = new List<Rechnung>();
            int t = Stand.TageProMonat;

            foreach (var u in Stand.Unternehmen.Where(w => w.Laeuft).ToList())
            {
                if (heute < u.ZuletztAbgerechnet) { u.ZuletztAbgerechnet = heute; continue; }   // Zeit zurückgestellt
                int monate = (heute - u.ZuletztAbgerechnet) / t;
                if (monate <= 0) continue;
                ergebnisse.Add(FirmaAbrechnen(u, monate, heute));
                u.ZuletztAbgerechnet += monate * t;
            }
            foreach (var i in Stand.Immobilien.Where(w => IstAktiv(w.Besitzer)).ToList())
            {
                if (heute < i.ZuletztAbgerechnet) { i.ZuletztAbgerechnet = heute; continue; }
                int monate = (heute - i.ZuletztAbgerechnet) / t;
                if (monate <= 0) continue;
                ergebnisse.Add(ImmobilieAbrechnen(i, monate, heute));
                i.ZuletztAbgerechnet += monate * t;
            }
            // Regierungen mit Firmentresor: Staatsausgaben monatlich buchen. Ohne Firmentresor läuft alles automatisch (keine Ausgaben).
            foreach (var p in Stand.Parteien.Where(w => !string.IsNullOrEmpty(w.RegTresor) && IstRegierungspartei(w)).ToList())
            {
                if (p.RegZuletzt <= 0 || heute < p.RegZuletzt) { p.RegZuletzt = heute; continue; }
                int monate = (heute - p.RegZuletzt) / t;
                if (monate <= 0) continue;
                p.RegZuletzt += monate * t;
                if (p.RegAus) continue;
                decimal ausgaben = RegierungAusgaben(p) * monate;
                if (ausgaben <= 0) continue;
                p.Konto -= ausgaben;
                Buchen(Stand.Kassenbuch, heute, "Staatsausgaben " + p.Name + " (" + monate + " Monat/e)", -ausgaben);
            }
            foreach (var k in Stand.Kredite.Where(w => !w.Erledigt).ToList())
            {
                if (heute < k.ZuletztAbgerechnet) { k.ZuletztAbgerechnet = heute; continue; }
                int monate = (heute - k.ZuletztAbgerechnet) / t;
                if (monate <= 0) continue;
                ergebnisse.Add(KreditAbrechnen(k, monate, heute));
                k.ZuletztAbgerechnet += monate * t;
            }
            return ergebnisse;
        }

        // ---------- Kredite ----------

        static double MonatsZins(decimal zinsPa) { return (double)zinsPa / 1200.0; }

        /// <summary>Feste Monatsrate (Annuität) für Summe, Zins pro Jahr und Laufzeit.</summary>
        public static decimal Annuitaet(decimal summe, decimal zinsPa, int monate)
        {
            if (monate <= 0) return summe;
            double i = MonatsZins(zinsPa);
            // Immer ganze Coins, aufgerundet.
            if (i <= 0) return Math.Ceiling(summe / monate);
            return Math.Ceiling((decimal)((double)summe * i / (1.0 - Math.Pow(1.0 + i, -monate))) - 0.0000001m);
        }

        /// <summary>Höchste Kreditsumme, die eine Monatsrate in dieser Laufzeit abträgt.</summary>
        public static decimal MaxSumme(decimal rate, decimal zinsPa, int monate)
        {
            if (rate <= 0 || monate <= 0) return 0m;
            double i = MonatsZins(zinsPa);
            if (i <= 0) return Math.Floor(rate * monate);
            return Math.Floor((decimal)((double)rate * (1.0 - Math.Pow(1.0 + i, -monate)) / i));
        }

        /// <summary>Nettoeinkommen pro Monat nach allen Kosten und Steuern. Firma: Monatsergebnis. Partei: Mieten ihrer Immobilien plus ihr Anteil am Ergebnis ihrer Firmen.</summary>
        public decimal NettoMonat(string nehmer, out bool istFirma)
        {
            var u = FirmaOderNull(nehmer);
            istFirma = u != null;
            if (u != null) return Vorschau(u).Einkommen;
            var p = ParteiOderNull(nehmer);
            if (p == null) throw new WirtschaftsFehler("'" + nehmer + "' ist weder Firma noch Partei.");
            decimal s = 0m;
            foreach (var i in Stand.Immobilien.Where(w => Gleich(w.Besitzer, p.Name)))
                s += Rechne(i.Name, i.MieteMonat, i.KostenMonat, i.Steuersatz, 1).Einkommen;
            foreach (var f in Stand.Unternehmen)
            {
                decimal anteil = AnteilVon(f, p.Name);
                if (anteil > 0) s += Runden(Vorschau(f).Einkommen * anteil / 100m);
                // Lohn als Angestellter (erweiterte Verwaltung, automatische Lohnabrechnung)
                if (Abm.LohnAutomatisch(f))
                {
                    decimal um = f.UmsatzMonat + Abm.VertragsUmsatzJetzt(f, Heute, Stand.TageProMonat);
                    foreach (var kv in Abm.Lohnempfaenger(f, um)) if (Gleich(kv.Key, p.Name)) s += kv.Value;
                }
            }
            return s;
        }

        public decimal LaufendeRaten(string nehmer)
        {
            return Stand.Kredite.Where(k => !k.Erledigt && Gleich(k.Nehmer, nehmer)).Sum(k => k.Rate);
        }

        /// <summary>Freie Monatsrate: Quote vom Nettoeinkommen minus Raten laufender Kredite.</summary>
        public decimal FreieRate(string nehmer)
        {
            bool firma;
            decimal netto = NettoMonat(nehmer, out firma);
            decimal frei = Runden(netto * Stand.KreditMaxQuote / 100m) - LaufendeRaten(nehmer);
            return frei < 0 ? 0m : frei;
        }

        /// <summary>Prüft einen Kreditwunsch gegen die Regeln. Gibt null zurück, wenn alles passt, sonst den Grund samt Vorschlag.</summary>
        public string KreditGrund(string nehmer, decimal summe, int monate)
        {
            if (summe <= 0) return "Die Kreditsumme muss größer als 0 sein.";
            if (monate < 1) return "Die Laufzeit muss mindestens 1 Monat sein.";
            var firma = FirmaOderNull(nehmer);
            if (firma != null && Finanzen.InKonkurs(firma))
                return "Die Firma " + firma.Name + " ist im Konkursverfahren. Kredite gibt es erst wieder, wenn das Verfahren eingestellt ist.";
            if (firma != null && Finanzen.Ueberschuldet(this, firma))
                return "Die Firma " + firma.Name + " ist überschuldet (Schulden mindestens so hoch wie der Firmenwert). Kein neuer Kredit möglich.";
            if (monate > Stand.KreditMaxMonate)
            {
                // Vorschlag: gleiche Summe über die längste erlaubte Laufzeit, falls die Rate dann passt
                decimal rateMax = Annuitaet(Math.Floor(summe), Stand.KreditZins, Stand.KreditMaxMonate);
                string v = rateMax <= FreieRate(nehmer)
                    ? " Möglich wäre dieselbe Summe über " + Stand.KreditMaxMonate + " Monate (Rate " + rateMax.ToString("N0") + ")."
                    : "";
                return "Höchstens " + Stand.KreditMaxMonate + " Monate Laufzeit erlaubt." + v;
            }
            summe = Math.Floor(summe);
            decimal rate = Annuitaet(summe, Stand.KreditZins, monate);
            decimal frei = FreieRate(nehmer);
            if (rate > frei)
            {
                string q = Stand.KreditMaxQuote.ToString("0.#") + " % vom Nettoeinkommen abzüglich laufender Raten";
                decimal moeglich = MaxSumme(frei, Stand.KreditZins, monate);
                decimal moeglichLang = MaxSumme(frei, Stand.KreditZins, Stand.KreditMaxMonate);
                string text = "Die Rate von " + rate.ToString("N0") + " übersteigt die erlaubte freie Rate von " + frei.ToString("N0") + " (" + q + ").";
                if (moeglich > 0)
                    text += " Möglich wären höchstens " + moeglich.ToString("N0") + " über " + monate + " Monate"
                            + (moeglichLang > moeglich ? " oder " + moeglichLang.ToString("N0") + " über " + Stand.KreditMaxMonate + " Monate" : "") + ".";
                else
                    text += " Aktuell ist gar kein Kredit möglich, das Einkommen trägt keine weitere Rate.";
                return text;
            }
            return null;
        }

        public Kredit KreditVergeben(string nehmer, decimal summe, int monate, string zweck, int heute)
        {
            string grund = KreditGrund(nehmer, summe, monate);
            if (grund != null) throw new WirtschaftsFehler(grund);
            var u = FirmaOderNull(nehmer);
            var p = u == null ? ParteiHolen(nehmer) : null;
            var k = new Kredit
            {
                Nr = Stand.Kredite.Count == 0 ? 1 : Stand.Kredite.Max(w => w.Nr) + 1,
                Bank = u != null ? u.Bank : "",
                Nehmer = u != null ? u.Name : p.Name,
                Summe = Math.Floor(summe), Zins = Stand.KreditZins, Monate = monate,
                Rate = Annuitaet(Math.Floor(summe), Stand.KreditZins, monate), Rest = Math.Floor(summe),
                Beginn = heute, ZuletztAbgerechnet = heute, Zweck = zweck ?? ""
            };
            if (u != null) { u.Konto += k.Summe; Buchen(u.Verlauf, heute, "Kredit Nr. " + k.Nr + " ausgezahlt", k.Summe); }
            else p.Konto += k.Summe;
            Buchen(Stand.Kassenbuch, heute, "Kredit Nr. " + k.Nr + " an " + k.Nehmer + (k.Zweck.Length > 0 ? " (" + k.Zweck + ")" : ""), k.Summe);
            Stand.Kredite.Add(k);
            return k;
        }

        /// <summary>Bucht fällige Monatsraten: Zins auf die Restschuld, der Rest tilgt.</summary>
        public Rechnung KreditAbrechnen(Kredit k, int monate, int heute)
        {
            var r = new Rechnung { Name = "Kredit Nr. " + k.Nr + " (" + k.Nehmer + ")", Monate = monate };
            double i = MonatsZins(k.Zins);
            decimal gezahlt = 0m;
            for (int m = 0; m < monate && !k.Erledigt; m++)
            {
                decimal zins = Math.Ceiling((decimal)((double)k.Rest * i) - 0.0000001m);   // ganze Coins
                decimal tilgung = Math.Min(k.Rest, k.Rate - zins);
                if (tilgung < 0) tilgung = 0;
                decimal zahlung = zins + tilgung;
                k.Rest = Runden(k.Rest - tilgung);
                k.Bezahlt++;
                gezahlt += zahlung;
                if (k.Rest <= 0.01m || k.Bezahlt >= k.Monate) { k.Rest = 0m; k.Erledigt = true; }
            }
            var u = FirmaOderNull(k.Nehmer);
            if (u != null) { u.Konto -= gezahlt; Buchen(u.Verlauf, heute, "Kreditrate Nr. " + k.Nr + " (" + monate + " Monat/e)", -gezahlt); }
            else { var p = ParteiOderNull(k.Nehmer); if (p != null) p.Konto -= gezahlt; }
            Buchen(Stand.Kassenbuch, heute, "Kreditrate Nr. " + k.Nr + " von " + k.Nehmer + (k.Erledigt ? " (abbezahlt)" : ""), -gezahlt);
            r.Ausgaben = gezahlt;
            r.Einkommen = -gezahlt;
            return r;
        }

        /// <summary>Manuell X Monate abrechnen: eine Firma, eine Immobilie oder alles einer Partei.</summary>
        public List<Rechnung> ManuellAbrechnen(string name, int monate, int heute)
        {
            if (monate <= 0) throw new WirtschaftsFehler("Die Anzahl Monate muss größer als 0 sein.");
            var ergebnisse = new List<Rechnung>();
            var u = FirmaOderNull(name);
            if (u != null) { ergebnisse.Add(FirmaAbrechnen(u, monate, heute)); return ergebnisse; }
            var im = ImmobilieOderNull(name);
            if (im != null) { ergebnisse.Add(ImmobilieAbrechnen(im, monate, heute)); return ergebnisse; }
            var p = ParteiHolen(name);
            foreach (var f in Stand.Unternehmen.Where(w => Gleich(w.Besitzer, p.Name)).ToList()) ergebnisse.Add(FirmaAbrechnen(f, monate, heute));
            foreach (var i in Stand.Immobilien.Where(w => Gleich(w.Besitzer, p.Name)).ToList()) ergebnisse.Add(ImmobilieAbrechnen(i, monate, heute));
            return ergebnisse;
        }

        // ---------- Rechnen ----------

        /// <summary>Rechnet für X Monate durch, ohne zu buchen.</summary>
        public static Rechnung Rechne(string name, decimal umsatz, decimal ausgaben, decimal steuersatz, int monate)
        {
            return RechneGesamt(name, Runden(umsatz * monate), Runden(ausgaben * monate), steuersatz, monate);
        }

        /// <summary>Wie Rechne, aber mit Gesamtbeträgen für alle Monate (eine einzige Steuerrechnung).</summary>
        public static Rechnung RechneGesamt(string name, decimal umsatzGesamt, decimal ausgabenGesamt, decimal steuersatz, int monate)
        {
            var r = new Rechnung { Name = name, Monate = monate };
            r.Umsatz = Runden(umsatzGesamt);
            r.Ausgaben = Runden(ausgabenGesamt);
            r.Zwischensumme = r.Umsatz - r.Ausgaben;
            r.Steuer = r.Zwischensumme > 0 ? Math.Ceiling(r.Zwischensumme * steuersatz / 100m) : 0m;   // ganze Coins, aufgerundet
            r.Einkommen = r.Zwischensumme - r.Steuer;
            return r;
        }

        public Rechnung Vorschau(Unternehmen u) { return RechneFirma(u, u.UmsatzMonat, u.AusgabenMonat, u.Steuersatz); }

        /// <summary>Ein Monat einer Firma mit den angegebenen Werten, plus erweiterte Verwaltung (Kundenverträge, automatische Löhne).
        /// Ohne erweiterte Verwaltung genau wie Rechne.</summary>
        public Rechnung RechneFirma(Unternehmen u, decimal umsatz, decimal ausgaben, decimal steuersatz)
        {
            decimal vert = Abm.VertragsUmsatzJetzt(u, Heute, Stand.TageProMonat);
            decimal lohn = Abm.LohnAutomatisch(u) ? Abm.LohnMonat(u, umsatz + vert) : 0m;
            return RechneGesamt(u.Name, Runden(umsatz) + vert, Runden(ausgaben) + lohn + Abm.KostenMonat(u), steuersatz, 1);
        }

        /// <summary>Löhne an die Konten der Personen (Mitarbeiter mit Lohnkonto). Das Geld ist beim Aufrufer schon vom Firmenkonto abgezogen.</summary>
        public void LoehneAuszahlen(Unternehmen u, List<KeyValuePair<string, decimal>> empfaenger, int monate, int heute)
        {
            var summe = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in empfaenger) { decimal b; summe.TryGetValue(kv.Key, out b); summe[kv.Key] = b + kv.Value * monate; }
            foreach (var kv in summe)
            {
                var p = ParteiOderNull(kv.Key);
                if (p == null) { Buchen(Stand.Kassenbuch, heute, "Lohn " + u.Name + " an " + kv.Key + " (Person fehlt, nicht gutgeschrieben)", kv.Value); continue; }
                p.Konto += kv.Value;
                Buchen(Stand.Kassenbuch, heute, "Lohn " + u.Name + " an " + p.Name, kv.Value);
            }
        }

        public void VerlaufBuchen(Unternehmen u, int heute, string text, decimal betrag) { Buchen(u.Verlauf, heute, text, betrag); }

        // ---------- Geld zurücksetzen (Börsenterminal, immer mit Rückfrage) ----------
        // Nur das Guthaben wird 0. Kredite, Anteile, Firmendaten und die erweiterte Verwaltung bleiben unverändert.
        // Hat die Person oder Firma einen Geldtresor, passt das Spiel die Geldgegenstände darin beim nächsten Abgleich an (dann leer).

        public decimal ParteiZuruecksetzen(string name, int heute)
        {
            var p = ParteiHolen(name);
            decimal alt = p.Konto;
            p.Konto = 0m;
            Buchen(Stand.Kassenbuch, heute, "Guthaben zurückgesetzt: " + p.Name + " (vorher " + Befehle.Betrag(alt) + ")", -alt);
            return alt;
        }

        public decimal FirmaZuruecksetzen(string name, int heute)
        {
            var u = Firma(name);
            decimal alt = u.Konto;
            u.Konto = 0m;
            Buchen(u.Verlauf, heute, "Guthaben zurückgesetzt (vorher " + Befehle.Betrag(alt) + ")", -alt);
            Buchen(Stand.Kassenbuch, heute, "Guthaben zurückgesetzt: Firma " + u.Name + " (vorher " + Befehle.Betrag(alt) + ")", -alt);
            return alt;
        }

        /// <summary>Was ein Stadt-Reset betrifft, als Satz für die Rückfrage.</summary>
        public string StadtResetFolgen(string bank)
        {
            var b = BankhausHolen(bank);
            var firmen = FirmenDerBank(b.Name);
            var reg = ParteiOderNull(b.Regierung);
            decimal summe = firmen.Sum(f => f.Konto) + (reg != null ? reg.Konto : 0m);
            return "Stadt " + b.Name + ": " + firmen.Count + " Firmenkonten" + (reg != null ? " und die Staatskasse von " + reg.Name : "")
                 + " werden auf 0 gesetzt (zusammen " + Befehle.Betrag(summe) + " " + Stand.Waehrung + "). Personen, Kredite und Anteile bleiben unverändert.";
        }

        public decimal StadtZuruecksetzen(string bank, int heute)
        {
            var b = BankhausHolen(bank);
            decimal summe = 0m;
            foreach (var f in FirmenDerBank(b.Name)) summe += FirmaZuruecksetzen(f.Name, heute);
            var reg = ParteiOderNull(b.Regierung);
            if (reg != null) summe += ParteiZuruecksetzen(reg.Name, heute);
            return summe;
        }

        public Rechnung FirmaAbrechnen(Unternehmen u, int monate, int heute)
        {
            // Erweiterte Verwaltung: Kundenverträge zählen zum Umsatz, automatische Löhne zu den Ausgaben - in DERSELBEN Rechnung
            // (eine Steuer, eine Buchung). Ohne erweiterte Verwaltung sind beide 0 und alles rechnet wie bisher.
            int t = Stand.TageProMonat;
            decimal vert = 0m, lohn = 0m;
            bool lohnAuto = Abm.LohnAutomatisch(u);
            var empfaenger = new List<KeyValuePair<string, decimal>>();
            for (int k = monate - 1; k >= 0; k--)
            {
                decimal v = Abm.VertragsUmsatzAm(u, heute - k * t, t);
                vert += v;
                if (lohnAuto)
                {
                    lohn += Abm.LohnMonat(u, u.UmsatzMonat + v);
                    empfaenger.AddRange(Abm.Lohnempfaenger(u, u.UmsatzMonat + v));
                }
            }
            if (Abm.ModulAn(u, Abm.Personal) && u.LohnManuell) u.LohnOffen += monate;
            decimal kosten = Abm.KostenMonat(u) * monate;
            var r = RechneGesamt(u.Name, Runden(u.UmsatzMonat * monate) + vert, Runden(u.AusgabenMonat * monate) + lohn + kosten, u.Steuersatz, monate);

            if (r.Steuer > 0)
            {
                var reg = RegierungVon(u.Bank);
                reg.Konto += r.Steuer;
                Buchen(Stand.Kassenbuch, heute, "Steuer " + u.Name + " an " + reg.Name, r.Steuer);
            }

            // Das Einkommen (oder ein Verlust) bleibt als Guthaben in der Firma. Auszahlen an der Hauptbank.
            u.Konto += r.Einkommen;
            r.Ruecklage = r.Einkommen;
            Buchen(u.Verlauf, heute, (r.Einkommen < 0 ? "Verlust (" : "Einkommen (") + monate + " Monat/e"
                + (vert > 0 ? ", davon Verträge " + Befehle.Betrag(vert) : "") + (lohn > 0 ? ", nach Löhnen " + Befehle.Betrag(lohn) : "")
                + (kosten > 0 ? ", nach lfd. Kosten " + Befehle.Betrag(kosten) : "") + ")", r.Einkommen);
            // Die Löhne sind oben schon als Ausgaben abgezogen; hier gehen sie nur an die Personen mit Lohnkonto (kein zweiter Abzug).
            if (empfaenger.Count > 0)
            {
                LoehneAuszahlen(u, empfaenger, 1, heute);
                foreach (var kv in empfaenger) r.Auszahlungen.Add(kv);
            }
            foreach (var ende in Abm.VertraegeAuslaufen(u, heute, t)) Buchen(u.Verlauf, heute, "Vertrag ausgelaufen: " + ende, 0m);
            return r;
        }

        public Rechnung ImmobilieAbrechnen(Immobilie i, int monate, int heute)
        {
            var r = Rechne(i.Name, i.MieteMonat, i.KostenMonat, i.Steuersatz, monate);
            if (r.Steuer > 0)
            {
                var reg = RegierungVon(i.Bank);
                reg.Konto += r.Steuer;
                Buchen(Stand.Kassenbuch, heute, "Steuer " + i.Name + " an " + reg.Name, r.Steuer);
            }
            var besitzer = ParteiOderNull(i.Besitzer);
            if (besitzer != null)
            {
                besitzer.Konto += r.Einkommen;
                r.Ausgeschuettet = r.Einkommen;
                r.Auszahlungen.Add(new KeyValuePair<string, decimal>(besitzer.Name, r.Einkommen));
                Buchen(Stand.Kassenbuch, heute, "Mietertrag " + i.Name + " an " + besitzer.Name, r.Einkommen);
            }
            return r;
        }

        /// <summary>Summe der Steuern pro Monat, für eine Bank oder (null) für alle.</summary>
        public decimal SteuernProMonat(string bank)
        {
            decimal s = 0m;
            foreach (var u in Stand.Unternehmen)
                if (bank == null || Gleich(u.Bank, bank)) s += Vorschau(u).Steuer;
            foreach (var i in Stand.Immobilien)
                if (bank == null || Gleich(i.Bank, bank)) s += Rechne(i.Name, i.MieteMonat, i.KostenMonat, i.Steuersatz, 1).Steuer;
            return s;
        }

        // ---------- Handel ----------

        public void Ueberweisen(string von, string an, decimal betrag, int heute, string zweck)
        {
            if (betrag <= 0) throw new WirtschaftsFehler("Der Betrag muss größer als 0 sein.");
            var a = ParteiHolen(von); var b = ParteiHolen(an);
            if (a == b) throw new WirtschaftsFehler("Absender und Empfänger sind gleich.");
            if (a.Konto < betrag) throw new WirtschaftsFehler(a.Name + " hat nur " + a.Konto.ToString("N2") + " auf dem Konto.");
            a.Konto -= betrag; b.Konto += betrag;
            Buchen(Stand.Kassenbuch, heute, (zweck ?? "Überweisung") + ": " + a.Name + " an " + b.Name, betrag);
        }

        public void AnteileVerkaufen(string firma, string von, string an, decimal prozent, decimal preis, int heute)
        {
            var u = Firma(firma);
            var verkaeufer = ParteiHolen(von); var kaeufer = ParteiHolen(an);
            if (prozent <= 0) throw new WirtschaftsFehler("Die Prozent müssen größer als 0 sein.");
            var alt = u.Anteile.FirstOrDefault(w => Gleich(w.Partei, verkaeufer.Name));
            decimal hat = alt == null ? 0 : alt.Prozent;
            if (hat < prozent) throw new WirtschaftsFehler(verkaeufer.Name + " hält nur " + hat + " % von " + u.Name + ".");
            if (preis > 0) Ueberweisen(kaeufer.Name, verkaeufer.Name, preis, heute, "Kauf " + prozent + " % " + u.Name);
            alt.Prozent -= prozent;
            if (alt.Prozent == 0) u.Anteile.Remove(alt);
            var neu = u.Anteile.FirstOrDefault(w => Gleich(w.Partei, kaeufer.Name));
            if (neu == null) u.Anteile.Add(new Anteil { Partei = kaeufer.Name, Prozent = prozent });
            else neu.Prozent += prozent;
            Buchen(u.Verlauf, heute, prozent + " % von " + verkaeufer.Name + " an " + kaeufer.Name, preis);
        }

        public void ImmobilieVerkaufen(string immobilie, string kaeufer, decimal preis, int heute)
        {
            var i = ImmobilieHolen(immobilie);
            var k = ParteiHolen(kaeufer);
            if (Gleich(i.Besitzer, k.Name)) throw new WirtschaftsFehler(k.Name + " besitzt " + i.Name + " schon.");
            if (preis > 0) Ueberweisen(k.Name, i.Besitzer, preis, heute, "Kauf " + i.Name);
            i.Besitzer = k.Name;
            i.ZuletztAbgerechnet = heute;
        }

        // ---------- Auswertung ----------

        /// <summary>Mitarbeiter-Staffel: Mitarbeiter 1–5 je 1.000, 6–10 je 2.000, ab dem 11. je 3.000.</summary>
        public static decimal MitarbeiterWert(int anzahl)
        {
            if (anzahl <= 0) return 0m;
            if (anzahl <= 5) return anzahl * 1000m;
            if (anzahl <= 10) return 5000m + (anzahl - 5) * 2000m;
            return 15000m + (anzahl - 10) * 3000m;
        }

        /// <summary>Aufschlüsselung des Firmenwerts. Fest = von Hand eingetragener Wert, die Teile sind dann nur zur Information.</summary>
        public Bewertung Bewerten(Unternehmen u)
        {
            var b = new Bewertung();
            var profil = Branchen.VonFirma(u);
            b.Branche = profil.Name;
            b.Faktor = profil.UmsatzFaktor < 2m ? 2m : profil.UmsatzFaktor;
            b.Umsatz = Runden(u.UmsatzMonat * b.Faktor);
            b.Mitarbeiter = MitarbeiterWert(u.Mitarbeiter);
            b.Immobilien = u.Immobilienwert;
            b.Fahrzeuge = u.Fahrzeugwert;
            b.Ertrag = Runden(Vorschau(u).Einkommen * Ertragsmonate);
            // Sachwerte (Immobilien, Fahrzeuge) bleiben auch bei Verlusten als Untergrenze erhalten.
            decimal substanz = Math.Max(0m, b.Immobilien + b.Fahrzeuge);
            b.Summe = Math.Max(substanz, b.Umsatz + b.Mitarbeiter + b.Immobilien + b.Fahrzeuge + b.Ertrag);
            if (u.Wert > 0) { b.Fest = true; b.Summe = u.Wert; }
            return b;
        }

        /// <summary>Börsenwert der Firma (oder fest eingetragener Wert). Einzelteile: siehe Bewerten.</summary>
        public decimal Firmenwert(Unternehmen u)
        {
            if (u.Wert > 0) return u.Wert;
            return Bewerten(u).Summe;
        }

        /// <summary>Preis für einen Anteil in Prozent.</summary>
        public decimal AnteilPreis(Unternehmen u, decimal prozent) { return Runden(Firmenwert(u) * prozent / 100m); }

        public decimal AnteilVon(Unternehmen u, string partei)
        {
            var a = u.Anteile.FirstOrDefault(w => Gleich(w.Partei, partei));
            return a == null ? 0m : a.Prozent;
        }

        /// <summary>Personen = alle Parteien, die gerade keine Regierung sind.</summary>
        public List<Partei> Personen() { return Stand.Parteien.Where(w => !IstRegierungspartei(w)).ToList(); }

        /// <summary>Anteile übertragen. Mit Zahlung zahlt der Käufer den Börsenpreis an den Verkäufer.</summary>
        public decimal AnteileUebertragen(Unternehmen u, string von, string an, decimal prozent, bool mitZahlung, int heute)
        {
            if (prozent != Math.Floor(prozent)) throw new WirtschaftsFehler("Nur ganze Prozent (1 %, 2 % ...).");
            decimal preis = mitZahlung ? AnteilPreis(u, prozent) : 0m;
            AnteileVerkaufen(u.Name, von, an, prozent, preis, heute);
            return preis;
        }

        public decimal Vermoegen(Partei p)
        {
            decimal summe = p.Konto;
            foreach (var u in Stand.Unternehmen)
                foreach (var a in u.Anteile.Where(w => Gleich(w.Partei, p.Name)))
                    summe += Runden(Firmenwert(u) * a.Prozent / 100m);
            foreach (var i in Stand.Immobilien)
                if (Gleich(i.Besitzer, p.Name)) summe += i.Wert;
            return summe;
        }

        // ---------- Hilfen ----------

        public static decimal Runden(decimal d) { return Math.Round(d, 2, MidpointRounding.AwayFromZero); }

        static void NameGueltig(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new WirtschaftsFehler("Der Name darf nicht leer sein.");
        }

        const int MaxVerlauf = 200;
        static void Buchen(List<Buchung> liste, int tag, string text, decimal betrag)
        {
            liste.Add(new Buchung { Tag = tag, Text = text, Betrag = betrag });
            if (liste.Count > MaxVerlauf) liste.RemoveRange(0, liste.Count - MaxVerlauf);
        }
    }
}
