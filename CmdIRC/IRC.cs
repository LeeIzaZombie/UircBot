using System;
using Rocket.Unturned;
using UnityEngine;
using System.Threading;
using Meebey.SmartIrc4net;
using Rocket.Unturned.Player;
using Rocket.Unturned.Plugins;
using Rocket.API;

namespace RocketMod
{
    public class PluginShutdown : RocketPlugin<IRCConfig>
    {
        static IrcClient irc = new IrcClient();
        static string server, channel, nick;
        static int port;
        static Thread ircThread;

        public string Name
        {
            get { return "irc"; }
        }

        protected override void Load()
        {
            server = this.Configuration.server;
            channel = this.Configuration.channel;
            nick = this.Configuration.nick;
            port = this.Configuration.port;

            if (server == "changeme" || channel == "#changeme")
            {
                RocketChat.Say("Error: Please make sure you configure the IRC settings! This message is normal if you have just started the plugin. Apply your settings and restart the server! Unloading plugin...");
                this.Unload();
                return;
            }

            Rocket.Unturned.Events.RocketPlayerEvents.OnPlayerChatted += RocketPlayerEvents_OnPlayerChatted;
            Rocket.Unturned.Events.RocketServerEvents.OnPlayerConnected += RocketServerEvents_OnPlayerConnected;
            Rocket.Unturned.Events.RocketServerEvents.OnPlayerDisconnected += RocketServerEvents_OnPlayerDisconnected;
            Rocket.Unturned.Events.RocketServerEvents.OnServerShutdown += RocketServerEvents_OnServerShutdown;

            ircThread = new Thread(new ThreadStart(delegate
            {
                irc.OnConnected += new EventHandler(OnConnected);
                irc.OnChannelMessage += new IrcEventHandler(OnChanMessage);
                irc.OnDisconnected += new EventHandler(OnDisconnected);
                irc.OnQueryMessage += irc_OnQueryMessage;

                try { irc.Connect(server, port); RocketChat.Say("Connecting to: " + server + " port: " + port); }
                catch (Exception ex) 
                {
                    RocketChat.Say("There was an error connecting to IRC.", Color.red); 
                    
                }
            }));
            ircThread.Start();
        }

        void irc_OnQueryMessage(object sender, IrcEventArgs e)
        {
            //For some reason it juss won work... Probably need to get latest IRC library.
        }

        void RocketServerEvents_OnServerShutdown()
        {
            irc.SendMessage(SendType.Message, channel, this.Configuration.shutdownMessage);
            irc.RfcQuit("Server Shutdown!");
        }

        void RocketServerEvents_OnPlayerDisconnected(RocketPlayer player)
        {
            if (this.Configuration.showJoinLeaveMsgs)
                irc.SendMessage(SendType.Message, channel, player.CharacterName + ", has left the server.");
        }

        void RocketServerEvents_OnPlayerConnected(RocketPlayer player)
        {
            if (this.Configuration.showJoinLeaveMsgs)
                irc.SendMessage(SendType.Message, channel, player.CharacterName + ", has joined the server!");
        }

        void RocketPlayerEvents_OnPlayerChatted(RocketPlayer player, ref Color color, string message)
        {
            irc.SendMessage(SendType.Message, channel, player.CharacterName + ": " + message);
            //Area, World and Global chat will show, as far as I'm aware, we can't find out what type the message is.
        }

        private void OnDisconnected(object sender, EventArgs e)
        {
            RocketChat.Say("[IRC] Server got disconnected.", Color.red);
        }

        private void OnChanMessage(object sender, IrcEventArgs e)
        {
            RocketChat.Say("[IRC] " + e.Data.Nick + ": " + e.Data.Message, Color.yellow);
        }

        private void OnConnected(object sender, EventArgs e)
        {
            RocketChat.Say("[IRC] Server is now connecting to IRC.", Color.yellow);
            irc.Login(nick, nick, 0, nick);
            //irc.SendMessage(SendType.Message, "nickserv", "IDENTIFY " + "password");
            irc.RfcJoin(channel);
            RocketChat.Say("[IRC] Joining channel: " + channel + " - " + server, Color.yellow);
            irc.Listen();
        }
    }

    public class IRCConfig : IRocketPluginConfiguration
    {
        public string server, channel, nick, shutdownMessage;
        public int port;
        public bool showJoinLeaveMsgs;
        Color IRCColor;
        public IRocketPluginConfiguration DefaultConfiguration
        {
            get
            {
                return new IRCConfig()
                {
                    server = "changeme",
                    channel = "#changeme",
                    nick = "UircBot",
                    port = 6667, 
                    showJoinLeaveMsgs = true,
                    shutdownMessage = "Server is shutting down! Bye bye!",
                    IRCColor =  RocketChat.GetColorFromName("yellow", Color.yellow)
                };
            }
        }
    }
}
