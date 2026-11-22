# Wirtschaft – Anleitung (Version 2.2.2)

Hauptbank und Firmentresor haben jetzt eigene Fenster. Pro Firma gibst du drei Werte ein, der Mod rechnet und bucht:

**Umsatz − Ausgaben = Zwischensumme − Steuer = Einkommen**

- Jede **Hauptbank gehört zu einer Bank (Stadt)** mit eigenen Firmen und **eigener Regierung**.
- Die **Steuer** einer Firma geht an die Regierung *ihrer* Stadt.
- Das **Einkommen bleibt als Guthaben in der Firma**. Auszahlen kannst du es an der Hauptbank: als Dukes, Old Cash, Silberbarren oder Goldbarren.
- Gebucht wird **am Ende jedes Monats**. Ein Monat = einstellbare Anzahl Spieltage (Standard 7).
- Jede Firma hat einen Schalter **„läuft“**: An = sie wird gebucht (du spielst sie gerade), Aus = pausiert. So kannst du eine Firma, eine Regierung (alle Firmen ihrer Stadt) oder beides gleichzeitig spielen.
- Silber- und Goldklumpen heißen jetzt **Silberbarren** und **Goldbarren** und haben ein Barren-Symbol.

## Bauen und starten

1. Spiel beenden
2. Im Ordner `Mods\Wirtschaft` auf **BAUEN** doppelklicken, warten auf „FERTIG“
3. Spiel starten, Welt laden

## Hauptbank (E drücken)

Beim ersten Öffnen: **Neue Bank eröffnen** (Name der Stadt, optional Name der Regierung) oder **mit vorhandener Bank verbinden** (Filiale).


- **Neu**: Namen eintippen, dann **Neue Partei** oder **Neue Firma**. Eine neue Firma gehört der Partei, die du gerade spielst.
- **Firma < >**: durch alle Firmen blättern
- **Besitzer < >**: wem die Firma gehört
- **Umsatz / Ausgaben / Steuersatz**: eintippen, die Rechnung darunter passt sich sofort an
- **Regierung < >**: welche Partei hier die Steuern bekommt, dazu Steuern pro Monat und Konto
- **Firma läuft An/Aus**, **Ganze Stadt An/Aus**
- **Guthaben**: der Firma und der Regierung
- **Monat = X Tage**: Monatslänge
- **Auszahlen**: Quelle (Firma oder Regierung), Betrag, Währung wählen → Gegenstände landen im Rucksack (was nicht passt, fällt vor dir auf den Boden)
- Kurse (Standard: Dukes 1, Old Cash 1, Silberbarren 50, Goldbarren 140) ändern per F1: `wb kurs Goldbarren 200`
- **Speichern** / **Schließen** (beim Schließen wird auch gespeichert)

## Firmentresor (E drücken)

- Noch frei: vorhandene Firma mit **< >** wählen und **Verbinden**, oder **Namen eintippen, Bank wählen, Anlegen und verbinden**
- Verbunden: Umsatz, Ausgaben, Steuer, **Mitarbeiter**, **Immobilienwert**, dazu Rechnung, Firmenwert, Guthaben und Schalter „läuft“. Änderungen gelten auch in der Hauptbank.
- **Tresor trennen**, wenn er zu einer anderen Firma soll

## Kurztest

1. Hauptbank: Name „Nordstadt“ → **Bank eröffnen**
2. Bei „Neu“ „Frank“ eintippen → **Neue Partei**
3. Firmentresor: „Baufirma“ eintippen, Bank Nordstadt → **Anlegen und verbinden**
4. Umsatz 60000, Ausgaben 20000, Steuer 20 eintippen → Einkommen 32.000, Steuer 8.000
5. F1 → `settime <heute+7> 8 0` (7 Tage weiter) → Guthaben Baufirma +32.000, Regierung Nordstadt +8.000
6. Hauptbank: Auszahlen von Baufirma, Betrag 1400, als Goldbarren → 10 Goldbarren im Rucksack

## Börsenterminal (neu, E drücken)

Im Kreativmenü: **Börsenterminal** (PC mit Monitor).

**Firmenwert** = Monatsumsatz + Mitarbeiter × 2.000 + Immobilienwert
(Mitarbeiter und Immobilienwert trägst du im Firmentresor ein, Wert pro Mitarbeiter per F1 `wb mitarbeiterwert 1500` änderbar)

- Reiter **Firmen**: Firma < >, Bewertung, Preis für 1 % und 10 %, Eigentümer
- **Anteile handeln**: Verkäufer, Käufer, Anteil (1-%-Schritte, +10), dann
  - **Verkaufen**: Käufer zahlt den Börsenpreis an den Verkäufer
  - **Eintragen**: ohne Zahlung, zum Einrichten deiner Eigentümer
- Reiter **Personen**: Charaktere mit Konto und Vermögen (Konto + Wert der Anteile), neue Person anlegen
- Personen lassen sich ihr Geld an der **Hauptbank** auszahlen (bei „Auszahlen“ die Person wählen)

## Testmodus (schnell prüfen)

F1 → `wb test 5` → 1 Tag dauert nur 5 Ingame-Minuten (bei Standard-Tageslänge ca. 12 Sekunden).
In der Hauptbank „Monat = 1 Tage“ → alle 5 Ingame-Minuten wird gebucht. Oben im Fenster steht „TEST“.
Zurück zum normalen Spiel: `wb test 0` und die Monatslänge wieder hochsetzen.

## Controller

Neben Umsatz, Ausgaben, Steuer und Betrag gibt es **− / +** Knöpfe (1.000er-Schritte, Steuer 1 %, Betrag 100er), damit du nicht tippen musst.

## Notlösung

Die Konsolenbefehle gibt es weiterhin: F1, `wb hilfe`. Öffnet sich statt des Fensters das F1-Fenster, wurde das Fenster nicht geladen. Dann bitte Bescheid geben, im Log steht warum.

Daten: `%AppData%\7DaysToDie\Saves\<Welt>\<Spiel>\Wirtschaft.xml`. Log: `%AppData%\7DaysToDie\logs`.

## Neu in 1.8: Kredite
- Kredite an Firmen oder Personen mit fester Monatsrate (Annuität). Die Rate wird jeden Monat automatisch vom Konto des Kreditnehmers abgebucht (Firmenkonto bzw. Parteikonto).
- Regeln (Standard): höchstens 36 Monate Laufzeit, alle Raten zusammen höchstens 40 % vom Nettoeinkommen nach allen Kosten und Steuern, Zins 8 % pro Jahr.
  Ändern: `wb kredit regeln 36 40 8`
- `wb kredit prüfen <Firma|Partei> <Summe> <Monate>`, `wb kredit neu <Firma|Partei> <Summe> <Monate> [Zweck]`, `wb kredit liste`, `wb kredit löschen <Nr>`
- Der KI-Banker aus der Figuren-Mod liest diese Daten und kann Kredite anbieten; du bestätigst im Gesprächsfenster mit "Kredit abschließen".

## Neu in 1.7: Börse nach Region filtern
- Im Börsenterminal gibt es oben die Zeile **Region** mit den Pfeilen `<` `>`. Du wählst **Alle Regionen** oder eine Bank (Stadt) und siehst dann nur die Firmen dieser Region samt Regierung der Stadt.
- Beim Öffnen startet die Börse automatisch in der Region der **nächsten Hauptbank** (bis 300 Blöcke vom Terminal). Gibt es keine in der Nähe, steht dort **Alle Regionen**. Mit den Pfeilen kannst du jederzeit wechseln.
- Der Filter betrifft die Firmen. Personen (Reiter Personen) gelten für alle Regionen gemeinsam.

## Neu in 1.6: Börse mit Namensliste und Gründern
- Das Börsenterminal steht jetzt im Kreativmenü gleich bei Hauptbank und Firmentresor.
- Vier Reiter: **Firmen** (Werte, Eigentümer), **Inhaber**, **Handel**, **Personen**.
- Alle Namen stehen in einer nummerierten Liste zum Anklicken (6 pro Seite, Seiten mit < >).
- **Inhaber:** Gründer 1 und Gründer 2 anklicken, Anteil von Gründer 1 einstellen, „Gründer festlegen“. Gründer 2 bekommt den Rest. Ersetzt alle bisherigen Eigentümer.
- **Handel:** Knopf „Verkäufer“ oder „Käufer“ aktivieren (Pfeil >), dann den Namen in der Liste anklicken. Mit Verkaufen (Käufer zahlt) oder Eintragen (ohne Zahlung) bestätigen.

## Neu in 1.9: Geldtresor (Banking), Konkurs, Kredit-Fenster
Alle Beträge sind **ganze Coins** (Raten, Zinsen, Steuern werden aufgerundet).

**Geldtresor (Banking)** – Kreativmenü, goldenes Tresor-Symbol. Zeigt das Guthaben einer Firma als echte Gegenstände.
- **Aufstellen, E drücken:** Fenster öffnet sich. Firma mit **< >** wählen (die mit dem nächsten Firmentresor zuerst), **Verbinden**. Nichts passiert automatisch.
- Verbunden zeigt das Fenster Guthaben, Schulden und was im Tresor liegt. **Tresor öffnen** zeigt die Gegenstände, **Andere Firma wählen** verbindet neu, **Trennen** (zweimal drücken) löst ihn. Das Geld bleibt immer auf dem Firmenkonto.
- **Herausnehmen** bucht vom Guthaben ab, **Hineinlegen** (Dukes, Old Cash, Silber, Gold) bucht dazu. Andere Gegenstände zählen nicht.
- **Stufen-Kaskade Münzen → Scheine → Silber → Gold:** bis 10.000 nur Münzen. Vom Überschuss 80 % in Scheine; sind die Scheine 20.000 wert, 80 % des Überschusses in Silber; ab 20.000 Silber 80 % in Gold. Zahlungen gehen zuerst über die Münzen; unter 5.000 Münzen wird zurückgetauscht. Beispiel 30.000 = 14.000 Dukes + 800 Scheine.
- Kurse (Standard): Dukes 1, Old Cash 20, Silberbarren 500, Goldbarren 5000. Ältere Spielstände werden beim ersten Laden einmalig umgestellt.

**Börse, Reiter Firmen (unten, jeweils zweimal drücken):** **Geldtresor auflösen** und **Firma löschen (liquidieren)**: offene Kredite werden aus dem Guthaben getilgt (Rest wird abgeschrieben), der Rest geht nach Anteilen an die Eigentümer. Dort steht auch die **Schuldenquote** (Schulden ÷ Firmenwert).

**Hauptbank → Knopf „Kredit“:** Fenster **Finanzierung** für die gewählte Firma. Betrag und Laufzeit einstellen (Zins und Regeln legt die Bank fest, `wb kredit regeln`); unten siehst du sofort Monatsrate und ob der Kredit erlaubt ist. Laufende Kredite durchblättern, **Tilgen** (Sondertilgung) oder **Ganz zurückzahlen**. Die Raten bucht das Kreditsystem aus 1.8 jeden Monat ab.

**Konkurs:** Konto im Minus und Schulden (Kredite + Minus) mindestens so hoch wie der Firmenwert → in Rot „KONKURS! Liquidationsverfahren“ mit Urteilstag (nach 1 Monat). Die Firma ist eingefroren und bekommt keine neuen Kredite. Bis zum Urteil saniert (Konto im Plus oder Schulden unter dem Firmenwert) → Verfahren eingestellt (Firma danach wieder einschalten). Sonst wird sie am Urteilstag liquidiert.

## Neu in 1.10: Feste Kreditgrenzen
**Kreditregeln (fest):** Laufzeit höchstens **36 Monate**, alle Raten zusammen höchstens **40 %** vom Nettoeinkommen (nach Kosten und Steuern). Diese Grenzen lassen sich nicht erhöhen, auch nicht per Befehl oder Datei (`wb kredit regeln` kann nur senken). Beispiel: Rate 1.200, Einkommen 2.000 → 60 % → Kredit abgelehnt, der Banker nennt dazu einen Gegenvorschlag (kleinerer Betrag oder längere Laufzeit). Der KI-Banker und das Kredit-Fenster an der Hauptbank prüfen mit derselben Regel. Firmen im Konkurs oder überschuldet bekommen nie einen Kredit.

## Neu in 1.11: Dynamische Sonderereignisse (Börse → Reiter „Ereignisse“)
Ein Sonderfaktor im Hintergrund: unregelmäßige, branchenpassende Ereignisse, die nur die **Liquidität** (Firmenkonto) verändern. Umsatz, Ausgaben, Mitarbeiter, Immobilien, Firmenwert und Börse bleiben unberührt. Jedes Ereignis ist eine normale Buchung im Verlauf der Firma; der Geldtresor gleicht sich wie bei jeder Buchung selbst an. Ereignisse zählen nicht zum Nettoeinkommen für Kredite.

**Wann:** Nach jeder Monatsbuchung einer laufenden Firma wird **genau einmal** geprüft (auch nach Neustart nicht doppelt). Ein Ereignis ist nie garantiert, eine Firma kann monatelang ohne laufen.
- Liquidität **an/über der Obergrenze** → hohe Chance, meist ein negatives Ereignis, das Richtung Zielbereich zurückführt (nie unter die Mindestliquidität).
- **unter der Mindestliquidität** → erhöhte Chance, meist ein positives Ereignis (nie über die Obergrenze). Kein garantiertes Auffüllen.
- **im Zielbereich** → selten, gemischt. Bleibt eine Firma mehrere Monate oben oder unten, steigt die Chance.
- Grundchance pro Monat: selten 12 %, normal 25 %, häufig 45 %, plus Zuschlag je nach Lage (höchstens 90 %).

**Höhe:** passt zur Firma: aus Monatsumsatz, Monatseinkommen und Mitarbeitern, mal Stärke (schwach 0,6 / normal 1 / stark 1,5), höchstens das Doppelte der typischen Größe. Ganze Coins.

**Branchenprofile:** KFZ-Handel, Transport und Spedition, Industrie und Produktion, Landwirtschaft, Bauunternehmen, Gastronomie und Hotellerie, IT und Dienstleistung, Waffen und Sicherheit, Kampfsport (Boxen, UFC, Arena), Theater und Unterhaltung (Bühne, Zirkus, Kino, Konzert, Casino), Handel und Allgemein. Das Profil wird aus der Branche der Firma erkannt (z. B. „Autohaus“ → KFZ-Handel) oder per Knopf fest gewählt. Jede Firma bekommt nur Ereignisse ihres Profils. Neue Profile: `Quellcode/Kern/Branchen.cs`, Liste `Alle()`.

**Einstellungen pro Firma (Reiter Ereignisse):**
- **An / Aus**: Aus = keinerlei automatische Sonderbuchungen. Standard bei allen Firmen: aus. Beim Einschalten zählt erst die nächste Monatsbuchung.
- **Profil**, **Stärke**, **Häufigkeit**, **Positive an/aus**, **Negative an/aus**.
- **Min / Ziel von–bis / Max** eintragen und **Speichern**. Leeres Feld = Vorschlag aus den Firmendaten (Min ≈ ein Monat Ausgaben, Ziel 1,6- bis 2-fach, Max 3,2-fach; kleiner KFZ-Händler mit 2.500 Ausgaben: 2.500 / 4.000–5.000 / 8.000). **Grenzen automatisch** setzt alles auf den Vorschlag zurück.
- **Manuell**: Betrag eintragen (oder leer = passend zur Firmengröße), dann **+ Ereignis** oder **− Ereignis**. Manuelle Ereignisse dürfen bewusst außerhalb der Grenzen liegen (ein negatives nimmt höchstens das Guthaben).

**Für eine ganze Region oder alle Firmen:** oben bei **Region** die Stadt wählen (oder „Alle Regionen“), dann **Ganze Auswahl: einschalten / ausschalten** (zweimal drücken). Die Zeile darunter zeigt, wie viele Firmen der Auswahl aktiv sind.

**Protokoll:** Jedes Ereignis wird mit Tag, Titel, Beschreibung, Branchenprofil, Betrag, Liquidität vorher und nachher und „auto/manuell“ gespeichert (die letzten 60 pro Firma). Der Reiter zeigt die letzten vier, dazu die Summen. Beim Auslösen erscheint ein Hinweis im Spiel.

## Neu in 1.12: Geldtresor auch für Privatpersonen
Ein Geldtresor (Banking) kann jetzt auch mit einer **Person** (David, Gustavo, …) verbunden werden, nicht nur mit einer Firma. Das Guthaben der Person erscheint dann als Münzen, Scheine und Barren, mit denselben Regeln (Kaskade, ganze Coins, Herausnehmen und Hineinlegen ändern das Konto).
- **E am Geldtresor → Andere Firma/Person wählen:** mit **< >** blättern. Firmen stehen zuerst, danach die Personen. Der breite Knopf darüber schaltet zwischen „Firmen und Personen“, „nur Firmen“ und „nur Personen“ um. Dann **Verbinden**.
- Das Fenster zeigt bei Personen „Privatperson“, Guthaben und offene Kredite. **Trennen** (zweimal drücken): das Geld bleibt auf dem Konto.
- Eine Person mit verbundenem Geldtresor lässt sich in der Börse erst löschen, wenn der Tresor getrennt ist. Regierungen können seit 1.15 einen Geldtresor bekommen (siehe unten).

## Neu in 1.13: Favoriten und Ereignis-Meldungen
Die Sonderereignisse werden im Spiel nur noch für **Favoriten** gemeldet, damit es bei vielen Firmen übersichtlich bleibt. Das Protokoll speichert weiterhin alle Ereignisse.
- **Börse → Reiter „Ereignisse“, Knopf „Favorit: aus/AN“:** markiert die gewählte Firma als Favorit.
- **Knopf „Ereignis-Meldungen: AN/AUS“:** schaltet die Meldungen insgesamt ein oder aus. Aus = keine Meldungen im Spiel, auch nicht für Favoriten.
- Die Meldung erscheint nach der Monatsbuchung als Hinweis im Spiel. Das Protokoll unten zeigt die letzten drei Ereignisse der gewählten Firma.

## Neu in 1.14: Person löschen mit Rückfrage, Auffangperson „Bürger“
- **Börse → Personen → Löschen:** Beim ersten Druck fragt eine gelbe Meldung unten: „Bist du sicher? …“ mit Guthaben, Firmen, Anteilen, Immobilien, Krediten und regierten Städten. Beim zweiten Druck (innerhalb von 15 Sekunden) wird gelöscht.
- **Was mit dem Besitz passiert:** Alles geht an die Person **Bürger** (wird bei Bedarf angelegt): Firmen, Anteile, Immobilien, offene Kredite und das Guthaben. Ein verbundener Geldtresor wird getrennt (das Geld bleibt auf dem Konto und geht an Bürger). Regierte Städte bekommen eine neue, leere Regierung.
- **Bürger selbst** lässt sich nicht löschen, nur umbenennen.
- **Neue Firmen** gehören zunächst immer **Bürger**. Die Firma läuft und hat ein Konto, auch ohne eingetragenen Inhaber. Den echten Inhaber stellst du später in der Börse oder an der Hauptbank ein.

## Neu in 1.15: Regierung mit Geldtresor und Firmentresor
**Geldtresor (Banking):** Eine Regierung lässt sich wie eine Firma oder Person verbinden. Der breite Knopf im Wahlfenster schaltet jetzt durch: „Firmen, Personen, Regierungen“, „nur Firmen“, „nur Personen“, „nur Regierungen“. Die Staatskasse (Konto der Regierung) erscheint dann als Münzen, Scheine und Barren, mit den gleichen Regeln wie sonst.

**Firmentresor:** Ein Firmentresor kann statt einer Firma auch eine **Regierung** tragen. Im freien Tresor unten **„Oder eine Regierung verbinden“**, mit **< >** wählen, **Verbinden**. Das Fenster zeigt dann:
- **Mitarbeiter** (jeder kostet 250 pro Monat) und **Weitere Ausgaben / Monat** (Flughafen, Security, Polizei, Straßen), per Tippen oder +/-.
- **Steuereinnahmen** pro Monat (alle Firmen und Immobilien der Banken, die diese Regierung beliefern), **Staatsausgaben**, **Saldo** und die **Staatskasse**.
- **Läuft: An/Aus:** Aus pausiert nur die Staatsausgaben. Steuern kommen immer weiter an.
- **Tresor trennen:** die Regierung läuft wieder automatisch.

**Regel:** Ohne Firmentresor läuft die Regierung automatisch: Sie bekommt Steuern und hat keine Ausgaben. Sobald sie verbunden ist, haben die Daten aus dem Firmentresor Vorrang: einmal pro Monatsbuchung werden die Staatsausgaben von der Staatskasse abgezogen und im Kassenbuch als „Staatsausgaben …“ vermerkt. Die Staatskasse darf dabei ins Minus rutschen (Staatsschulden). Die Zählung beginnt beim Verbinden mit dem nächsten Spieltag, es gibt keine Nachzahlung.

## Neu in 1.16: Neue Firmenbewertung, Fahrzeuge, mehr Branchen
**Firmenwert = Umsatzwert + Mitarbeiter + Immobilien + Fahrzeuge + Ertrag.** Börse (Reiter „Firmen“) und Firmentresor zeigen jeden Teil einzeln.
- **Umsatzwert:** Monatsumsatz × Branchenfaktor, mindestens × 2. Beispiel: 4.000 Umsatz = mindestens 8.000.
- **Mitarbeiter (Staffel):** Mitarbeiter 1–5 zählen je 1.000, 6–10 je 2.000, ab dem 11. je 3.000. Beispiele: 5 = 5.000, 10 = 15.000, 15 = 30.000.
- **Immobilien** und neu **Fahrzeuge und Maschinen:** beide trägst du im Firmentresor ein (Fahrzeuge in Schritten von 1.000).
- **Ertrag:** 6 Monatsgewinne nach Steuer. Macht die Firma Verlust, werden 6 Monatsverluste abgezogen. Der Wert fällt aber nie unter Immobilien + Fahrzeuge.
- Ein fest eingetragener Wert (`wb firma <Name> setze wert …`) hat weiter Vorrang, 0 = automatisch.
- Befehle: `wb wert <Firma>` zeigt alle Teile, `wb firma <Name> setze fahrzeuge <Betrag>`. `wb mitarbeiterwert` ist durch die Staffel ersetzt.

**Branchen mit Kennzahlen:** Jede Branche hat jetzt einen Umsatzfaktor, eine übliche Umsatzrendite (zum Vergleich in der Börse) und eine Schwankung für die Sonderereignisse.

| Branche | Umsatz × | übliche Rendite | Schwankung |
|---|---|---|---|
| KFZ-Handel | 2,0 | 3–8 % | 1,0 |
| Transport und Spedition | 2,0 | 4–10 % | 1,0 |
| Industrie und Produktion | 2,4 | 6–14 % | 0,9 |
| Landwirtschaft | 2,0 | 5–15 % | 1,2 |
| Bauunternehmen | 2,0 | 4–10 % | 1,1 |
| Gastronomie und Hotellerie | 2,0 | 5–12 % | 1,0 |
| IT und Dienstleistung | 3,0 | 15–30 % | 0,8 |
| Waffen und Sicherheit | 2,5 | 10–20 % | 1,1 |
| Kampfsport | 2,2 | 8–20 % | 1,3 |
| Theater und Unterhaltung | 2,0 | 5–15 % | 1,2 |
| Luftfahrt und Flughafen (neu) | 2,2 | 3–10 % | 1,2 |
| Gesundheit und Medizin (neu) | 2,8 | 12–25 % | 0,7 |
| Immobilien und Vermietung (neu) | 3,0 | 20–40 % | 0,6 |
| Finanzen und Versicherung (neu) | 3,0 | 15–30 % | 0,9 |
| Energie und Versorgung (neu) | 2,6 | 8–18 % | 0,7 |
| Handel und Allgemein | 2,0 | 3–8 % | 0,9 |

Die Branche wird aus dem Feld „Branche“ der Firma erkannt (oder aus dem Profil im Reiter „Ereignisse“). Die Schwankung macht Sonderereignisse größer (Kampfsport) oder kleiner (Immobilien). Die fünf neuen Branchen haben eigene Ereignisse.

## Neu in 1.17: Schild-Sicherung
Der Mod merkt sich den Text jedes beschriebenen Schilds in deiner Nähe (Umkreis rund 100 Blöcke) in der Datei `Wirtschaft_Schilder.xml` im Spielstand. Alle 15 Sekunden wird geprüft:
- **Neues oder geändertes Schild:** Text wird gesichert, die alte Fassung kommt in den Verlauf (die letzten 10).
- **Schild ist leer, hatte aber Text:** der letzte Text wird automatisch zurückgeschrieben, mit Hinweis im Spiel und Eintrag im Log.
- **Schild abgebaut:** der Text bleibt in der Sicherung, wird aber nicht ungefragt auf ein neues Schild an derselben Stelle geschrieben.

Befehle (F1):
- `wb schilder` – Stand der Sicherung und Prüfung jetzt.
- `wb schilder liste` – die 40 nächsten gesicherten Schilder mit Koordinaten.
- `wb schilder wiederherstellen` – alle leeren Schilder in der Nähe mit dem gesicherten Text beschreiben (auch an Stellen, wo ein Schild neu gesetzt wurde).
- `wb schilder auto an` / `aus` – automatisches Zurückschreiben ein oder aus.
- `wb schild <x> <y> <z>` – Text und Verlauf eines Schilds. Mit `leeren` absichtlich leeren (sonst schreibt die Sicherung den Text wieder hinein), mit `zurueck` die vorige Fassung verwenden.

Die Erst-Sicherung vom 14.11.2026 enthält alle 657 beschriebenen Schilder aus dem Spielstand „ChrismasX“.

## Neu in 1.9: Banken und Regierungen im Börsenterminal (Reiter Personen)
- **Regierungen sind keine Personen mehr.** Hauptfiguren wie Gustavo oder David können nie Regierung sein. Regierungen stehen in der Liste mit „(Regierung)“ bzw. „(Regierung, frei)“, wenn keine Bank sie nutzt.
- **Bank anlegen:** Namen bei „Name“ eintippen, „Bank anlegen“. Sie bekommt automatisch die Regierung „Regierung <Bankname>“.
- **Bank umbenennen / löschen:** Mit `<` `>` rechts die Bank wählen. „Bank umbenennen“ nimmt den Namen aus dem Feld (Firmen, Immobilien, Kredite ziehen mit). „Bank löschen“ braucht zwei Klicks; Firmen, Immobilien und Kredite wechseln zur nächsten Bank. Die Regierung der Bank bleibt bestehen.
- **Regierung anlegen / umbenennen / löschen:** „Regierung anlegen“ mit Namen im Feld; Umbenennen und Löschen wie bei Personen (links anklicken). Wird eine Regierung gelöscht, bekommt ihre Bank bei Bedarf wieder eine neue „Regierung <Bank>“.
- **Regierung einer Bank zuweisen:** links die Regierung anklicken, rechts die Bank wählen, „Gewählte Regierung für diese Bank“.
- Die Pfeile am Hauptbank-Block blättern nur noch durch Regierungen.
- Konsole: `wb bank loeschen <Name>`, `wb bank umbenennen <alt> <neu>`.
- Beim ersten Laden eines älteren Spielstands wird automatisch aufgeräumt: steht bei einer Bank eine Person als Regierung und gibt es die Standard-Regierung „Regierung <Bank>“, wird wieder diese eingetragen.

## Neu in 2.0: Erweiterte Verwaltung (Advanced Business Management)
Optional pro Firma. Ohne sie rechnet alles genau wie bisher.

**Öffnen:** Firmentresor (E) > Knopf **„Erweitert …“**. Reiter: Übersicht, Personal, Lieferanten, Kunden, Produkte, Projekte, Logistik.

**Übersicht:** „Erweiterte Verwaltung: AUS/AN“ schaltet für diese Firma ein. Darunter jedes Modul einzeln ([x] = an). Ausschalten löscht nichts, die Daten wirken nur nicht mehr.

**Keine Doppelbuchung:**
- Umsatz und Ausgaben im Firmentresor sind ab jetzt „sonstiger Umsatz“ und „sonstige Ausgaben“. Kundenverträge kommen zum Umsatz dazu, Löhne zu den Ausgaben, in derselben Monatsbuchung (eine Steuerrechnung).
- Beim ersten Einschalten von Personal wird die alte Mitarbeiterzahl als Gruppe „Bisherige Mitarbeiter“ mit „Lohn schon in Ausgaben? = Ja“ übernommen. Die zählt als Köpfe, kostet aber nichts extra. Willst du deren Lohn lieber hier führen: Lohn eintragen, auf „Nein“ stellen und die sonstigen Ausgaben im Firmentresor um denselben Betrag senken.
- Mit Personal an ist die Mitarbeiterzahl im Firmentresor nur Anzeige (Summe aus der Personalliste, zählt für Firmenwert und Börse).

**Personal:** Einzelne Mitarbeiter (Name, Position, Tätigkeit, Abteilung, Lohn, Status, Bonus, Provision % vom Umsatz) und Gruppen (z. B. 10 Verkäufer × 1.500). Befördern = Position und Lohn ändern, Speichern. Status Beurlaubt/Deaktiviert = kein Lohn.
- **Lohnkonto:** Steht bei „Person (Lohnkonto)“ ein Name, geht sein Lohn jeden Monat auf das Konto dieser Person (z. B. Francis). Gibt es die Person noch nicht, wird sie mit 0 angelegt. Abheben wie immer an der Hauptbank oder über ihren Geldtresor. Der Banker sieht den Lohn als Einkommen.
- **Lohnabrechnung automatisch:** mit der Monatsbuchung, vor der Steuer.
- **Manuell:** jede Monatsbuchung merkt einen offenen Monat vor, „Offene Löhne jetzt zahlen“ bucht sie vom Firmenkonto (nach der Steuer, also ohne Steuerersparnis).

**Lieferanten:** Suchfeld (Name, Branche, Produkt) findet Firmen der Spielwelt und Lieferanten, die andere Firmen schon kennen. Treffer anklicken = übernehmen. „Als neu anlegen“ legt einen neuen Partner an. Gibt es den Namen schon als Firma, wird verknüpft statt doppelt angelegt. Mehrere Lieferanten für dasselbe Produkt sind erlaubt (Preise vergleichen).

**Verhandeln:** Mit dem Unternehmer (KI-Figur) dieser Lieferfirma reden. Er kennt seine Lieferbeziehungen und verhandelt mit eigenen Interessen (Gegenangebote, Preiserhöhungen, Ablehnung). Eine Einigung trägst du selbst unter Lieferanten > Vereinbarungen ein, durch das Gespräch fließt kein Geld. Der Operator kann Lieferanten ebenfalls spielen.

**Kunden & Verträge:** Kunde, Vertrag, Leistung, Wert pro Monat, Beginn, Laufzeit (0 = unbefristet), Abrechnung, Status, Kündigungsfrist. Nur Status „Aktiv“ zählt zum Umsatz. Abgelaufene Verträge stehen automatisch auf „Beendet“. Die Abrechnung (monatlich, Quartal, jährlich) ist nur Info, gebucht wird der Monatswert jeden Monat.

**Produkte:** Name, Kategorie, Einkaufs- und Verkaufspreis, Bestand, Mindestbestand (in der Liste mit ! markiert). Kein Geld wird automatisch gebucht.

**Projekte:** Titel, Verantwortlich, Beteiligte, geschätzte Kosten, Zeitraum, Status (Idee bis Abgebrochen). Nur Planung, kein Geld.

**Logistik:** Lieferaufträge mit Status. Bei „Geliefert“ wird die Menge einmal beim Produkt gleichen Namens auf den Bestand gebucht. Transportkosten sind nur Info.

**Game Operator:** sieht alle Firmen mit erweiterter Verwaltung (Personal, Lieferanten, Verträge, Lager, Lieferungen, Projekte) und schlägt passende Ereignisse vor. Er ändert nichts selbst: Vorschläge sind keine Tatsachen.

## Neu in 2.0: Geld zurücksetzen (Börsenterminal)
Immer zweimal drücken (Rückfrage). Nur das Guthaben wird 0. Kredite, Anteile, Firmendaten und Verträge bleiben. Ein Geldtresor wird beim nächsten Abgleich leer.
- **Person oder Regierung:** Reiter Personen, links anklicken, „Geld zurücksetzen“.
- **Firma:** Reiter Firmen, Firma wählen, „Guthaben zurücksetzen“.
- **Stadt:** Reiter Personen, rechts die Bank wählen, „Stadt zurücksetzen“: alle Firmenkonten der Stadt und die Staatskasse ihrer Regierung. Personen gehören zu keiner Stadt und bleiben.
- Konsole: `wb reset person|firma|stadt <Name>` zeigt die Vorschau, mit `ja` am Ende wird ausgeführt. `wb abm <Firma>` zeigt die erweiterte Verwaltung.

Die Währung heißt jetzt überall „Coins“ (ältere Spielstände werden beim Laden umgestellt).

## Neu in 2.1: Material, Einkauf, Herstellung, Ausführen-Knöpfe
Reiter **Produkte** (Modul „Produkte & Lager“):
- **„+ Neu: Material“** z. B. Hardware: Lieferant (z. B. „BN Baufirma“), Preis pro Einheit, Lagerbestand, Mindestbestand, Standard-Kaufmenge, Mindestbestellung.
- **Jetzt kaufen:** Menge eintragen, zweimal drücken. Das Geld geht sofort vom Firmenkonto bzw. Firmen-Geldtresor ab und der Bestand steigt. Ist der Lieferant eine Firma im Spiel, bekommt sie das Geld auf ihr Konto. Ein erfundener Händler bekommt nichts, das Geld ist dann einfach weg. Bei zu wenig Geld oder unter der Mindestbestellung wird abgelehnt.
- **Produkt, „Material pro Stück“:** z. B. `Hardware 20` oder `Hardware 20; Kabel 5`. Neben dem Knopf steht, wie viele Stück das Lager hergibt (100 Hardware ergibt 5 Stück).
- **Herstellen:** Stückzahl eintragen und drücken. Das Material wird abgezogen, der Produktbestand steigt.
- Einkäufe sind keine Monatsausgaben, sie werden sofort bezahlt und nicht noch einmal abgerechnet.

**Projekte, „Kosten bezahlen“:** Zieht die geschätzten Kosten einmalig vom Firmenkonto ab und setzt das Projekt auf Aktiv.
**Logistik, „Lieferung annehmen“:** Status wird Geliefert, die Transportkosten werden bezahlt und die Menge kommt ins Lager (Material oder Produkt gleichen Namens).
**Kundenverträge:** laufen automatisch. Aktive Verträge kommen jeden Monat aufs Firmenkonto (als Umsatz, versteuert).
**Lieferanten:** Lieferzeit und Lieferbedingungen sind entfernt. „Firma im Spiel (auto)“ zeigt, mit welcher Spielfirma der Lieferant verknüpft ist.

## Neu in 2.2: Klare Anzeige und laufende Kosten
- Über jedem Formular steht jetzt in Grün, was der Eintrag gerade bewirkt, z. B. „AKTIV: +150 Coins jeden Monat aufs Firmenkonto“, „Status Entwurf: zählt NICHT“ oder „Lohn 1.500/Monat an Francis, automatisch“.
- In der Vertragsliste steht vorne „AKTIV 150/M“, ein Entwurf steht in Klammern. Die Übersicht zeigt die aktiven Kunden und wie oft gebucht wird.
- Neuer Reiter **Kosten** (Modul „Laufende Kosten“): Miete, Strom, Versicherung … mit Betrag pro Monat. Aktive Posten werden jeden Monat als Ausgabe abgezogen, in derselben Monatsbuchung und vor der Steuer. Steht ein Posten schon in den sonstigen Ausgaben, auf „Schon in sonst. Ausgaben? = Ja“ stellen, dann dient er nur der Übersicht.

## 2.2.1: Speichern repariert
- Zahlen dürfen jetzt Zusätze haben: „150 Coins“, „20 Stück“, „12,50 €“, „1.500“ und „1500,-“ werden alle erkannt und als reine Zahl gespeichert.
- Passt ein Feld nicht, bleiben alle deine Eingaben stehen. Über dem Formular steht dann „NICHT GESPEICHERT: …“ mit dem Feld, das korrigiert werden muss. Bisher wurde in diesem Fall alles zurückgesetzt.

## 2.2.2: Vertragsbeginn
- „Beginn“ leer lassen heißt: Der Vertrag zählt ab heute (beim Speichern wird der heutige Spieltag eingetragen). Die Übersicht zeigt „Heute: Spieltag …“.
- Nach dem Ausschalten des Testmodus werden Verträge mit Beginn in der Zukunft beim Laden einmalig auf heute gesetzt, ebenso bei jeder Zeitumstellung.
- Ein Vertrag mit Laufzeit läuft ab seinem Beginn. Wer bei Beginn 1 einträgt, startet also in der Vergangenheit, und eine Laufzeit kann dann schon abgelaufen sein. Deshalb besser leer lassen.
