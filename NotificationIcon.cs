/*
 * Erstellt mit SharpDevelop.
 * Benutzer: buck
 * Datum: 05.01.2016
 * Zeit: 08:38
 *
* 
* Select Name,ProcessID,creationdate,Caption,commandline,executablepath,WorkingSetSize,peakWorkingSetSize,OSName From Win32_Process where name = 'JM4.exe'
 */
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Linq;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Management;

using WmiLight;

using MemoryMonitor.Forms;
using MemoryMonitor.Klassen;

namespace MemoryMonitor
{
	public sealed class NotificationIcon
	{
		#region Eigenschaften
		private NotifyIcon notifyIcon;
		private ContextMenu notificationMenu;
		private bool bBallonTipShowing;
		private MainForm mainForm;
		private System.Timers.Timer garbageTimer;
		
		private List<clsProcessTimer> psTimer;
		FileSystemWatcher m_Watcher;
		private int warningMemUsage = 800000000;
		private int criticalMemUsage = 850000000;
		

		/// <summary>
		/// Verwendete Hostliste für die Routine
		/// </summary>
		string _strHostFileList;
		public string strHostFileList {
			get { return _strHostFileList; }
			set { _strHostFileList = value; }
		}
		
		Dictionary<string, string> alias = new Dictionary<string, string>();
		
		private string _commandFile = "c:\\temp\\MemUsageLog\\command.file";
		private string _commandResultFile = "c:\\temp\\MemUsageLog\\command.result";
		private string _commandStatusFile = "c:\\temp\\MemUsageLog\\command.status";
		
		public string commandFile {
			get { return _commandFile; }
			private set { _commandFile = value; }
		}
		public string commandResultFile {
			get { return _commandResultFile; }
			private set { _commandResultFile = value; }
		}
		public string commandStatusFile {
			get { return _commandStatusFile; }
			private set { _commandStatusFile = value; }
		}

		// Eigenschaft als Liste erstellen.
		// Um auch mehere Anwendung unabhängig überwachen zu können
		private List<string> _ProcessListToWatch;
		private string _processToWatch = string.Empty;

		public string processToWatch {
			get { return _processToWatch; }
			private set { 
				if (!string.IsNullOrWhiteSpace(value)) {
					if (!_ProcessListToWatch.Contains(value)) {
						_ProcessListToWatch.Add(value);
					}
				}				
			}
		}
		
		string protokollSavePath = @"c:\temp\MemUsageLog\";
		string protokollFileNamePrefix = @"MemLog";
		string protokollFileLogExt = "csv";
		string protokollImageExt = "png";
		#endregion

		#region Initialize icon and menu
		public NotificationIcon()
		{
			notifyIcon = new NotifyIcon();
			notificationMenu = new ContextMenu(InitializeMenu());
			List<Process> ps = new List<Process>();
			_ProcessListToWatch = new List<string>();
			psTimer = new List<clsProcessTimer>();
			m_Watcher = new FileSystemWatcher();
			garbageTimer = new System.Timers.Timer();
			
			processToWatch = "JM4";
			
			//	####
			//	Timer per Default für JobManager erstellen
			//	####
			foreach(string str in _ProcessListToWatch)
			{
				ps.AddRange(Process.GetProcessesByName(str));
				for (int i = 0; i < ps.Count; i++) {
					psTimer.Add(new clsProcessTimer(ps[i].Id, clsProcessTimer.enumIntervallState.normalInterval));
					psTimer[i].Elapsed += timerTick;
					psTimer[i].Enabled = true;
				}
			}
			//	####
			
			//	####
			//	Aufräumen der nicht benötigten Prozesstimer
			//	####
			garbageTimer.Interval = 6000;
			garbageTimer.Elapsed += garbageTimerTick;
			garbageTimer.Enabled = true;
			//	####
			
			notifyIcon.BalloonTipShown += iconShowBallonToolTip;
			notifyIcon.BalloonTipClosed += iconCloseBallonToolTip;
			notifyIcon.BalloonTipClicked += iconClickBallonToolTip;
			
			notifyIcon.DoubleClick += IconDoubleClick;
			notifyIcon.MouseMove += IconMouseOver;
			ComponentResourceManager resources = new ComponentResourceManager(typeof(NotificationIcon));
			notifyIcon.Icon = (Icon)resources.GetObject("$this.Icon");
			notifyIcon.ContextMenu = notificationMenu;
			
//			getRemoteWMIData_WMILight();
			
			#region systemfilewatcher
			m_Watcher.Path = "c:\\temp\\MemUsageLog\\";
			m_Watcher.Filter = "command.file";
			m_Watcher.NotifyFilter = NotifyFilters.Size;
//						NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Attributes |
//						NotifyFilters.CreationTime | NotifyFilters.Security | NotifyFilters.DirectoryName

			m_Watcher.IncludeSubdirectories = false;
			
			m_Watcher.Changed += new FileSystemEventHandler(OnChanged);
			m_Watcher.Created += new FileSystemEventHandler(OnChanged);
			m_Watcher.Deleted += new FileSystemEventHandler(OnChanged);
			m_Watcher.Renamed += new RenamedEventHandler(OnRenamed);
			
			m_Watcher.EnableRaisingEvents = true;
			#endregion
		}
		
		void IconMouseOver(object sender, EventArgs e)
		{
			if (!bBallonTipShowing) {
				notifyIcon.BalloonTipText = "Überwacht den Speicherverbrauch einer Anwendung und erstellt einen Screenshot bei überschreitung eines bestimmten Wertes";
				notifyIcon.BalloonTipTitle = "MemoryMonitor";
				notifyIcon.ShowBalloonTip(0);				
			}
		}
		
		void iconShowBallonToolTip(object sender, EventArgs e)
		{
			bBallonTipShowing = true;
		}
		
		void iconCloseBallonToolTip(object sender, EventArgs e)
		{
			bBallonTipShowing = false;
		}
		
		void iconClickBallonToolTip(object sender, EventArgs e)
		{
			bBallonTipShowing = false;
		}

		private MenuItem[] InitializeMenu()
		{
			MenuItem[] menu = new MenuItem[] {
				new MenuItem("Open", menuOpenClick),
				new MenuItem("About", menuAboutClick),
				new MenuItem("Exit", menuExitClick)
					//	TODO : Stop und Start Timer
			};
			return menu;
		}
		#endregion
		
		#region Main - Program entry point
		/// <summary>Program entry point.</summary>
		/// <param name="args">Command Line Arguments</param>
		[STAThread]
		public static void Main(string[] args)
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			
#if DEBUG
	string mutexName = "MemoryMonitor | DEBUG";
#else
	string mutexName = "MemoryMonitor | RELEASE";
#endif			
			bool isFirstInstance;
			// Please use a unique name for the mutex to prevent conflicts with other programs
			using (Mutex mtx = new Mutex(true, mutexName, out isFirstInstance)) {
				if (isFirstInstance) {
					NotificationIcon notificationIcon = new NotificationIcon();
					notificationIcon.notifyIcon.Visible = true;
					
					Application.Run();

					notificationIcon.notifyIcon.Dispose();
				} else {
					// The application is already running
					// TODO: Display message box or change focus to existing application instance
				}
			} // releases the Mutex
		}
		#endregion
		
		#region Event Handlers
		private void menuAboutClick(object sender, EventArgs e)
		{
			MessageBox.Show("About This Application");
		}
		
		private void menuOpenClick(object sender, EventArgs e)
		{
			mainForm = new MainForm();
			mainForm.Show();
			mainForm.Location = new Point(Screen.PrimaryScreen.WorkingArea.Width - mainForm.Width - 5, Screen.PrimaryScreen.WorkingArea.Height - mainForm.Height - 5);
		}
		
		private void menuExitClick(object sender, EventArgs e)
		{
			if (psTimer.Count > 0) {
				foreach (clsProcessTimer pcstimer in psTimer) {
					pcstimer.Enabled = false;
					pcstimer.Dispose();
				}
				psTimer.Clear();
				m_Watcher.EnableRaisingEvents = false;
			}
			Application.Exit();
		}
		
		private void IconDoubleClick(object sender, EventArgs e)
		{
		}
		#endregion
		
		void timerTick(object sender, EventArgs e)
		{
			Debug.WriteLine("##### Start #####", "timerTick()");
			Process pcs = null;;
			
			
			if ((sender as clsProcessTimer) != null)
			{
				int psID = 0;

				try 
				{
					psID = ((clsProcessTimer)sender).processID;
					pcs = Process.GetProcessById(psID);
				} 
				catch (ArgumentException ae) 
				{
					
					Debug.WriteLine("Die ProzessID " + psID + " konnte nicht gefunden werden!\r\n" + ae.Message, "TimerTick()");
				}
			}
			
			string strCsv;
			string fileName;

			if (pcs != null)
			{
				fileName = checkFileName(0, pcs.ProcessName, pcs.Id);
				Debug.WriteLine(string.Format("PID: {0} | Name: {1} | Time: {2}| CurrentMem: {3}| PeakMem: {4}", pcs.Id, pcs.ProcessName, DateTime.Now.ToString(), pcs.PrivateMemorySize64.ToString(), pcs.PeakWorkingSet64), "TimerTick()");
				strCsv = string.Format("{0};\"{1}\";\"{2}\";{3};{4}\r\n", DateTime.Now.ToString("HH:mm:ss"), pcs.Id, pcs.ProcessName, pcs.PrivateMemorySize64.ToString(), pcs.PeakWorkingSet64);
				
				if (!File.Exists(fileName)) 
				{
					File.AppendAllText(fileName, "Time;PID;Name;MemUsage;PeakMemUsage\r\n", new UTF8Encoding(false));
				}
				File.AppendAllText(fileName, strCsv, new UTF8Encoding(false));
				
				if (pcs.PeakWorkingSet64 > warningMemUsage && pcs.PeakWorkingSet64 < criticalMemUsage) 
				{
					if ((sender as clsProcessTimer) != null) 
					{
						((clsProcessTimer)sender).setIntervalState(clsProcessTimer.enumIntervallState.warningInterval);
					}
					else if (pcs.PeakWorkingSet64 > criticalMemUsage) 
					{
						((clsProcessTimer)sender).setIntervalState(clsProcessTimer.enumIntervallState.criticalInterval);
					}
					else
						((clsProcessTimer)sender).setIntervalState(clsProcessTimer.enumIntervallState.normalInterval);
				}
			}
			else		//	PID Kann nicht gefunden werden
			{
				((clsProcessTimer)sender).Stop();
				((clsProcessTimer)sender).obsolete = true;
				Debug.WriteLine("Prozess nicht mehr aktiv, Timer wird deaktiviert", "timerTick()");
			}
			Debug.WriteLine("##### Ende #####", "timerTick()");
		}

		/// <summary>
		/// Hängt dem Dateinamen einen Count an um Doppelungen bei
		/// Mehrfach vorhandenen Prozessen (JM4) zu vermeiden.
		/// </summary>
		/// <param name="count">Zähler der dem Dateinamen angehängt wird</param>
		/// <param name="suffix">Platzhalter im Dateinamen</param>
		/// <param name="pid">Prozess ID kann dem Dateinamen angefügt werden, 0 ignoriert diesen Parameter</param>
		/// <param name="filetype">0 = PNG, 1 = LOG</param>
		/// <returns>String generierter Dateiname</returns>
		string checkFileName(int count, string suffix = "", int pid = 0, int filetype = 0)
		{
			//
			//	TODO: Erstellen einer Eigenschaft für das Datumsformat
			//
			string strDate = DateTime.Now.ToString("yyyy-MM-dd");
			//	#### Verzeichnis zum speichern bei bedarf anlegen
			if (!Directory.Exists(protokollSavePath)) {
				Directory.CreateDirectory(protokollSavePath);
			}
			//	#### Unterverzeichnis mit Datum anlegen
			if (!Directory.Exists(protokollSavePath + strDate)) {
				Directory.CreateDirectory(protokollSavePath + strDate);
			}
			//	####
			
			//	#### Dateinamen und vollen Pfad erzeugen
			string FullFileName = string.Format("{0}\\{6}\\{1}_{2}{7}_{3}{4}.{5}",
			                                    protokollSavePath, 
			                                    protokollFileNamePrefix, 
			                                    suffix == string.Empty ? "" : suffix,
			                                    strDate, 
			                                    count > 0 ? "_"+count : "",
			                                    filetype == 0 ? protokollFileLogExt : protokollImageExt,
			                                    strDate,
			                                    pid > 0 ? "_"+pid : ""
			                                   );
			//	####
			return FullFileName;
		}
		
		void getRemoteWMIData_WMILight()
		{
			var wmiQuery = "Select Name,ProcessID,creationdate,Caption,commandline,executablepath,WorkingSetSize,peakWorkingSetSize,OSName From Win32_Process where name = 'JM4.exe'";
			var opt = new WmiConnectionOptions() { EnablePackageEncryption = true };
			
			try
			{
				using (WmiConnection con = new WmiConnection(@"\\HH-VS-JM-003\root\cimv2", opt))
				{
					con.Open();

				    foreach (WmiObject partition in con.CreateQuery(wmiQuery))
				    {
				    	Debug.WriteLine("##### ----- #####");
				    	foreach (var element in partition.GetPropertyNames()) {
				    		Debug.WriteLine(element + " : " + partition.GetPropertyValue(element));
				    	}
				    }
				}
			}
			catch(Exception e)
			{
				Debug.WriteLine(e.Message);
			}
		}
		
		#region helper

		/// <summary>
		/// Timer Event um die nicht mehr benötigten Prozesstimer wieder freizugeben
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void garbageTimerTick(object sender, EventArgs e)
		{
			Debug.WriteLine("#####","garbageTimerTick()");
			//	#####
			//	Zuerst alle nicht mehr benötigten Timer entfernen
			//	#####
			
			var obsoleteTimer = psTimer.Where(t => t.obsolete).ToList();

			foreach(clsProcessTimer pt in obsoleteTimer)
			{
				pt.Enabled = false;
				psTimer.Remove(pt);
				Debug.WriteLine("nicht mehr benötigten Timer entfernen PID: " + pt.processID, "garbageTimerTick():ObsoleteTimer");
				pt.Dispose();
			}

			//	#####
			//	Neue Prozesse mit neuen Timer hinzufügen
			//	#####
			//	Liste mit Instanzen des zu überwachenden Prozess
			List<Process> PS = new List<Process>();
			foreach (string strToWatch in _ProcessListToWatch) {
				PS.AddRange(Process.GetProcessesByName(strToWatch));
			}
			
			if (PS.Count > 0) 
			{
				var n = PS.Select(p => p.Id).Except(psTimer.Select(t => t.processID));
				foreach(var z in n)
				{
					Debug.WriteLine(string.Format("PID noch nicht in der liste: " + z), ":NeueTimer");
					
					clsProcessTimer cpt = new clsProcessTimer(z, clsProcessTimer.enumIntervallState.normalInterval);
					psTimer.Add(cpt);
					cpt.Elapsed += timerTick;
					cpt.Enabled = true;
				}
			}
			else
			{
				//TODO:	Alle Timer Freigeben. Es ist kein Prozess mehr vorhanden
			}
			
			if (psTimer.Count > 0) {
				foreach (clsProcessTimer element in psTimer) {
					Debug.WriteLine(string.Format("PID: {0} | Enable: {1} | Intervall: {2}", element.processID, element.Enabled, element.Interval), "Debug: Ausgabe aller Timer | garbageTimerTick()");
					
				} 
			}
			else
				Debug.WriteLine("Keine Aktiven Timer","garbageTimerTick()");
			
			PS = null;
			Debug.WriteLine("#####","garbageTimerTick()");
			//	#####
		}
		
		/// <summary>
		/// Liest die Liste der Hosts aus der angegebenen Text Datei ein
		/// Die Liste ist Zeilenorientiert
		/// Ein Host pro Zeile
		/// Zeilen beginnend mit '#' werden ignoriert und können als Kommentar genutzt werden
		/// </summary>
		/// <param name="hostFileList">Dateiname inkl. Pfad</param>
		/// <returns>true bei erfolgreichem Einlesen</returns>
		bool readRoutineListFromFile(string hostFileList)
		{
			TextReader trHostList = null;
			string strTmp = null;
			List<string> hosts; 
			
			if (hostFileList == null) {
				hostFileList = strHostFileList;
			}
			
			if (File.Exists(hostFileList)) 
			{
				hosts = new List<string>();

				try
				{
					if (alias.Count > 0) {
						alias.Clear();
					}
					using (trHostList = File.OpenText(hostFileList)) 
					{
						while((strTmp = trHostList.ReadLine()) != null)
						{
							if (!strTmp.StartsWith("#", StringComparison.CurrentCultureIgnoreCase)) 
							{
								//	Regex auf AC Namenskonvention oder IP Adresse
								if (Regex.IsMatch(strTmp, "^((([0-9]{1,3}).){3}.[0-9]{1,3})|((([A-Za-z]{2})-){3}[0-9]{3})$"))
								{
									if (strTmp.Split(';').Length > 1)
									{
//										clbRoutineListe.Items.Add(strTmp.Split(';')[1]);
										hosts.Add(strTmp.Split(';')[1]);
										alias.Add(strTmp.Split(';')[1],strTmp.Split(';')[0]);
									}
									else
										hosts.Add(strTmp);
								}
							}
						}
					}
				} 
				#region exception				
				catch (IOException ioE) 
				{
					Debug.WriteLine(string.Format("Error: {0}", ioE.Message), "IOException");
				}
				catch (OutOfMemoryException oomE)
				{
					Debug.WriteLine(string.Format("Error: {0}", oomE.Message), "OutOfMemoryException");
				}
				catch (ObjectDisposedException odE) 
				{
					Debug.WriteLine(string.Format("Error: {0}", odE.Message), "ObjectDisposedException");
				}
				catch (ArgumentOutOfRangeException aoorE) 
				{
					Debug.WriteLine(string.Format("Error: {0}", aoorE.Message), "ArgumentOutOfRangeException");
				}
		        catch (Exception exc)
		        {
					Debug.WriteLine(string.Format("Error: {0}", exc.Message), "Exception");
		        }
				#endregion
				
				return true;
			}

			return false;
		}

        /// <summary>Auslesen der Parameter aus dem Kommandozeilen Aufruf</summary>
        /// 
        /// <param name="key">Gesuchter Parameter</param>
        /// <param name="cmdline">Parameterstring in dem nach <paramref name="key"/> gesucht werden soll</param>
        /// <param name="value">Rückgabe falls ein Werte im Parameter vorhanden ist</param>
        /// <returns>True wenn key enthalten ist</returns>
        bool ParseCmdLineParamGetValue(string key, string cmdline, ref string value)
        {
        	bool hasValue;
       		value = ParseCmdLineParam(key, cmdline, out hasValue);
        	return hasValue;
        }
        
        //	Gibt nur den Wert eines Parameters zurück
        string ParseCmdLineParam(string key, string cmdline, out bool hasValue)
		{
        	/*
        	 * TODO:
        	 * 1. Was ist wenn ein Parameterwert '-' oder ' -' enthält ?
        	 * 2. Was ist wenn ein Parameterwert mit " eingeshlossen ist ?
        	 * 
        	 */
        	hasValue = false;
			string res = "";
			try
			{
				int end = 0;
				int start = 0;
				int pos = 0;
				
				//	Ersetzem von Anführungszeichen in der Parameterliste
				cmdline = Regex.Replace(cmdline, "\"", "");

                //  Start auf ersten Parameter beginnend mit ' -' setzen
                if ((pos = cmdline.IndexOf(" -", start, StringComparison.CurrentCulture)) > -1)
                {
                    start = cmdline.IndexOf(" -", start, StringComparison.CurrentCulture);
                    cmdline = cmdline.Substring(start, cmdline.Length - start);
                }
                else
                    return string.Empty;

                //Wenn Key nicht gefunden wurde, dann beenden.
                if ((start = cmdline.ToLower().IndexOf(key.ToLower(), StringComparison.CurrentCulture)) <= 0)
					return string.Empty;
				
				if (cmdline.Length == start+key.Length)
					return cmdline.Substring(start, cmdline.Length-start);
				
				//prüfen ob dem Parameter ein Wert mit '=' angehängt ist
				if (cmdline[start+key.Length] == '=') {
					//Start hinter das '=' setzten
					start += key.Length+1;
					hasValue = true;
				}
//				else				
//					start += key.Length;
				
				//Position des nächsten Parameters ermitteln
				if(cmdline.Length > start)
					end = cmdline.IndexOf(" -", start, StringComparison.CurrentCulture);

				int length = 0;
				
				if (end > 0)
				{
					length = end-start;
				} 
				else 
				{
					length = cmdline.Length-start;
				}
				if(length >= 0)
					res = cmdline.Substring(start, length);
				
			} 
			catch (System.Exception ex)
			{
				Debug.WriteLine(ex.Message);
			}
			return res;
		}
		#endregion
		
		#region SystemFilewatcherEvents
		void OnChanged(object sender, FileSystemEventArgs e)
		{
			switch (e.ChangeType) 
			{
				case WatcherChangeTypes.Changed:
					Debug.WriteLine(string.Format("{0} Datei wurde geändert", DateTime.Now.ToString()), "WachterEvent() - Changed");
					readCommandFromFile();
					break;
					
				case WatcherChangeTypes.Created:
					Debug.WriteLine(string.Format("{0} Datei wurde erstellt", DateTime.Now.ToString()), "WachterEvent() - Created");
					break;
					
				case WatcherChangeTypes.Deleted:
					Debug.WriteLine(string.Format("{0} Datei wurde gelöscht", DateTime.Now.ToString()), "WachterEvent() - Delete");
					break;
			}
		}
		
		void OnRenamed(object sender, RenamedEventArgs e)
		{
			if(e.ChangeType == WatcherChangeTypes.Renamed)
			{
				Debug.WriteLine(string.Format("{0} Datei wurde umbenannt", DateTime.Now.ToString()), "WachterEvent() - Rename");
			}
		}
		#endregion
		
		#region commandFile
		void readCommandFromFile()
		{
			
			if (File.Exists(commandFile)) {
				FileInfo fi = new FileInfo(commandFile);
				Queue<string> commandStack = new Queue<string>();
				
				if (fi.Length > 0) {
					Debug.WriteLine(string.Format("{0} Datei wurde geändert, Command Wait", DateTime.Now.ToString()), "readCommandFromFile() - Change");
					
					foreach(string cmd in File.ReadLines(commandFile))
					{
						if (!string.IsNullOrEmpty(cmd)) {
							commandStack.Enqueue(cmd);
						}
					}
					executeCommandsFromFile(commandStack);
					File.WriteAllText(commandFile, "");
				}
				else
				{
					Debug.WriteLine(string.Format("{0} Datei wurde geleert, Command Run", DateTime.Now.ToString()), "readCommandFromFile() - Delete");
				}
			}
			else
			{
				File.Create(commandFile);
				Debug.WriteLine(string.Format("{0} Datei wurde angelegt, Command Empty", DateTime.Now.ToString()), "readCommandFromFile() - Create");
			}
		}
		
		void executeCommandsFromFile(Queue<string> cmdStack)
		{
			if (cmdStack.Count > 0) {
				int i = 0;
				string strTempCmd;

				while(cmdStack.Count > 0)
				{
					strTempCmd = cmdStack.Dequeue();
					Debug.WriteLine(string.Format("{0}: " + strTempCmd, i), "Command in Stack");
					i++;
					/*
					 * 
					 * Timer Start			= 		Starten der Timer
					 * Timer Stop			= 		Stoppen der aller Timer
					 * Timer Intervall [Normal|Warnung|Critical] = Setzen der Timer Intervalle
					 * Timer Reset			=		Alle Timer auf Default Werte zurücksetzen. Stoppen und erneut starten.
					 * Prozess [ADD|RESET|REMOVE][NAME|PID]		= 		Setzen des Prozessnamen für Überwachung, reinizialisieren der Timer
					 * Prozess Status		= 		Schreiben einer Datei mit allen Metadaten zur Prozessüberwachung
					 * Log Path				= 		Setzen des Logverzeichnisses
					 * Screenshot			=		Auslösen eines Screenshots
					 * Quit|Exit			= 		Beenden der Anwendung
					 * 
					 */
					
					if(Regex.IsMatch(strTempCmd, "^(Timer|Prozess|Log|Screenshot|Quit|Exit)",RegexOptions.IgnoreCase | RegexOptions.Multiline))
					{
						if (strTempCmd.ToLower().StartsWith("Timer".ToLower(), StringComparison.CurrentCulture)) {
							execCommandTimer(strTempCmd);
						}
						if (strTempCmd.ToLower().StartsWith("Prozess".ToLower(), StringComparison.CurrentCulture)) {
							execCommandProcess(strTempCmd);
						}
						if (strTempCmd.ToLower().StartsWith("Log".ToLower(), StringComparison.CurrentCulture)) {
							execCommandLog(strTempCmd);
						}
						if (strTempCmd.ToLower().StartsWith("Screenshot".ToLower(), StringComparison.CurrentCulture)) {
							execCommandScreenshot(strTempCmd);
						}
						if (strTempCmd.ToLower().StartsWith("Exit".ToLower(), StringComparison.CurrentCulture)) {
							execCommandQuit(strTempCmd);
						}
						if (strTempCmd.ToLower().StartsWith("Quit".ToLower(), StringComparison.CurrentCulture)) {
							execCommandQuit(strTempCmd);
						}
					}
				}
			}
		}
		
		/// <summary>
		/// Command zum steuern der Timer Funktionen
		/// </summary>
		/// <param name="strCommand"></param>
		/// <returns></returns>
		bool execCommandTimer(string strCommand)
		{
			/*
			 * 
			 * Timer Start [PID]	= 		Starten der Timer
			 * Timer Stop [PID]		= 		Stoppen der aller Timer
			 * Timer Intervall [PID][Normal|Warnung|Critical] = Setzen der Timer Intervalle
			 * Timer Reset [PID]	=		Alle Timer auf Default Werte zurücksetzen. Stoppen und erneut starten.
			 * 
			 */
			
			Dictionary<string, object> dicCmd = new Dictionary<string, object>();
			List<string> cmds = new List<string>() {"START", "STOP", "INTERVAL", "RESET"};
			bool hasValue;
			string tmp = "";
			string key = "";
			
			for(int i = 0; i < cmds.Count; i++)
			{
				key = cmds[i];
				if((tmp = ParseCmdLineParam(key, strCommand, out hasValue)) != string.Empty) 
				{
					switch (key.ToUpper())
					{
						case "START":
							dicCmd.Add(key, tmp);
							tmp = ParseCmdLineParam("PID", strCommand, out hasValue);
							if(hasValue && tmp != string.Empty)
							{
								int iPid = 0;
								if(int.TryParse(tmp, out iPid))
									dicCmd.Add("PID", iPid);
							}
							break;
						
						case "STOP":
							dicCmd.Add(key, tmp);
							tmp = ParseCmdLineParam("PID", strCommand, out hasValue);
							if(hasValue && tmp != string.Empty)
							{
								dicCmd.Add("PID", tmp);
							}
							break;
						
						case "INTERVAL":
							dicCmd.Add(key, tmp);
							tmp = ParseCmdLineParam("PID", strCommand, out hasValue);
							if(hasValue && tmp != string.Empty)
							{
								dicCmd.Add("PID", tmp);
							}
							break;
						
						case "RESET":
							dicCmd.Add(key, tmp);
							tmp = ParseCmdLineParam("PID", strCommand, out hasValue);
							if(hasValue && tmp != string.Empty)
							{
								dicCmd.Add("PID", tmp);
							}
							break;
					}
				}
				Debug.WriteLine("Command eingelesen: " + strCommand, "execCommandTimer()");
			}
			
			
			foreach (var element in dicCmd) {
				Debug.WriteLine(element.Key + "=" + element.Value, "ParameterTimer():Key=Value");
			}
			return true;
		}
		
		/// <summary>
		/// Command zum steuern der Überwachten Prozesse
		/// Add: Einen weiteren Prozess der Überwachung hinzufügen
		/// Reset: Alle Timer zurücksetzen
		/// Remove: Einen Prozess aus der Überwachung entfernen
		/// </summary>
		/// <param name="strCommand"></param>
		/// <returns></returns>
		bool execCommandProcess(string strCommand)
		{
			/*
			 * Prozess [ADD|RESET|REMOVE][NAME]		= 		Setzen des Prozessnamen für Überwachung, reinizialisieren der Timer
			 * Prozess Status		= 		Schreiben einer Datei mit allen Metadaten zur Prozessüberwachung
			 * 
			 */
			
			Dictionary<string, object> dicCmd = new Dictionary<string, object>();
			
			List<string> cmds = new List<string>() {"ADD", "REMOVE", "RESET"};
			bool hasValue;
			string tmp = "";
			string key = "";
			
			for(int i = 0; i < cmds.Count; i++)
			{
				key = cmds[i];
				if((tmp = ParseCmdLineParam(key, strCommand, out hasValue)) != string.Empty) 
				{
					//	Command gefunden
					break;
				}
			}
			
			//	#####
			//	Aufsplitten der Parameter
			//	#####
			switch (key.ToUpper())
			{
				case "ADD":
					dicCmd.Add(key, tmp);
					tmp = ParseCmdLineParam("PID", strCommand, out hasValue);
					if(hasValue && tmp != string.Empty)
					{
						int iPid = 0;
						if(int.TryParse(tmp, out iPid))
							dicCmd.Add("PID", iPid);
					}
					tmp = ParseCmdLineParam("NAME", strCommand, out hasValue);
					if(hasValue && tmp != string.Empty)
					{
						dicCmd.Add("NAME", tmp);
					}

					break;
				
				case "REMOVE":
					dicCmd.Add(key, tmp);
					tmp = ParseCmdLineParam("PID", strCommand, out hasValue);
					if(hasValue && tmp != string.Empty)
					{
						int iPid = 0;
						if(int.TryParse(tmp, out iPid))
							dicCmd.Add("PID", iPid);
					}
					tmp = ParseCmdLineParam("NAME", strCommand, out hasValue);
					if(hasValue && tmp != string.Empty)
					{
						dicCmd.Add("NAME", tmp);
					}
					break;
				
				case "RESET":
					dicCmd.Add(key, tmp);
					tmp = ParseCmdLineParam("PID", strCommand, out hasValue);
					if(hasValue && tmp != string.Empty)
					{
						dicCmd.Add("PID", tmp);
					}
					break;
			}
			//	#####



			//	#####
			//	Verarbeiten der Commandos ADD
			//	#####
			object cmd, pid, name;
			if (dicCmd.TryGetValue("ADD", out cmd)) 
			{
				int PID;
				string NAME = "";
				clsProcessTimer pst;
				
				//	#####
				//	Verarbeiten der Commandos
				//	Hinzufügen per Prozess ID
				//	#####
				if(dicCmd.TryGetValue("PID", out pid))
				{
					PID = (int)pid;
					Process ps = null;
					
					try {
						ps = Process.GetProcessById(PID);
						if (ps != null) {
							garbageTimer.Enabled = false;

							pst = new clsProcessTimer(ps.Id, clsProcessTimer.enumIntervallState.normalInterval);
							psTimer.Add(pst);
							processToWatch = ps.ProcessName;
							pst.Elapsed += timerTick;
							pst.Enabled = true;

							garbageTimer.Enabled = true;

							Debug.WriteLine(string.Format("Neuen Prozess zur Überwachung hinzufügen: {0} / {1}",ps.ProcessName, ps.Id), "execCommandProcess():ADD|PID");
						}
					} catch (ArgumentException ae) {
						
						Debug.WriteLine("Error: AddProcess - Die Prozess ID: " + PID + " konnte nicht gefunden werden", "execCommandProcess():ADD|PID");
						Debug.WriteLine(ae.Message, "execCommandProzess():ADD|NAME");
					}
				}
				
				//	#####
				//	Verarbeiten der Commandos
				//	Hinzufügen per Prozess Name
				//	Übernimmt alle gefundenen Instanzen in die Überwachung
				//	#####
				if(dicCmd.TryGetValue("NAME", out name))
				{
					NAME = name as string;

					try {
						foreach(Process pcs in Process.GetProcessesByName(NAME))
						{
							psTimer.Add(pst = new clsProcessTimer(pcs.Id));
							processToWatch = pcs.ProcessName;
							pst.Enabled = true;
							pst.Elapsed += timerTick;
							Debug.WriteLine(string.Format("Neuen Prozess zur Überwachung hinzufügen: {0} / {1}",pcs.ProcessName, pcs.Id), "execCommandProcess():ADD|NAME");
						}
					} catch (ArgumentException ae) {
						
						Debug.WriteLine("Error: AddProcess - Der Prozess mit dem Namen: " + NAME + " konnte nicht gefunden werden", "execCommandProzess():ADD|NAME");
						Debug.WriteLine(ae.Message, "execCommandProzess():ADD|NAME");
					}							
				}
			}
			
			
			//	#####
			//	Verarbeiten der Commandos ADD
			//	#####
			if (dicCmd.TryGetValue("REMOVE", out cmd)) 
			{
				int PID;
				string NAME = "";
				clsProcessTimer pst;
				
				//	#####
				//	Verarbeiten der Commandos
				//	Entfernen per Prozess ID
				//	#####
				if(dicCmd.TryGetValue("PID", out pid))
				{
					PID = (int)pid;
					Process ps = null;
					
					try {
						ps = Process.GetProcessById(PID);
						if (ps != null) {
							garbageTimer.Enabled = false;
							
							var obsoleteTimer = psTimer.Where(t => t.processID == PID).ToList();
							int cntRemove = obsoleteTimer.Count;
				
							foreach(clsProcessTimer pt in obsoleteTimer)
							{
								pt.Enabled = false;
								psTimer.Remove(pt);
								Debug.WriteLine("nicht mehr benötigten Timer entfernen PID: " + pt.processID, "execCommandProcess():REMOVE|PID");
								pt.Dispose();
							}
							
							//if (Process.GetProcessesByName(ps.ProcessName).Count > 1) 
							{
								//	Wenn > 1, dann sind noch mehr Instanzen vorhanden
								var n = Process.GetProcessesByName(ps.ProcessName).Select(p => p.Id).Except(psTimer.Select(t => t.processID)).ToList();
								if(n.Count == cntRemove)
								{
									//	Keine aktiven Timer auf weiteren Instanzen zu diesem Prozess vorhanden
									_ProcessListToWatch.Remove(ps.ProcessName);
								}
							}
							
							Debug.WriteLine(string.Format("Neuen Prozess zur Überwachung hinzufügen: {0} / {1}",ps.ProcessName, ps.Id), "execCommandProcess():REMOVE|PID");
						}
					} catch (ArgumentException ae) {
						
						Debug.WriteLine("Error: AddProcess - Die Prozess ID: " + PID + " konnte nicht gefunden werden", "execCommandProcess():REMOVE|PID");
						Debug.WriteLine(ae.Message, "execCommandProzess():REMOVE|PID");
					}
					finally
					{
						garbageTimer.Enabled = true;
					}
				}
				
				//	#####
				//	Verarbeiten der Commandos
				//	Entfernen per Prozess Name
				//	Entfernt alle Instanzen eines Prozesses
				//	#####
				if(dicCmd.TryGetValue("NAME", out name))
				{
					NAME = name as string;

					try {
						foreach(Process pcs in Process.GetProcessesByName(NAME))
						{
							PID = pcs.Id;
							garbageTimer.Enabled = false;
							
							var obsoleteTimer = psTimer.Where(t => t.processID == pcs.Id).ToList();
							
							foreach(clsProcessTimer pt in obsoleteTimer)
							{
								pt.Enabled = false;
								psTimer.Remove(pt);
								Debug.WriteLine("nicht mehr benötigten Timer entfernen PID: " + pt.processID, "execCommandProcess():REMOVE|NAME");
								pt.Dispose();
								
								_ProcessListToWatch.Remove(NAME);
							}
							
							Debug.WriteLine(string.Format("Neuen Prozess zur Überwachung hinzufügen: {0} / {1}", NAME, PID), "execCommandProcess():REMOVE|NAME");
						}
					} catch (ArgumentException ae) {
						
#if DEBUG
						Debug.WriteLine("Error: AddProcess - Der Prozess mit dem Namen: " + NAME + " konnte nicht gefunden werden", "execCommandProzess():ADD|NAME");
						Debug.WriteLine(ae.Message, "execCommandProzess():ADD|NAME");
#else
						throw ae.Message;
#endif				
					}							
					finally
					{
						garbageTimer.Enabled = true;
					}
				}
			}
			
			if (dicCmd.TryGetValue("RESET", out cmd)) 
			{
				garbageTimer.Enabled = false();
				
				garbageTimer.Enabled = true;
			}
			
			//	#####
#if DEBUG
			if (psTimer.Count > 0) {
				Debug.WriteLine("#####");
				foreach (clsProcessTimer element in psTimer) {
					Debug.WriteLine(string.Format("PID: {0} | Enable: {1} | Intervall: {2} | State: {3}", element.processID, element.Enabled, element.Interval, element.timerState.ToString()), "Debug: Ausgabe aller Timer|execCommandProcess():ADD|PID");
				}
				Debug.WriteLine("#####");						
			}
			else
				Debug.WriteLine("Keine Aktiven Timer", "execCommandProcess():ADD|PID");
			
			foreach (var element in dicCmd) {
				Debug.WriteLine(element.Key + "=" + element.Value, "execCommandProcess():Key=Value");
			}
			Debug.WriteLine("Command eingelesen: " + strCommand, "execCommandProcess()");
			//	#####
#endif				
			return true;
		}
		
		/// <summary>
		/// Command zum Steuern der Log Funktionen
		/// </summary>
		/// <param name="strCommand"></param>
		/// <returns></returns>
		bool execCommandLog(string strCommand)
		{
			/*
			 * 
			 * Log Path				= 		Setzen des Logverzeichnisses
			 * 
			 */
			Dictionary<string, string> dicCmd = new Dictionary<string, string>() {
													{"LOG", ""},
												};
			
			Debug.WriteLine("Command eingelesen: " + strCommand, "execCommandLog()");
			return true;
		}

		/// <summary>
		/// Command zum steuern der Screenshot Funktion
		/// </summary>
		/// <param name="strCommand"></param>
		/// <returns></returns>
		bool execCommandScreenshot(string strCommand)
		{
			/*
			 * Screenshot			=		Auslösen eines Screenshots
			 */
			
			Dictionary<string, string> dicCmd = new Dictionary<string, string>() {
													{"SCREENSHOT", ""},
												};
			
			Debug.WriteLine("Command eingelesen: " + strCommand, "execCommandScreenshot()");
			return true;
		}

		/// <summary>
		/// Command zum beenden der Anwendung
		/// </summary>
		/// <param name="strCommand"></param>
		/// <returns></returns>
		bool execCommandQuit(string strCommand)
		{
			/*
			 * 
			 * Quit|Exit			= 		Beenden der Anwendung
			 * 
			 */
			Debug.WriteLine("Command eingelesen: " + strCommand, "execCommandQuit()");
			if (psTimer.Count > 0) {
				foreach (var ps in psTimer) {
					ps.Stop();
				}
			}
			Application.Exit();
			return true;
		}
		#endregion
	}
}
