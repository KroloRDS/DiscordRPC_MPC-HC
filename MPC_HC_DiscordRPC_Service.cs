﻿using System;
using System.Text;
using System.Timers;
using System.Diagnostics;
using System.ServiceProcess;
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
			bool playing = false;
			string title = "";
			string positionstring = "";
			GetVariables(ref playing, ref title, ref positionstring);

			return new RichPresence()
			{
				Details = title,
				State = playing ? "Now Playing" : "Paused",
				Timestamps = playing ? GetPlaybackStartTime(positionstring) : new Timestamps(),
				Assets = new Assets()
				{
					LargeImageKey = "logo",
					SmallImageKey = playing ? "play" : "pause"
				}
			};
		}

		//Getting and parsing HTML to get values
		private void GetVariables(ref bool playing, ref string title, ref string positionstring)
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
						title = node.InnerText;
						break;
					case "positionstring":
						positionstring = node.InnerText;
						break;
				}
			}
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
