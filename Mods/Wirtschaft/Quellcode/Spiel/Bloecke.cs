// Wirtschaft-Mod – Hauptbank und Firmentresor als Blöcke
// Geprüft gegen 7 Days to Die V 1.4 (b8): Block-Methoden, BlockActivationCommand, GameManager.ShowTooltip.
// Anklicken (E) öffnet das F1-Fenster und zeigt dort Übersicht bzw. Firmendetails.
using System;
using Wirtschaft.Kern;
using Wirtschaft.Spiel;

// Die Blockklassen liegen bewusst ohne Namespace: Das Spiel sucht "Class=Hauptbank, Wirtschaft"
// als Typ "BlockHauptbank" in der Datei Wirtschaft.dll.
static class BlockHilfe
{
    public static string Position(Vector3i p) { return p.x + "," + p.y + "," + p.z; }

    /// <summary>Bei Blöcken über mehrere Felder (Geldautomat: 2 hoch) immer die Position des Hauptteils.</summary>
    public static string Hauptposition(Vector3i pos, BlockValue bv)
    {
        try
        {
            if (bv.ischild && bv.Block != null && bv.Block.multiBlockPos != null)
                return Position(bv.Block.multiBlockPos.GetParentPos(pos, bv));
        }
        catch (Exception) { }
        return Position(pos);
    }

    /// <summary>Öffnet das F1-Fenster und schreibt den Bericht hinein. Klappt das nicht, landet er im Log.</summary>
    public static void Zeigen(EntityPlayerLocal spieler, string kurz, string bericht)
    {
        string[] zeilen = bericht.Replace("\r", "").Split('\n');
        bool imFenster = false;
        try
        {
            var ui = LocalPlayerUI.primaryUI;
            var konsole = ui == null ? null : ui.windowManager.GetWindow<GUIWindowConsole>(GUIWindowConsole.ID);
            if (konsole != null)
            {
                GameManager.Instance.SetConsoleWindowVisible(true);
                konsole.Clear();
                konsole.AddLines(zeilen);
                imFenster = true;
            }
        }
        catch (Exception e)
        {
            Log.Warning("[Wirtschaft] F1-Fenster nicht erreichbar: " + e.Message);
        }
        if (!imFenster)
            foreach (var z in zeilen) Log.Out("[Wirtschaft] " + z);

        try { if (spieler != null) GameManager.ShowTooltip(spieler, kurz, false); }
        catch (Exception) { }
    }

    public static readonly BlockActivationCommand[] Befehl = { new BlockActivationCommand("wb_open", "search", true, false) };
}

/// <summary>Hauptbank: zeigt alle Parteien, Firmen und Immobilien.</summary>
public class BlockHauptbank : Block
{
    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, int _clrIdx, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return "[E] Hauptbank öffnen";
    }

    public override bool HasBlockActivationCommands(WorldBase _world, BlockValue _blockValue, int _clrIdx, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return true;
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, int _clrIdx, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return BlockHilfe.Befehl;
    }

    public override bool OnBlockActivated(string _commandName, WorldBase _world, int _cIdx, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        return Oeffnen(BlockHilfe.Hauptposition(_blockPos, _blockValue), _player);
    }

    public override bool OnBlockActivated(WorldBase _world, int _clrIdx, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        return Oeffnen(BlockHilfe.Hauptposition(_blockPos, _blockValue), _player);
    }

    /// <summary>Wird die Hauptbank abgebaut, gehört der Platz zu keiner Bank mehr. Die Bank selbst bleibt bestehen.</summary>
    public override void OnBlockRemoved(WorldBase _world, Chunk _chunk, Vector3i _blockPos, BlockValue _blockValue)
    {
        base.OnBlockRemoved(_world, _chunk, _blockPos, _blockValue);
        var bank = WirtschaftMod.Bank;
        if (bank == null || _blockValue.ischild) return;
        bank.StandortEntfernen(BlockHilfe.Position(_blockPos));
        WirtschaftMod.Speichern();
    }

    static bool Oeffnen(string pos, EntityPlayerLocal spieler)
    {
        var bank = WirtschaftMod.Bank;
        if (bank == null) return false;
        if (XUiC_WbHauptbank.Oeffnen(spieler, pos)) return true;
        // Notlösung, falls das Fenster nicht geladen ist: Übersicht im F1-Fenster
        BlockHilfe.Zeigen(spieler, "Hauptbank geöffnet",
            Befehle.Uebersicht(bank, WirtschaftMod.Heute) + "\nAlle Befehle: wb hilfe");
        return true;
    }
}

/// <summary>Firmentresor: zeigt die verbundene Firma, oder merkt sich die Position zum Verbinden.</summary>
public class BlockFirmentresor : Block
{
    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, int _clrIdx, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        var bank = WirtschaftMod.Bank;
        var firma = bank == null ? null : bank.FirmaAmTresor(BlockHilfe.Position(_blockPos));
        if (firma != null) return "[E] Tresor " + firma.Name;
        var reg = bank == null ? null : bank.RegierungAmTresor(BlockHilfe.Position(_blockPos));
        return reg != null ? "[E] Tresor " + reg.Name : "[E] Firmentresor (nicht verbunden)";
    }

    public override bool HasBlockActivationCommands(WorldBase _world, BlockValue _blockValue, int _clrIdx, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return true;
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, int _clrIdx, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return BlockHilfe.Befehl;
    }

    public override bool OnBlockActivated(string _commandName, WorldBase _world, int _cIdx, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        return Oeffnen(_blockPos, _player);
    }

    public override bool OnBlockActivated(WorldBase _world, int _clrIdx, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        return Oeffnen(_blockPos, _player);
    }

    static bool Oeffnen(Vector3i pos3, EntityPlayerLocal spieler)
    {
        var bank = WirtschaftMod.Bank;
        if (bank == null) return false;
        string pos = BlockHilfe.Position(pos3);
        var firma = bank.FirmaAmTresor(pos);
        if (firma == null && bank.RegierungAmTresor(pos) == null) bank.LetzterFreierTresor = pos;
        if (XUiC_WbTresor.Oeffnen(spieler, pos)) return true;
        // Notlösung, falls das Fenster nicht geladen ist
        if (firma == null)
        {
            BlockHilfe.Zeigen(spieler, "Tresor nicht verbunden",
                "Firmentresor bei " + pos + " ist noch frei.\nVerbinden mit:  wb tresor \"Name der Firma\"");
            return true;
        }
        BlockHilfe.Zeigen(spieler, "Tresor " + firma.Name, Befehle.FirmaBericht(bank, firma, WirtschaftMod.Heute));
        return true;
    }

    /// <summary>Wird der Tresor abgebaut, verliert die Firma ihre Verbindung, bleibt aber bestehen.</summary>
    public override void OnBlockRemoved(WorldBase _world, Chunk _chunk, Vector3i _blockPos, BlockValue _blockValue)
    {
        base.OnBlockRemoved(_world, _chunk, _blockPos, _blockValue);
        var bank = WirtschaftMod.Bank;
        var firma = bank == null ? null : bank.FirmaAmTresor(BlockHilfe.Position(_blockPos));
        if (firma != null) { firma.Tresor = ""; WirtschaftMod.Speichern(); }
        var reg = bank == null ? null : bank.RegierungAmTresor(BlockHilfe.Position(_blockPos));
        if (reg != null) { reg.RegTresor = ""; reg.RegZuletzt = 0; WirtschaftMod.Speichern(); }
    }
}

/// <summary>Börsenterminal: öffnet das Börsenfenster (Firmenwerte, Eigentümer, Anteilshandel, Personen).</summary>
public class BlockBoerse : Block
{
    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, int _clrIdx, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return "[E] Börsenterminal öffnen";
    }

    public override bool HasBlockActivationCommands(WorldBase _world, BlockValue _blockValue, int _clrIdx, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return true;
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, int _clrIdx, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return BlockHilfe.Befehl;
    }

    public override bool OnBlockActivated(string _commandName, WorldBase _world, int _cIdx, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        return Oeffnen(_player, BlockHilfe.Position(_blockPos));
    }

    public override bool OnBlockActivated(WorldBase _world, int _clrIdx, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        return Oeffnen(_player, BlockHilfe.Position(_blockPos));
    }

    static bool Oeffnen(EntityPlayerLocal spieler, string pos)
    {
        if (WirtschaftMod.Bank == null) return false;
        if (XUiC_WbBoerse.Oeffnen(spieler, pos)) return true;
        BlockHilfe.Zeigen(spieler, "Börse", Befehle.Uebersicht(WirtschaftMod.Bank, WirtschaftMod.Heute));
        return true;
    }
}
