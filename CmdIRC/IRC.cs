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
                irc.OnQueryMessage += new IrcEventHandler(OnQueryMessage);

                try { irc.Connect(server, port); RocketChat.Say("Connecting to: " + server + " port: " + port); }
                catch (Exception ex) 
                {
                    RocketChat.Say("There was an error connecting to IRC.", Color.red); 
                    
                }
            }));
            ircThread.Start();
        }

        private void OnQueryMessage(object sender, IrcEventArgs e) //For some reason this just disconnects the server...
        {
            ChannelUser user = irc.GetChannelUser(channel, e.Data.Nick);

            if (user.IsIrcOp || user.IsOp)
            {
                string cmd;
                string msg;
                int len = e.Data.Message.Split(' ').Length;
                cmd = e.Data.Message.Split(' ')[0];

                if (len > 1)
                {
                    msg = e.Data.Message.Substring(e.Data.Message.IndexOf(' ')).Trim();
                }
                else
                {
                    msg = "";
                }
                string plr = msg.Split()[0];
                RocketPlayer p = RocketPlayer.FromName(plr);

                if (msg != "")
                {
                    switch (cmd)
                    {
                        case "help":
                            irc.SendMessage(SendType.Message, e.Data.Nick, "kick (player) - Kicks the player from the server.");
                            irc.SendMessage(SendType.Message, e.Data.Nick, "ban (player) (duration) - Bans the player from the server.");
                            irc.SendMessage(SendType.Message, e.Data.Nick, "whois (player) - Shows basic information about the player.");
                            irc.SendMessage(SendType.Message, e.Data.Nick, "god (player) - Toggles godmode for player.");
                            break;

                        case "kick":
                            if (p == null)
                            {
                                irc.SendMessage(SendType.Message, e.Data.Nick, "Could not find player: " + plr);
                            }
                            else
                            {
                                p.Kick("You've been kicked by an IRC Operator!");
                            }
                            break;

                        case "ban":
                            if (p == null)
                            {
                                irc.SendMessage(SendType.Message, e.Data.Nick, "Could not find player: " + plr);
                            }
                            else
                            {
                                try { p.Ban("You've been ban by an IRC Operator!", Convert.ToUInt32(msg.Split()[1])); }
                                catch { irc.SendMessage(SendType.Message, e.Data.Nick, "There was an error trying to ban " + p.CharacterName + ". Invalid duration?"); }
                            }
                            break;

                        case "whois":

                            if (p == null)
                            {
                                irc.SendMessage(SendType.Message, e.Data.Nick, "Could not find player: " + plr);
                            }
                            else
                            {
                                irc.SendMessage(SendType.Message, e.Data.Nick, "Character Name: " + p.CharacterName);
                                irc.SendMessage(SendType.Message, e.Data.Nick, "Steam Name: " + p.SteamName);
                                irc.SendMessage(SendType.Message, e.Data.Nick, "SteamID: " + p.CSteamID.ToString());
                                irc.SendMessage(SendType.Message, e.Data.Nick, "Health: " + p.Health);
                                irc.SendMessage(SendType.Message, e.Data.Nick, "Hunger: " + p.Hunger);
                                irc.SendMessage(SendType.Message, e.Data.Nick, "Thirst: " + p.Thirst);
                                irc.SendMessage(SendType.Message, e.Data.Nick, "Infection: " + p.Infection);
                                irc.SendMessage(SendType.Message, e.Data.Nick, "Stamina: " + p.Stamina);
                                irc.SendMessage(SendType.Message, e.Data.Nick, "Experience: " + p.Experience);
                            }
                            break;

                        case "god":

                            if (p == null)
                            {
                                irc.SendMessage(SendType.Message, e.Data.Nick, "Could not find player: " + plr);
                            }
                            else
                            {
                                if (p.Features.GodMode)
                                {
                                    p.Features.GodMode = false;
                                    irc.SendMessage(SendType.Message, e.Data.Nick, "Disabled GodMode for: " + p.CharacterName);
                                    RocketChat.Say(p, e.Data.Nick + " from IRC has disabled your GodMode.");
                                }
                                else
                                {
                                    p.Features.GodMode = true;
                                    irc.SendMessage(SendType.Message, e.Data.Nick, "Enabled GodMode for: " + p.CharacterName);
                                    RocketChat.Say(p, e.Data.Nick + " from IRC has enabled GodMode for you.");
                                }
                            }
                            break;
                        default:
                            irc.SendMessage(SendType.Message, e.Data.Nick, "Only IRC OPs can use commands here!");
                            break;
                    }
                }
            }
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

            try { irc.Connect(server, port); }
            catch { RocketChat.Say("Failed to reconnect to IRC", Color.red); } //Try reconnect.
        }

        private void OnChanMessage(object sender, IrcEventArgs e)
        {
            RocketChat.Say("[IRC] " + e.Data.Nick + ": " + e.Data.Message, this.Configuration.IRCColor);
        }

        private void OnConnected(object sender, EventArgs e)
        {
            RocketChat.Say("[IRC] Server is now connecting to IRC.", Color.yellow);
            irc.Login(nick, nick, 0, nick);
            //irc.SendMessage(SendType.Message, "nickserv", "IDENTIFY " + "password");
            irc.RfcJoin(channel);
            RocketChat.Say("[IRC] Joining channel: " + channel + " - " + server, this.Configuration.IRCColor);
            irc.Listen();
        }
    }

    public class IRCConfig : IRocketPluginConfiguration
    {
        public string server, channel, nick, shutdownMessage, IRCOPS;
        public int port;
        public bool showJoinLeaveMsgs;
        public Color IRCColor;
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
