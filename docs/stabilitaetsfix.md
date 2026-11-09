# Stabile Messwertanzeige und Netzwerk-Spitzenwert

## Behoben

- Der Netzwerk-„2-min peak“ bleibt sichtbar, wenn sein Wert dem aktuellen Download entspricht. Es sind unterschiedliche Messgrößen; gleicher Zahlenwert ist kein Grund zum Ausblenden. Der vorherige Platzhalter konnte die Zeile um zwei Pixel verkleinern.
- Hauptzahlen reservieren Platz für mehrstellige Werte vor der ersten Aktualisierung. Dadurch bleiben Einheiten beim Wechsel von 9 auf 100 an ihrer Position.
- Die Lastbeschriftung in Pure · Zahl behält ihren Platz bei 0 bis 100 Prozent.

Gezielt geprüft wurden Clean Dark, Compact Pro, Performance View, Pure · Balken, Pure · Zahl und Pure · Segmente. Der gemeinsame Spitzenwert-Fix gilt zusätzlich für Pure, Pure Widgets und weitere Designs, welche dieselbe Faktenliste benutzen. Bestehende Widget-Einstellungen, Reihenfolge und Sensorwahl bleiben erhalten.

## Nachweis

Der neue Test `NewDesignStabilityTests` rendert echte Avalonia-Controls und prüft Kartenhöhe, Position des folgenden Widgets, Temperatureinheiten und Lastbeschriftung:

- sechs Designs, dunkle und helle Darstellung, 280 und 375 Pixel Breite;
- CPU, GPU, RAM, Netzwerk, Laufwerke, Kühlung und Leistung;
- wechselnde Ziffernlängen, 0/1130/9999 U/min sowie B/s, KB/s und MB/s;
- sichtbarer Spitzenwert bei gleichem aktuellem Download;
- korrektes Ablaufen des alten Spitzenwerts aus dem 120-Messwerte-Puffer, ohne Bewegung oder Ausblenden der Zeile.

1.891 Tests erfolgreich; 1.587 Regression-Fälle ohne Fehler. Die Regressionen prüfen geänderte Daten und Geometrie; ein Live-Hardware-Test der EXE auf dem Benutzerrechner wurde nicht durchgeführt.

```powershell
dotnet test tests/Lumen.Tests -c Release -m:1
dotnet run --project tools/Lumen.Regression -c Release -- --expect-fixed
```
