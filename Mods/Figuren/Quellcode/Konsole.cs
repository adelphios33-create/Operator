// Figuren-Mod – Konsolenbefehl "npc" (F1), auch als Notlösung, falls E an der Figur nicht reagiert
// Geprüft gegen 7 Days to Die V 1.4 (b8): ConsoleCmdAbstract, SdtdConsole.Output.
using System;
using System.Collections.Generic;
using System.Linq;
using Figuren.Kern;
using UnityEngine;

namespace Figuren.Spiel
{
    public class KonsoleFiguren : ConsoleCmdAbstract
    {
        public override string[] getCommands() { return new[] { "npc", "figur" }; }

        public override string getDescription() { return "Figuren (NPCs): aufstellen, Rollen, reden (npc hilfe)"; }

        public override string getHelp() { return Hilfe; }

        const string Hilfe =
            "npc rollen            alle Rollen aus Mods/Figuren/Rollen\n" +
            "npc laden             Rollendateien neu einlesen\n" +
            "npc liste             aufgestellte Figuren\n" +
            "npc setzen <Rolle> [Stadt]    Figur 2 m vor dir aufstellen (Vorlagen wie Bürgermeister brauchen die Stadt, z. B. npc setzen Bürgermeister Springfield)\n" +
            "npc reden             mit der nächsten Figur reden\n" +
            "npc einrichten        nächste Figur einrichten\n" +
            "npc entfernen         nächste Figur (bis 5 m) entfernen\n" +
            "npc immo [Stadt]      eingetragene Immobilien-Schilder anzeigen\n" +
            "npc prompt <Rolle>    komplette Anweisung an das Modell in eine Datei im Spielstand schreiben (zum Prüfen)\n" +
            "npc test              zeigt, was die E-Taste-Abfrage gerade sieht\n" +
            "npc ki                KI.txt neu einlesen\n" +
            "npc leitung           Leitungsfenster (Operator, Gespräche aus der Ferne), auch per Taste Pos1 (Home)\n" +
            "npc gedaechtnis <Rolle>          Gedächtnis einer KI-Figur anzeigen (Operator: npc gedaechtnis Operator)\n" +
            "npc merke <Rolle> <Text>         festen Fakt eintragen\n" +
            "npc vergiss <Rolle> <Nummer>     festen Fakt löschen\n" +
            "--- Tests Aussehen (gilt bis Neuladen der Welt) ---\n" +
            "npc teile [lang]      alle Modelle analysieren -> Figuren_Teile.txt im Spielstand (lang = Figuren bleiben ca. 30 Sekunden stehen)\n" +
            "npc farbe <Teil|alle> <Farbe>    nächste Figur färben (rot, grün, blau, gelb, schwarz, weiß, braun, blond, lila, #RRGGBB, normal)\n" +
            "npc teil <Teil> aus|an          Teil der nächsten Figur aus-/einblenden\n" +
            "npc größe <0.7-1.4>             nächste Figur größer/kleiner\n" +
            "--- Test Typ 3 ---\n" +
            "npc schuss            nächste Figur schießt einmal auf den nächsten Zombie (bis 40 m)\n" +
            "npc körper            welche Knochen/Waffe die Körpersteuerung bei der nächsten Figur gefunden hat\n" +
            "npc lauf [schnell|0.4] nächste Figur (15 m) geht dorthin, wo du hinschaust (Test Typ 4)\n" +
            "npc lauf status|stop|zurück   Ergebnis / anhalten / zurück an ihren Platz\n" +
            "npc waffe <keine|pistole|mp|ak|schrotflinte>   Waffe der nächsten Figur (jede bewaffnete Figur wehrt sich gegen Zombies)\n" +
            "nf1 (oder npc f1)     ALLE Söldner feindlich (schießen auf Spieler, sind verwundbar)\n" +
            "nf2 (oder npc f2)     ALLE Söldner freundlich (nur Zombies)\n" +
            "npc haltung <wache|feindlich>   nur ein Söldner (der nächste)\n" +
            "npc lage x|y|z <cm> | dreh x|y|z <Grad> | null   Waffe in der Hand der nächsten Figur nachjustieren\n" +
            "npc stimmen           Liste der Sätze ohne Sprachausgabe neu schreiben (danach STIMMEN.bat)";

        static void Aus(string text)
        {
            foreach (var z in text.Replace("\r", "").Split('\n')) SdtdConsole.Instance.Output(z);
        }

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            try { Aus(Ausfuehren(_params)); }
            catch (Exception e) { Aus("Fehler: " + e.Message); }
        }

        static string Ausfuehren(List<string> p)
        {
            string cmd = p.Count == 0 ? "hilfe" : p[0].ToLowerInvariant();
            var welt = GameManager.Instance == null ? null : GameManager.Instance.World;
            var spieler = welt == null ? null : welt.GetPrimaryPlayer();

            switch (cmd)
            {
                case "rollen":
                    if (Rollen.Liste.Count == 0) return "Keine Rollen. Ordner: " + Rollen.Ordner;
                    return string.Join("\n", Rollen.Liste.Select((r, i) => (i + 1) + ". " + r.Name + (r.Titel.Length > 0 ? " - " + r.Titel : "") + "  [" + r.Art + (r.Vorlage ? ", Städte: " + string.Join("/", Rollen.StaedteFuer(r).ToArray()) : ", Aussehen " + r.Aussehen) + "]" + "  Datei " + r.Datei + ".txt").ToArray());
                case "laden":
                    return Rollen.Laden();
                case "test":
                    return FigurenMod.FokusBericht();
                case "gedaechtnis":
                case "merke":
                case "vergiss":
                    {
                        if (FigurenMod.Stand == null) return "Erst eine Welt laden.";
                        if (p.Count < 2) return Hilfe;
                        var rr = Rollen.Holen(p[1]);
                        string name = rr != null ? rr.Name : (p[1].Equals(Ki.ErzaehlerName, StringComparison.OrdinalIgnoreCase) || p[1].Equals("Erzaehler", StringComparison.OrdinalIgnoreCase) || p[1].Equals("Erzähler", StringComparison.OrdinalIgnoreCase) ? Ki.ErzaehlerName : null);
                        if (name == null) return "Rolle '" + p[1] + "' gibt es nicht. npc rollen zeigt alle.";
                        string rest = string.Join(" ", p.Skip(2).ToArray()).Trim();
                        if (cmd == "merke") { string m = Ki.Merken(name, rest); FigurenMod.Speichern(); return m; }
                        if (cmd == "vergiss") { string m = Ki.FaktLoeschen(name, rest); FigurenMod.Speichern(); return m; }
                        var g = Ki.Gespraech(name);
                        var z = new List<string>();
                        z.Add("Gedächtnis von " + name + ":");
                        z.Add("Feste Fakten:" + (g.Fakten.Count == 0 ? " keine" : ""));
                        for (int i = 0; i < g.Fakten.Count; i++) z.Add("  " + (i + 1) + ". " + g.Fakten[i]);
                        z.Add("Erinnerung:");
                        z.Add(string.IsNullOrEmpty(g.Gedaechtnis) ? "  noch keine" : g.Gedaechtnis);
                        z.Add("Wörtlich gespeichert: " + g.Nachrichten.Count + " Nachrichten.");
                        return string.Join("\n", z.ToArray());
                    }
                case "ki":
                    return Ki.EinstellungenLaden() + (Ki.Modell.Length > 0 ? ", Modell " + Ki.Modell : ", Modell: das in LM Studio geladene");
            }

            if (welt == null || spieler == null || FigurenMod.Stand == null) return "Erst eine Welt laden.";

            switch (cmd)
            {
                case "liste":
                    if (FigurenMod.Stand.Figuren.Count == 0) return "Noch keine Figuren aufgestellt.";
                    return string.Join("\n", FigurenMod.Stand.Figuren.Select(f =>
                        "#" + f.Id + " " + (f.Rolle.Length > 0 ? f.Rolle : "(keine Rolle)") + " bei " + (int)f.X + " " + (int)f.Y + " " + (int)f.Z).ToArray());
                case "setzen":
                    {
                        string name = string.Join(" ", p.Skip(1).ToArray()).Trim();
                        string stadt = "";
                        var r = Rollen.Holen(name);
                        if (r == null && p.Count > 2)
                        {
                            // letztes Wort = Stadt, z. B. "npc setzen Bürgermeister Springfield"
                            string name2 = string.Join(" ", p.Skip(1).Take(p.Count - 2).ToArray()).Trim();
                            var r2 = Rollen.Holen(name2);
                            if (r2 != null && r2.Vorlage) { r = r2; name = name2; stadt = p[p.Count - 1]; }
                        }
                        if (name.Length > 0 && r == null) return "Rolle '" + name + "' gibt es nicht. npc rollen zeigt alle.";
                        var staedte = Rollen.StaedteFuer(r);
                        if (r != null && r.Vorlage && stadt.Length == 0 && staedte.Count > 0)
                            return "Für " + r.Name + " bitte die Stadt dazuschreiben: " + string.Join(", ", staedte.ToArray()) + ". Beispiel: npc setzen " + r.Name + " " + staedte[0];
                        if (r != null && r.Vorlage && staedte.Count > 0)
                        {
                            string treffer = staedte.FirstOrDefault(w => w.Equals(stadt, StringComparison.OrdinalIgnoreCase));
                            if (treffer == null) return "Stadt '" + stadt + "' gibt es nicht (" + string.Join(", ", staedte.ToArray()) + "). Weitere Städte: Zeile Städte: in KI.txt.";
                            stadt = treffer;
                        }
                        float yaw = spieler.rotation.y * Mathf.Deg2Rad;
                        var pos = spieler.position + new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)) * 2f;
                        var f = FigurenMod.Aufstellen(r != null ? r.Name : "", pos, FigurenMod.DrehungZu(pos, spieler.position), stadt);
                        var pl = FigurenMod.PlatzVon(f);
                        string wer = pl != null && pl.Rolle.Length > 0 ? pl.Rolle + (r != null && r.Vorlage ? " (" + r.Name + (stadt.Length > 0 ? " " + stadt : "") + ")" : "") : null;
                        return "Figur #" + f.entityId + " aufgestellt" + (wer != null ? " als " + wer : " (ohne Rolle)") + ".";
                    }
                case "leitung":
                    FigurenMod.Spaeter(() => XUiC_FigLeitung.Oeffnen(spieler));
                    return "Öffne Leitungsfenster (F1-Konsole schließen).";
                case "reden":
                case "einrichten":
                    {
                        var f = FigurenMod.NaechsteFigur(spieler, 10f);
                        if (f == null) return "Keine Figur in 10 m Umkreis.";
                        int id = f.entityId;
                        if (cmd == "reden") FigurenMod.Spaeter(() => FigurenMod.Reden(spieler, id));
                        else FigurenMod.Spaeter(() => XUiC_FigEinrichten.Oeffnen(spieler, id));
                        return "Öffne Fenster (F1-Konsole schließen).";
                    }
                case "prompt":
                    {
                        if (p.Count < 2) return "npc prompt <Rolle>";
                        string name = string.Join(" ", p.Skip(1).ToArray()).Trim();
                        var r = name.Equals(Ki.ErzaehlerName, StringComparison.OrdinalIgnoreCase) ? Ki.Erzaehler : Rollen.Holen(name);
                        if (r == null) return "Rolle '" + name + "' gibt es nicht (bei Grundrollen den Namen der Figur nehmen, z. B. Julian Moor).";
                        string text = Ki.Systemtext(r, "Spieler", false, "Hallo");
                        string datei = System.IO.Path.Combine(GameIO.GetSaveGameDir(), "Figuren_Prompt_" + r.Name + ".txt");
                        System.IO.File.WriteAllText(datei, text, System.Text.Encoding.UTF8);
                        return "Anweisung für " + r.Name + " (" + text.Length + " Zeichen, Immobilien " + (r.Immobilien ? "ja" : "nein") + ", Finanzen " + (r.Finanzen ? "ja" : "nein") + ", Stadt " + (r.Stadt.Length > 0 ? r.Stadt : "-") + ") gespeichert in: " + datei;
                    }
                case "immo":
                    {
                        string st = p.Count > 1 ? p[1] : "";
                        string t = Immo.FuerKi(st);
                        return t.Length == 0 ? "Keine Daten." : t;
                    }
                case "f1":
                case "feindlich":
                    return Waffen.AlleSoeldner(true);
                case "f2":
                case "freundlich":
                    return Waffen.AlleSoeldner(false);
                case "stimmen":
                    {
                        int n = Stimmen.ListeSchreiben();
                        return n == 0 ? "Alle festen Sätze haben schon eine Stimme (oder es gibt keine)." : n + " Sätze ohne Stimme in Mods/Figuren/Stimmen/*.jsonl geschrieben. Jetzt STIMMEN.bat im Mod-Ordner doppelklicken.";
                    }
                case "teile":
                    return Optik.AlleAnalysieren(spieler, p.Count > 1 && p[1].ToLowerInvariant() == "lang" ? 1800 : 0);
                case "farbe":
                case "teil":
                case "größe":
                case "groesse":
                case "schuss":
                case "wache":
                case "lage":
                case "waffe":
                case "haltung":
                    {
                        var f = FigurenMod.NaechsteFigur(spieler, 10f);
                        if (f == null) return "Keine Figur in 10 m Umkreis.";
                        if (cmd == "farbe")
                        {
                            if (p.Count < 3) return "npc farbe <Teil|alle> <Farbe>, z. B. npc farbe hair grün";
                            Color c;
                            if (!Optik.FarbeLesen(p[2], out c)) return "Farbe '" + p[2] + "' unbekannt.";
                            return Optik.Faerben(f, p[1], c);
                        }
                        if (cmd == "teil")
                        {
                            if (p.Count < 3) return "npc teil <Teil> aus|an";
                            return Optik.Schalten(f, p[1], p[2].ToLowerInvariant() == "an");
                        }
                        if (cmd == "schuss")
                        {
                            var ziel = Optik.NaechsterGegner(f.position, 40f);
                            if (ziel == null) return "Kein Zombie bis 40 m um die Figur.";
                            return Optik.Schuss(f, ziel, 35, "ak47_fire");
                        }
                        if (cmd == "wache") return "Alle bewaffneten Figuren verteidigen sich immer gegen Zombies. Söldner: npc f1 (feindlich) / npc f2 (freundlich).";
                        if (cmd == "lage") return Waffen.LageBefehl(f, p.Skip(1).ToList());
                        if (cmd == "waffe") return p.Count < 2 ? "npc waffe keine|pistole|mp|ak|schrotflinte" : Waffen.WaffeSetzen(f, p[1]);
                        if (cmd == "haltung") return p.Count < 2 ? "npc haltung friedlich|wache|feindlich" : Waffen.HaltungSetzen(f, p[1]);
                        float g;
                        if (p.Count < 2 || !float.TryParse(p[1].Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out g)) return "npc größe 1.1";
                        return Optik.Groesse(f, g);
                    }
                case "lauf":
                    {
                        var f = FigurenMod.NaechsteFigur(spieler, 15f);
                        if (f == null) return "Keine Figur in 15 m Umkreis.";
                        string was = p.Count > 1 ? p[1].ToLowerInvariant() : "";
                        if (was == "status") return Laufen.Status(f);
                        if (was == "stop") return Laufen.Stoppen(f);
                        float tempo = 0.25f;
                        if (was == "schnell") tempo = 0.6f;
                        else if (was.Length > 0) { float tt; if (float.TryParse(was.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out tt)) tempo = tt; }
                        var pl = FigurenMod.PlatzVon(f);
                        if (was == "zurück" || was == "zurueck")
                        {
                            if (pl == null) return "Figur hat keinen Platz.";
                            return Laufen.Starten(f, new Vector3(pl.X, pl.Y, pl.Z), 0.25f);
                        }
                        return Laufen.Starten(f, Laufen.Blickpunkt(spieler), tempo);
                    }
                case "körper":
                case "koerper":
                    {
                        var f = FigurenMod.NaechsteFigur(spieler, 10f);
                        if (f == null) return "Keine Figur in 10 m Umkreis.";
                        return FigurKoerper.Fuer(f).Bericht();
                    }
                case "entfernen":
                    {
                        var f = FigurenMod.NaechsteFigur(spieler, 5f);
                        if (f == null) return "Keine Figur in 5 m Umkreis.";
                        FigurenMod.Entfernen(f.entityId);
                        return "Figur entfernt.";
                    }
            }
            return Hilfe;
        }
    }

    /// <summary>Kurzbefehl "nf1": alle Söldner feindlich.</summary>
    public class KonsoleNf1 : ConsoleCmdAbstract
    {
        public override string[] getCommands() { return new[] { "nf1" }; }
        public override string getDescription() { return "Figuren: alle Söldner feindlich (schießen auf Spieler)"; }
        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            try { SdtdConsole.Instance.Output(Waffen.AlleSoeldner(true)); }
            catch (Exception e) { SdtdConsole.Instance.Output("Fehler: " + e.Message); }
        }
    }

    /// <summary>Kurzbefehl "nf2": alle Söldner freundlich.</summary>
    public class KonsoleNf2 : ConsoleCmdAbstract
    {
        public override string[] getCommands() { return new[] { "nf2" }; }
        public override string getDescription() { return "Figuren: alle Söldner freundlich (nur Zombies)"; }
        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            try { SdtdConsole.Instance.Output(Waffen.AlleSoeldner(false)); }
            catch (Exception e) { SdtdConsole.Instance.Output("Fehler: " + e.Message); }
        }
    }
}
