# Bauplan – Gebäude kopieren und einfügen

## Bauen und starten
1. Spiel beenden.
2. `BAUEN.bat` in diesem Ordner doppelklicken. Bei Fehlern: `Baufehler.txt` an Claude schicken.
3. Spiel starten. Der Mod braucht „0_TFP_Harmony“ (ist bei 7 Days to Die dabei).

## Werkzeug holen
- F1: `giveself bauplanWerkzeug`
- oder craften: 1 Holz (Handwerk)
- oder im Kreativmenü suchen: „Bauplan“

## 1. Gebäude kopieren
1. Werkzeug in die Hand.
2. **Linksklick** auf einen Block = Ecke 1, **Rechtsklick** auf einen Block = Ecke 2. Die beiden Blöcke gehören mit zur Fläche. Der blaue Rahmen zeigt die Markierung.
   Tipp: Ecke 1 unten an einer Ecke im Boden (Fundament), Ecke 2 an der gegenüberliegenden oberen Ecke (Dach).
3. **Rechtsklick in die Luft** öffnet das Fenster. Dort zeigt die Zeile oben Ecken, Grundfläche und Höhe.
4. Bei Bedarf mit „+1 Block nach unten/oben“ die Höhe erweitern.
5. Namen eintragen, **Kopieren und speichern**. Gibt es den Namen schon, nochmal drücken = überschreiben.

Gespeichert wird als normales Prefab (wie im Prefab-Editor) im Ordner `LocalPrefabs`. Kisten mit Inhalt, Schildtexte und Anstrich kommen mit.
Größte Kante: 256 Blöcke. Die ganze Fläche muss geladen sein (nah genug stehen).

## 2. Gebäude einfügen
1. Im Fenster unten links ein Gebäude anklicken. „Zeigen“ schaltet zwischen Alle, Eigene und Spiel-Gebäude (alle POIs des Spiels). Mit „Suche“ filtern.
2. Rechts: **Drehen** (90°-Schritte), **Fläche freiräumen** (AN = Luftblöcke werden mit eingefügt, Bäume und Erde im Rahmen verschwinden), **Keller einsenken** (nur Spiel-Gebäude: Keller kommt unter die Erde, wie bei der Welterzeugung).
3. **Platzieren (Werkzeug)**: Fenster schließt sich. Werkzeug auf den Boden richten, der grüne Rahmen zeigt, wo das Gebäude hinkommt (mittig um den Zielpunkt).
4. **Linksklick** = Einfügen-Abfrage, **nochmal Linksklick** innerhalb von 5 Sekunden = einfügen. **Rechtsklick** = drehen.
5. **Rechtsklick in die Luft** = zurück ins Fenster. „Platz. beenden“ = wieder Ecken markieren.

**Rückgängig:** Vor jedem Einfügen merkt sich der Mod den Bereich. „Rückgängig“ (oder `bau rueckgaengig`) stellt ihn wieder her – nur das letzte Einfügen, und nur bis zum Verlassen der Welt.

Eigene Kopien lassen sich im Fenster löschen (zweimal drücken). Spiel-Gebäude nicht.

## Wie im Prefab-Editor (Strg-Tasten)
Solange das Bauplan-Werkzeug in der Hand ist, gelten die Tasten des Prefab-Editors – auch im normalen Spiel:
- Der Rahmen ist der originale Auswahlrahmen des Editors. Ecken setzt du mit Links-/Rechtsklick.
- **Strg+C** kopiert die Fläche im Rahmen in die Zwischenablage.
- **Strg+V** fügt die Zwischenablage ein. Passt der Rahmen nicht zur Größe, stellt das erste Strg+V den Rahmen auf die Größe der Zwischenablage, das zweite fügt ein.
- **Strg+Z** macht ein Einfügen mit Strg+V rückgängig.
- Beim Speichern und beim Platzieren legt der Mod das Gebäude automatisch in die Zwischenablage. Im Fenster legt „Zwischenablage“ das ausgewählte Gebäude (mit Drehung) hinein.
Ohne Werkzeug in der Hand ist alles wie im normalen Spiel.

## Abriss-Werkzeug (schnell abbauen)
Holen: `giveself bauplanAbriss` oder craften (1 Holz). Rote Spitzhacke.
- **Linksklick** auf einen Block = Block ist sofort weg. Egal welcher: Holz, Beton, Stahl, Erde, Stein, Pflanzen, Möbel. Keine Ausbeute, kostet nichts.
- **Shift + Linksklick** = 3×3×3 Blöcke um den angeklickten Block auf einmal.
- **Rechtsklick** = den letzten Abriss zurückholen (mehrmals = weitere Schritte zurück, bis ca. 2000 Blöcke, bis zum Verlassen der Welt). Auch per F1: `bau abrissrueck`.
- Kisten/Behälter mit Inhalt: erster Klick warnt, zweiter Klick innerhalb von 3 Sekunden reißt ab – **der Inhalt ist dann weg** und kommt auch beim Zurückholen nicht wieder.
- Große Blöcke (Betten, Autos, Türen …) gehen als Ganzes weg. Die unterste Weltschicht bleibt.
- Reichweite 100 Blöcke.

## Befehle (F1)
`bau` (Fenster), `bau ecke1`, `bau ecke2`, `bau kopieren <Name>`, `bau liste [Suche]`, `bau waehlen <Name>`, `bau platzieren`, `bau drehen`, `bau abbrechen`, `bau rueckgaengig`, `bau abrissrueck`, `bau hilfe`.

## Hinweise
- Einfügen ist kostenlos, wie im Kreativmodus.
- Eingefügte Spiel-Gebäude bekommen keine Schläfer-Zombies und keine Quests; es sind nur die Blöcke.
- Große Gebäude (über 100 Blöcke) können kurz ruckeln.

## Neu in 1.3: Vorschaubild
Rechts im Bauplan-Fenster steht jetzt ein Bild des gewählten Gebäudes. Spiel-Gebäude bringen eins mit (z. B. `Data/Prefabs/POIs/hospital_01.jpg`). Eigene Kopien haben meist keins. Legst du ein Bild mit gleichem Namen als `.jpg` oder `.png` neben die gespeicherte Kopie, wird es ebenfalls angezeigt.

## Neu in 1.4: Ohne Zombies einfügen
Knopf **„Zombies: AUS (keine)“** (Standard): Beim Einfügen werden die Schläfer-Zonen des Gebäudes und ihre Auslöser entfernt, im Krankenhaus wacht also niemand auf. Zum Umschalten draufdrücken, dann „Zombies: AN (wie im Spiel)“. Nur das Einfügen ist betroffen, die Gebäudedatei bleibt unverändert. Zombies, die von außen herumlaufen (normale Spawns, Horden), verhindert das nicht.

## 1.4.1: Erde rutscht nicht mehr nach
Spiel-Gebäude enthalten „terrainFiller“, Platzhalter für Gelände. Beim Einfügen werden sie jetzt durch den Boden ersetzt, der vorher da war, oder durch Erde, wenn dort nichts war. Für schon eingefügte Gebäude: das Gebäude mit dem Werkzeug markieren und im Fenster **„Boden reparieren“** drücken, oder `bau boden` in die Konsole eingeben. Direkt nach einem Einfügen geht es auch ohne Markierung.

## 1.4.2: Grube auffüllen
Neuer Knopf **Grube auffüllen** (Konsole: `bau auffuellen`). Markiere eine Ecke unten in der Grube und die gegenüberliegende Ecke oben auf der gewünschten Bodenhöhe (z. B. Straße). Zweimal drücken: alle Luftlöcher und Platzhalter im Rahmen werden bis zur oberen Ecke mit Erde gefüllt. Gebäude- und Restblöcke bleiben stehen. Rückgängig geht.

## 1.4.3: Asphalt auffüllen
Der Knopf heißt jetzt **Auffüllen: Erde**, daneben **Auffüllen: Asphalt** (Konsole: `bau asphalt`). Asphalt füllt Löcher wie gewohnt mit Erde, macht aber die oberste Schicht (Höhe der oberen Ecke) im ganzen Rahmen zu Asphalt, auch dort, wo schon Erde, Sand oder Schutt liegt. Gebäudeblöcke bleiben. Rückgängig geht. Für eine Straße: Rahmen so breit wie die Straße, Ecke 2 auf Straßenhöhe.

## 1.5.0: Fläche füllen (Fundament)
Unten rechts im Fenster: **Material** (anklicken wechselt: Beton, Stahl, Ziegel, Kopfstein, Holz) und **Fläche füllen**. Rahmen mit dem Werkzeug markieren; die Höhe des Rahmens ist die Dicke der Platte (Ecke 1 und Ecke 2 auf gleicher Höhe = 1 Block dick). Zweimal drücken: Luft, Erde und Platzhalter im Rahmen werden zu vollen Blöcken aus dem Material. Gebäudeblöcke bleiben. Rückgängig geht.
Konsole: `bau fuellen beton` (oder stahl, ziegel, kopfstein, holz), 2x eingeben.
