// Bauplan-Mod – das Werkzeug in der Hand.
// items.xml: Action0 und Action1 mit Class "Bauplan, Bauplan" -> das Spiel sucht ItemActionBauplan in Bauplan.dll (ohne Namespace).
// Geprüft gegen V 1.4: ItemAction (abstract ExecuteAction(ItemActionData, bool), OnHoldingUpdate, StopHolding, ActionIndex),
// ItemActionData.invData.holdingEntity.
using System;
using UnityEngine;
using Bauplan;

public class ItemActionBauplan : ItemAction
{
    static float letzterKlick;

    static EntityPlayerLocal Spieler(ItemActionData d)
    {
        return d == null || d.invData == null ? null : d.invData.holdingEntity as EntityPlayerLocal;
    }

    public override void ExecuteAction(ItemActionData _actionData, bool _bReleased)
    {
        if (!_bReleased) return;
        var sp = Spieler(_actionData);
        if (sp == null) return;
        float jetzt = Time.time;
        if (jetzt - letzterKlick < 0.25f) return;
        letzterKlick = jetzt;
        try
        {
            bool links = ActionIndex == 0;
            Vector3i p;
            bool imBlick = Plan.Blick(sp, out p, false);

            if (Plan.Modus == Modus.Platzieren)
            {
                if (links) { Plan.EinfuegenKlick(sp); return; }
                if (!imBlick) { XUiC_Bauplan.Oeffnen(sp); return; }
                Plan.Drehen();
                Plan.Melden(sp, "Gedreht: " + Plan.Drehung * 90 + "°");
                return;
            }

            // Markieren
            if (!links && !imBlick) { XUiC_Bauplan.Oeffnen(sp); return; }
            Plan.SetzeEcke(sp, links ? 1 : 2);
        }
        catch (Exception e) { Log.Error("[Bauplan] Werkzeug: " + e); }
    }

    public override void OnHoldingUpdate(ItemActionData _actionData)
    {
        base.OnHoldingUpdate(_actionData);
        if (ActionIndex != 0 || Plan.Modus != Modus.Platzieren) return;
        try { Plan.ZielAktualisieren(Spieler(_actionData)); } catch (Exception) { }
    }

    public override void StartHolding(ItemActionData _data)
    {
        base.StartHolding(_data);
        if (ActionIndex != 0) return;
        try { Vorschau.Markierung(); } catch (Exception) { }
    }

    public override void StopHolding(ItemActionData _data)
    {
        base.StopHolding(_data);
        if (ActionIndex != 0) return;
        try { Vorschau.Weg(); } catch (Exception) { }
    }
}
