// Bauplan-Mod – Einstieg und Konsolenbefehl "bau" (F1).
// Geprüft gegen V 1.4: IModApi.InitMod(Mod), ModEvents.WorldShuttingDown, ConsoleCmdAbstract, SdtdConsole.Instance.Output.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bauplan
{
    public class BauplanMod : IModApi
    {
        public void InitMod(Mod _modInstance)
        {
            ModEvents.WorldShuttingDown.RegisterHandler(Beendet);
            ModEvents.GameStartDone.RegisterHandler(Gestartet);
            try
            {
                new HarmonyLib.Harmony("adelphios.bauplan").PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
                Log.Out("[Bauplan] Mod geladen, Editor-Tasten (Strg+C / Strg+V / Strg+Z) mit dem Werkzeug aktiv.");
            }
            catch (Exception e) { Log.Error("[Bauplan] Harmony-Patch fehlgeschlagen, Editor-Tasten nicht verfügbar: " + e.Message); }
        }

        // items.xml nennt nur die harmlose Spiel-Aktion "Cancel". So bleibt das Spiel heil, auch wenn Bauplan.dll einmal fehlt
        // (sonst bricht das Spiel das Einlesen ALLER Gegenstände ab). Erst hier, mit geladener DLL, kommen unsere Aktionen hinein.
        static void Gestartet()
        {
            Einsetzen<ItemActionBauplan>(Editor.Werkzeug);
            Einsetzen<ItemActionAbriss>(Abriss.Werkzeug);
        }

        static void Einsetzen<T>(string itemName) where T : ItemAction, new()
        {
            try
            {
                var ic = ItemClass.GetItemClass(itemName, false);
                if (ic == null || ic.Actions == null) { Log.Warning("[Bauplan] Gegenstand " + itemName + " nicht gefunden."); return; }
                for (int i = 0; i < ic.Actions.Length && i < 2; i++)
                {
                    var alt = ic.Actions[i];
                    if (alt == null || alt is T) continue;
                    var neu = new T();
                    neu.item = alt.item ?? ic;
                    neu.ActionIndex = alt.ActionIndex;
                    neu.Range = alt.Range;
                    neu.SphereRadius = alt.SphereRadius;
                    neu.ExecutionRequirements = alt.ExecutionRequirements;
                    if (alt.Properties != null) neu.ReadFrom(alt.Properties);
                    neu.Delay = alt.Delay;
                    ic.Actions[i] = neu;
                }
                Log.Out("[Bauplan] Werkzeug bereit: " + itemName);
            }
            catch (Exception e) { Log.Error("[Bauplan] Werkzeug " + itemName + " konnte nicht eingerichtet werden: " + e); }
        }

        static void Beendet()
        {
            try { Vorschau.Weg(); } catch (Exception) { }
            Plan.Modus = Modus.Markieren;
            Plan.HatEcke1 = false;
            Plan.HatEcke2 = false;
            Plan.Gewaehlt = null;
            Plan.Platz = null;
            Plan.GewaehltName = "";
            Liste.Neu();
            Abriss.Leeren();
        }
    }

    public class KonsoleBauplan : ConsoleCmdAbstract
    {
        public override string[] getCommands() { return new[] { "bau", "bauplan" }; }

        public override string getDescription() { return "Bauplan: Gebäude kopieren und einfügen (bau hilfe)"; }

        public override string getHelp() { return Hilfe; }

        const string Hilfe =
            "bau                       Stand und Fenster öffnen\n" +
            "bau ecke1 / bau ecke2     Ecke auf den Block setzen, auf den du schaust\n" +
            "bau kopieren <Name>       Markierung als eigenes Gebäude speichern\n" +
            "bau boden                 Boden unter eingefügtem Gebäude reparieren (terrainFiller)\n" +
            "bau auffuellen            Löcher in der Markierung bis zur oberen Ecke mit Erde füllen, 2x eingeben\n" +
            "bau asphalt               wie auffuellen, oberste Schicht wird Asphalt (Straßen), 2x eingeben\n" +
            "bau fuellen [beton|stahl|ziegel|kopfstein|holz]  ganze Markierung mit Baumaterial füllen (Fundament), 2x eingeben\n" +
            "bau liste [Suche]         Gebäude suchen (eigene zuerst)\n" +
            "bau waehlen <Name>        Gebäude zum Einfügen auswählen\n" +
            "bau platzieren            Platzieren mit dem Werkzeug starten\n" +
            "bau drehen                um 90° drehen\n" +
            "bau abbrechen             Platzieren beenden\n" +
            "bau rueckgaengig          letztes Einfügen zurücknehmen\n" +
            "Mit dem Werkzeug in der Hand wie im Prefab-Editor: Strg+C kopiert den Rahmen, Strg+V fügt ein, Strg+Z macht rückgängig.\n" +
            "bau leeren [alles]        Markierung leeren (ohne 'alles' bleibt Erde/Stein), 2x eingeben\n" +
            "bau abrissrueck           letzten Abriss mit dem Abriss-Werkzeug zurückholen\n" +
            "Werkzeug holen: giveself bauplanWerkzeug   (oder craften: 1 Holz)\n" +
            "Abriss-Werkzeug: giveself bauplanAbriss    (Linksklick = Block weg, Shift = 3x3x3, Rechtsklick = zurück)";

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            var welt = GameManager.Instance == null ? null : GameManager.Instance.World;
            var sp = welt == null ? null : welt.GetPrimaryPlayer();
            if (sp == null) { SdtdConsole.Instance.Output("Bauplan: Erst eine Welt laden."); return; }
            string was = _params.Count > 0 ? _params[0].ToLowerInvariant() : "";
            string rest = _params.Count > 1 ? string.Join(" ", _params.Skip(1).ToArray()) : "";
            string antwort;
            switch (was)
            {
                case "": antwort = Plan.MarkierungText() + "\nAusgewählt: " + (Plan.GewaehltName.Length > 0 ? Plan.GewaehltName : "-"); XUiC_Bauplan.Oeffnen(sp); break;
                case "hilfe": case "help": antwort = Hilfe; break;
                case "ecke1": Plan.SetzeEcke(sp, 1); antwort = Plan.Meldung; break;
                case "ecke2": Plan.SetzeEcke(sp, 2); antwort = Plan.Meldung; break;
                case "kopieren": antwort = Plan.Kopieren(rest, false); break;
                case "liste":
                    {
                        var l = Liste.Gefiltert(0, rest);
                        antwort = l.Count + " Gebäude" + (rest.Length > 0 ? " mit '" + rest + "'" : "") + ":\n" +
                                  string.Join("\n", l.Take(60).Select(e => "  " + e.Name + (e.Eigen ? "  [eigen]" : "")).ToArray());
                        break;
                    }
                case "waehlen": case "wählen":
                    {
                        var e = Liste.Alle().FirstOrDefault(x => string.Equals(x.Name, rest, StringComparison.OrdinalIgnoreCase));
                        antwort = e == null ? "Kein Gebäude mit dem Namen '" + rest + "'. Suchen mit: bau liste <Teil des Namens>" : Plan.Waehlen(e);
                        break;
                    }
                case "platzieren": antwort = Plan.Platzieren(sp); break;
                case "drehen": Plan.Drehen(); antwort = "Drehung: " + Plan.Drehung * 90 + "°"; break;
                case "abbrechen": Plan.Abbrechen(); antwort = "Platzieren beendet."; break;
                case "rueckgaengig": case "rückgängig": antwort = Plan.Rueckgaengig(); break;
                case "leeren": antwort = Plan.FlaecheLeeren(rest.Trim().ToLowerInvariant() == "alles"); break;
                case "boden": antwort = Plan.BodenReparieren(); break;
                case "auffuellen": antwort = Plan.BodenAuffuellen(); break;
                case "asphalt": antwort = Plan.BodenAuffuellen(true); break;
                case "fuellen":
                    {
                        string mn = rest.Trim().ToLowerInvariant();
                        if (mn.Length > 0)
                        {
                            int gef = -1;
                            for (int i = 0; i < Plan.MaterialNamen.Length; i++) if (Plan.MaterialNamen[i].ToLowerInvariant().StartsWith(mn)) gef = i;
                            if (gef < 0) { antwort = "Material: " + string.Join(", ", Plan.MaterialNamen); break; }
                            Plan.Material = gef;
                        }
                        antwort = Plan.FlaecheFuellen();
                        break;
                    }
                case "abrissrueck": Abriss.Zurueck(sp); antwort = Plan.Meldung; break;
                default: antwort = Hilfe; break;
            }
            foreach (var z in antwort.Replace("\r", "").Split('\n')) SdtdConsole.Instance.Output(z);
        }
    }
}
