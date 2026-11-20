# Figuren – Anleitung (Version 0.2)

NPCs, die du aufstellst und ansprechen kannst (Typ 1: feste Dialoge aus Textdateien).

## Bauen
1. Spiel beenden.
2. In `Mods\Figuren` die Datei **BAUEN** doppelklicken, auf "FERTIG" warten.
3. Spiel starten.

## Im Spiel
- Kreativmenü: **Figur aufstellen** (sieht aus wie eine Schaufensterpuppe).
- Platzieren: Statt der Puppe erscheint ein NPC, der zu dir schaut. Das Einrichten-Fenster öffnet sich.
- **Einrichten:** Rolle aus der Liste wählen. Passt das Aussehen nicht, wird die Figur an derselben Stelle neu aufgestellt.
- **E** (Controller: Interaktionstaste) in 3 Meter Nähe, Blick auf die Figur: Gespräch. Beim Hinschauen erscheint kurz "[E] mit Name reden".
- Im Gespräch unten: **Einrichten / Entfernen** (Rolle ändern oder Figur abbauen).
- Figuren bewegen sich nicht und nehmen keinen Schaden.

## Rollen
- Textdateien in `Mods\Figuren\Rollen`, eine pro Charakter. Aufbau: `Rollen\_ANLEITUNG.txt`.
- Beispiele: Frank (Barkeeper), Adam (Waffenhändler), Churchill (Regierungsbeamter), Rodrigo (Bauunternehmer), Volkan (Söldnerboss).
- Nach Änderungen: im Einrichten-Fenster "Rollen neu laden".
- Aussehen: Joel, Jen, Bob, Hugh, Rekt (Modelle der Händler).

## Konsole (F1), auch als Notlösung
`npc rollen`, `npc laden`, `npc liste`, `npc setzen Frank`, `npc reden`, `npc einrichten`, `npc entfernen`

## Gespeichert
Welche Figur welche Rolle hat, steht in `Figuren.xml` im Spielstand-Ordner.

## Falls E nicht reagiert
F1-Konsole öffnen, vor eine Figur stellen und `npc test` eingeben. Das zeigt, ob das Spiel die Figur sieht (Abstand, Winkel). Mit `npc reden` öffnet sich das Gespräch auch ohne E.

## KI-Figuren (Typ 2) mit LM Studio
1. LM Studio öffnen, Modell laden (empfohlen: Gemma 4 12B QAT), im Bereich "Developer" den Server starten (Port 1234).
2. Eine Rolle mit Abschnitt `[ki]` bekommt freie Gespräche. Beispiele: Volkan und Churchill.
3. Im Spiel: E bei der Figur, unten Text eintippen, Enter oder "Senden". Die Antwort kann je nach Modell ein paar Sekunden dauern.
4. KI-Figuren haben ein blaues Sprechsymbol mit Namen über dem Kopf und einen Punkt auf Kompass und Karte.
5. "Gespräch neu beginnen" löscht das Gedächtnis dieser Figur. Gespräche werden im Spielstand gespeichert (`Figuren_Gespraeche.xml`).
6. Einstellungen (Adresse, Antwortlänge, Weltbeschreibung für alle Figuren): `Mods\Figuren\KI.txt`, danach F1: `npc ki`.


## Neu in 0.6.0: Grundrollen neu sortiert
- Grundrollen: Banker, Barkeeper, Bauunternehmer (neu), Bürgermeister, Söldner, Unternehmer (neu, mit Branche).
- Unternehmer: beim Einrichten Stadt und Branche wählen (Kfz-Werkstatt, Spedition, Treibstoff, Möbelhandel, IT, Pharma, Apotheke, Lebensmittel, Sicherheitsdienst, Landwirtschaft). Neue Branche: in Unternehmer.txt bei "Branchen:" anhängen und eine Zeile "@Branche Beschreibung" dazuschreiben.
- Waffenhändler ist jetzt eine feste Figur (Rico Stahl, Berlin). Bürger ist abgeschaltet (Datei mit #aus).
- Liste im Einrichten-Fenster: erst Grundrollen, dann feste Figuren, jeweils so beschriftet. "Neuer Name" würfelt bei Grundrollen einen neuen Namen.

## Neu in 0.5.0: Banker mit echten Daten und Krediten
- Immobilien-Angebote und Kreditdaten stehen jetzt ganz hinten in der Anweisung als "AKTUELLE SPIELDATEN" und gelten auch dann, wenn die Figur vorher etwas anderes gesagt hat.
- Rollen mit `Finanzen: ja` (Banker) bekommen aus der Wirtschaft-Mod: Kreditregeln, alle Firmen und Personen mit Nettoeinkommen, freier Rate und Kreditrahmen, laufende Kredite, dazu die Monatsraten zu jedem Immobilienangebot.
- Einigt man sich auf einen Kredit, erscheint im Gesprächsfenster "Kredit abschließen". Erst dieser Knopf zahlt aus (über die Wirtschaft-Mod, mit nochmaliger Regelprüfung). Der Banker merkt sich den Kredit als festen Fakt.
- Kürzere Antworten: die Obergrenze passt sich der Länge deiner Nachricht an (kurze Frage = kurze Antwort). Vorher wurde versehentlich immer eine "Denkreserve" von 1200 Tokens dazugerechnet.
- `npc prompt <Name>` schreibt die komplette Anweisung an das Modell in eine Datei im Spielstand, zum Nachprüfen.

## Neu in 0.4.0: Immobilien-Schild und neues Einrichten-Fenster
- Block **Immobilien-Schild** (Holzschild 1x1, im Kreativmenü unter Bauen): an die Wand setzen, Fenster öffnet sich. Bezeichnung, Stadt, Art (kleine/mittlere Wohnung, kleines Haus, Haus 2-3 Etagen, Gewerbe, Grundstück), Zustand (gesäubert oder nicht), Status (zu verkaufen, reserviert, verkauft), Preis und Beschreibung eintragen. "Richtwert einsetzen" rechnet Kaufpreis + Gebühr Bürgermeister + Säuberung aus deinen Richtwerten. Später mit E am Schild ändern; Schild abbauen = Angebot weg.
- Banker und Bürgermeister (`Immobilien: ja` in der Rollendatei) bekommen die Angebote ihrer Stadt als echte Daten und nennen genau diese Preise.
- `npc immo` oder `npc immo Springfield` zeigt die Angebote in der Konsole.
- Einrichten-Fenster (E an der Figur): 1. Rolle wählen, 2. Stadt mit < > einstellen, 3. Übernehmen. Ist die Figur schon diese Grundrolle, behält sie Namen und Gedächtnis und wechselt nur die Stadt.
- Städte stehen in KI.txt (`Städte: Springfield, Berlin, LZ, WS`), weitere einfach anhängen.

## Neu in 0.3.0: Natürliche Gespräche
- Neuer Aufbau der Anweisung an das Modell: A allgemeine Gesprächsregeln (Vorrang), B Rollenprofil, C Weltfakten, D Gedächtnis, E Situation (erstes Treffen oder bekannt, aus der Ferne).
- Die Regeln stehen in `Dialogregeln.txt` und sind frei änderbar (danach `npc ki`).
- Alte Zwangsregeln entfernt (Witz in jeder Antwort, Anekdoten, Aufträge anbieten). Antwortlänge passt sich der Eingabe an.
- Sampling: TopP und Wiederholung (repeat_penalty) in KI.txt. Abgeschnittene Antworten enden beim letzten ganzen Satz.
- Ausgabe wird bereinigt (keine *Regieanweisungen*, kein "Name:", keine Markdown-Reste).
- Fehler 400: LM Studio wird mit einfacherer Anfrage erneut gefragt; bleibt es dabei, steht der Grund von LM Studio in der Meldung.

## Neu in 0.2.6
- Das Leitungsfenster (Operator) öffnet jetzt mit **Pos1 (Home)** statt Pos1, denn Pos1 blendet im Spiel das HUD aus. Andere Taste: in KI.txt eine Zeile `Taste: Home` (oder z. B. `Taste: End`).

## Neu in 0.2.5: Vorlagen mit Stadt und Zufallsnamen
- Rollen mit `Vorlage: ja` (Banker, Bürgermeister, Waffenhändler, Barkeeper, Söldner, Bürger) erzeugen bei jedem Aufstellen eine eigene Figur mit Zufallsnamen aus `Namen:`, eigenem Gedächtnis und Chatverlauf.
- Stadt: `npc setzen Bürgermeister Springfield` oder im Einrichten-Fenster (E auf die Figur) den Eintrag "Bürgermeister (Springfield)" wählen. Zeilen mit `@Stadt ` im Abschnitt [ki] gelten nur dort (Stil, Preise).
- `Welt.txt`: ausführliche Weltgeschichte, die nur der Operator kennt. Nach Änderungen: F1, `npc ki`.
- Nur männliche Aussehen (Joel, Bob, Hugh, Rekt) in den Vorlagen.

## Neu in 0.2.4: Operator und Grundrollen
- Der "Erzähler" heißt jetzt **Operator**. Er simuliert Ereignisse: Du schilderst deine Lage und fragst "Was könnte jetzt passieren?", er entwirft Ereignisse mit Zeitpunkt, Auslöser und Wahlmöglichkeit und führt die Folgesequenz weiter. Alte Erzähler-Gespräche werden automatisch übernommen.
- Neue Rollen: Banker_Weiss (Banker und Makler), Buergermeister_Springfield, Buergermeister_Berlin, Waffenhaendler_Berlin, Waffenhaendler_LZ, Barkeeper, Soeldner, Buerger. Pro Stadt eine eigene Datei kopieren und Standort, Namen und Preise anpassen.
- Gemeinsame Regeln für alle (Währung Coins, Kaufablauf, Preise) stehen im Abschnitt [welt] von KI.txt.

## Neu in 0.2.3: Gedächtnis
- **20 Nachrichten** pro KI-Figur werden wörtlich gespeichert und mitgelesen. Mit **Älter / Neuer** blätterst du im Fenster durch.
- **Erinnerung:** Wird das Gespräch länger, verdichtet das Modell die älteren Nachrichten zu Stichpunkten: Beziehung zum Spieler (Vertrauen x/10), Über den Spieler, Personen, Gemeinsame Erlebnisse, Vereinbarungen und Offenes. Nur Nennenswertes bleibt.
- **Feste Fakten:** `/merke Wir haben zusammen die Tankstelle überfallen.` ins Eingabefeld schreiben. Wird nie gekürzt. `/vergiss 2` löscht Fakt 2.
- **Knopf Gedächtnis:** zeigt feste Fakten, Erinnerung und Anzahl der gespeicherten Nachrichten.
- **Gespräch neu beginnen** löscht Gespräch und Erinnerung, die festen Fakten bleiben.
- **Protokoll:** Alles steht zusätzlich lesbar im Spielstand-Ordner unter `Figuren_Chatprotokoll\<Name>.txt`.
- **Konsole:** `npc gedaechtnis Volkan`, `npc merke Volkan <Text>`, `npc vergiss Volkan <Nummer>`.
- **Rollen:** Im Abschnitt `[ki]` jetzt Felder pro Zeile (Wer, Herkunft, Persönlichkeit, Sprechweise, Humor, Haltung zum Spieler, Ziele, Geheimnisse, Beziehungen ...). Vorlage: `Rollen\_VORLAGE_KI.txt`.

## Neu in 0.7.0 (Tests Aussehen + Typ 3)
- `npc teile`: stellt alle Händler-Modelle kurz vor dir auf, analysiert sie (Einzelteile, Materialien, Skelett) und schreibt `Figuren_Teile.txt` in den Spielstand-Ordner.
- `npc farbe <Teil|alle> <Farbe>`, `npc teil <Teil> aus|an`, `npc größe 1.1` – an der nächsten Figur ausprobieren (gilt bis Neuladen).
- `npc schuss` / `npc wache an|aus` – Schießtest: Figur schießt auf Zombies (Ton, Schaden; keine Waffe in der Hand).
- Jen (Frauen-Modell): Barkeeper und Unternehmer (nur Apotheke/Pharma). Rollendatei: `Aussehen: ..., Jen`, `Frauennamen: ...`, optional `Frauen-Branchen: ...`.

## Neu in 0.8.0 (Rollenarten, Gefühle)
- Vier Arten im Einrichten-Fenster: **Grundrolle** (KI, Zufallsname, Stadt), **Feste Figur** (Klick-Dialog ohne KI: Bürger, Mitarbeiter, Überlebender), **Hauptrolle** (Gustavo, David, George Orwell), **Nebenrolle** (Volkan, Frank, Churchill).
- Einzelfiguren bekommen im Kopf `Art: Hauptrolle` oder `Art: Nebenrolle`.
- Feste Dialoge mit Abwechslung: `Text A || Text B || Text C` – bei jedem Gespräch zufällig eine Variante.
- KI-Figuren haben eine **Tagesstimmung** (wechselt jeden Spieltag) und sind persönlicher (neue Dialogregeln, Zeile `Privat:` in den Grundrollen).
- Waffenhändler ist wieder Grundrolle (Rico Stahl ist einer der Zufallsnamen). Adam und Rodrigo abgeschaltet (#aus).

## Neu in 0.8.1 (Test: Modelle aus khzmusik NPC Rogues)
- Vier Test-Aussehen: RogueBlonde, RogueCowboy, RogueYoung, RogueAged (nur Männer). Das Modellpaket liegt in `Resources/Rogues_KHz.unity3d`.
- `npc teile lang`: alle Modelle erscheinen vor dir und bleiben ca. 15 Sekunden stehen, dann Analyse in Figuren_Teile.txt.
- Modelle: khzmusik (CC0 für seinen Anteil; Mixamo-Fuse-Teile frei nutzbar in Spielen). Nur privat verwenden bzw. mit Quellenangabe weitergeben.

## Neu in 0.9.0 (28 Modelle, Zufall, Aussehen wählen)
- 28 Modelle: 5 Händler, 8 Rogues, 15 Civilians (khzmusik). Pakete in `Resources/`: Rogues_KHz.unity3d, CivilianMales_KHz.unity3d, CivilianFemales_KHz.unity3d.
- Neue Figur (Block) bekommt ein zufälliges Männer-Modell. Beim Übernehmen einer Grundrolle/festen Figur zufällig aus deren Liste.
- Einrichten-Fenster: Zeile **Aussehen** mit `<` `>` (Figur wechselt sofort, durch alle 28) und **Zufall** (aus der Liste der Rolle).
- Rollendatei: `Aussehen:` mit Modellen und Gruppen (Händler, Rogues, Zivil, Männer, Frauen, alle) – siehe Rollen/_ANLEITUNG.txt.
- `npc teile lang`: alle Modelle im Raster vor dir, ca. 30 Sekunden.

## Neu in 0.9.1 (Fehlerbehebung Modelle)
- Neue Modelle als ModelType "Standard" (wie Zombies/Banditen) statt "Npc" (nur Händler). Ersatz-Variante "…2", sonst Joel – eine Figur verschwindet nie mehr.
- Modelle, die nicht laden, werden für die Sitzung beim Zufall übersprungen; Fehler mit Details im Log und in Figuren_Teile.txt.
- Übernehmen behält ein von Hand gewähltes Aussehen (Pfeile/Zufall). Sonst würfelt eine Grundrolle/feste Figur aus ihrer Liste.

## Neu in 0.10.0 (Typ 3: Waffen)
- Neue feste Figur **Wachmann** (Klick-Dialog, Vorgabe AK + Wache). Söldner hat jetzt Vorgabe AK + Wache.
- Einrichten-Fenster: Zeilen **Waffe** (keine, Pistole, MP, AK, Schrotflinte) und **Haltung** (Friedlich, Wache, Feindlich) – wirken sofort, für jede Figur.
- Wache: schießt auf Zombies in Reichweite (Pistole 20 m, MP 22 m, AK 30 m, Schrotflinte 12 m). Feindlich: zuerst auf Spieler. Wände blockieren.
- Konsole: `npc waffe ak`, `npc haltung feindlich`, `npc wache an|aus` (nächste Figur).
- BAUEN.bat bindet zusätzlich UnityEngine.AnimationModule und PhysicsModule ein.

## Neu in 0.11.0
- Grundrolle **Söldner → Händler** (KI: Handel, Aufträge, Gerüchte). Neue **feste Figur Söldner** (Klick-Dialog, AK).
- Jede bewaffnete Figur wehrt sich automatisch gegen Zombies. **Nur Söldner** können feindlich werden: `npc f1` = alle Söldner feindlich, `npc f2` = freundlich.
- Schießen: Figur setzt Waffenhaltung/Zielen im Animator (WeaponClass, IsAiming, WeaponFire) – Wirkung hängt vom Modell ab.
- Waffe in der Hand nachjustieren: `npc lage x 2`, `npc lage dreh y 15`, `npc lage null` (gilt pro Waffe, Datei Waffenlage.txt).

## Neu in 0.11.1
- Kurzbefehle **nf1** (alle Söldner feindlich) und **nf2** (freundlich).
- Feindliche Söldner sind **verwundbar**: 150 Lebenspunkte, Kopftreffer doppelt. Tot = Todesanimation/Blut (je nach Modell), Leiche verschwindet nach 3 Minuten (ab 0.11.2), Figur ist aus dem Spielstand gelöscht.
- Söldner hat keine Standardwaffe mehr – Waffe im Einrichten-Fenster wählen.

## Neu in 0.12.0 (Sprachausgabe, Piper)
- Feste Dialoge (Bürger, Mitarbeiter, Überlebender, Wachmann, Söldner …) werden gesprochen, wenn Audiodateien da sind. Frauenmodelle = Frauenstimme.
- Einrichtung: Inhalt von piper_windows_amd64.zip nach `Stimmen\` (→ `Stimmen\piper\piper.exe`), Stimmen `de_DE-thorsten-medium` (Mann) und `de_DE-kerstin-low` (Frau, optional) je .onnx + .onnx.json nach `Stimmen\`.
- Spiel einmal starten (schreibt die Satzliste), dann **STIMMEN.bat** doppelklicken. Neue Sätze: `npc stimmen`, dann wieder STIMMEN.bat.
- Sätze mit {Name}/{Stadt} bleiben stumm.

## Neu in 0.13.0 (Soldaten)
- 9 neue Modelle (jetzt 37): **Soldat1–Soldat8** (Menschen-Soldaten aus „Guppycur's Soldiers“, Datei `Resources\Soldaten_Guppy.unity3d`) und **ZombieSoldat** (Soldaten-Zombie aus dem Spiel, steht friedlich).
- Das Soldaten-Paket ist von 2019. Die Mod ersetzt beim Erzeugen kaputte Shader. Lädt ein Modell trotzdem nicht, erscheint wie bisher Joel und es steht im Log.
- Soldaten kommen nicht in den allgemeinen Zufall. Im Einrichten-Fenster hinblättern oder in der Rolle `Aussehen: Soldaten` schreiben.
- Die Soldaten tragen ihr Gewehr fest am Körper; eine zusätzlich gewählte Waffe kann doppelt aussehen.

## Neu in 0.14.0 (Körpersteuerung: Waffe, Treffer, Tod)
- Neues Bauteil `Quellcode/Koerper.cs`: stellt die Knochen nach der Animation selbst. Die Animator-Controller der Modelle reagieren nicht zuverlässig auf Waffen-Befehle.
- **Waffe:** Figur dreht sich zum Ziel, hebt die Waffe (beide Arme, Oberkörper dreht und neigt mit), schießt erst, wenn die Waffe oben ist und zeigt. Rückstoß, Mündungsfeuer (Partikel + kurzer Lichtblitz). Pistole einhändig mit Stützhand, MP/AK/Schrotflinte beidhändig. 3 s ohne Ziel: Waffe runter, alte Blickrichtung.
- **Treffer:** Kopf, Arm links/rechts, Bein links/rechts, Rumpf zucken unterschiedlich; starke Treffer = kurz aus dem Gleichgewicht. Gilt für alle Figuren (unverwundbare zucken nur).
- **Tod (Söldner):** kein Spiel-Tod mehr (der hat mit 1000 Schaden die Zombie-Zerstückelung ausgelöst). Figur fällt um, bleibt liegen, behält ihr Aussehen, Blut, Leiche nach 3 Minuten weg, nicht ansprechbar, nicht gespeichert.
- Schaden aus der Umgebung (Ersticken, Feuer, Fallen) zählt nicht mehr; kleine Treffer werden nicht mehr auf 5 aufgerundet.
- **Treffer-Zonen:** eigene Physik-Körper `figurMixamo` (khzmusik-Modelle) und `figurGuppy` (Soldaten), Datei `Config/physicsbodies.xml`. Vorher fehlten die Zonen (Log: "path not found").
- **Leben:** Figuren schauen den Spieler bis 7 m an (Kopf/Hals), leichtes Atmen.
- **Aufstellen:** braucht 2 Blöcke Höhe, sonst Hinweis und keine Figur.
- Log-Fehler behoben: Modelle mit "2" am Namensende (ZivilJung2, Soldat2 …) wurden fälschlich als "Variante 2" gemeldet.
- Konsole: `npc körper` zeigt, welche Knochen und welche Waffe bei der nächsten Figur gefunden wurden.

## Neu in 0.14.1 / 0.15.0
- Söldner sind im Einrichten-Fenster nicht mehr auf feindlich stellbar. `nf1` = Söldner im Umkreis von 400 Blöcken feindlich, `nf2` = alle freundlich.
- **Stimmen:** Jede Figur hat eine leicht eigene Tonhöhe (fest nach Rolle und Platz).
- **Typ 4, Lauf-Test:** `npc lauf` schickt die nächste Figur (bis 15 m) dorthin, wo du hinschaust. `npc lauf schnell` rennt, `npc lauf status` zeigt Ergebnis und Gang, `npc lauf stop`, `npc lauf zurück`.
  Die Mod probiert drei Wege (Wegfindung des Spiels, Bewegungshilfe, selbst gesteuert) und nimmt den ersten, der funktioniert.
  Zeigt das Modell keine eigene Gehanimation, bewegt die Mod Beine und Arme selbst. Beim Neuladen steht die Figur wieder an ihrem Platz.

## Neu in 0.16.0 (Umherlaufen)
- Einrichten-Fenster, Zeile **Bewegung**: Steht fest / Umher 10 / 25 / 50 / 100 Blöcke. Gilt für jede Figur, gespeichert.
- Tagsüber: warten (6-20 s), dann ein Stück (4-20 m) zu einem zufälligen Punkt im Umkreis um ihren Platz, auf etwa gleicher Höhe (nicht aufs Dach). Nachts zurück an den Platz.
- Bewaffnete Figuren bleiben stehen und kämpfen, sobald ein Ziel da ist. Angesprochene Figuren drehen sich zum Spieler und bleiben 40 s stehen.
- Der Söldner-Status (freundlich/feindlich) steht jetzt neben der Waffe.
- Gang weicher: Füße rollen ab, Becken federt und kippt, Oberkörper dreht gegen, Ellbogen leicht gebeugt.

## Neu in 0.17.0
- **Kein Fliegen/Klettern mehr:** Figuren können nicht mehr an Bäumen/Wänden hochklettern. Hängt eine Figur länger als 2 s in der Luft oder ist sie mehr als 3 m über (5 m unter) ihrem Platz, wird sie zurück an ihren Platz gestellt (Log: "hing in der Luft").
- Umherlaufen wählt nur noch flachen, freien Boden auf ungefähr gleicher Höhe (nicht auf Schutthaufen, Büsche, Bäume). Ein Lauf bricht ab, sobald die Figur höher als 1,4 m über Start/Ziel steigt.
- **Figuren sterben, wenn man lange genug schießt:** normale Figuren 400 Lebenspunkte, feindliche Söldner 150. Haupt- und Nebenrollen bleiben unverwundbar. Tote Figuren fallen um, Leiche nach 3 Minuten weg, Platz wird gelöscht.

## Neu in 0.17.1
- Eigene Trefferzonen (figurMixamo/figurGuppy aus 0.14) wieder entfernt: seit 0.14 kamen keine Treffer mehr an, und genau diese Modelle sind geflogen. Trefferteil wird wieder über den nächsten Knochen bestimmt.
- Umherlaufen benutzt die Bewegungshilfe des Spiels nicht mehr (die hat Figuren springen/klettern lassen): nur noch Wegfindung oder selbst gesteuert.
- Schadensleisten-Fix aus 0.16.2 entfernt (hat vermutlich die Controller-Bedienung in Menüs gestört).

## Neu in 0.17.2 (gründliche Prüfung)
- Treffer: Viele Modelle (z. B. die khz-Modelle) hatten keine Treffer-Kollider - Kugeln flogen durch. Die Mod legt jetzt selbst Trefferzonen an (Kopf, Rumpf, Arme, Beine). Im Log steht pro Figur "Trefferzonen #...", bei jedem Treffer "Schaden an #...".
- Sprung-Haltung ("fliegen"): Der Zombie-Animator des Spiels ließ den Sprung-Schalter bei den khz-Modellen eingeschaltet. Steht die Figur am Boden, wird er jetzt zurückgesetzt (im Log "Sprung-Haltung #...").
- Körper-Kollider stoßen nicht mehr gegen die eigene Stand-Kollision (konnte die Figur anheben).
- Controller: Die Fenster schlossen jeden Frame die Block-Lebensanzeige - das störte die Controller-Steuerung. Jetzt nur noch einmal beim Öffnen.

## Neu in 0.18.0 (stabile Bewegung, Figuren können sterben)
- **Sprung-Haltung behoben:** Die khz-Modelle kommen aus dem Sprung nur heraus, wenn der Animator-Schalter "IsOnGround" an ist – das Spiel setzt ihn aber nur bei Zombies. Die Mod setzt ihn jetzt jeden Frame (dazu "CanFall" aus, "JumpLandResponse" 0, "IsClimbing" aus). Hängt eine Figur trotzdem 1,5 s im Sprung, wird sie in den Grundzustand überblendet (Log "Sprung-Zustand #...").
- **Figuren springen nicht mehr:** Die Bewegungshilfe des Spiels sprang über Hindernisse und über andere Figuren (sah aus wie Fliegen). Sprünge sind gesperrt (Log "Sprung gesperrt #..."), nur Schwimmen bleibt erlaubt. Kleine Stufen gehen sie normal hoch.
- **Laufen:** Weg 2 rechnet die Bewegungshilfe nicht mehr doppelt (früher zusätzlich jeden Frame). Weg 3 setzt das Tempo nur noch bei Änderung (früher jeden Frame – das löschte den Schwung, ruckelte und hing von den FPS ab).
- **Bodenhaftung:** gemessen gegen den Boden direkt unter der Figur, nicht mehr gegen die Höhe ihres Platzes. Schwebt sie, wird sie auf den Boden darunter gestellt (kein Teleport mehr an den Platz). Nur wenn sie ohne Boden fällt oder über 12 m unter ihrem Platz ist, kommt sie zurück an den Platz. Jede Korrektur steht im Log.
- **Leine:** Ist eine umherlaufende Figur mehr als Umkreis + 5 Blöcke vom Platz weg, geht sie zu Fuß zurück.
- **Zielpunkte:** Andere Figuren, Spieler und Fahrzeuge zählen beim Suchen eines Ziels nicht mehr als Boden.
- **Pose-Schutz:** Knochen, die die Animation in einem Frame nicht neu setzt, werden vor den Körper-Korrekturen zurückgestellt – so summiert sich über Stunden nichts auf.
- **Kampf:** Hat eine bewaffnete Figur ein Ziel, bleibt sie stehen (laufen und zielen gleichzeitig hat sie hin und her gedreht).
- **Sterben:** Figuren aus einer Vorlage (Wachmann, Söldner, Bürger, Banker ...) galten fälschlich als Nebenrolle und waren unverwundbar. Jetzt sterben sie nach ca. 10 Rumpftreffern (Kopftreffer zählen doppelt). Sie sterben nur durch Spieler und Söldner – Zombies lassen sie nur zucken (sonst wären Banker, Händler usw. nach dem ersten Zombie-Angriff weg). Feindliche Söldner sterben wie bisher durch alles. Haupt- und Nebenrollen (Gustavo, David, Orwell, Volkan, Frank, Churchill ...) bleiben unverwundbar. Achtung: Eine gestorbene Figur ist aus dem Spielstand weg.
- `npc körper` zeigt jetzt auch: Boden-Schalter, wie oft aus dem Sprung geholt, Pose-Schutz, gesperrte Sprünge.

## Neu in 0.18.1 (wer wodurch sterben kann)
- **Zombies töten keine Figur mehr** – auch keine Söldner. Zombies und Tiere lassen Figuren nur zucken.
- **Spieler** kann alle Figuren töten außer Haupt- und Nebenrollen (die bleiben immer unverwundbar).
- **Starke Explosionen** (ab 100 Schaden: Granate, Rakete, Mine, Dynamit …) können jede normale Figur töten und zählen voll.
- **Wachmann gegen Söldner:** Nur diese beiden kämpfen gegeneinander und können sich gegenseitig töten. Feindliche Söldner (nf1) schießen auf Wachmänner und freundliche Söldner, Wachmänner und freundliche Söldner schießen auf feindliche Söldner. Alle anderen Figuren schießen nie aufeinander.
- Im Log steht bei jedem Treffer, ob er zählt oder warum nicht (z. B. "nur Zucken: Zombies/Tiere töten keine Figuren").

## Neu in 0.18.2 (Rollen: Bürgermeister, Bankfilialleiter, Bauunternehmer, Unternehmer)
- **Neue KI-Texte** für Bürgermeister, Bankfilialleiter (Datei bleibt `Banker.txt`, die Rolle heißt jetzt Bankfilialleiter) und Bauunternehmer.
- **Unternehmer ist jetzt universell.** Im Einrichten-Fenster (E auf die Figur, Rolle Unternehmer) gibt es drei Angaben: **Firma** (Textfeld), **Branche** (12 Knöpfe: Logistik, Industrie, Kfz, Technik, Immobilien, Energie, Telekom, Agrar, Sicherheit, Sport, Handel, Bau) und **Größe** (Klein, Mittel, Groß). Dazu wie immer die Stadt. Danach **Übernehmen**.
- Die drei Angaben gehen in den KI-Text: Der Unternehmer ist Geschäftsführer der Firma, kennt das Fachgebiet seiner Branche und verhält sich der Größe entsprechend (klein: knappes Geld, groß: viel Einfluss). Persönlichkeit, Produkte und Aufträge entstehen von selbst im Gespräch.
- Firma, Branche und Größe werden im Spielstand gespeichert. Schon aufgestellte Unternehmer bleiben erhalten (alte Branchen wie Spedition werden auf die neuen abgebildet, die Firma ist dann leer und er denkt sich einen Namen aus).
- Neue Branche ergänzen: in `Rollen/Unternehmer.txt` in der Zeile `Branchen:` als `Schlüssel=Anzeigename` anhängen (bis 16 Knöpfe) und eine Zeile `@Schlüssel Branche: ...` dazuschreiben.
- Währung in den Rollentexten ist Coins.

## Neu in 0.19.0: Game Operator (Spielleiter mit Ereignissen)
Der Operator im Leitungsfenster (Taste Pos1, ganz vorne in der Liste) ist jetzt ein richtiger Spielleiter. Er kennt die Weltgeschichte (Welt.txt), alle KI-Figuren mit Stadt und Firma, die Firmen, Banken und Regierungen aus der Wirtschaft-Mod und alle Ereignisse.

**Knöpfe unten (nur beim Operator):**
- **Szenarien**: Der Operator schlägt mindestens drei deutlich unterschiedliche Ereignisse vor, jeweils mit Titel, Beschreibung, Grund, Beteiligten, Auswirkungen, Wahrscheinlichkeit und Folgen für dich. Steht Text im Eingabefeld, ist das das Thema (zum Beispiel: Telekom 1 in Springfield).
- **Welt entwickeln**: zwei bis drei Entwicklungen, die ohne dich in der Welt passieren könnten.
- **Ereignisse**: Liste aller Ereignisse mit Details. Mit `<` und `>` wählen, mit **Filter** die Ansicht wechseln (Offen, Vorschlag, In Planung, Freigegeben, Aktiv, Abgeschlossen, Zurückgestellt, Abgebrochen, Ketten, Wünsche).
- **Hilfe**: alle Befehle.

**Ablauf mit Status:**
- Vorschlag: nur eine Idee.
- **Planen**: Ereignis auswählen; der Operator arbeitet daraus eine Ereigniskette in Schritten aus. Willst du etwas ändern, schreib es einfach (in der Ereignis-Ansicht bezieht sich dein Text auf das gewählte Ereignis). Der angepasste Plan wird gespeichert.
- **Freigeben**: soll eintreten, ist aber noch nicht passiert.
- **Aktiv**: läuft jetzt in der Welt (ab hier Tatsache).
- **Fertig**: ist im Spiel wirklich eingetreten (zweimal drücken). Was genau passiert ist, vorher ins Eingabefeld schreiben. Abgeschlossenes gehört zur Geschichte und wird nie mehr geändert.
- **Später** (zurückstellen) und **Abbruch** (tritt nicht ein).
- **Folgen**: wie geht es nach dem gewählten Ereignis weiter. Die neuen Vorschläge hängen in derselben Ereigniskette.

**Wichtig:** Nur Aktiv und Abgeschlossen gelten als Tatsachen. Pläne behauptet der Operator nie als geschehen. Die anderen KI-Figuren (Bürgermeister, Barkeeper ...) erfahren nur die Tatsachen, keine Pläne.

**Befehle im Eingabefeld (beim Operator):** /szenarien Thema, /welt, /folgen 12, /plan 12, /freigeben 12, /aktiv 12, /fertig 12 Ergebnis, /spaeter 12, /abbruch 12, /info 12, /wunsch Text, /wunschweg 3, /hilfe. Weiterhin /merke Text (fester Weltfakt).

**Wünsche:** Mit /wunsch (oder einfach im Gespräch, zum Beispiel "Ich möchte, dass Springfield stärker wächst") merkt sich der Operator langfristige Ziele und berücksichtigt sie bei Simulationen, wenn sie plausibel sind.

**Gespeichert** wird alles im Spielstand in `Figuren_Weltereignisse.xml` (mit .bak). "Gespräch neu beginnen" löscht die Ereignisse nicht.

**LM Studio:** Der Operator bekommt viel Weltwissen mit. In LM Studio die Kontextlänge auf mindestens 16384 stellen. Für den Operator wartet das Spiel bis zu 5 Minuten auf die Antwort.
