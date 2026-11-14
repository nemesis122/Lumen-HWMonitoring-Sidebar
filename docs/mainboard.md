# Lumen 4.4.0 – Mainboard und Design-Auswahl

Mainboard-Designs und die bereinigte Design-Auswahl von Lumen 4.4.0.

## Direkt als Design auswählen

Lumen-Einstellungen öffnen → Widgets → bis ans Ende der Designliste scrollen. Dort stehen Mainboard, Mainboard Map, Mainboard Map Desktop und Mainboard Cores zusammen. Die Auswahl gilt wie bisher für alle Widgets oder das gewählte Register. Es gibt keinen zusätzlichen Mainboard-Map-Eintrag im Traymenü.

Entfernt aus allen Design-Auswahlen: Mainboard Typografie, Mainboard Datenmatrix, Mainboard Instrumente, Mainboard Messstreifen, AIO Pop Console, AIO Chromatic und Aperture. Alte Einstellungen mit diesen Designs werden auf Standard umgestellt; andere Widget-Einstellungen bleiben erhalten. Die alten Design-IDs bleiben für das Einlesen bestehen, damit andere gespeicherte Designs nicht verschoben werden. Insgesamt sind noch 82 Designs auswählbar.

Mainboard Cores: CPU und GPU als gleich hohe Felder oben, anschließend die breite Core-Reihe, RAM/PSU, Laufwerke, Lüfter und Netzwerk. Bei 440 px stehen 16 Kerne in einer Reihe. Bei weniger Platz oder größerer Skalierung werden sie auf mehrere Reihen verteilt. Einzelwerte einschließlich der VRAM-Belegung bleiben enthalten, und die Leiterbahnen verlaufen außerhalb der breiten Felder. Das bisherige AIO · Mainboard bleibt erhalten.


## Haupt-Widgets je Design speichern

Ein erstmals gewähltes normales Design startet mit CPU, GPU, RAM, Laufwerken, Netzwerk und den vier INFO-Widgets Uhr, Weltuhren, Kalender und Wetter. Ein erstmals gewähltes AIO-/Mainboard-Design zeigt zusätzlich Kühlung/Mainboard und Stromversorgung: sieben HW- plus vier INFO-Widgets.

Eigene Häkchen dieser elf Haupt-Widgets werden für das jeweilige Design gespeichert. Beim Wechsel zu einem neuen Design gilt dessen Vorgabe; beim Zurückwechseln zu einem bereits angepassten Design erscheint dessen eigene gespeicherte Auswahl wieder. Das aktive Design samt aktueller Auswahl bleibt auch nach einem Neustart erhalten. Derselbe Design-Knopf setzt bestehende Änderungen nicht zurück. Bei der Auswahl für nur ein Register bleibt das andere Register unverändert.

Die Regel betrifft ausschließlich diese Haupt-Widgets. Zusatz-Widgets wie FPS, Ping oder Akku, die Reihenfolge, Register und einzelne Sensorwerte unter Customise werden dadurch nicht zurückgesetzt.

„Mainboard Map Desktop“ aktiviert beim Auswählen die ganze Arbeitsfläche des ausgewählten Monitors, mit einer Spalte und ohne frei schwebende Widgets. Die Platine bleibt auf 600 logische Pixel Höhe begrenzt, das Dashboard auf 1400 logische Pixel Breite zentriert. Links stehen CPU, GPU und Netzwerk, rechts RAM, PSU und Laufwerke; darunter Kernsäulen und Lüfter. Beim Wechsel vom Desktop-Design zurück zu einem Sidebar-Design wird die Vollbildplatzierung deaktiviert. Bildschirmwahl und weitere Platzierungseinstellungen bleiben unter Placement erreichbar.

Das originale Mainboard-Design hält Netzwerkwerte mit eigenem Innenabstand und passender Verkleinerung in drei getrennten Spalten. Geprüft wurden unter anderem Upload 32 kbit/s und Wechsel bis in Gbit/s.

Die mittleren Leiterbahnen werden vor breiten Feldern unterbrochen. Sie verlaufen nicht mehr hinter NETWORK, FANS oder deren Werten. Das funktioniert auch bei transparenten Karten, weil in diesen Bereichen keine Linien mehr gezeichnet werden. Die ursprünglichen Verbindungslinien zwischen den schmalen Hardware-Feldern bleiben erhalten.

## INFO und Zusatz-Widgets bei allen AIO-Designs

Uhr, Weltuhren, Kalender, Wetter, Medien, Akku, FPS, Prozesse, System, Speicherplatz, Ping, Datennutzung, Notizen, Laufwerkszustand und Timer erhalten jetzt passende Zusatzkarten. Sie behalten ihre vorhandenen Live-Daten, Sensor-Schalter und Bedienelemente. Sie fallen nicht mehr pauschal auf die Standard-Darstellung zurück, während die Einstellungen einen AIO-Namen anzeigen. Rahmen und Farben folgen dem gewählten AIO; die enthaltene Darstellung nutzt die passenden vorhandenen Tabellen-, Zahlen- oder Instrumentelemente.

Ausgeblendete Widgets bleiben ausgeblendet. Zum Anzeigen unter Widgets das jeweilige Häkchen setzen. Auch auf INFO folgt die Gestaltung dem dort ausgewählten AIO-Design. Notizen und Timer ohne eigene Designauswahl übernehmen ein einheitliches AIO ihres Registers automatisch, auch bei älteren Profilen. Gemischte Register überschreiben diese beiden Widgets nicht. Overview bleibt die gemeinsame Hardware-Ansicht.

Für Mainboard Cores empfiehlt sich zunächst eine Sidebar-Breite von 440 px. ZIP in einen neuen Ordner entpacken, laufende ältere Lumen-Instanz schließen und Lumen.exe starten. Bestehende Einstellungen weiterverwenden, um eigene Präferenzen zu behalten.

## Gestaltung

Cyan: 1 CPU und 3 RAM. Violett: 2 GPU und 4 PSU. Kästen, dickere Verbindungspfeile und Bauteil-Konturen folgen derselben Farbe und Nummer. Auf der Platine bezeichnet 4 ATX den Stromanschluss des PSU-Kastens.

Die ganze Platine steht oben in einer eigenen Reihe. Darunter folgen CPU/GPU und RAM/PSU im 2×2-Raster. Die VRAM-Belegung steht wieder in einer Zeile rechts neben VRAM. Die zwei Laufwerkskästen, Netzwerk, Kernsäulen und Lüfter folgen darunter. Oben gibt es nur HW und INFO.

Bei 16 Kernen werden zwei Reihen mit je acht Säulen angezeigt, farblich passend in Cyan und Violett. Alle Messwerte und die bestehenden Sensor-/Widget-Schalter bleiben erhalten. Die ursprüngliche Mainboard-Sidebar bleibt ebenfalls als eigenes Design verfügbar.

## Daten

Die Anwendung nutzt die vorhandenen Live-Sensorquellen von Lumen. Vorschaubilder verwenden ausdrücklich Beispieldaten. Die tatsächliche Sensorverfügbarkeit hängt von Hardware, Berechtigungen und gewählter Quelle ab; fehlende Werte erscheinen als „—“. Die Platine ist eine generische Illustration, nicht das erkannte Mainboard-Modell.

## Quellcode bauen (.NET 8 SDK)

    dotnet test tests/Lumen.Tests -c Release -m:1
    dotnet publish src/Lumen.App -c Release -r win-x64 --self-contained -m:1 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o publish

Programm-Vorschauen:

    dotnet build tools/Lumen.Preview -c Release -m:1
    dotnet tools/Lumen.Preview/bin/Release/net8.0/Lumen.Preview.dll preview --mainboard --en

Basis: https://github.com/nemesis122/Lumen-HWMonitoring-Sidebar
Lizenzhinweise: LICENSE und licenses/.
