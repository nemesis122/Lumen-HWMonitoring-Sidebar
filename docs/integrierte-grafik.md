# Integrierte Grafik bei den Prozessor-Sensorwerten

Die Auswahl **Integrierte Grafik** steht unter **Einstellungen → Widgets → Prozessor → Anpassen → Sensorwerte**. Der bisherige Schalter auf der Seite **Sensoren** wurde entfernt. Suche und Zurücksetzen ordnen die Einstellung der Widget-Seite zu.

Bei neuen Einstellungen ist die Option ausgeschaltet. Eine bereits gespeicherte ausdrückliche Auswahl bleibt beim Laden erhalten. Aktivieren zeigt zusätzlich die erkannte Prozessorgrafik in der Sidebar; Deaktivieren entfernt sie bei Systemen mit separater Grafikkarte. Das bisherige Verhalten für Rechner mit ausschließlich integrierter Grafik bleibt erhalten.

Alle bisherigen Designs, Sensorfilter und Widget-Einstellungen bleiben verfügbar. Die Tests prüfen die Voreinstellung, den vorhandenen Speicherwert, die Platzierung des Kontrollkästchens, die sofortige Anzeigeänderung und das Entfernen aus der Sensorseite.

```powershell
dotnet run --project tools/Lumen.Gallery -c Release -- outputs/integrated-option --integrated-option
```
