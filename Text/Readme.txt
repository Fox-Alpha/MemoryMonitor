Die Anwendung MemoryMonitor dient dazu über einen Zeitraum einen Verlauf des Verwendeten Arbeitsspeichers aufzuzeichnen.
Dazu wird in einem Definierten Zeitraum der Verbrauch der überwachten Prozesse abgerufen und protolliert.
Diese Protokolle werden im csv Format erstellt und können somit sehr einfach in anderen Anwendungen z.B. Excel verwendet werden um den Verlauf grafisch darzustellen.

Der MemoryMonitor arbeitet hierbei in 3 definierbaren intervallen
1. Im Normal zustand wird die Abfrage sinnvollerweise sehr selten ausgeführt. Als Standardvorgabe liegt dieser Wert hier bei ca alle 4 Minuten. 
	Dieser Wert sollte austreichend verhindern das die Protokolle unnützer Weise sehr groß werden.
	Während die überwachte Anwendung sich im 'Normal' Zustand befindet sollte bei der Anwendung mit keinen Problemen gerechnet werden.

2. Als zweiten Bereich der Überwachung ist der Zustand 'Warnung'. In diesem Zustand sollte der Speicherverbrauch noch keine kritischen Auswirkungen haben. 
	Dient aber dazu die Intervalle der Überwachung zu verkürzen, damit bei erreichen des kritischen Bereiches dieser Zeitpunkt möglichs zeitgleich aufgezeichnet wird.
	Das Protokoll sollte dann bei einer Ursachen Forschung behilflich sein, da es den Zeitpunkt schon sehr genau eingrenzen kann.
	Die Vorgabe des 'Warnung' Intervalls liegt bei 45 Sekunden.

3. Der als Kritisch definierte Wert der liegt per Vorgabe bei 15 Sekunden. Hierdurch kann leicht nachvollzogen werden wie schnell der Speicherverbrauch ansteigt.

Die Protokolle werden in einem Konfigurierbaren Verzeich abgelegt. Es wird täglich automatisch ein Unterordner mit dem Datum erzeugt. 
Dies soll die Übersichtlichkeit verbessern und dabei helfen einen Zeitraum leichter aufzufinden. Hierdurch wird die maximale Größe eines Protokolls kontrolliert. 
Selbst bei 24 stündiger Protokollierung im 15 Sekunden Takt sollte die die macimale Größe bei etwa 414.000 Byte liegen

Die Größe lässt sich planen und erechnen. Ich habe diese Werte als Grundlage genommen.

Aufbau des Protokolls
=====================
Enthaltene Informationen
	Time;PID;Name;MemUsage;PeakMemUsage

	Time				 8 zch	=	Zeitstempel Uhrzeit, HH:MM:SS. 8 Zeichen, Keine Anführungszeichen
	PID					 6 zch	=	ProzessID die u.a im Taskmanager angezeigt wird. Max 6 stellig nummerischer Wert 1 - 999999
	Name				20 zch	=	Text, Prozessname ohne Dateierweiterung der Anwendung. Max 20 Zeichen. Umschlossen mit Anführungszeichen, falls Leerzeichen im Namen enthalten sind.
	MemUsage			15 zch	=	Aktueller Wert des Reservierten Arbeitsspeichers. Max 15 Zeichen. Enthält zur besseren lessbarkeit tausendertrennzeichen. Wert entspricht Verbrauch Byte.
	PeakMemUsage		15 zch	=	Maximaler Wert des Reservierten Arbeitsspeichers seit start der Anwendung. Max 15 Zeichen. Enthält zur besseren lessbarkeit tausendertrennzeichen. Wert entspricht Verbrauch Byte.
						------
						64 zch

	Spaltentrenner ;	 4 zch
	Textumschluss "		 2 zch
	Zeilenumbruch		 2 zch
						------
						74 zch
						======

	Die maximale Zeilenlänge beträgt 74 Zeichen. Inkl. Zeilenende.
	
	Dateigröße:

	74 byte * {Intervall pro Min.} * 60 Minuten * 24 Stunden ~~ 426.240 Byte {Bei 15 sek. Intervall}

