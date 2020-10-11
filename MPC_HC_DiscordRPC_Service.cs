using System;
using System.IO;
using System.Text;
using System.Timers;
using System.Diagnostics;
using System.ServiceProcess;
using System.Text.RegularExpressions;

using DiscordRPC;
using HtmlAgilityPack;

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

			// Set up a timer that triggers every second
			Timer timer = new Timer(1000);
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
			bool playing = false;
			string title = "";
			Timestamps elapsedTime = null;
			GetVariables(ref playing, ref title, ref elapsedTime);

			return new RichPresence()
			{
				Details = title,
				State = playing ? "Now Playing" : "Paused",
				Timestamps = playing ? elapsedTime : new Timestamps(),
				Assets = new Assets()
				{
					LargeImageKey = "logo",
					SmallImageKey = playing ? "play" : "pause"
				}
			};
		}

		//Getting and parsing HTML to get values
		private void GetVariables(ref bool playing, ref string title, ref Timestamps elapsedTime)
		{
			HtmlDocument doc;
			var web = new HtmlWeb
			{
				OverrideEncoding = Encoding.UTF8
			};
			try
			{
				doc = web.Load(mpcUrl);
			}
			catch (Exception e)
			{
				eventLog.WriteEntry("Can't download HTML from MPC Web Interface: " + e.Message);
				return;
			}

			foreach (HtmlNode node in doc.DocumentNode.SelectNodes("//p[@id]"))
			{
				switch (node.Id)
				{
					case "state":
						playing = node.InnerText.Equals("2");
						break;
					case "file":
						title = ParseFilename(node.InnerText);
						break;
					case "positionstring":
						elapsedTime = GetPlaybackStartTime(node.InnerText);
						break;
				}
			}
		}

		//Try to match popular file naming schemas and extract title and episode number
		private string ParseFilename(string title)
		{
			title = Path.GetFileNameWithoutExtension(title);
			title = title.Replace('_', ' ');

			//Remove subgroup
			if (Regex.IsMatch(title, @"^\s*[\[(【].*[)\]】]"))
			{
				char[] chars = { ')', '】', ']' };
				title = title.Substring(title.IndexOfAny(chars) + 1);
				title = title.Trim();
			}

			//Don't parse stuff like 01.XXX
			//it's common for songs to have title after track no.
			//and we don't want to cut that
			if (Regex.IsMatch(title, @"\d{1,3}\."))
			{
				return title;
			}

			//Try to find episode no.
			Match match;
			Regex[] patterns = {
				new Regex(@"[Ee][Pp]\.?\s?\d{1,4}"),
				new Regex(@"[-–—−]\s?\d{1,4}")
			};

			//And remove stuff that comes after it
			foreach (Regex pattern in patterns)
			{
				match = pattern.Match(title);
				if (match.Success)
				{
					return title.Substring(0, match.Index + match.Length);
				}
			}

			//Finally, if everything else fails remove everything after first digit group
			//This won't work properly for stuff like "009 Sound System - Dreamscape", but oh well...
			Regex digitGropuPattern = new Regex(@"\d{2,4}");
			match = digitGropuPattern.Match(title);
			return match.Success ? title.Substring(0, match.Index + match.Length) : title;
		}

		private Timestamps GetPlaybackStartTime(string positionstring)
		{
			if (string.IsNullOrEmpty(positionstring))
			{
				return new Timestamps();
			}

			TimeSpan span = TimeSpan.Parse(positionstring);
			return new Timestamps(DateTime.UtcNow.Subtract(span));
		}
	}
}
