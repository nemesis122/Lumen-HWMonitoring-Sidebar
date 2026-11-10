using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Lumen.Core;

namespace Lumen.App;

public static class L
{
	private const string Table = """
    Drive order|Laufwerksreihenfolge|Ordre des disques|Ordine delle unità|Orden de unidades|Ordem das unidades
    C: first|C: zuerst|C: en premier|C: prima|C: primero|C: primeiro
    Drive letter A–Z|Laufwerksbuchstabe A–Z|Lettre du lecteur A–Z|Lettera unità A–Z|Letra de unidad A–Z|Letra da unidade A–Z
    Detection order|Erkennungsreihenfolge|Ordre de détection|Ordine di rilevamento|Orden de detección|Ordem de deteção
    Recommended selection|Empfohlene Auswahl|Sélection recommandée|Selezione consigliata|Selección recomendada|Seleção recomendada
    Select the essential values for this widget. Additional values remain individually selectable.|Wählt die wichtigsten Werte für dieses Widget aus. Weitere Werte bleiben einzeln auswählbar.|Sélectionne les valeurs essentielles ; les autres restent disponibles.|Seleziona i valori essenziali; gli altri restano selezionabili.|Selecciona los valores esenciales; los demás siguen disponibles.|Seleciona os valores essenciais; os restantes continuam disponíveis.
    Not available|Nicht verfügbar|Indisponible|Non disponibile|No disponible|Indisponível
    Nord|Nord|Nord|Nord|Nord|Nord
    Emerald|Smaragd|Émeraude|Smeraldo|Esmeralda|Esmeralda
    Copper|Kupfer|Cuivre|Rame|Cobre|Cobre
    Plum|Pflaume|Prune|Prugna|Ciruela|Ameixa
    Eight themes with coordinated colours, backgrounds and fonts.|Acht Themes mit abgestimmten Farben, Hintergründen und Schriften.|Huit thèmes aux couleurs, arrière-plans et polices assortis.|Otto temi con colori, sfondi e caratteri coordinati.|Ocho temas con colores, fondos y fuentes coordinados.|Oito temas com cores, fundos e fontes coordenados.
    Clarity|Klartext|Clarté|Chiarezza|Claridad|Clareza
    ClarityColor|Klartext Farbe|Clarté couleur|Chiarezza colori|Claridad color|Clareza cor
    ClarityRows|Klartext Zeilen|Clarté lignes|Chiarezza righe|Claridad filas|Clareza linhas
    Accent colour|Akzentfarbe|Couleur d’accent|Colore d’accento|Color de acento|Cor de destaque
    Amber|Bernstein|Ambre|Ambra|Ámbar|Âmbar
    Floating|Schwebend|Flottant|Fluttuante|Flotante|Flutuante
    Single cards with a gap and a shadow instead of a continuous bar; no space is reserved.|Einzelne Karten mit Abstand und Schatten statt einer durchgehenden Leiste; es wird kein Platz reserviert.|Cartes séparées avec espace et ombre au lieu d’une barre continue ; aucun espace n’est réservé.|Schede separate con spazio e ombra invece di una barra continua; nessuno spazio riservato.|Tarjetas separadas con espacio y sombra en lugar de una barra continua; no se reserva espacio.|Cartões separados com espaço e sombra em vez de uma barra contínua; nenhum espaço é reservado.
    Hide in full screen|Im Vollbild ausblenden|Masquer en plein écran|Nascondi a schermo intero|Ocultar en pantalla completa|Ocultar em ecrã inteiro
    Hides the sidebar while a game or video fills the monitor it is on.|Blendet die Sidebar aus, solange ein Spiel oder Video ihren Monitor ganz ausfüllt.|Masque la barre tant qu’un jeu ou une vidéo remplit son écran.|Nasconde la barra finché un gioco o un video riempie il suo monitor.|Oculta la barra mientras un juego o vídeo ocupa todo su monitor.|Oculta a barra enquanto um jogo ou vídeo preenche o seu monitor.
    Off|Aus|Arrêt|Spento|Apagado|Desligado
    Click for the last 10 minutes|Klicken für die letzten 10 Minuten|Cliquer pour les 10 dernières minutes|Clic per gli ultimi 10 minuti|Clic para los últimos 10 minutos|Clique para os últimos 10 minutos
    10 minutes|10 Minuten|10 minutes|10 minuti|10 minutos|10 minutos
    min.|min.|min.|min.|mín.|mín.
    max.|max.|max.|max.|máx.|máx.
    Reset positions|Positionen zurücksetzen|Réinitialiser les positions|Ripristina posizioni|Restablecer posiciones|Repor posições
    Card positions|Kartenpositionen|Positions des cartes|Posizioni delle schede|Posiciones de las tarjetas|Posições dos cartões
    Hold Ctrl and drag a card to place it anywhere on the monitor. Clicks between the cards go to the windows behind.|Strg halten und eine Karte ziehen, um sie frei auf dem Monitor zu platzieren. Klicks zwischen den Karten gehen an die Fenster dahinter.|Maintenez Ctrl et faites glisser une carte pour la placer n’importe où. Les clics entre les cartes vont aux fenêtres derrière.|Tieni premuto Ctrl e trascina una scheda per posizionarla ovunque. I clic tra le schede vanno alle finestre dietro.|Mantén Ctrl y arrastra una tarjeta para colocarla donde quieras. Los clics entre tarjetas van a las ventanas de detrás.|Mantenha Ctrl e arraste um cartão para o colocar em qualquer lugar. Os cliques entre cartões vão para as janelas atrás.
    Average clock|Ø Takt|Fréquence moy.|Clock medio|Reloj medio|Relógio médio
    Core voltage|Kernspannung|Tension cœur|Tensione core|Voltaje núcleo|Tensão núcleo
    Load per thread|Last pro Thread|Charge par thread|Carico per thread|Carga por hilo|Carga por thread
    Video memory|Videospeicher|Mémoire vidéo|Memoria video|Memoria de vídeo|Memória de vídeo
    Core clock|Kerntakt|Fréquence cœur|Clock core|Reloj núcleo|Relógio núcleo
    Connection|Verbindung|Connexion|Connessione|Conexión|Ligação
    Working well|Läuft gut|Fonctionne bien|Funziona bene|Funciona bien|A funcionar bem
    Unstable|Instabil|Instable|Instabile|Inestable|Instável
    Packet loss|Paketverlust|Perte de paquets|Perdita pacchetti|Pérdida de paquetes|Perda de pacotes
    Connection quality|Verbindungsqualität|Qualité de connexion|Qualità connessione|Calidad de conexión|Qualidade da ligação
    Ping|Ping|Ping|Ping|Ping|Ping
    Next drive|Nächstes Laufwerk|Disque suivant|Unità successiva|Siguiente unidad|Próxima unidade
    Used space|Belegter Platz|Espace utilisé|Spazio usato|Espacio usado|Espaço usado
    Read activity|Leseaktivität|Activité lecture|Attività lettura|Actividad lectura|Atividade leitura
    Write activity|Schreibaktivität|Activité écriture|Attività scrittura|Actividad escritura|Atividade escrita
    Total activity|Gesamtaktivität|Activité totale|Attività totale|Actividad total|Atividade total
    Total space|Gesamtplatz|Espace total|Spazio totale|Espacio total|Espaço total
    Read rate|Leserate|Débit lecture|Velocità lettura|Velocidad lectura|Taxa leitura
    Write rate|Schreibrate|Débit écriture|Velocità scrittura|Velocidad escritura|Taxa escrita
    Power-on count|Einschaltvorgänge|Mises sous tension|Accensioni|Encendidos|Ligações
    Power-on hours|Betriebsstunden|Heures de fonctionnement|Ore di funzionamento|Horas de uso|Horas de uso
    Life left|Restlebensdauer|Durée de vie restante|Vita residua|Vida restante|Vida restante
    Connected by|Verbunden über|Connecté par|Connesso tramite|Conectado por|Ligado por
    On|Ein|Activé|Attivo|Activado|Ligado
    WLAN|WLAN|Wi-Fi|Wi-Fi|Wi-Fi|Wi-Fi
    Ethernet|Ethernet|Ethernet|Ethernet|Ethernet|Ethernet
    Memory junction|VRAM-Temp.|Temp. VRAM|Temp. VRAM|Temp. VRAM|Temp. VRAM
    History graph|Verlauf|Historique|Andamento|Historial|Histórico
    Double ring|Doppelring|Double anneau|Doppio anello|Doble anillo|Anel duplo
    Level columns|Säulen|Colonnes de niveau|Colonne|Columnas|Colunas
    Overview|Übersicht|Vue d’ensemble|Panoramica|Resumen|Visão geral
    Drive health|Laufwerksgesundheit|Santé des disques|Salute dischi|Salud de unidades|Saúde dos discos
    started|gestartet|démarrages|avvii|arranques|arranques
    This drive reports no health values.|Dieses Laufwerk meldet keine Gesundheitswerte.|Ce disque ne fournit aucune valeur de santé.|Questa unità non fornisce valori di salute.|Esta unidad no informa valores de salud.|Este disco não indica valores de saúde.
    No drives found.|Keine Laufwerke gefunden.|Aucun disque trouvé.|Nessuna unità trovata.|No se encontraron unidades.|Nenhum disco encontrado.
    All good|Alles gut|Tout va bien|Tutto bene|Todo bien|Tudo bem
    {0} to check|{0} prüfen|{0} à vérifier|{0} da controllare|{0} por revisar|{0} a verificar
    Critical|Kritisch|Critique|Critico|Crítico|Crítico
    Check|Prüfen|À vérifier|Da controllare|Revisar|Verificar
    Good|Gut|Bon|Buono|Bueno|Bom
    Timer|Timer|Minuteur|Timer|Temporizador|Temporizador
    Click to start or pause|Klicken zum Starten oder Anhalten|Cliquer pour démarrer ou mettre en pause|Clic per avviare o mettere in pausa|Clic para iniciar o pausar|Clique para iniciar ou pausar
    Time is up|Zeit ist um|Temps écoulé|Tempo scaduto|Se acabó el tiempo|Tempo esgotado
    Running|Läuft|En cours|In corso|En marcha|A decorrer
    Ready|Bereit|Prêt|Pronto|Listo|Pronto
    Paused|Pausiert|En pause|In pausa|En pausa|Em pausa
    Neon tiles|Neon-Kacheln|Tuiles néon|Riquadri neon|Mosaicos neón|Mosaicos néon
    Thermostat|Regler|Thermostat|Termostato|Termostato|Termóstato
    Purple dashboard|Lila-Dashboard|Tableau violet|Dashboard viola|Panel morado|Painel roxo
    Capsule row graph|Kapselreihe-Graph|Graphe en rangée de capsules|Grafico a capsule in fila|Gráfico de cápsulas en fila|Gráfico de cápsulas em fila
    List graph|Liste-Graph|Graphe en liste|Grafico a elenco|Gráfico de lista|Gráfico de lista
    Pure widgets|Pure-Widgets|Widgets Pure|Widget Pure|Widgets Pure|Widgets Pure
    Neon HUD graph|Neon-HUD-Graph|Graphe HUD néon|Grafico HUD neon|Gráfico HUD neón|Gráfico HUD néon
    Fill bars graph|Füllbalken-Graph|Graphe à barres pleines|Grafico a barre piene|Gráfico de barras llenas|Gráfico de barras cheias
    Table graph|Tabelle-Graph|Graphe en tableau|Grafico a tabella|Gráfico de tabla|Gráfico em tabela
    Segment graph|Segment-Graph|Graphe à segments|Grafico a segmenti|Gráfico de segmentos|Gráfico de segmentos
    Purple list|Lila-Liste|Liste violette|Lista viola|Lista morada|Lista roxa
    Evolution|Evolution|Évolution|Evoluzione|Evolución|Evolução
    Hottest|Wärmstes|Le plus chaud|Il più caldo|El más caliente|O mais quente
    Speed|Drehzahl|Vitesse|Velocità|Velocidad|Velocidade
    Used|Belegt|Utilisé|Usata|Usado|Usado
    Total|Gesamt|Total|Totale|Total|Total
    Clock|Takt|Fréquence|Frequenza|Frecuencia|Frequência
    Avail.|Frei|Libre|Libera|Libre|Livre
    Peak 2m|Spitze 2 Min.|Pic 2 min|Picco 2 min|Pico 2 min|Pico 2 min
    DL max|DL max|DL max|DL max|DL máx.|DL máx.
    Link|Verbindung|Liaison|Collegamento|Enlace|Link
    Traffic|Auslastung|Trafic|Traffico|Tráfico|Tráfego
    Choose what the sidebar shows, in which design and in which order.|Was die Sidebar zeigt, in welchem Design und in welcher Reihenfolge.|Ce que la barre affiche, dans quel design et dans quel ordre.|Cosa mostra la barra, con quale design e in quale ordine.|Qué muestra la barra, con qué diseño y en qué orden.|O que a barra mostra, com que design e em que ordem.
    Colours, effects, text and themes.|Farben, Effekte, Schrift und Themes.|Couleurs, effets, texte et thèmes.|Colori, effetti, testo e temi.|Colores, efectos, texto y temas.|Cores, efeitos, texto e temas.
    Screen edge, monitor, columns and registers.|Bildschirmrand, Monitor, Spalten und Register.|Bord d’écran, moniteur, colonnes et onglets.|Bordo dello schermo, monitor, colonne e schede.|Borde de pantalla, monitor, columnas y pestañas.|Margem do ecrã, monitor, colunas e separadores.
    Where the readings come from and how often they update.|Woher die Messwerte kommen und wie oft sie aktualisiert werden.|D’où viennent les mesures et à quelle fréquence elles se mettent à jour.|Da dove arrivano le misure e ogni quanto si aggiornano.|De dónde vienen las lecturas y cada cuánto se actualizan.|De onde vêm as leituras e com que frequência são atualizadas.
    Weather, world clocks, power costs and ping.|Wetter, Weltuhren, Stromkosten und Ping.|Météo, horloges mondiales, coût de l’énergie et ping.|Meteo, orologi mondiali, costi energia e ping.|Tiempo, relojes mundiales, coste de energía y ping.|Meteorologia, relógios mundiais, custo de energia e ping.
    Language, autostart, warnings and diagnostics.|Sprache, Autostart, Warnungen und Diagnose.|Langue, démarrage automatique, alertes et diagnostic.|Lingua, avvio automatico, avvisi e diagnostica.|Idioma, inicio automático, avisos y diagnóstico.|Idioma, arranque automático, avisos e diagnóstico.
    Ctrl+F searches, Esc closes.|Strg+F sucht, Esc schließt.|Ctrl+F recherche, Échap ferme.|Ctrl+F cerca, Esc chiude.|Ctrl+F busca, Esc cierra.|Ctrl+F procura, Esc fecha.
    ClarityTiles|Klartext Kacheln|Clarté tuiles|Chiarezza riquadri|Claridad mosaicos|Clareza blocos
    ControlDeck|Leitstand|Poste de contrôle|Postazione|Centro de control|Central de controle
    Duplex|Doppelspur|Double voie|Doppia traccia|Doble pista|Dupla via
    SignalRows|Signalzeilen|Signaux en ligne|Righe segnale|Filas de señal|Linhas de sinal
    OpenPanel|Offenes Panel|Panneau ouvert|Pannello aperto|Panel abierto|Painel aberto
    Graphic size|Grafikgröße|Taille des graphiques|Dimensione grafici|Tamaño de gráficos|Tamanho dos gráficos
    Label size|Beschriftungsgröße|Taille des libellés|Dimensione etichette|Tamaño de etiquetas|Tamanho dos rótulos
    Progress|Fortschritt|Progression|Avanzamento|Progreso|Progresso
    Application|Anwendung|Application|Applicazione|Aplicación|Aplicação
    Period|Zeitraum|Période|Periodo|Período|Período
    Cover|Coverbild|Pochette|Copertina|Portada|Capa
    Energy today|Energie heute|Énergie du jour|Energia oggi|Energía de hoy|Energia de hoje
    Charging state|Ladestatus|État de charge|Stato di carica|Estado de carga|Estado de carga
    Weather condition|Wetterzustand|Conditions météo|Condizioni meteo|Estado del tiempo|Condição do tempo
    Operating system|Betriebssystem|Système d’exploitation|Sistema operativo|Sistema operativo|Sistema operativo
    Artist|Interpret|Artiste|Artista|Artista|Artista
    VRAM used|VRAM belegt|VRAM utilisée|VRAM utilizzata|VRAM usada|VRAM utilizada
    VRAM total|VRAM gesamt|VRAM totale|VRAM totale|VRAM total|VRAM total
    Drive count|Laufwerksanzahl|Nombre de disques|Numero di unità|Número de unidades|Número de unidades
    Time difference|Zeitverschiebung|Décalage horaire|Differenza oraria|Diferencia horaria|Diferença horária
    Value size|Wertgröße (Liste)|Taille des valeurs (liste)|Dimensione valori (elenco)|Tamaño de valores (lista)|Tamanho dos valores (lista)
    Widget size|Widgetgröße|Taille du widget|Dimensione widget|Tamaño del widget|Tamanho do widget
    Sizes for all widgets|Größen für alle Widgets|Tailles pour tous les widgets|Dimensioni per tutti i widget|Tamaños para todos los widgets|Tamanhos para todos os widgets
    Sets every widget at once; each one can still be changed under Widgets → Customise.|Stellt alle Widgets auf einmal ein; jedes lässt sich weiterhin unter Widgets → Anpassen einzeln ändern.|Règle tous les widgets d'un coup ; chacun reste modifiable sous Widgets → Personnaliser.|Imposta tutti i widget insieme; ognuno resta modificabile in Widget → Personalizza.|Ajusta todos los widgets a la vez; cada uno se puede cambiar en Widgets → Personalizar.|Ajusta todos os widgets de uma vez; cada um continua editável em Widgets → Personalizar.
    Main value size|Hauptwertgröße|Taille de la valeur principale|Dimensione valore principale|Tamaño del valor principal|Tamanho do valor principal
    Sensor values|Sensorwerte|Valeurs des capteurs|Valori sensori|Valores de sensores|Valores dos sensores
    No sensor values for this widget.|Keine Sensorwerte für dieses Widget.|Aucune valeur pour ce widget.|Nessun valore per questo widget.|Sin valores para este widget.|Sem valores para este widget.
    Increase the row height for larger values and graphics.|Für größere Werte und Grafiken die Zeilenhöhe erhöhen.|Augmentez la hauteur pour des valeurs et graphiques plus grands.|Aumentare l’altezza per valori e grafici più grandi.|Aumenta la altura para valores y gráficos más grandes.|Aumente a altura para valores e gráficos maiores.
    Register {0}|Register {0}|Onglet {0}|Scheda {0}|Pestaña {0}|Separador {0}
    Register {0}: {1}|Register {0}: {1}|Onglet {0} : {1}|Scheda {0}: {1}|Pestaña {0}: {1}|Separador {0}: {1}
    Liquid|Liquid|Liquide|Liquido|Líquido|Líquido
    TwinFocus|Twin Focus|Twin Focus|Twin Focus|Twin Focus|Twin Focus
    Contour|Contour|Contour|Contorno|Contorno|Contorno
    SplitEdge|Split Edge|Split Edge|Split Edge|Split Edge|Split Edge
    Ribbon|Ribbon|Ruban|Nastro|Cinta|Fita
    CoreGrid|Core Grid|Grille de cœurs|Griglia core|Cuadrícula de núcleos|Grelha de núcleos
    Secondary|Zusatzwert|Valeur secondaire|Valore secondario|Valor secundario|Valor secundário
    Core {0}|Kern {0}|Cœur {0}|Core {0}|Núcleo {0}|Núcleo {0}
    {0} cores|{0} Kerne|{0} cœurs|{0} core|{0} núcleos|{0} núcleos
    No core readings|Keine Kerndaten|Aucune donnée par cœur|Nessun dato core|Sin datos por núcleo|Sem dados dos núcleos
    Core readings hidden|Kerndaten ausgeblendet|Données par cœur masquées|Dati core nascosti|Datos de núcleos ocultos|Dados dos núcleos ocultos
    No additional readings|Keine Zusatzwerte|Aucune valeur supplémentaire|Nessun valore aggiuntivo|Sin valores adicionales|Sem valores adicionais
    SignalPro|Signal Pro|Signal Pro|Signal Pro|Signal Pro|Signal Pro
    RailPro|Rail Pro|Rail Pro|Rail Pro|Rail Pro|Rail Pro
    OrbitPro|Orbit Pro|Orbit Pro|Orbit Pro|Orbit Pro|Orbit Pro
    Prism|Prisma|Prisme|Prisma|Prisma|Prisma
    Spectrum|Spektrum|Spectre|Spettro|Espectro|Espectro
    PillDuoPlus|Doppelkapsel Plus|Double capsule Plus|Doppia capsula Plus|Cápsula doble Plus|Cápsula dupla Plus
    Clock speed|Takt|Fréquence|Frequenza|Frecuencia|Frequência
    SignalBand|Signalband|Bande de signal|Banda segnale|Banda de señal|Faixa de sinal
    Rail|Rail|Rail|Binario|Raíl|Trilho
    Executable|Programmdatei|Fichier exécutable|File eseguibile|Archivo ejecutable|Ficheiro executável
    Open folder|Ordner öffnen|Ouvrir le dossier|Apri cartella|Abrir carpeta|Abrir pasta
    Copy path|Pfad kopieren|Copier le chemin|Copia percorso|Copiar ruta|Copiar caminho
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
    2-min peak|Spitze 2 Min.|Pic 2 min|Picco 2 min|Pico 2 min|Pico 2 min
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
    Theme|Modus|Thème|Tema|Tema|Tema
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
    Sensor source|Sensorquelle|Source des capteurs|Fonte sensori|Fuente de sensores|Fonte dos sensores
    Built-in|Eingebaut|Intégrée|Integrata|Integrada|Integrada
    LibreHardwareMonitor and Open Hardware Monitor, built into Lumen.|LibreHardwareMonitor und Open Hardware Monitor, in Lumen eingebaut.|LibreHardwareMonitor et Open Hardware Monitor, intégrés à Lumen.|LibreHardwareMonitor e Open Hardware Monitor, integrati in Lumen.|LibreHardwareMonitor y Open Hardware Monitor, integrados en Lumen.|LibreHardwareMonitor e Open Hardware Monitor, integrados no Lumen.
    In AIDA64: Preferences → Hardware Monitoring → External Applications → Enable shared memory.|In AIDA64: Einstellungen → Hardware Überwachung → Externe Applikationen → „Aktiviere Shared Memory“ und die gewünschten Werte anhaken.|Dans AIDA64 : Préférences → Surveillance matérielle → Applications externes → Activer la mémoire partagée.|In AIDA64: Preferenze → Monitoraggio hardware → Applicazioni esterne → Abilita memoria condivisa.|En AIDA64: Preferencias → Monitorización de hardware → Aplicaciones externas → Activar memoria compartida.|No AIDA64: Preferências → Monitorização de hardware → Aplicações externas → Ativar memória partilhada.
    In HWiNFO: Settings → Shared Memory Support (the free version turns it off after 12 hours).|In HWiNFO: Einstellungen → Shared Memory Support (die Gratisversion schaltet es nach 12 Stunden ab).|Dans HWiNFO : Paramètres → Shared Memory Support (la version gratuite le coupe après 12 heures).|In HWiNFO: Impostazioni → Shared Memory Support (la versione gratuita lo disattiva dopo 12 ore).|En HWiNFO: Ajustes → Shared Memory Support (la versión gratuita lo desactiva tras 12 horas).|No HWiNFO: Definições → Shared Memory Support (a versão gratuita desliga-o após 12 horas).
    Uses AIDA64 if it is running, otherwise HWiNFO, otherwise the built-in sensors.|Nimmt AIDA64, wenn es läuft, sonst HWiNFO, sonst die eingebauten Sensoren.|Utilise AIDA64 s'il tourne, sinon HWiNFO, sinon les capteurs intégrés.|Usa AIDA64 se è in esecuzione, altrimenti HWiNFO, altrimenti i sensori integrati.|Usa AIDA64 si está en marcha, si no HWiNFO, si no los sensores integrados.|Usa o AIDA64 se estiver a correr, senão o HWiNFO, senão os sensores integrados.
    Connected to {0}. Readings it does not have come from the built-in sensors.|Verbunden mit {0}. Was dort fehlt, kommt von den eingebauten Sensoren.|Connecté à {0}. Ce qui y manque vient des capteurs intégrés.|Connesso a {0}. Ciò che manca proviene dai sensori integrati.|Conectado a {0}. Lo que falte viene de los sensores integrados.|Ligado a {0}. O que faltar vem dos sensores integrados.
    Status|Statuszeile|Ligne d'état|Riga di stato|Línea de estado|Linha de estado
    Gauge|Halbkreis|Jauge|Indicatore|Indicador|Indicador
    Segments|Segmente|Segments|Segmenti|Segmentos|Segmentos
    DualRing|Doppelring|Double anneau|Doppio anello|Doble anillo|Anel duplo
    Temp|Temp|Temp|Temp|Temp|Temp
    Status fits from 40 px, Compact from 64 px; Ring, Tile and Bar read best in a low bar.|Statuszeile passt ab 40 px, Kompakt ab 64 px; Ring, Kachel und Balken wirken in einer niedrigen Leiste am besten.|Ligne d'état dès 40 px, Compact dès 64 px ; Anneau, Tuile et Barre conviennent à une barre basse.|Riga di stato da 40 px, Compatto da 64 px; Anello, Riquadro e Barra rendono al meglio in una barra bassa.|Línea de estado desde 40 px, Compacto desde 64 px; Anillo, Mosaico y Barra lucen mejor en una barra baja.|Linha de estado a partir de 40 px, Compacto a partir de 64 px; Anel, Mosaico e Barra ficam melhor numa barra baixa.
    Changed from the default|Vom Standard geändert|Modifié par rapport au standard|Modificato rispetto al predefinito|Cambiado respecto al estándar|Alterado em relação ao padrão
    Changed – click to reset|Geändert – klicken setzt zurück|Modifié – cliquer pour rétablir|Modificato – clic per ripristinare|Cambiado – clic para restablecer|Alterado – clique para repor
    Colour scheme|Farbschema|Palette de couleurs|Schema colori|Esquema de colores|Esquema de cores
    Drag to sort|Ziehen zum Sortieren|Glisser pour trier|Trascina per ordinare|Arrastrar para ordenar|Arrastar para ordenar
    Effects|Effekte|Effets|Effetti|Efectos|Efeitos
    Extras|Extras|Extras|Extra|Extras|Extras
    Hardware|Hardware|Matériel|Hardware|Hardware|Hardware
    Hidden|Ausgeblendet|Masqués|Nascosti|Ocultos|Ocultos
    Info|Infos|Infos|Info|Información|Informação
    Live preview|Live-Vorschau|Aperçu en direct|Anteprima dal vivo|Vista previa en vivo|Pré-visualização ao vivo
    No setting found.|Keine Einstellung gefunden.|Aucun réglage trouvé.|Nessuna impostazione trovata.|No se encontró ningún ajuste.|Nenhuma definição encontrada.
    Only with the glass effect.|Nur mit Glas-Effekt.|Uniquement avec l'effet verre.|Solo con l'effetto vetro.|Solo con el efecto cristal.|Apenas com o efeito vidro.
    Placement|Platzierung|Placement|Posizionamento|Ubicación|Posicionamento
    Reset all …|Alles zurücksetzen …|Tout réinitialiser …|Ripristina tutto …|Restablecer todo …|Repor tudo …
    Reset this page|Diese Seite zurücksetzen|Réinitialiser cette page|Ripristina questa pagina|Restablecer esta página|Repor esta página
    Results for “{0}”|Treffer für „{0}“|Résultats pour « {0} »|Risultati per «{0}»|Resultados para «{0}»|Resultados para «{0}»
    Search settings…|Einstellung suchen …|Rechercher un réglage…|Cerca impostazione…|Buscar ajuste…|Procurar definição…
    Shows at once how colours, glass and text size look – without looking at the sidebar.|Zeigt sofort, wie Farben, Glas und Textgröße wirken – ohne zur Sidebar zu schauen.|Montre aussitôt l'effet des couleurs, du verre et de la taille du texte – sans regarder la barre.|Mostra subito l'effetto di colori, vetro e dimensione del testo – senza guardare la barra.|Muestra al instante cómo quedan colores, cristal y tamaño de texto – sin mirar la barra.|Mostra logo como ficam cores, vidro e tamanho do texto – sem olhar para a barra.
    Sure? Click again|Sicher? Nochmal klicken|Sûr ? Cliquer encore|Sicuro? Clicca di nuovo|¿Seguro? Pulsa otra vez|Tem a certeza? Clique outra vez
    Text|Text|Texte|Testo|Texto|Texto
    Tick to show|Haken setzen zum Anzeigen|Cocher pour afficher|Spunta per mostrare|Marca para mostrar|Marque para mostrar
    Tick what the sidebar shows; drag by the handle or use the arrows to sort. “Customise” opens style, column, register and single values.|Haken setzen, was die Sidebar zeigt; am Griff ziehen oder mit den Pfeilen sortieren. „Anpassen“ öffnet Stil, Spalte, Register und einzelne Werte.|Cochez ce que la barre affiche ; triez par la poignée ou avec les flèches. « Personnaliser » ouvre style, colonne, onglet et valeurs.|Spunta ciò che la barra mostra; ordina con la maniglia o con le frecce. «Personalizza» apre stile, colonna, scheda e singoli valori.|Marca lo que muestra la barra; ordena con el asa o las flechas. «Personalizar» abre estilo, columna, pestaña y valores.|Marque o que a barra mostra; ordene pela pega ou com as setas. «Personalizar» abre estilo, coluna, separador e valores.
    {0} shown|{0} sichtbar|{0} affichés|{0} visibili|{0} visibles|{0} visíveis
    Not reachable – the program is not running or its shared memory is off. The built-in sensors are shown meanwhile.|Nicht erreichbar – das Programm läuft nicht oder Shared Memory ist aus. Solange werden die eingebauten Sensoren gezeigt.|Injoignable – le programme ne tourne pas ou sa mémoire partagée est désactivée. Les capteurs intégrés sont affichés en attendant.|Non raggiungibile – il programma non è in esecuzione o la memoria condivisa è disattivata. Nel frattempo si vedono i sensori integrati.|No disponible – el programa no está en marcha o su memoria compartida está desactivada. Mientras tanto se muestran los sensores integrados.|Indisponível – o programa não está a correr ou a memória partilhada está desligada. Entretanto são mostrados os sensores integrados.
    Readings it does not have come from the built-in sensors.|Was dort fehlt, kommt von den eingebauten Sensoren.|Ce qui y manque vient des capteurs intégrés.|Ciò che manca proviene dai sensori integrati.|Lo que falte viene de los sensores integrados.|O que faltar vem dos sensores integrados.
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
    Style for all widgets|Stil für alle Widgets|Style pour tous les widgets|Stile per tutti i widget|Estilo para todos los widgets|Estilo para todos os widgets
    DL|DL|DL|DL|DL|DL
    UP|UP|UP|UP|UP|UP
    1% low|1 % Tief|1 % bas|1 % minimo|1 % bajo|1 % baixo
    DL peak · 2 min|DL-Spitze · 2 Min.|Pic DL · 2 min|Picco DL · 2 min|Pico DL · 2 min|Pico DL · 2 min
    Conditions|Wetterlage|Conditions|Condizioni|Condiciones|Condições
    Playback|Wiedergabe|Lecture|Riproduzione|Reproducción|Reprodução
    Time|Zeit|Heure|Ora|Hora|Hora
    Model|Modell|Modèle|Modello|Modelo|Modelo
    Free|Frei|Libre|Libero|Libre|Livre
    Used / total|Belegt / gesamt|Utilisé / total|Usato / totale|Usado / total|Usado / total
    Style per register|Stil pro Register|Style par onglet|Stile per scheda|Estilo por pestaña|Estilo por separador
    All registers|Alle Register|Tous les onglets|Tutte le schede|Todas las pestañas|Todos os separadores
    Sets the design only for the widgets of the chosen register.|Setzt das Design nur für die Widgets des gewählten Registers.|Applique le design uniquement aux widgets de l’onglet choisi.|Applica il design solo ai widget della scheda scelta.|Aplica el diseño solo a los widgets de la pestaña elegida.|Aplica o design apenas aos widgets do separador escolhido.
    One click sets every widget with styles; each one can still be changed below.|Ein Klick stellt alle Widgets mit Stilen um; jedes lässt sich unten weiterhin einzeln ändern.|Un clic règle tous les widgets à styles ; chacun reste modifiable ci-dessous.|Un clic imposta tutti i widget con stili; ognuno resta modificabile qui sotto.|Un clic cambia todos los widgets con estilos; cada uno se puede cambiar abajo.|Um clique muda todos os widgets com estilos; cada um pode ser alterado abaixo.
    Link speed|Verbindung|Liaison|Collegamento|Enlace|Ligação
    Each drive|Jedes Laufwerk|Chaque disque|Ogni unità|Cada unidad|Cada unidade
    Read|Lesen|Lecture|Lettura|Lectura|Leitura
    Write|Schreiben|Écriture|Scrittura|Escritura|Escrita
    Model name|Modellname|Nom du modèle|Nome modello|Nombre del modelo|Nome do modelo
    Usage bar|Belegungsbalken|Barre d'occupation|Barra di utilizzo|Barra de uso|Barra de uso
    Used in %|Belegt in %|Occupé en %|Usato in %|Usado en %|Usado em %
    Volume name|Laufwerksname|Nom du volume|Nome volume|Nombre del volumen|Nome do volume
    Free space|Freier Platz|Espace libre|Spazio libero|Espacio libre|Espaço livre
    Drives shown|Angezeigte Laufwerke|Disques affichés|Unità mostrate|Unidades mostradas|Unidades mostradas
    Volumes shown|Angezeigte Partitionen|Volumes affichés|Volumi mostrati|Volúmenes mostrados|Volumes mostrados
    Internal IP|Interne IP|IP interne|IP interno|IP interna|IP interno
    External IP|Externe IP|IP externe|IP esterno|IP externa|IP externo
    Get|Abrufen|Obtenir|Recupera|Obtener|Obter
    Asking…|Wird abgerufen…|Demande…|Richiesta…|Consultando…|A consultar…
    Not reachable|Nicht erreichbar|Injoignable|Non raggiungibile|No disponible|Indisponível
    Not fetched yet|Noch nicht abgerufen|Pas encore demandée|Non ancora recuperato|Aún no obtenida|Ainda não obtido
    Asks api.ipify.org once for the public address of this connection.|Fragt api.ipify.org einmal nach der öffentlichen Adresse dieser Verbindung.|Demande une fois à api.ipify.org l'adresse publique de cette connexion.|Chiede una volta ad api.ipify.org l'indirizzo pubblico di questa connessione.|Pregunta una vez a api.ipify.org la dirección pública de esta conexión.|Pergunta uma vez a api.ipify.org o endereço público desta ligação.
    Title|Titel|Titre|Titolo|Título|Título
    Show|Anzeigen|Afficher|Mostra|Mostrar|Mostrar
    None|Keiner|Aucun|Nessuno|Ninguno|Nenhum
    Hardware name|Hardware-Name|Nom du matériel|Nome hardware|Nombre del hardware|Nome do hardware
    Each volume|Jede Partition|Chaque volume|Ogni volume|Cada volumen|Cada volume
    Register names|Register-Namen|Noms des pages|Nomi delle pagine|Nombres de páginas|Nomes das páginas
    Shown at the top instead of 1, 2, 3; leave empty for the number.|Steht oben statt 1, 2, 3; leer lassen für die Nummer.|Affiché en haut au lieu de 1, 2, 3 ; vide = numéro.|Mostrato in alto al posto di 1, 2, 3; vuoto = numero.|Se muestra arriba en lugar de 1, 2, 3; vacío = número.|Mostrado no topo em vez de 1, 2, 3; vazio = número.
    Whole screen|Ganzer Bildschirm|Plein écran|Schermo intero|Pantalla completa|Ecrã inteiro
    Covers the whole monitor up to the taskbar; the columns share its width.|Füllt den ganzen Monitor bis zur Taskleiste; die Spalten teilen sich die Breite.|Couvre tout l'écran jusqu'à la barre des tâches ; les colonnes se partagent la largeur.|Copre tutto il monitor fino alla barra delle applicazioni; le colonne si dividono la larghezza.|Cubre todo el monitor hasta la barra de tareas; las columnas se reparten el ancho.|Cobre todo o monitor até à barra de tarefas; as colunas partilham a largura.
    The file could not be read or written.|Die Datei konnte nicht gelesen oder geschrieben werden.|Impossible de lire ou d'écrire le fichier.|Impossibile leggere o scrivere il file.|No se pudo leer ni escribir el archivo.|Não foi possível ler nem gravar o ficheiro.
    One ZIP file with error log, sensor lists and settings – without notes, places or own names. Nothing is sent.|Eine ZIP-Datei mit Fehlerprotokoll, Sensorlisten und Einstellungen – ohne Notizen, Orte und eigene Namen. Es wird nichts verschickt.|Un fichier ZIP avec journal d'erreurs, listes de capteurs et réglages – sans notes, lieux ni noms personnels. Rien n'est envoyé.|Un file ZIP con registro errori, elenchi dei sensori e impostazioni – senza note, luoghi o nomi propri. Non viene inviato nulla.|Un archivo ZIP con registro de errores, listas de sensores y ajustes – sin notas, lugares ni nombres propios. No se envía nada.|Um ficheiro ZIP com registo de erros, listas de sensores e definições – sem notas, locais ou nomes próprios. Nada é enviado.
    Narrow fonts fit more values into the bar.|Schmale Schriften bringen mehr Werte in die Leiste.|Les polices étroites placent plus de valeurs dans la barre.|I caratteri stretti fanno stare più valori nella barra.|Las fuentes estrechas caben más valores en la barra.|Fontes estreitas cabem mais valores na barra.
    Saved as {0}.|Gespeichert als {0}.|Enregistré sous {0}.|Salvato come {0}.|Guardado como {0}.|Guardado como {0}.
    Theme file|Theme-Datei|Fichier de thème|File del tema|Archivo de tema|Ficheiro de tema
    Bahnschrift narrow|Bahnschrift schmal|Bahnschrift étroite|Bahnschrift stretta|Bahnschrift estrecha|Bahnschrift estreita
    Inter (built in)|Inter (eingebaut)|Inter (intégrée)|Inter (integrato)|Inter (integrada)|Inter (integrada)
    Themes|Themes|Thèmes|Temi|Temas|Temas
    Create error report|Fehlerbericht erstellen|Créer un rapport d'erreur|Crea rapporto errori|Crear informe de errores|Criar relatório de erros
    Error report|Fehlerbericht|Rapport d'erreur|Rapporto errori|Informe de errores|Relatório de erros
    Colours, background, glass, text size and typeface – no layout and no personal data.|Farben, Hintergrund, Glas, Textgrösse und Schrift – kein Layout und keine persönlichen Daten.|Couleurs, fond, verre, taille du texte et police – sans disposition ni données personnelles.|Colori, sfondo, vetro, dimensione del testo e carattere – niente layout e niente dati personali.|Colores, fondo, cristal, tamaño de texto y fuente – sin diseño ni datos personales.|Cores, fundo, vidro, tamanho do texto e fonte – sem disposição nem dados pessoais.
    Theme “{0}” applied.|Theme „{0}“ übernommen.|Thème « {0} » appliqué.|Tema «{0}» applicato.|Tema «{0}» aplicado.|Tema «{0}» aplicado.
    Typeface|Schrift|Police|Carattere|Fuente|Fonte
    This is not a Lumen theme file.|Das ist keine Lumen-Theme-Datei.|Ce n'est pas un fichier de thème Lumen.|Questo non è un file di tema Lumen.|Esto no es un archivo de tema de Lumen.|Isto não é um ficheiro de tema do Lumen.
    Saved on the desktop: {0}|Auf dem Desktop gespeichert: {0}|Enregistré sur le bureau : {0}|Salvato sul desktop: {0}|Guardado en el escritorio: {0}|Guardado no ambiente de trabalho: {0}
    Save theme …|Theme speichern …|Enregistrer le thème …|Salva tema …|Guardar tema …|Guardar tema …
    Monospace (Cascadia, Consolas)|Festbreite (Cascadia, Consolas)|Chasse fixe (Cascadia, Consolas)|Monospazio (Cascadia, Consolas)|Monoespaciada (Cascadia, Consolas)|Monoespaçada (Cascadia, Consolas)
    Load theme …|Theme laden …|Charger un thème …|Carica tema …|Cargar tema …|Carregar tema …
    Timeline|Verlauf breit|Historique large|Storico ampio|Historial ancho|Histórico largo
    Heatmap|Heatmap|Carte de chaleur|Mappa di calore|Mapa de calor|Mapa de calor
    Dial|Messuhr|Jauge|Indicatore|Indicador|Medidor
    Rings|Ringe|Anneaux|Anelli|Anillos|Anéis
    BigNumber|Große Zahl|Grand chiffre|Numero grande|Número grande|Número grande
    Table|Tabelle|Tableau|Tabella|Tabla|Tabela
    Down|Down|Desc.|Giù|Bajada|Descida
    Up|Up|Mont.|Su|Subida|Subida
    Max|Max|Max|Max|Máx|Máx
    Busiest|Am stärksten|Le plus chargé|Più carico|Más cargado|Mais carregado
    Column bars|Säulen|Colonnes|Colonne|Columnas|Colunas
    Grey – only warnings in colour|Grau – nur Warnungen farbig|Gris – seules les alertes en couleur|Grigio – solo gli avvisi a colori|Gris – solo avisos en color|Cinzento – só avisos a cores
    Built-in themes|Eingebaute Themes|Thèmes intégrés|Temi integrati|Temas integrados|Temas integrados
    Retro LCD in amber, neon outlines, or grey with colour only for warnings.|Retro-LCD in Bernstein, Neon-Umrisse oder Grau mit Farbe nur für Warnungen.|LCD rétro ambre, contours néon, ou gris avec couleur seulement pour les alertes.|LCD retrò ambra, contorni neon, o grigio con colore solo per gli avvisi.|LCD retro ámbar, contornos neón o gris con color solo para avisos.|LCD retro âmbar, contornos néon ou cinzento com cor só para avisos.
    Lumen|Lumen|Lumen|Lumen|Lumen|Lumen
    Retro LCD|Retro-LCD|LCD rétro|LCD retrò|LCD retro|LCD retro
    Neon|Neon|Néon|Neon|Neón|Néon
    Mono|Mono|Mono|Mono|Mono|Mono
    Pill|Kapsel|Capsule|Capsula|Cápsula|Cápsula
    DotMatrix|Dot-Matrix|Matrice de points|Matrice a punti|Matriz de puntos|Matriz de pontos
    Bento|Bento|Bento|Bento|Bento|Bento
    Ticker|Ticker|Téléscripteur|Ticker|Ticker|Ticker
    MiniArc|Mini-Bogen|Mini-arc|Mini arco|Mini arco|Mini arco
    FillBars|Füllbalken|Barres pleines|Barre piene|Barras de relleno|Barras de preenchimento
    Split|Zweigeteilt|Divisé|Diviso|Dividido|Dividido
    Chips|Chips|Pastilles|Chip|Chips|Chips
    Modern look|Moderner Look|Look moderne|Aspetto moderno|Aspecto moderno|Visual moderno
    Cards without borders, rounder corners, icons in tinted circles, bolder numbers.|Karten ohne Rand, rundere Ecken, Symbole in farbigen Kreisen, kräftigere Zahlen.|Cartes sans bordure, coins plus arrondis, icônes dans des cercles teintés, chiffres plus marqués.|Schede senza bordo, angoli più arrotondati, icone in cerchi colorati, numeri più marcati.|Tarjetas sin borde, esquinas más redondeadas, iconos en círculos de color, números más marcados.|Cartões sem contorno, cantos mais arredondados, ícones em círculos coloridos, números mais fortes.
    Scale|Skala|Échelle|Scala|Escala|Escala
    HistoryBars|Balkenverlauf|Historique en barres|Storico a barre|Historial en barras|Histórico em barras
    Backdrop|Hintergrundkurve|Courbe de fond|Curva di sfondo|Curva de fondo|Curva de fundo
    PillDuo|Doppelkapsel|Double capsule|Doppia capsula|Cápsula doble|Cápsula dupla
    PillRow|Kapselreihe|Rangée de capsules|Fila di capsule|Fila de cápsulas|Fila de cápsulas
    KeyFigures|Kennzahlen|Chiffres clés|Cifre chiave|Cifras clave|Números-chave
    BadgeList|Liste|Liste|Elenco|Lista|Lista
    """;

	private static readonly Dictionary<string, string[]> Rows = Parse();

	private static readonly string[] Codes = new string[5] { "de", "fr", "it", "es", "pt" };

	private static readonly Dictionary<string, string> Cultures = new Dictionary<string, string>
	{
		["en"] = "en-GB",
		["de"] = "de-CH",
		["fr"] = "fr-FR",
		["it"] = "it-IT",
		["es"] = "es-ES",
		["pt"] = "pt-PT"
	};

	public static readonly (string Code, string Name)[] Choices = new(string, string)[6]
	{
		("de", "Deutsch"),
		("en", "English"),
		("fr", "Français"),
		("it", "Italiano"),
		("es", "Español"),
		("pt", "Português")
	};

	public static string Language { get; private set; } = "en";

	public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("en-GB");

	public static IReadOnlyDictionary<string, string[]> All => Rows;

	private static Dictionary<string, string[]> Parse()
	{
		Dictionary<string, string[]> dictionary = new Dictionary<string, string[]>();
		string[] array = Table.Split('\n');
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].TrimEnd('\r').Split('|');
			if (array2.Length == 6)
			{
				dictionary[array2[0]] = array2[1..];
			}
		}
		return dictionary;
	}

	public static void Use(string setting)
	{
		string text = setting;
		if (!Settings.Languages.Contains(text))
		{
			text = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
			if (!Settings.Languages.Contains(text))
			{
				text = "en";
			}
		}
		Language = text;
		Culture = CultureInfo.GetCultureInfo(Cultures[text]);
		Format.RpmUnit = text == "de" ? "U/min" : "rpm";
	}

	public static string T(string key)
	{
		int num = Array.IndexOf(Codes, Language);
		if (num < 0 || !Rows.TryGetValue(key, out string[] value) || value[num].Length <= 0)
		{
			return key;
		}
		return value[num];
	}

	public static string F(string key, params object[] args)
	{
		return string.Format(Culture, T(key), args);
	}
}






