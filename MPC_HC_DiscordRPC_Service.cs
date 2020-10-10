using System;
using System.Net;
using System.Text;
using System.Timers;
using System.Diagnostics;
using System.ServiceProcess;
using DiscordRPC;

namespace MPC_HC_DiscordRPC_Service
{
	public partial class MPC_HC_DiscordRPC_Service : ServiceBase
	{
		private const string discordAppID = "764435247169273877";
		private const string mpcUrl = "http://localhost:13579/variables.html";
		private readonly EventLog eventLog;
		private DiscordRpcClient discordClient;

		public MPC_HC_DiscordRPC_Service()
		{
			InitializeComponent();

			//Create event log
			string source = "MPC-HC DiscordRPC Service";
			string log = "MPC-HC DiscordRPC Service Log";
			eventLog = new EventLog();
			if (!EventLog.SourceExists(source))
			{
				EventLog.CreateEventSource(source, log);
			}
			eventLog.Source = source;
			eventLog.Log = log;
		}

		protected override void OnStart(string[] args)
		{
			discordClient = new DiscordRpcClient(discordAppID);
			discordClient.Initialize();

			// Set up a timer that triggers every 5 seconds
			Timer timer = new Timer(5000);
			timer.Elapsed += new ElapsedEventHandler(OnTimer);
			timer.Start();
		}

		protected override void OnStop()
		{
			discordClient.ClearPresence();
			discordClient.Dispose();
		}

		private void OnTimer(object sender, ElapsedEventArgs args)
		{
			//Check if MPC-HC is running
			if (Process.GetProcessesByName("mpc-hc64").Length > 0 ||
				Process.GetProcessesByName("mpc-hc32").Length > 0 ||
				Process.GetProcessesByName("mpc-hc").Length > 0)
			{
				discordClient.SetPresence(GetStatus());
			}
			else
			{
				discordClient.ClearPresence();
			}
		}

		//Gets values from MPC-HC web interface and creates discord status
		private RichPresence GetStatus()
		{
			string htmlStr = GetHtmlString();
			bool playing = GetVariable(htmlStr, "state").Equals("2");
			if (string.IsNullOrEmpty(htmlStr))
			{
				return null;
			}

			return new RichPresence()
			{
				Details = GetVariable(htmlStr, "file"),
				State = playing ? "Now Playing" : "Paused",
				Timestamps = playing ? GetPlaybackStartTime(htmlStr) : new Timestamps(),
				Assets = new Assets()
				{
					LargeImageKey = "logo",
					SmallImageKey = playing ? "play" : "pause"
				}
			};
		}

		//Download UTF-8 HTML string
		private string GetHtmlString()
		{
			try
			{
				using (WebClient webClient = new WebClient())
				{
					webClient.Encoding = Encoding.UTF8;
					return webClient.DownloadString(mpcUrl);
				}
			}
			catch (Exception e)
			{
				eventLog.WriteEntry("Can't download HTML from MPC Web Interface: " + e.Message);
				return "";
			}
		}

		//Gets variable value from html string
		private string GetVariable(string htmlStr, string variableName)
		{
			//Variable name is paragraph element id
			string startStr = variableName + "\">";
			string endStr = "</p>";

			//Search for substring contained within specific paragraph element
			if (htmlStr.Contains(startStr) && htmlStr.Contains(endStr))
			{
				int startPos, endPos;
				startPos = htmlStr.IndexOf(startStr, 0) + startStr.Length;
				endPos = htmlStr.IndexOf(endStr, startPos);
				return htmlStr.Substring(startPos, endPos - startPos);
			}

			eventLog.WriteEntry("Can't find value for variable name: " + variableName);
			return "";
		}

		private Timestamps GetPlaybackStartTime(string htmlStr)
		{
			string positionstring = GetVariable(htmlStr, "positionstring");
			if (string.IsNullOrEmpty(positionstring))
			{
				return new Timestamps();
			}

			DateTime startTime = DateTime.UtcNow;
			TimeSpan span = TimeSpan.Parse(positionstring);
			startTime = startTime.Subtract(span);
			return new Timestamps(startTime);
		}
	}
}
