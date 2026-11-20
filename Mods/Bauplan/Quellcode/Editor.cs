// Bauplan-Mod – Prefab-Editor-Funktionen im normalen Spiel.
// Das Spiel hat ein eigenes Auswahl-Werkzeug (BlockToolSelection) mit Strg+C (kopieren), Strg+V (einfügen, mit Vorschau)
// und Strg+Z (rückgängig). Diese Tasten fragt das Spiel nur im Prefab-Editor ab (PlayerMoveController.OnGUI prüft
// GameManager.IsEditMode()). Solange das Bauplan-Werkzeug in der Hand ist, rufen wir die Tastenabfrage selbst auf und
// melden dabei – nur für diesen einen Aufruf – "Editor-Modus". Sonst ändert sich am Spiel nichts.
// Geprüft gegen V 1.4: PlayerMoveController.OnGUI(), .gameManager, .entityPlayerLocal, .playerInput,
// GameManager.IsEditMode(), GetActiveBlockTool(), IBlockTool.CheckSpecialKeys(Event, PlayerActionsLocal),
// GUIWindowManager.IsInputActive(), IsModalWindowOpen(), Inventory.holdingItem, ItemClass.GetItemName(), Harmony 2 (HarmonyLib).
using System;
using HarmonyLib;
using UnityEngine;

namespace Bauplan
{
    public static class Editor
    {
        public const string Werkzeug = "bauplanWerkzeug";
        /// <summary>Größer 0, solange GameManager.IsEditMode() "true" melden soll.</summary>
        public static int Erzwingen;
        static bool fehlerGemeldet;

        public static bool HaeltWerkzeug(EntityPlayerLocal sp)
        {
            try
            {
                if (sp == null || sp.inventory == null) return false;
                var ic = sp.inventory.holdingItem;
                return ic != null && ic.GetItemName() == Werkzeug;
            }
            catch (Exception) { return false; }
        }

        public static void Fehler(string wo, Exception e)
        {
            if (fehlerGemeldet) return;
            fehlerGemeldet = true;
            Log.Warning("[Bauplan] " + wo + ": " + e.Message);
        }
    }

    [HarmonyPatch(typeof(GameManager), "IsEditMode")]
    public static class PatchIsEditMode
    {
        public static void Postfix(ref bool __result)
        {
            if (Editor.Erzwingen > 0) __result = true;
        }
    }

    [HarmonyPatch(typeof(PlayerMoveController), "OnGUI")]
    public static class PatchOnGUI
    {
        public static void Postfix(PlayerMoveController __instance)
        {
            if (Editor.Erzwingen > 0 || __instance == null) return;
            try
            {
                var gm = __instance.gameManager;
                if (gm == null || gm.IsEditMode()) return;          // im echten Editor macht das Spiel es selbst
                var sp = __instance.entityPlayerLocal;
                if (!Editor.HaeltWerkzeug(sp)) return;
                var ui = sp.PlayerUI;
                if (ui != null && ui.windowManager != null && (ui.windowManager.IsInputActive() || ui.windowManager.IsModalWindowOpen())) return;
                var werkzeug = gm.GetActiveBlockTool();
                var ev = Event.current;
                if (werkzeug == null || ev == null) return;
                Editor.Erzwingen++;
                try { werkzeug.CheckSpecialKeys(ev, __instance.playerInput); }
                finally { Editor.Erzwingen--; }
            }
            catch (Exception e) { Editor.Fehler("Editor-Tasten", e); }
        }
    }
}
