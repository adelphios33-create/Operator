// Wirtschaft-Mod – Konsolenbefehl "wb" (F1)
// Geprüft gegen 7 Days to Die V 1.4 (b8): ConsoleCmdAbstract (getCommands, getDescription, getHelp, Execute).
using System.Collections.Generic;
using Wirtschaft.Kern;

namespace Wirtschaft.Spiel
{
    public class KonsoleWirtschaft : ConsoleCmdAbstract
    {
        public override string[] getCommands() { return new[] { "wb", "wirtschaft" }; }

        public override string getDescription() { return "Wirtschaft: Parteien, Firmen, Steuern, Anteile, Immobilien (wb hilfe)"; }

        public override string getHelp() { return Befehle.Hilfe; }

        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            if (WirtschaftMod.Bank == null)
            {
                SdtdConsole.Instance.Output("Wirtschaft: Erst eine Welt laden.");
                return;
            }
            string erstes = _params.Count > 0 ? _params[0].ToLowerInvariant() : "";
            string antwort = (erstes == "schilder" || erstes == "schild")
                ? Schilder.Befehl(_params)
                : Befehle.Ausfuehren(WirtschaftMod.Bank, _params, WirtschaftMod.Heute);
            foreach (var zeile in antwort.Replace("\r", "").Split('\n'))
                SdtdConsole.Instance.Output(zeile);
            WirtschaftMod.Speichern();
        }
    }
}
