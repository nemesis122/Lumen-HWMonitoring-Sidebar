using System.Globalization;

namespace Lumen.App;

// All visible text in six languages. The English text is the key, so a missing line
// falls back to readable English rather than an identifier.
public static class L
{
    // English key | Deutsch | Français | Italiano | Español | Português
    private const string Table = """
Processor|Prozessor|Processeur|Processore|Procesador|Processador
Graphics|Grafik|Graphique|Grafica|Gráficos|Gráficos
Memory|Arbeitsspeicher|Mémoire|Memoria|Memoria|Memória
Drives|Laufwerke|Disques|Unità|Unidades|Unidades
Network|Netzwerk|Réseau|Rete|Red|Rede
Today|Heute|Aujourd'hui|Oggi|Hoy|Hoje
Weather|Wetter|Météo|Meteo|Tiempo|Tempo
Battery|Akku|Batterie|Batteria|Batería|Bateria
Cooling|Kühlung|Refroidissement|Raffreddamento|Refrigeración|Arrefecimento
Top apps|Top-Programme|Applications actives|App principali|Apps principales|Apps principais
Load|Last|Charge|Carico|Carga|Carga
Power|Leistung|Puissance|Potenza|Potencia|Potência
Voltage|Spannung|Tension|Tensione|Voltaje|Tensão
Fan|Lüfter|Ventilateur|Ventola|Ventilador|Ventoinha
Hot spot|Hotspot|Point chaud|Hotspot|Hotspot|Hotspot
Idle|Ruht|Arrêté|Ferma|Parado|Parada
used|belegt|utilisés|usati|usados|usados
Available|Frei|Libre|Libera|Libre|Livre
Installed|Verbaut|Installée|Installata|Instalada|Instalada
2-min peak|Max 2 min|Pic 2 min|Picco 2 min|Pico 2 min|Pico 2 min
drive|Laufwerk|disque|unità|unidad|unidade
drives|Laufwerke|disques|unità|unidades|unidades
Download|Download|Réception|Download|Descarga|Download
Upload|Upload|Envoi|Upload|Subida|Upload
Link use|Nutzung|Usage|Utilizzo|Uso|Uso
Address|Adresse|Adresse|Indirizzo|Dirección|Endereço
Day|Tag|Jour|Giorno|Día|Dia
Week|KW|Sem.|Sett.|Sem.|Sem.
{0} h {1:00} m left|noch {0} h {1:00} min|encore {0} h {1:00}|ancora {0} h {1:00} min|quedan {0} h {1:00} min|faltam {0} h {1:00} min
Integrated|Integriert|Intégrée|Integrata|Integrada|Integrada
Temperatures need administrator rights and the PawnIO driver.|Temperaturen brauchen Administratorrechte und den PawnIO-Treiber.|Les températures nécessitent les droits administrateur et le pilote PawnIO.|Le temperature richiedono i diritti di amministratore e il driver PawnIO.|Las temperaturas requieren derechos de administrador y el controlador PawnIO.|As temperaturas precisam de direitos de administrador e do controlador PawnIO.
Restart as administrator|Als Administrator neu starten|Redémarrer en administrateur|Riavvia come amministratore|Reiniciar como administrador|Reiniciar como administrador
LIVE|LIVE|EN DIRECT|LIVE|EN VIVO|AO VIVO
LIMITED|EINGESCHRÄNKT|LIMITÉ|LIMITATO|LIMITADO|LIMITADO
DEMO|DEMO|DÉMO|DEMO|DEMO|DEMO
Charging|Lädt|En charge|In carica|Cargando|A carregar
On battery|Akkubetrieb|Sur batterie|A batteria|Con batería|Na bateria
Plugged in|Netzbetrieb|Branché|Collegato|Conectado|Ligado à corrente
Time left|Restzeit|Autonomie|Autonomia|Tiempo restante|Tempo restante
Until full|Bis voll|Jusqu'à plein|Fino a carica|Hasta llenar|Até cheia
Rate|Rate|Débit|Flusso|Tasa|Taxa
Health|Zustand|Santé|Salute|Salud|Saúde
fans|Lüfter|ventilateurs|ventole|ventiladores|ventoinhas
Stopped|Steht|Arrêté|Ferma|Parado|Parada
Fans|Lüfter|Ventilateurs|Ventole|Ventiladores|Ventoinhas
Board|Mainboard|Carte mère|Scheda madre|Placa base|Placa-mãe
Chipset|Chipsatz|Chipset|Chipset|Chipset|Chipset
Coolant|Kühlmittel|Liquide|Liquido|Líquido|Líquido
Chassis|Gehäuse|Boîtier|Case|Caja|Gabinete
Pump|Pumpe|Pompe|Pompa|Bomba|Bomba
AIO Pump|AIO-Pumpe|Pompe AIO|Pompa AIO|Bomba AIO|Bomba AIO
Water Pump|Wasserpumpe|Pompe à eau|Pompa acqua|Bomba de agua|Bomba de água
Auxiliary|Zusatz|Auxiliaire|Ausiliaria|Auxiliar|Auxiliar
CPU Opt|CPU Opt|CPU Opt|CPU Opt|CPU Opt|CPU Opt
Power Fan|Netzteil-Lüfter|Ventilateur alim.|Ventola alim.|Ventilador PSU|Ventoinha PSU
Feels like|Gefühlt|Ressenti|Percepita|Sensación|Sensação
Wind|Wind|Vent|Vento|Viento|Vento
Humidity|Feuchte|Humidité|Umidità|Humedad|Humidade
Set a place in the settings.|Ort in den Einstellungen festlegen.|Choisissez un lieu dans les paramètres.|Imposta un luogo nelle impostazioni.|Elige un lugar en los ajustes.|Defina um local nas definições.
clear|Klar|Dégagé|Sereno|Despejado|Limpo
mostly-clear|Überwiegend klar|Plutôt dégagé|Poco nuvoloso|Mayormente despejado|Pouco nublado
partly-cloudy|Teilweise bewölkt|Partiellement nuageux|Parzialmente nuvoloso|Parcialmente nublado|Parcialmente nublado
overcast|Bedeckt|Couvert|Coperto|Cubierto|Encoberto
fog|Nebel|Brouillard|Nebbia|Niebla|Nevoeiro
drizzle|Nieselregen|Bruine|Pioviggine|Llovizna|Chuvisco
freezing|Gefrierender Regen|Pluie verglaçante|Pioggia gelata|Lluvia helada|Chuva gelada
rain|Regen|Pluie|Pioggia|Lluvia|Chuva
snow|Schnee|Neige|Neve|Nieve|Neve
showers|Schauer|Averses|Rovesci|Chubascos|Aguaceiros
storm|Gewitter|Orage|Temporale|Tormenta|Trovoada
Temperature warning|Temperaturwarnung|Alerte de température|Avviso temperatura|Aviso de temperatura|Aviso de temperatura
{0} is at {1} – above your limit of {2}.|{0} hat {1} – über deiner Grenze von {2}.|{0} atteint {1} – au-dessus de votre limite de {2}.|{0} è a {1} – sopra il tuo limite di {2}.|{0} está a {1} – por encima de tu límite de {2}.|{0} está a {1} – acima do seu limite de {2}.
Show or hide|Ein-/Ausblenden|Afficher ou masquer|Mostra o nascondi|Mostrar u ocultar|Mostrar ou ocultar
Settings|Einstellungen|Paramètres|Impostazioni|Ajustes|Definições
Quit|Beenden|Quitter|Esci|Salir|Sair
Changes apply immediately.|Änderungen gelten sofort.|Les modifications s'appliquent immédiatement.|Le modifiche si applicano subito.|Los cambios se aplican al instante.|As alterações aplicam-se de imediato.
Reset all|Alles zurücksetzen|Tout réinitialiser|Ripristina tutto|Restablecer todo|Repor tudo
Done|Fertig|Terminé|Fatto|Listo|Concluído
Appearance|Darstellung|Apparence|Aspetto|Apariencia|Aparência
Position|Position|Position|Posizione|Posición|Posição
Readings|Messwerte|Mesures|Misure|Mediciones|Medições
Widgets|Widgets|Widgets|Widget|Widgets|Widgets
System|System|Système|Sistema|Sistema|Sistema
Language|Sprache|Langue|Lingua|Idioma|Idioma
Automatic (Windows)|Automatisch (Windows)|Automatique (Windows)|Automatica (Windows)|Automático (Windows)|Automático (Windows)
Theme|Farbschema|Thème|Tema|Tema|Tema
Like Windows|Wie Windows|Comme Windows|Come Windows|Como Windows|Como o Windows
Dark|Dunkel|Sombre|Scuro|Oscuro|Escuro
Light|Hell|Clair|Chiaro|Claro|Claro
High contrast|Hoher Kontrast|Contraste élevé|Contrasto elevato|Alto contraste|Alto contraste
All labels in full brightness.|Alle Beschriftungen in voller Helligkeit.|Toutes les étiquettes en pleine luminosité.|Tutte le etichette alla massima luminosità.|Todas las etiquetas con brillo máximo.|Todas as etiquetas com brilho máximo.
Glass effect|Glaseffekt|Effet verre|Effetto vetro|Efecto cristal|Efeito vidro
Translucent background with blur|Durchscheinender Hintergrund mit Unschärfe|Fond translucide et flou|Sfondo traslucido sfocato|Fondo translúcido con desenfoque|Fundo translúcido com desfoque
Opacity|Deckkraft|Opacité|Opacità|Opacidad|Opacidade
Colours|Farben|Couleurs|Colori|Colores|Cores
By state (blue to red)|Nach Zustand (blau bis rot)|Selon l'état (bleu à rouge)|Per stato (blu fino a rosso)|Por estado (azul a rojo)|Por estado (azul a vermelho)
Windows accent colour|Windows-Akzentfarbe|Couleur d'accent Windows|Colore principale di Windows|Color de énfasis de Windows|Cor de destaque do Windows
Blue|Blau|Bleu|Blu|Azul|Azul
Violet|Violett|Violet|Viola|Violeta|Violeta
Green|Grün|Vert|Verde|Verde|Verde
Orange|Orange|Orange|Arancione|Naranja|Laranja
Pink|Pink|Rose|Rosa|Rosa|Rosa
Hot readings still turn amber and red.|Heisse Werte werden trotzdem gelb und rot.|Les valeurs chaudes passent quand même à l'orange et au rouge.|I valori caldi diventano comunque gialli e rossi.|Los valores calientes siguen pasando a ámbar y rojo.|Os valores quentes continuam a ficar âmbar e vermelhos.
Text size|Schriftgrösse|Taille du texte|Dimensione testo|Tamaño del texto|Tamanho do texto
Density|Dichte|Densité|Densità|Densidad|Densidade
Automatic|Automatisch|Automatique|Automatica|Automática|Automática
Comfortable|Komfortabel|Confortable|Comoda|Cómoda|Confortável
Compact|Kompakt|Compacte|Compatta|Compacta|Compacta
Automatic fills the height up to the taskbar, without scrolling.|Automatisch füllt die Höhe bis zur Taskleiste, ohne Scrollen.|Automatique remplit la hauteur jusqu'à la barre des tâches, sans défilement.|Automatica riempie l'altezza fino alla barra delle applicazioni, senza scorrere.|Automática llena la altura hasta la barra de tareas, sin desplazarse.|Automática preenche a altura até à barra de tarefas, sem deslocar.
Width|Breite|Largeur|Larghezza|Ancho|Largura
Screen edge|Bildschirmrand|Bord de l'écran|Bordo dello schermo|Borde de pantalla|Margem do ecrã
Right|Rechts|Droite|Destra|Derecha|Direita
Left|Links|Gauche|Sinistra|Izquierda|Esquerda
Screen|Bildschirm|Écran|Schermo|Pantalla|Ecrã
Main screen|Hauptbildschirm|Écran principal|Schermo principale|Pantalla principal|Ecrã principal
main|Haupt|principal|principale|principal|principal
Always on top|Immer im Vordergrund|Toujours au premier plan|Sempre in primo piano|Siempre visible|Sempre visível
Reserve space|Platz reservieren|Réserver l'espace|Riserva spazio|Reservar espacio|Reservar espaço
Maximised windows stop before the sidebar.|Maximierte Fenster enden vor der Sidebar.|Les fenêtres agrandies s'arrêtent avant la barre.|Le finestre ingrandite si fermano prima della barra.|Las ventanas maximizadas terminan antes de la barra.|As janelas maximizadas param antes da barra.
Update interval|Aktualisierung|Actualisation|Aggiornamento|Actualización|Atualização
Temperature|Temperatur|Température|Temperatura|Temperatura|Temperatura
Celsius (°C)|Celsius (°C)|Celsius (°C)|Celsius (°C)|Celsius (°C)|Celsius (°C)
Fahrenheit (°F)|Fahrenheit (°F)|Fahrenheit (°F)|Fahrenheit (°F)|Fahrenheit (°F)|Fahrenheit (°F)
Warn from|Warnung ab|Alerte dès|Avviso da|Aviso desde|Aviso a partir de
Above this the temperature turns red.|Ab hier wird die Temperatur rot.|Au-delà, la température passe au rouge.|Oltre questo valore la temperatura diventa rossa.|Por encima, la temperatura se pone roja.|Acima disto a temperatura fica vermelha.
Warning pop-up|Warnmeldung|Notification d'alerte|Notifica di avviso|Aviso emergente|Aviso emergente
Shows a notice when a temperature crosses the limit.|Zeigt einen Hinweis, wenn eine Temperatur die Grenze überschreitet.|Affiche un avis quand une température dépasse la limite.|Mostra un avviso quando una temperatura supera il limite.|Muestra un aviso cuando una temperatura supera el límite.|Mostra um aviso quando uma temperatura passa o limite.
Integrated graphics|Integrierte Grafik|Graphique intégré|Grafica integrata|Gráficos integrados|Gráficos integrados
Also show the graphics built into the processor.|Auch die Grafik im Prozessor anzeigen.|Afficher aussi le graphique intégré au processeur.|Mostra anche la grafica integrata nel processore.|Mostrar también los gráficos del procesador.|Mostrar também os gráficos do processador.
Clock and date|Uhr und Datum|Horloge et date|Orologio e data|Reloj y fecha|Relógio e data
Graphics card|Grafikkarte|Carte graphique|Scheda grafica|Tarjeta gráfica|Placa gráfica
Cooling and mainboard|Kühlung und Mainboard|Refroidissement et carte mère|Raffreddamento e scheda madre|Refrigeración y placa base|Arrefecimento e placa-mãe
Move up|Nach oben|Monter|Sposta su|Subir|Subir
Move down|Nach unten|Descendre|Sposta giù|Bajar|Descer
Values|Werte|Valeurs|Valori|Valores|Valores
Core bars|Kernbalken|Barres des cœurs|Barre dei core|Barras de núcleos|Barras dos núcleos
History chart|Verlaufskurve|Courbe d'historique|Grafico storico|Gráfico de historial|Gráfico de histórico
Segment bar|Segmentbalken|Barre segmentée|Barra a segmenti|Barra segmentada|Barra segmentada
Read and write rates|Lese- und Schreibrate|Débits lecture et écriture|Velocità lettura e scrittura|Velocidad de lectura y escritura|Taxas de leitura e escrita
Seconds|Sekunden|Secondes|Secondi|Segundos|Segundos
Date|Datum|Date|Data|Fecha|Data
Day progress|Tagesfortschritt|Progression du jour|Avanzamento del giorno|Progreso del día|Progresso do dia
Forecast|Vorhersage|Prévisions|Previsioni|Previsión|Previsão
Board temperatures|Mainboard-Temperaturen|Températures de la carte mère|Temperature scheda madre|Temperaturas de la placa|Temperaturas da placa
Place|Ort|Lieu|Luogo|Lugar|Local
Search|Suchen|Chercher|Cerca|Buscar|Procurar
Searching…|Suche…|Recherche…|Ricerca…|Buscando…|A procurar…
Found: {0}|Gefunden: {0}|Trouvé : {0}|Trovato: {0}|Encontrado: {0}|Encontrado: {0}
No place found.|Kein Ort gefunden.|Aucun lieu trouvé.|Nessun luogo trovato.|No se encontró el lugar.|Nenhum local encontrado.
No connection to the weather service.|Keine Verbindung zum Wetterdienst.|Pas de connexion au service météo.|Nessuna connessione al servizio meteo.|Sin conexión con el servicio meteorológico.|Sem ligação ao serviço meteorológico.
Weather data from Open-Meteo; only the place's coordinates are sent.|Wetterdaten von Open-Meteo; übertragen werden nur die Koordinaten des Orts.|Données météo d'Open-Meteo ; seules les coordonnées du lieu sont envoyées.|Dati meteo da Open-Meteo; vengono inviate solo le coordinate del luogo.|Datos de Open-Meteo; solo se envían las coordenadas del lugar.|Dados do Open-Meteo; só são enviadas as coordenadas do local.
Start with Windows|Mit Windows starten|Démarrer avec Windows|Avvia con Windows|Iniciar con Windows|Iniciar com o Windows
Starts with administrator rights, without a prompt.|Startet mit Administratorrechten, ohne Nachfrage.|Démarre avec les droits administrateur, sans demande.|Si avvia con i diritti di amministratore, senza richiesta.|Se inicia con derechos de administrador, sin preguntar.|Inicia com direitos de administrador, sem pedido.
Running with administrator rights – all sensors are reachable.|Läuft mit Administratorrechten – alle Sensoren erreichbar.|Exécuté en administrateur – tous les capteurs sont accessibles.|In esecuzione come amministratore – tutti i sensori sono raggiungibili.|Ejecutándose como administrador: todos los sensores accesibles.|A correr como administrador – todos os sensores acessíveis.
Without administrator rights the CPU temperatures are missing.|Ohne Administratorrechte fehlen die CPU-Temperaturen.|Sans droits administrateur, les températures du processeur manquent.|Senza diritti di amministratore mancano le temperature della CPU.|Sin derechos de administrador faltan las temperaturas de la CPU.|Sem direitos de administrador faltam as temperaturas da CPU.
Settings stored in {0}|Einstellungen in {0}|Paramètres dans {0}|Impostazioni in {0}|Ajustes en {0}|Definições em {0}
Screen {0}|Bildschirm {0}|Écran {0}|Schermo {0}|Pantalla {0}|Ecrã {0}
A server on the internet, e.g. 1.1.1.1 or google.com.|Ein Server im Internet, z. B. 1.1.1.1 oder google.com.|Un serveur sur Internet, p. ex. 1.1.1.1 ou google.com.|Un server su Internet, ad es. 1.1.1.1 o google.com.|Un servidor en Internet, p. ej. 1.1.1.1 o google.com.|Um servidor na Internet, p. ex. 1.1.1.1 ou google.com.
CPU + GPU + {0} W for the rest (estimate)|CPU + GPU + {0} W für den Rest (geschätzt)|CPU + GPU + {0} W pour le reste (estimation)|CPU + GPU + {0} W per il resto (stima)|CPU + GPU + {0} W para el resto (estimado)|CPU + GPU + {0} W para o resto (estimativa)
Calendar|Kalender|Calendrier|Calendario|Calendario|Calendário
Choose cities in the settings.|Städte in den Einstellungen wählen.|Choisissez des villes dans les paramètres.|Scegli le città nelle impostazioni.|Elige ciudades en los ajustes.|Escolha cidades nas definições.
Column|Spalte|Colonne|Colonna|Columna|Coluna
Columns|Spalten|Colonnes|Colonne|Columnas|Colunas
Costs|Kosten|Coûts|Costi|Costes|Custos
Customise|Anpassen|Personnaliser|Personalizza|Personalizar|Personalizar
Data today|Daten heute|Données aujourd'hui|Dati oggi|Datos hoy|Dados hoje
Data usage|Datenverbrauch|Consommation de données|Consumo dati|Consumo de datos|Consumo de dados
Jitter|Jitter|Gigue|Jitter|Jitter|Jitter
Loss|Verlust|Perte|Perdita|Pérdida|Perda
Minimal|Minimal|Minimal|Minimale|Mínimo|Mínimo
Note on the estimate|Hinweis zur Schätzung|Note sur l'estimation|Nota sulla stima|Nota sobre la estimación|Nota sobre a estimativa
Notes|Notizen|Notes|Note|Notas|Notas
Nothing is playing.|Gerade läuft nichts.|Rien en cours de lecture.|Nessuna riproduzione.|No se está reproduciendo nada.|Nada a tocar.
Now playing|Läuft gerade|En cours de lecture|In riproduzione|Reproduciendo|A tocar
Power and cost|Strom und Kosten|Énergie et coût|Energia e costi|Energía y coste|Energia e custo
Price per kWh|Preis pro kWh|Prix par kWh|Prezzo per kWh|Precio por kWh|Preço por kWh
Processes|Prozesse|Processus|Processi|Procesos|Processos
Rest of the PC|Rest des PCs|Reste du PC|Resto del PC|Resto del PC|Resto do PC
Ring|Ring|Anneau|Anello|Anillo|Anel
Router|Router|Routeur|Router|Router|Router
Screen, board, drives and fans are not measured; this estimate is added.|Bildschirm, Mainboard, Laufwerke und Lüfter werden nicht gemessen; dieser Schätzwert wird addiert.|Écran, carte mère, disques et ventilateurs ne sont pas mesurés ; cette estimation est ajoutée.|Schermo, scheda madre, unità e ventole non sono misurati; questa stima viene aggiunta.|Pantalla, placa, unidades y ventiladores no se miden; se suma esta estimación.|Ecrã, placa, unidades e ventoinhas não são medidos; esta estimativa é somada.
Standard|Standard|Standard|Standard|Estándar|Padrão
Started|Gestartet|Démarré|Avviato|Iniciado|Iniciado
Storage space|Speicherplatz|Espace disque|Spazio su disco|Espacio en disco|Espaço em disco
Style|Stil|Style|Stile|Estilo|Estilo
Target|Ziel|Cible|Destinazione|Destino|Destino
This month|Monat|Mois|Mese|Mes|Mês
This week|Woche|Semaine|Settimana|Semana|Semana
Threads|Threads|Threads|Thread|Hilos|Threads
Two columns side by side: twice as wide, everything stays large.|Zwei Spalten nebeneinander: doppelt so breit, alles bleibt gross.|Deux colonnes côte à côte : deux fois plus large, tout reste grand.|Due colonne affiancate: larghezza doppia, tutto resta grande.|Dos columnas lado a lado: el doble de ancho, todo sigue grande.|Duas colunas lado a lado: o dobro da largura, tudo fica grande.
Up to four cities.|Bis zu vier Städte.|Jusqu'à quatre villes.|Fino a quattro città.|Hasta cuatro ciudades.|Até quatro cidades.
Uptime|Laufzeit|Temps de fonctionnement|Tempo di attività|Tiempo activo|Tempo ligado
Used of total|Belegt von gesamt|Utilisé sur total|Usato su totale|Usado del total|Usado do total
World clocks|Weltuhren|Horloges mondiales|Orologi mondiali|Relojes mundiales|Relógios mundiais
Write a note…|Notiz schreiben…|Écrire une note…|Scrivi una nota…|Escribe una nota…|Escreva uma nota…
today|heute|aujourd'hui|oggi|hoy|hoje
tomorrow|morgen|demain|domani|mañana|amanhã
yesterday|gestern|hier|ieri|ayer|ontem
{0} free|{0} frei|{0} libres|{0} liberi|{0} libres|{0} livres
{0} of {1} used|{0} von {1} belegt|{0} sur {1} utilisés|{0} di {1} usati|{0} de {1} usados|{0} de {1} usados
Hardware names|Hardware-Namen|Noms du matériel|Nomi hardware|Nombres del hardware|Nomes do hardware
Model names under the titles, e.g. Ryzen 9 5900X.|Modellnamen unter den Titeln, z. B. Ryzen 9 5900X.|Noms des modèles sous les titres, p. ex. Ryzen 9 5900X.|Nomi dei modelli sotto i titoli, ad es. Ryzen 9 5900X.|Nombres de modelo bajo los títulos, p. ej. Ryzen 9 5900X.|Nomes dos modelos sob os títulos, p. ex. Ryzen 9 5900X.
Sensors|Sensoren|Capteurs|Sensori|Sensores|Sensores
Show all sensors|Alle Sensoren anzeigen|Afficher tous les capteurs|Mostra tutti i sensori|Mostrar todos los sensores|Mostrar todos os sensores
Lists everything LibreHardwareMonitor and Open Hardware Monitor found.|Listet alles, was LibreHardwareMonitor und Open Hardware Monitor gefunden haben.|Liste tout ce que LibreHardwareMonitor et Open Hardware Monitor ont trouvé.|Elenca tutto ciò che LibreHardwareMonitor e Open Hardware Monitor hanno trovato.|Muestra todo lo que encontraron LibreHardwareMonitor y Open Hardware Monitor.|Lista tudo o que o LibreHardwareMonitor e o Open Hardware Monitor encontraram.
Everything LibreHardwareMonitor and Open Hardware Monitor found on this PC. If a value is missing in the sidebar, send this list.|Alles, was LibreHardwareMonitor und Open Hardware Monitor auf diesem PC gefunden haben. Fehlt ein Wert in der Sidebar, schick diese Liste.|Tout ce que LibreHardwareMonitor et Open Hardware Monitor ont trouvé sur ce PC. Si une valeur manque, envoyez cette liste.|Tutto ciò che LibreHardwareMonitor e Open Hardware Monitor hanno trovato su questo PC. Se manca un valore, invia questo elenco.|Todo lo que LibreHardwareMonitor y Open Hardware Monitor encontraron en este PC. Si falta un valor, envía esta lista.|Tudo o que o LibreHardwareMonitor e o Open Hardware Monitor encontraram neste PC. Se faltar um valor, envie esta lista.
Refresh|Aktualisieren|Actualiser|Aggiorna|Actualizar|Atualizar
Copy|Kopieren|Copier|Copia|Copiar|Copiar
Copied.|Kopiert.|Copié.|Copiato.|Copiado.|Copiado.
Save as file|Als Datei speichern|Enregistrer dans un fichier|Salva come file|Guardar como archivo|Guardar como ficheiro
Saved: {0}|Gespeichert: {0}|Enregistré : {0}|Salvato: {0}|Guardado: {0}|Guardado: {0}
Shown in the sidebar|In der Sidebar angezeigt|Affiché dans la barre|Mostrato nella barra|Mostrado en la barra|Mostrado na barra
Smooth transitions|Sanfte Übergänge|Transitions douces|Transizioni fluide|Transiciones suaves|Transições suaves
Numbers count, bars glide and charts scroll smoothly.|Zahlen zählen weich, Balken gleiten und Kurven scrollen gleichmässig.|Les chiffres défilent, les barres glissent et les courbes défilent en douceur.|I numeri scorrono, le barre scivolano e i grafici scorrono in modo fluido.|Los números cuentan, las barras se deslizan y las curvas se desplazan con suavidad.|Os números contam, as barras deslizam e os gráficos deslizam suavemente.
1 % low|1 % Low|1 % bas|1 % minimo|1 % mínimo|1 % mínimo
Frame time|Frametime|Temps d'image|Tempo frame|Tiempo de fotograma|Tempo de frame
App|Programm|Application|App|Aplicación|Aplicação
No game in the foreground.|Kein Spiel im Vordergrund.|Aucun jeu au premier plan.|Nessun gioco in primo piano.|Ningún juego en primer plano.|Nenhum jogo em primeiro plano.
FPS (games)|FPS (Spiele)|FPS (jeux)|FPS (giochi)|FPS (juegos)|FPS (jogos)
Click-through|Durchklickbar|Clics traversants|Clic passante|Clic a través|Clique através
Clicks go to the windows behind the sidebar; open the settings from the tray icon.|Klicks gehen an die Fenster dahinter; Einstellungen dann über das Tray-Symbol öffnen.|Les clics vont aux fenêtres derrière ; ouvrez les paramètres via l'icône de la barre des tâches.|I clic vanno alle finestre dietro; apri le impostazioni dall'icona nella barra.|Los clics pasan a las ventanas de detrás; abre los ajustes desde el icono de la bandeja.|Os cliques vão para as janelas atrás; abra as definições pelo ícone da bandeja.
Names|Namen|Noms|Nomi|Nombres|Nomes
Give fans and temperature probes your own names, or switch single readings off.|Gib Lüftern und Temperaturfühlern eigene Namen oder schalte einzelne Werte aus.|Nommez vous-même ventilateurs et sondes, ou masquez certaines valeurs.|Dai nomi propri a ventole e sonde, o disattiva singoli valori.|Pon nombres propios a ventiladores y sondas, o desactiva valores sueltos.|Dê nomes próprios a ventoinhas e sondas, ou desligue valores isolados.
No fans or probes found yet.|Noch keine Lüfter oder Fühler gefunden.|Aucun ventilateur ni sonde trouvé pour l'instant.|Nessuna ventola o sonda trovata finora.|Aún no se encontraron ventiladores ni sondas.|Ainda não foram encontradas ventoinhas nem sondas.
FPS need administrator rights.|FPS brauchen Administratorrechte.|Les FPS nécessitent les droits administrateur.|Gli FPS richiedono i diritti di amministratore.|Los FPS requieren derechos de administrador.|Os FPS precisam de direitos de administrador.
Several columns side by side; each is as wide as set below, everything stays large.|Mehrere Spalten nebeneinander; jede so breit wie unten eingestellt, alles bleibt gross.|Plusieurs colonnes côte à côte ; chacune aussi large que réglé plus bas, tout reste grand.|Più colonne affiancate; ognuna larga come impostato sotto, tutto resta grande.|Varias columnas lado a lado; cada una tan ancha como se ajusta abajo, todo sigue grande.|Várias colunas lado a lado; cada uma tão larga como definido abaixo, tudo fica grande.
Registers|Register|Onglets|Schede|Pestañas|Separadores
Register|Register|Onglet|Scheda|Pestaña|Separador
Pages at the top; each widget goes on one of them. Switch by click or mouse wheel.|Seiten oben; jedes Widget liegt auf einer davon. Umschalten per Klick oder Mausrad.|Pages en haut ; chaque widget est sur l'une d'elles. Changez par clic ou molette.|Pagine in alto; ogni widget sta su una di esse. Cambia con clic o rotella.|Páginas arriba; cada widget va en una. Cambia con clic o rueda del ratón.|Páginas no topo; cada widget fica numa delas. Mude com clique ou roda do rato.
Layout|Anordnung|Disposition|Disposizione|Disposición|Disposição
Weather and clocks|Wetter und Uhren|Météo et horloges|Meteo e orologi|Tiempo y relojes|Tempo e relógios
Power and ping|Strom und Ping|Énergie et ping|Energia e ping|Energía y ping|Energia e ping
Tick what the sidebar shows and order it with the arrows; Customise sets style, column, register and single values.|Anhaken, was die Sidebar zeigt, mit den Pfeilen sortieren; „Anpassen“ legt Stil, Spalte, Register und einzelne Werte fest.|Cochez ce que la barre affiche, triez avec les flèches ; « Personnaliser » règle style, colonne, page et valeurs.|Spunta ciò che la barra mostra, ordina con le frecce; «Personalizza» imposta stile, colonna, pagina e valori.|Marca lo que muestra la barra y ordénalo con las flechas; «Personalizar» define estilo, columna, página y valores.|Marque o que a barra mostra e ordene com as setas; «Personalizar» define estilo, coluna, página e valores.
Column {0}|Spalte {0}|Colonne {0}|Colonna {0}|Columna {0}|Coluna {0}
Cyan|Cyan|Cyan|Ciano|Cian|Ciano
Teal|Petrol|Sarcelle|Verde acqua|Verde azulado|Verde-azulado
Lime|Limette|Citron vert|Lime|Lima|Lima
Gold|Gold|Or|Oro|Oro|Ouro
Red|Rot|Rouge|Rosso|Rojo|Vermelho
Rose|Rosé|Rose|Rosa antico|Rosa palo|Rosé
Magenta|Magenta|Magenta|Magenta|Magenta|Magenta
Indigo|Indigo|Indigo|Indaco|Índigo|Índigo
Silver|Silber|Argent|Argento|Plata|Prata
Background|Hintergrund|Arrière-plan|Sfondo|Fondo|Fundo
Tone of the sidebar and the cards.|Farbton der Sidebar und der Karten.|Teinte de la barre et des cartes.|Tonalità della barra e delle schede.|Tono de la barra y las tarjetas.|Tom da barra e dos cartões.
Graphite|Graphit|Graphite|Grafite|Grafito|Grafite
Black (OLED)|Schwarz (OLED)|Noir (OLED)|Nero (OLED)|Negro (OLED)|Preto (OLED)
Midnight blue|Nachtblau|Bleu nuit|Blu notte|Azul noche|Azul-noite
Deep sea|Tiefsee|Grand bleu|Abisso|Mar profundo|Mar profundo
Forest|Wald|Forêt|Foresta|Bosque|Floresta
Aubergine|Aubergine|Aubergine|Melanzana|Berenjena|Beringela
Burgundy|Bordeaux|Bordeaux|Bordeaux|Burdeos|Bordô
Mocha|Mokka|Moka|Moka|Moca|Moca
Slate|Schiefer|Ardoise|Ardesia|Pizarra|Ardósia
Bar|Balken|Barre|Barra|Barra|Barra
Graph|Kurve|Courbe|Grafico|Gráfica|Gráfico
Tile|Kachel|Tuile|Riquadro|Mosaico|Mosaico
Top (bar)|Oben (Leiste)|En haut (barre)|In alto (barra)|Arriba (barra)|Em cima (barra)
Bottom (bar)|Unten (Leiste)|En bas (barre)|In basso (barra)|Abajo (barra)|Em baixo (barra)
A bar across the whole width; the widgets sit side by side.|Eine Leiste über die ganze Breite; die Widgets stehen nebeneinander.|Une barre sur toute la largeur ; les widgets côte à côte.|Una barra su tutta la larghezza; i widget affiancati.|Una barra a todo el ancho; los widgets uno al lado del otro.|Uma barra em toda a largura; os widgets lado a lado.
Bar height|Leistenhöhe|Hauteur de la barre|Altezza barra|Altura de la barra|Altura da barra
Ring, Tile and Bar styles read best in a low bar.|In einer niedrigen Leiste lesen sich die Stile Ring, Kachel und Balken am besten.|Dans une barre basse, les styles Anneau, Tuile et Barre se lisent le mieux.|In una barra bassa si leggono meglio Anello, Riquadro e Barra.|En una barra baja se leen mejor Anillo, Mosaico y Barra.|Numa barra baixa leem-se melhor Anel, Mosaico e Barra.
Bar at the top or bottom: the widgets sit side by side.|Leiste oben oder unten: Die Widgets stehen nebeneinander.|Barre en haut ou en bas : les widgets côte à côte.|Barra in alto o in basso: i widget affiancati.|Barra arriba o abajo: los widgets uno al lado del otro.|Barra em cima ou em baixo: os widgets lado a lado.
Whole width|Ganze Breite|Toute la largeur|Tutta la larghezza|Todo el ancho|Toda a largura
In the bar, Compact makes the widgets smaller and tighter.|In der Leiste macht Kompakt die Widgets kleiner und enger.|Dans la barre, Compact rend les widgets plus petits et serrés.|Nella barra, Compatto rende i widget più piccoli e stretti.|En la barra, Compacto hace los widgets más pequeños y juntos.|Na barra, Compacto torna os widgets menores e mais juntos.
Widget width|Widget-Breite|Largeur des widgets|Larghezza widget|Ancho de widget|Largura do widget
Rows|Reihen|Rangées|Righe|Filas|Linhas
Row|Reihe|Rangée|Riga|Fila|Linha
Row {0}|Reihe {0}|Rangée {0}|Riga {0}|Fila {0}|Linha {0}
Row height|Reihenhöhe|Hauteur de rangée|Altezza riga|Altura de fila|Altura da linha
Rows one below the other, like columns at the side; each is as tall as the row height.|Reihen untereinander, wie Spalten am Rand; jede ist so hoch wie die Reihenhöhe.|Rangées l'une sous l'autre, comme les colonnes sur le côté ; chacune a la hauteur de rangée.|Righe una sotto l'altra, come le colonne ai lati; ognuna alta quanto l'altezza riga.|Filas una debajo de otra, como las columnas al lado; cada una tan alta como la altura de fila.|Linhas umas por baixo das outras, como colunas ao lado; cada uma com a altura da linha.
Off: the bar is only as wide as its widgets, centred.|Aus: Die Leiste ist nur so breit wie ihre Widgets und steht in der Mitte.|Désactivé : la barre a la largeur de ses widgets, centrée.|Disattivato: la barra è larga quanto i widget, centrata.|Desactivado: la barra solo ocupa el ancho de sus widgets, centrada.|Desligado: a barra só tem a largura dos widgets, centrada.
Above 100 % the sidebar grows wider by the same amount.|Über 100 % wird die Sidebar im gleichen Mass breiter.|Au-delà de 100 %, la barre s'élargit d'autant.|Oltre il 100 % la barra si allarga in proporzione.|Por encima del 100 %, la barra se ensancha en la misma medida.|Acima de 100 %, a barra alarga na mesma medida.
Nothing on this register yet – choose its widgets in the settings under Widgets → Customise → Register.|Noch nichts auf diesem Register – Widgets dafür in den Einstellungen unter Widgets → Anpassen → Register wählen.|Rien sur cette page pour l'instant – choisissez ses widgets dans les réglages sous Widgets → Personnaliser → Page.|Ancora niente in questa pagina – scegli i widget nelle impostazioni in Widget → Personalizza → Pagina.|Aún no hay nada en esta página – elige sus widgets en los ajustes, en Widgets → Personalizar → Página.|Ainda nada nesta página – escolha os widgets nas definições em Widgets → Personalizar → Página.
Register names|Register-Namen|Noms des pages|Nomi delle pagine|Nombres de páginas|Nomes das páginas
Shown at the top instead of 1, 2, 3; leave empty for the number.|Steht oben statt 1, 2, 3; leer lassen für die Nummer.|Affiché en haut au lieu de 1, 2, 3 ; vide = numéro.|Mostrato in alto al posto di 1, 2, 3; vuoto = numero.|Se muestra arriba en lugar de 1, 2, 3; vacío = número.|Mostrado no topo em vez de 1, 2, 3; vazio = número.
Whole screen|Ganzer Bildschirm|Plein écran|Schermo intero|Pantalla completa|Ecrã inteiro
Covers the whole monitor up to the taskbar; the columns share its width.|Füllt den ganzen Monitor bis zur Taskleiste; die Spalten teilen sich die Breite.|Couvre tout l'écran jusqu'à la barre des tâches ; les colonnes se partagent la largeur.|Copre tutto il monitor fino alla barra delle applicazioni; le colonne si dividono la larghezza.|Cubre todo el monitor hasta la barra de tareas; las columnas se reparten el ancho.|Cobre todo o monitor até à barra de tarefas; as colunas partilham a largura.
""";

    private static readonly Dictionary<string, string[]> Rows = Parse();
    public static string Language { get; private set; } = "en";
    public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("en-GB");
    public static IReadOnlyDictionary<string, string[]> All => Rows;

    private static readonly string[] Codes = { "de", "fr", "it", "es", "pt" };
    private static readonly Dictionary<string, string> Cultures = new() { ["en"] = "en-GB", ["de"] = "de-CH", ["fr"] = "fr-FR", ["it"] = "it-IT", ["es"] = "es-ES", ["pt"] = "pt-PT" };

    public static readonly (string Code, string Name)[] Choices =
        { ("de", "Deutsch"), ("en", "English"), ("fr", "Français"), ("it", "Italiano"), ("es", "Español"), ("pt", "Português") };

    private static Dictionary<string, string[]> Parse()
    {
        var rows = new Dictionary<string, string[]>();
        foreach (var line in Table.Split('\n'))
        {
            var parts = line.TrimEnd('\r').Split('|');
            if (parts.Length == 6) rows[parts[0]] = parts[1..];
        }
        return rows;
    }

    // "auto" follows the Windows display language, falling back to English.
    public static void Use(string setting)
    {
        string code = setting;
        if (!Lumen.Core.Settings.Languages.Contains(code))
        {
            code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            if (!Lumen.Core.Settings.Languages.Contains(code)) code = "en";
        }
        Language = code;
        Culture = CultureInfo.GetCultureInfo(Cultures[code]);
    }

    public static string T(string key)
    {
        int i = Array.IndexOf(Codes, Language);
        return i >= 0 && Rows.TryGetValue(key, out var row) && row[i].Length > 0 ? row[i] : key;
    }

    public static string F(string key, params object[] args) => string.Format(Culture, T(key), args);
}
