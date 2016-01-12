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
using System.Management;

using WmiLight;

using MemoryMonitor.Forms;
using MemoryMonitor.Klassen;

namespace MemoryMonitor
{
	public sealed class NotificationIcon
	{
		private NotifyIcon notifyIcon;
		private ContextMenu notificationMenu;
		private bool bBallonTipShowing;
		private MainForm mainForm;
		private System.Windows.Forms.Timer garbageTimer;
		
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
		
//		private string csvLogFile = "";
		private string commandFile = "c:\\temp\\MemUsageLog\\command.file";
		
		//TODO: Eigenschaft als Liste erstellen.
		// Um auch mehere Anwendung unabhängig überwachen zu können
		private string _processToWatch = "JM4";

		public string processToWatch {
			get { return _processToWatch; }
			set { _processToWatch = value; }
		}
		
		string protokollSavePath = @"c:\temp\MemUsageLog\";
		string protokollFileNamePrefix = @"MemLog";
		string protokollFileLogExt = "csv";
		string protokollImageExt = "png";
		
		#region Initialize icon and menu
		public NotificationIcon()
		{
			notifyIcon = new NotifyIcon();
			notificationMenu = new ContextMenu(InitializeMenu());
			List<Process> ps = new List<Process>();
			psTimer = new List<clsProcessTimer>();
			m_Watcher = new FileSystemWatcher();
			garbageTimer = new System.Windows.Forms.Timer();
			
			ps.AddRange(Process.GetProcessesByName(processToWatch));
			for (int i = 0; i < ps.Count; i++) {
				psTimer.Add(new clsProcessTimer(ps[i].Id, clsProcessTimer.enumIntervallState.normalInterval));
				psTimer[i].Tick += timerTick;
				psTimer[i].Enabled = true;
			}
			garbageTimer.Interval = 6000;		//	Aufräumen der nicht benötigten Prozesstimer
			garbageTimer.Tick += garbageTimerTick;
			garbageTimer.Start();
			
			
			notifyIcon.BalloonTipShown += iconShowBallonToolTip;
			notifyIcon.BalloonTipClosed += iconCloseBallonToolTip;
			notifyIcon.BalloonTipClicked += iconClickBallonToolTip;
			
			notifyIcon.DoubleClick += IconDoubleClick;
			notifyIcon.MouseMove += IconMouseOver;
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(NotificationIcon));
			notifyIcon.Icon = (Icon)resources.GetObject("$this.Icon");
			notifyIcon.ContextMenu = notificationMenu;
			
			getRemoteWMIData_WMILight();
			
			#region systemfilewatcher
			m_Watcher.Path = "c:\\temp\\MemUsageLog\\";
			m_Watcher.Filter = "command.file";
			m_Watcher.NotifyFilter = 
//						NotifyFilters.LastAccess |
//                        NotifyFilters.LastWrite | 
//                        NotifyFilters.FileName | 
						NotifyFilters.Size 
//						NotifyFilters.Attributes |
//						NotifyFilters.CreationTime |
//						NotifyFilters.Security |
//                        NotifyFilters.DirectoryName
				;
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
//			notifyIcon.Text = "Mouseoverevent()";
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
//			MessageBox.Show("About This Application");
		}
		
		private void menuExitClick(object sender, EventArgs e)
		{
			//timer.Enabled = false;
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
//			List<Process> ps = new List<Process>();
//			ps.AddRange(Process.GetProcessesByName("JM4"));
			Process pcs = null;;
			
			
			if ((sender as clsProcessTimer) != null)
			{
				int psID = 0;
				//pcs = Process.GetProcessById(((clsProcessTimer)sender).processID);
				try {
					psID = ((clsProcessTimer)sender).processID;
					pcs = Process.GetProcessById(psID);
				} catch (ArgumentException ae) {
					
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
			}
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
			                                    strDate,//DateTime.Now.ToString("yyyyMMdd-HHmmss"), 
			                                    count > 0 ? "_"+count : "",
			                                    filetype == 0 ? protokollFileLogExt : protokollImageExt,
			                                    strDate,
			                                    pid > 0 ? "_"+pid : ""
			                                   );
			//	####
			
//			if (!string.IsNullOrWhiteSpace("")) {
//				
//			}
			return FullFileName;
		}
		
		void getRemoteWMIData_WMILight()
		{
//			var wmiQuery = "Select * From Win32_Process";
//			var wmiQuery = "Select Name,ProcessID,commandline,WorkingSetSize,peakWorkingSetSize From Win32_Process where Name = 'JM4.exe'";
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
			//	#####
			//	Zuerst alle nicht mehr benötigten Timer entfernen
			//	#####
			
			var obsoleteTimer = psTimer.Where(t => t.obsolete).ToList();

			foreach(clsProcessTimer pt in obsoleteTimer)
			{
				psTimer.Remove(pt);
				Debug.WriteLine("PID2: " + pt.processID, "ObsoleteTimer");
				pt.Dispose();
			}

			//	#####
			//	Neue Prozesse mit neuen Timer hinzufügen
			//	#####
			//	Liste mit Instanzen des zu überwachenden Prozess
			List<Process> PS = new List<Process>();
			PS.AddRange(Process.GetProcessesByName(processToWatch));
			
			if (PS.Count > 0) 
			{
				var n = PS.Select(p => p.Id).Except(psTimer.Select(t => t.processID));
				foreach(var z in n)
				{
					Debug.WriteLine("PID2 noch nicht in der liste: " + z);
					
					clsProcessTimer cpt = new clsProcessTimer(z, clsProcessTimer.enumIntervallState.normalInterval);
					psTimer.Add(cpt);
					cpt.Tick += timerTick;
					cpt.Enabled = true;
				}
			}
			else
			{
				//TODO:	Alle Timer Freigeben. Es ist kein Prozess mehr vorhanden
			}
			
			PS = null;
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
			//string[] alias;
			
			if (hostFileList == null) {
				hostFileList = strHostFileList;
			}
			
			if (File.Exists(hostFileList)) 
			{
				hosts = new List<string>();
//				clbRoutineListe.Items.Clear();
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
//							else
//								strHostGroup = strTmp;								
						}
						
//						if (hosts.Count>0) {
//							clbRoutineListe.Items.Clear();
//							clbRoutineListe.Items.AddRange(hosts.ToArray());
//						}
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
				Stack<string> commandStack = new Stack<string>();
				
				if (fi.Length > 0) {
					Debug.WriteLine(string.Format("{0} Datei wurde geändert, Command Wait", DateTime.Now.ToString()), "readCommandFromFile() - Change");
					
					foreach(string cmd in File.ReadLines(commandFile))
					{
						if (!string.IsNullOrEmpty(cmd)) {
							commandStack.Push(cmd);
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
		
		void executeCommandsFromFile(Stack<string> cmdStack)
		{
			if (cmdStack.Count > 0) {
				//for(int i = cmdStack.Count; i > 0; i--)
				int i = 0;
				string strTempCmd;
				while(cmdStack.Count > 0)
				{
					strTempCmd = cmdStack.Pop();
					Debug.WriteLine(string.Format("{0}: " + strTempCmd, i), "Command in Stack");
					i++;
					/*
					 * 
					 * Timer Start			= 		Starten der Timer
					 * Timer Stop			= 		Stoppen der aller Timer
					 * Timer Intervall [Normal|Warnung|Critical] = Setzen der Timer Intervalle
					 * Timer Reset			=		Alle Timer auf Default Werte zurücksetzen. Stoppen und erneut starten.
					 * Prozess [ADD|RESET|REMOVE][NAME]		= 		Setzen des Prozessnamen für Überwachung, reinizialisieren der Timer
					 * Log Path				= 		Setzen des Logverzeichnisses
					 * Screenshot			=		Auslösen eines Screenshots
					 * Quit|Exit			= 		Beenden der Anwendung
					 * 
					 */
					
					if(Regex.IsMatch(strTempCmd, "^(Timer|Prozess|Log|Screenshot|Quit|Exit)",RegexOptions.IgnoreCase | RegexOptions.Multiline))
					{
						if (strTempCmd.StartsWith("Timer", StringComparison.CurrentCulture)) {
							execCommandTimer(strTempCmd);
						}
						if (strTempCmd.StartsWith("Prozess", StringComparison.CurrentCulture)) {
							execCommandProcess(strTempCmd);
						}
						if (strTempCmd.StartsWith("Log", StringComparison.CurrentCulture)) {
							execCommandLog(strTempCmd);
						}
						if (strTempCmd.StartsWith("Screenshot", StringComparison.CurrentCulture)) {
							execCommandScreenshot(strTempCmd);
						}
						if (strTempCmd.StartsWith("Exit", StringComparison.CurrentCulture)) {
							execCommandQuit(strTempCmd);
						}
						if (strTempCmd.StartsWith("Quit", StringComparison.CurrentCulture)) {
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
			Debug.WriteLine("Command eingelesen: " + strCommand, "execCommandTimer()");
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
			Debug.WriteLine("Command eingelesen: " + strCommand, "execCommandProcess()");
			return true;
		}
		
		/// <summary>
		/// Command zum Steuern der Log Funktionen
		/// </summary>
		/// <param name="strCommand"></param>
		/// <returns></returns>
		bool execCommandLog(string strCommand)
		{
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
			Debug.WriteLine("Command eingelesen: " + strCommand, "execCommandQuit()");
			return true;
		}
		#endregion
	}
}
