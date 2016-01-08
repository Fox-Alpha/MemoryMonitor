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
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;
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
		private ToolTip iconToolTip;
		private bool bBallonTipShowing;
		private MainForm mainForm;
		//private System.Windows.Forms.Timer timer;
		private List<Process> ps;
		private List<clsProcessTimer> psTimer;
		
		private int warningMemUsage = 800000000;
		private int criticalMemUsage = 850000000;
		
		
		private string csvLogFile = "";
		private string processToWatch = "JM4";
		
		string protokollSavePath = @"c:\temp\MemUsageLog\";
		string protokollFileNamePrefix = @"MemLog";
		string protokollFileLogExt = "csv";
		string protokollImageExt = "png";
		
		#region Initialize icon and menu
		public NotificationIcon()
		{
			notifyIcon = new NotifyIcon();
			notificationMenu = new ContextMenu(InitializeMenu());
			iconToolTip = new ToolTip();
			ps = new List<Process>();
			psTimer = new List<clsProcessTimer>();
			
			ps.AddRange(Process.GetProcessesByName(processToWatch));
			for (int i = 0; i < ps.Count; i++) {
				psTimer.Add(new clsProcessTimer(ps[i].Id, clsProcessTimer.enumIntervallState.normalInterval));
				psTimer[i].Tick += timerTick;
				psTimer[i].Enabled = true;
			}
			
			
			notifyIcon.BalloonTipShown += iconShowBallonToolTip;
			notifyIcon.BalloonTipClosed += iconCloseBallonToolTip;
			notifyIcon.BalloonTipClicked += iconClickBallonToolTip;
			
			notifyIcon.DoubleClick += IconDoubleClick;
			notifyIcon.MouseMove += IconMouseOver;
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(NotificationIcon));
			notifyIcon.Icon = (Icon)resources.GetObject("$this.Icon");
			notifyIcon.ContextMenu = notificationMenu;
			
//			getRemoteWMIData();
			getRemoteWMIData_WMILight();
			
//			csvLogFile = Path.GetDirectoryName(Application.ExecutablePath) + "\\ProcessLog.csv";

//			timer = new System.Windows.Forms.Timer();	//(timerTick, null, Infinite, 15000);
//			timer.Tick += timerTick;
//			timer.Interval = normalInterval;
//			timer.Enabled = true;
			
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
			}
			Application.Exit();
		}
		
		private void IconDoubleClick(object sender, EventArgs e)
		{
		}
		#endregion
		
		void timerTick(object sender, EventArgs e)
		{
			ps.Clear();
			ps.AddRange(Process.GetProcessesByName("JM4"));
			Process pcs = null;;
			
			if ((sender as clsProcessTimer) != null)
			{
				pcs = Process.GetProcessById(((clsProcessTimer)sender).processID);
					
			}
			
			string strCsv;
			string fileName;
			
			if (pcs != null) {
				//foreach (Process pcs in ps) {
					fileName = checkFileName(ps.IndexOf(pcs)+1, pcs.ProcessName, pcs.Id);
					Debug.WriteLine(string.Format("PID: {0} | Name: {1} | Time: {2}| CurrentMem: {3}| PeakMem: {4}", pcs.Id, pcs.ProcessName, DateTime.Now.ToString(), pcs.PrivateMemorySize64.ToString(), pcs.PeakWorkingSet64), "TimerTick()");
					strCsv = string.Format("{0};\"{1}\";\"{2}\";{3};{4}\r\n", DateTime.Now.ToString("HH:mm:ss"), pcs.Id, pcs.ProcessName, pcs.PrivateMemorySize64.ToString(), pcs.PeakWorkingSet64);
					
					if (!File.Exists(fileName)) {
						File.AppendAllText(fileName, "Time;PID;Name;MemUsage;PeakMemUsage\r\n", new UTF8Encoding(false));
					}
					File.AppendAllText(fileName, strCsv, new UTF8Encoding(false));
					
					if (pcs.PeakWorkingSet64 > warningMemUsage && pcs.PeakWorkingSet64 < criticalMemUsage) {
						if ((sender as clsProcessTimer) != null) {
							((clsProcessTimer)sender).setIntervalState(clsProcessTimer.enumIntervallState.warningInterval);
						}
						else if (pcs.PeakWorkingSet64 > criticalMemUsage) {
							((clsProcessTimer)sender).setIntervalState(clsProcessTimer.enumIntervallState.criticalInterval);
						}
						else
							((clsProcessTimer)sender).setIntervalState(clsProcessTimer.enumIntervallState.normalInterval);
					}
				//}
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
			string FullFileName = string.Format("{0}\\{6}\\{1}_{2}{7}_{3}_{4}.{5}",
			                                    protokollSavePath, 
			                                    protokollFileNamePrefix, 
			                                    suffix == string.Empty ? "" : suffix,
			                                    strDate,//DateTime.Now.ToString("yyyyMMdd-HHmmss"), 
			                                    count,
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

	}
}
