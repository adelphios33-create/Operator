// Bauplan-Mod – Abriss-Werkzeug: angeklickter Block verschwindet sofort (jeder Block, auch Erde und Stein).
// Linksklick = Block weg, Shift + Linksklick = 3×3×3 um den Block weg, Rechtsklick = letzten Abriss zurückholen.
// Volle Kisten/Behälter erst beim zweiten Klick (Inhalt geht verloren). Unterste Weltschicht (y = 0) bleibt.
// Geprüft gegen V 1.4: WorldBase.SetBlocksRPC(List<BlockChangeInfo>), BlockChangeInfo(int, Vector3i, BlockValue, sbyte [, long]),
// World.GetBlock/GetDensity/GetTileEntity, ChunkCluster.GetTextureFull, BlockValue.Air/ischild/Block, Block.multiBlockPos.GetParentPos,
// TileEntityLootContainer.IsEmpty(), MarchingCubes.DensityAir, EntityPlayerLocal.playerInput.Run (InControl).
using System;
using System.Collections.Generic;
using UnityEngine;
using Bauplan;

namespace Bauplan
{
    public static class Abriss
    {
        public const string Werkzeug = "bauplanAbriss";
        const int MaxRueck = 2000;

        struct Alt { public Vector3i Pos; public BlockValue Block; public sbyte Dichte; public long Textur; }

        static readonly List<List<Alt>> rueck = new List<List<Alt>>();
        static Vector3i bestaetigt;
        static float bestaetigtBis;

        static World Welt { get { return GameManager.Instance == null ? null : GameManager.Instance.World; } }

        public static void Leeren() { rueck.Clear(); bestaetigtBis = 0f; }

        static bool VolleKiste(World w, Vector3i p)
        {
            try
            {
                var te = w.GetTileEntity(p) as TileEntityLootContainer;
                return te != null && !te.IsEmpty();
            }
            catch (Exception) { return false; }
        }

        static Vector3i Eltern(World w, Vector3i p, BlockValue bv)
        {
            try
            {
                if (bv.ischild && bv.Block != null && bv.Block.multiBlockPos != null) return bv.Block.multiBlockPos.GetParentPos(p, bv);
            }
            catch (Exception) { }
            return p;
        }

        public static void Klick(EntityPlayerLocal sp, bool gross)
        {
            var w = Welt;
            if (w == null || sp == null) return;
            Vector3i ziel;
            if (!Plan.Blick(sp, out ziel, false)) { Plan.Melden(sp, "Kein Block im Blick."); return; }

            var liste = new List<Vector3i>();
            int r = gross ? 1 : 0;
            for (int x = -r; x <= r; x++)
                for (int y = -r; y <= r; y++)
                    for (int z = -r; z <= r; z++)
                        liste.Add(new Vector3i(ziel.x + x, ziel.y + y, ziel.z + z));

            // Volle Kisten: erst nach Bestätigung
            foreach (var p in liste)
            {
                if (!VolleKiste(w, p)) continue;
                float jetzt = Time.time;
                if (jetzt > bestaetigtBis || bestaetigt.x != ziel.x || bestaetigt.y != ziel.y || bestaetigt.z != ziel.z)
                {
                    bestaetigt = ziel;
                    bestaetigtBis = jetzt + 3f;
                    Plan.Melden(sp, "Hier ist ein Behälter mit Inhalt. Nochmal klicken zum Abreißen – der Inhalt geht verloren.");
                    return;
                }
                break;
            }
            bestaetigtBis = 0f;

            var aenderungen = new List<BlockChangeInfo>();
            var gemerkt = new List<Alt>();
            var erledigt = new HashSet<string>();
            foreach (var roh in liste)
            {
                if (roh.y <= 0 || roh.y > 254) continue;
                var bv = w.GetBlock(roh);
                if (bv.isair) continue;
                var p = Eltern(w, roh, bv);
                string k = p.x + "," + p.y + "," + p.z;
                if (!erledigt.Add(k)) continue;
                if (p.x != roh.x || p.y != roh.y || p.z != roh.z) bv = w.GetBlock(p);
                if (bv.isair) continue;
                long tex = 0;
                try { tex = w.ChunkCache.GetTextureFull(p); } catch (Exception) { }
                gemerkt.Add(new Alt { Pos = p, Block = bv, Dichte = w.GetDensity(0, p), Textur = tex });
                aenderungen.Add(new BlockChangeInfo(0, p, BlockValue.Air, MarchingCubes.DensityAir));
            }
            if (aenderungen.Count == 0) { Plan.Melden(sp, "Da ist nichts zum Abreißen."); return; }
            try
            {
                w.SetBlocksRPC(aenderungen);
                rueck.Add(gemerkt);
                int summe = 0;
                foreach (var g in rueck) summe += g.Count;
                while (rueck.Count > 1 && summe > MaxRueck) { summe -= rueck[0].Count; rueck.RemoveAt(0); }
                if (gross) Plan.Melden(sp, aenderungen.Count + " Blöcke abgerissen. Rechtsklick holt sie zurück.");
            }
            catch (Exception e) { Log.Error("[Bauplan] Abriss: " + e); Plan.Melden(sp, "Abreißen fehlgeschlagen: " + e.Message); }
        }

        public static void Zurueck(EntityPlayerLocal sp)
        {
            var w = Welt;
            if (w == null) return;
            if (rueck.Count == 0) { Plan.Melden(sp, "Nichts zum Zurückholen."); return; }
            var g = rueck[rueck.Count - 1];
            rueck.RemoveAt(rueck.Count - 1);
            var aenderungen = new List<BlockChangeInfo>();
            foreach (var a in g) aenderungen.Add(new BlockChangeInfo(0, a.Pos, a.Block, a.Dichte, a.Textur));
            try
            {
                w.SetBlocksRPC(aenderungen);
                Plan.Melden(sp, g.Count + " Block/Blöcke zurückgeholt" + (rueck.Count > 0 ? " (noch " + rueck.Count + " Schritte)." : ".") + " Kisteninhalte kommen nicht zurück.");
            }
            catch (Exception e) { Plan.Melden(sp, "Zurückholen fehlgeschlagen: " + e.Message); }
        }
    }
}

public class ItemActionAbriss : ItemAction
{
    static float letzterKlick;

    public override void ExecuteAction(ItemActionData _actionData, bool _bReleased)
    {
        if (!_bReleased) return;
        var sp = _actionData == null || _actionData.invData == null ? null : _actionData.invData.holdingEntity as EntityPlayerLocal;
        if (sp == null) return;
        float jetzt = Time.time;
        if (jetzt - letzterKlick < 0.12f) return;
        letzterKlick = jetzt;
        try
        {
            if (ActionIndex != 0) { Abriss.Zurueck(sp); return; }
            bool gross = false;
            try { gross = sp.playerInput != null && sp.playerInput.Run.IsPressed; } catch (Exception) { }
            Abriss.Klick(sp, gross);
        }
        catch (Exception e) { Log.Error("[Bauplan] Abriss-Werkzeug: " + e); }
    }
}
