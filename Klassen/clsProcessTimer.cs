/*
 * Erstellt mit SharpDevelop.
 * Benutzer: buck
 * Datum: 07.01.2016
 * Zeit: 13:50
 * 
 */
using System;
using System.Diagnostics;

namespace MemoryMonitor.Klassen
{
	/// <summary>
	/// Description of clsProcessTimer.
	/// </summary>
	public class clsProcessTimer : System.Windows.Forms.Timer
	{
		#region properties
		
		public enum enumIntervallState
		{
			normalInterval,
			warningInterval,
			criticalInterval
		}
#if DEBUG
		private int _normalInterval = 15000;	//~ 15 sek. wenn im DEBUG
#else
		private int _normalInterval = 240000;	//~ 4Min = 240 sek.
#endif
		private int _warningInterval = 45000;	//~ 45 sek.
		private int _criticalInterval = 15000;	//~ 15 sek.

		public int normalInterval {
			get { return _normalInterval; }
			set { _normalInterval = value; }
		}
		public int warningInterval {
			get { return _warningInterval; }
			set { _warningInterval = value; }
		}
		public int criticalInterval {
			get { return _criticalInterval; }
			set { _criticalInterval = value; }
		}
		
		private bool _isRemoteHost = false;
		public bool isRemoteHost { get { return _isRemoteHost; }
			private set
			{
				if(!value)
				{
					_isRemoteHost = value;
					strHost = ".";
				}
				else
					_isRemoteHost = value;
			}
		}		
		
		string _strHost = ".";
		
		public string strHost {
			get { return _strHost; }
			private set { _strHost = isRemoteHost ? value : "."; }
		}
		
		/// <summary>
		/// Merken des aktuellen States.
		/// </summary>
		enumIntervallState _timerState = enumIntervallState.normalInterval;
		public enumIntervallState timerState {
			get { return _timerState; }
			private set { _timerState = value; }
		}
		
		/// <summary>
		/// Zugeornete Prozess ID
		/// </summary>
		int _processID;
		public int processID {
			get { return _processID; }
			private set { setProcessID(value); }
		}
		
		#endregion
		
		#region ctor
		/// <summary>
		/// Ctor
		/// </summary>
		public clsProcessTimer()
		{
			processID = 0;
			Enabled = false;
			Tick += timerTick;
			setIntervalState(enumIntervallState.normalInterval);
		}
		
		public clsProcessTimer(int PID)
		{
			processID =  PID > 0 ? PID : 0;
			Enabled = false;
			Tick += timerTick;
			setIntervalState(enumIntervallState.normalInterval);
		}

		public clsProcessTimer(int PID, enumIntervallState eIS)
		{
			processID = PID > 0 ? PID : 0;
			Enabled = false;
			Tick += timerTick;
			setIntervalState(eIS);
		}
		
		#endregion
		
		#region setter
		/// <summary>
		/// Setzen des Timerintervalls
		/// </summary>
		/// <param name="iState"></param>
		public void setIntervalState(enumIntervallState iState)
		{
			switch (iState) 
			{
				case enumIntervallState.normalInterval:
					Interval = normalInterval;
					break;
				
				case enumIntervallState.warningInterval:
					Interval = warningInterval;
					break;

				case enumIntervallState.criticalInterval:
					Interval = criticalInterval;
					break;
			}
		}
		
		private void setProcessID(int value)
		{
			//	Merken ob Timer Aktiv
			bool isEnabled = Enabled;
			
			//	Timer stoppen
			if (Enabled) {
				Enabled = false;
			}
			
			//	Intervall zurücksetzen
			setIntervalState(enumIntervallState.normalInterval);
			timerState = enumIntervallState.normalInterval;
			
			//	Intervall und PID Setzen
			//	Timer wieder starten falls vorher aktiv
			if (value > 0) {
				_processID = value;
				Enabled = isEnabled;
			}
			else		//	Falls Value <= 0
			{
				_processID = 0;
				return;	
			}
		}
		
		public void setIsRemoteHost(string strHostName)
		{
			if (strHostName == String.Empty || strHostName == "." || String.IsNullOrWhiteSpace(strHostName)) {
				isRemoteHost = false;
			}
			else
			{
				isRemoteHost = true;
				strHost = strHostName;
			}
		}
		
		#endregion
		
		#region events
		
		/// <summary>
		/// Timer Tick event
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void timerTick(object sender, EventArgs e)
		{
//			Debug.WriteLine("Tick Funktion in Klasse");
		}
		
		#endregion
	}
}
