using System;
using Rocket.Unturned;
using UnityEngine;
using System.Threading;
using Meebey.SmartIrc4net;
using Rocket.Unturned.Player;
using Rocket.Unturned.Plugins;
using Rocket.API;
using System.IO;

namespace UircBot
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

            if (!Directory.Exists(Environment.CurrentDirectory + "/Extra/"))
            {
                Directory.CreateDirectory(Environment.CurrentDirectory + "/Extra/");
            }

            if (!File.Exists(Environment.CurrentDirectory + "/Extra/IRCModerators.txt"))
            {
                using (StreamWriter write = new StreamWriter(Environment.CurrentDirectory + "/Extra/IRCModerators.txt"))
                {
                    write.WriteLine("//Use the IRC nick of the IRC users you want to add in list format.");
                    write.Flush();  write.Close();
                }
            }

            ircThread = new Thread(new ThreadStart(delegate
            {
                irc.OnConnecting += new EventHandler(OnConnecting);
                irc.OnConnected += new EventHandler(OnConnected);
                irc.OnChannelMessage += new IrcEventHandler(OnChanMessage);
                irc.OnJoin += new JoinEventHandler(OnJoin);
                irc.OnPart += new PartEventHandler(OnPart);
                irc.OnQuit += new QuitEventHandler(OnQuit);
                irc.OnNickChange += new NickChangeEventHandler(OnNickChange);
                irc.OnDisconnected += new EventHandler(OnDisconnected);
                irc.OnQueryMessage += new IrcEventHandler(OnPrivMsg);
                irc.OnNames += new NamesEventHandler(OnNames);
                irc.OnChannelAction += new ActionEventHandler(OnAction);

                try { irc.Connect(server, port); RocketChat.Say("Connecting to: " + server + " port: " + port); }
                catch (Exception ex) 
                {
                    RocketChat.Say("There was an error connecting to IRC.", Color.red);
                    Reset();
                }
            }));
            ircThread.Start();
        }

        private bool IsModerator(string Nick)
        {
            bool flag = false;
            using (StreamReader read = new StreamReader(Environment.CurrentDirectory + "/Extra/IRCModerators.txt"))
            {
                string line;
                while ((line = read.ReadLine()) != null)
                {
                    if (line == Nick)
                        flag = true;
                }
            }
            return flag;
        }

        void OnConnecting(object sender, EventArgs e)
        {
            RocketChat.Say("Connecting to IRC", this.Configuration.IRCColor);
        }

        public static string[] GetConnectedUsers()
        {
            return names;
        }

        public static void ShutDown()
        {
            irc.Disconnect();
            ircThread.Abort();
        }

        public static void Reset()
        {
            if (irc.IsConnected)
                irc.Disconnect();
            ircThread = new Thread(new ThreadStart(delegate
            {
                try { irc.Connect(server, port); }
                catch (Exception e)
                {
                    RocketChat.Say("Error Connecting to IRC", Color.red);
                }
            }));
            ircThread.Start();
        }

        void OnConnected(object sender, EventArgs e)
        {
            RocketChat.Say("Connected to IRC");
            irc.Login(nick, nick, 0, nick);

            RocketChat.Say("Identifying with Nickserv");
            irc.SendMessage(SendType.Message, "nickserv", "IDENTIFY " + this.Configuration.password);

            RocketChat.Say("Joining channel: " + channel);
            irc.RfcJoin(channel);

            irc.Listen();
        }

        public static string[] names;

        void OnNames(object sender, NamesEventArgs e)
        {
            names = e.UserList;
        }

        void OnDisconnected(object sender, EventArgs e)
        {
            RocketChat.Say("Disconnected from IRC!", Color.red);

            try { irc.Connect(server, 6667); }
            catch { RocketChat.Say("Failed to reconnect to IRC", Color.red); }
        }

        void OnChanMessage(object sender, IrcEventArgs e)
        {
            string message = e.Data.Message; string user = e.Data.Nick;

            if (!message.StartsWith("!"))
                RocketChat.Say("[IRC] " + user + ": " + message, this.Configuration.IRCColor);
            else
            {
                
            }
        }

        private string GetCommand(string message)
        {
            //return message.Split(' ')[0].ToString().Replace("!", "");
            return message.Split(' ')[0];
        }

        void OnJoin(object sender, JoinEventArgs e)
        {
            if (this.Configuration.showJoinLeaveMsgs)
                RocketChat.Say("[IRC] " + e.Data.Nick + " has joined the channel", this.Configuration.IRCColor);

            irc.RfcNames(channel);
        }

        void OnPart(object sender, PartEventArgs e)
        {
            if (this.Configuration.showJoinLeaveMsgs)
                RocketChat.Say("[IRC] " + e.Data.Nick + " has left the channel", this.Configuration.IRCColor);

            irc.RfcNames(channel);
        }

        void OnQuit(object sender, QuitEventArgs e)
        {
            if (this.Configuration.showJoinLeaveMsgs)
                RocketChat.Say("[IRC] " + e.Data.Nick + " has left IRC", this.Configuration.IRCColor);

            irc.RfcNames(channel);
        }

        void OnPrivMsg(object sender, IrcEventArgs e)
        {
            string message = e.Data.Message; string user = e.Data.Nick;
            if (IsModerator(user))
            {
                if (GetCommand(message) == "help")
                {
                    irc.RfcNotice(user, "kick (player) - Kicks the player from the server.");
                    irc.RfcNotice(user, "ban (player) (duration) - Bans the player from the server.");
                    irc.RfcNotice(user, "whois (player) - Shows basic information about the player.");
                    irc.RfcNotice(user, "god (player) - Toggles godmode for player.");
                    irc.RfcNotice(user, "players - Display number of players online.");
                    irc.RfcNotice(user, "abortirc - Closes IRC plugin connection.");
                }
                else if (GetCommand(message) == "kick")
                {
                    string plr = e.Data.Message.Split(' ')[1];
                    if (RocketPlayer.FromName(plr) == null)
                    {
                        irc.RfcNotice(user, "Could not find player: " + plr);
                    }
                    else
                    {
                        RocketPlayer.FromName(plr).Kick("You've been kicked by an IRC Operator!");
                    }
                }
                else if (GetCommand(message) == "abortirc")
                {
                    ShutDown();
                }
                else if (GetCommand(message) == "ban")
                {
                    string plr = e.Data.Message.Split(' ')[1];
                    if (RocketPlayer.FromName(plr) == null)
                    {
                        irc.RfcNotice(user, "Could not find player: " + plr);
                    }
                    else
                    {
                        try { RocketPlayer.FromName(plr).Ban("You've been banned by an IRC Operator!", Convert.ToUInt32(message.Split(' ')[2])); }
                        catch { irc.RfcNotice(user, "There was an error trying to ban " + RocketPlayer.FromName(plr).CharacterName + ". Invalid duration?"); }
                    }
                }
                else if (GetCommand(message) == "players")
                {
                    irc.RfcNotice(user, "Players online: " + SDG.Steam.Players.Count + "/" + SDG.Steam.MaxPlayers);
                }
                else if (GetCommand(message) == "whois")
                {
                    string plr = e.Data.Message.Split(' ')[1];
                    if (RocketPlayer.FromName(plr) == null)
                    {
                        irc.RfcNotice(user, "Could not find player: " + plr);
                    }
                    else
                    {
                        irc.RfcNotice(user, "Character Name: " + RocketPlayer.FromName(plr).CharacterName);
                        irc.RfcNotice(user, "Steam Name: " + RocketPlayer.FromName(plr).SteamName);
                        irc.RfcNotice(user, "SteamID: " + RocketPlayer.FromName(plr).CSteamID.ToString());
                        irc.RfcNotice(user, "Health: " + RocketPlayer.FromName(plr).Health);
                        irc.RfcNotice(user, "Hunger: " + RocketPlayer.FromName(plr).Hunger);
                        irc.RfcNotice(user, "Thirst: " + RocketPlayer.FromName(plr).Thirst);
                        irc.RfcNotice(user, "Infection: " + RocketPlayer.FromName(plr).Infection);
                        irc.RfcNotice(user, "Stamina: " + RocketPlayer.FromName(plr).Stamina);
                        irc.RfcNotice(user, "Experience: " + RocketPlayer.FromName(plr).Experience);
                    }
                }
                else if (GetCommand(message) == "god")
                {
                    string plr = e.Data.Message.Split(' ')[1];
                    if (RocketPlayer.FromName(plr) == null)
                    {
                        irc.RfcNotice(user, "Could not find player: " + plr);
                    }
                    else
                    {
                        if (RocketPlayer.FromName(plr).Features.GodMode)
                        {
                            RocketPlayer.FromName(plr).Features.GodMode = false;
                            irc.RfcNotice(user, "Disabled GodMode for: " + RocketPlayer.FromName(plr).CharacterName);
                            RocketChat.Say(RocketPlayer.FromName(plr), e.Data.Nick + " from IRC has disabled your GodMode.", this.Configuration.IRCColor);
                        }
                        else
                        {
                            RocketPlayer.FromName(plr).Features.GodMode = true;
                            irc.RfcNotice(user, "Enabled GodMode for: " + RocketPlayer.FromName(plr).CharacterName);
                            RocketChat.Say(RocketPlayer.FromName(plr), e.Data.Nick + " from IRC has enabled GodMode for you.", this.Configuration.IRCColor);
                        }
                    }
                }
                else
                {
                    irc.RfcNotice(user, "Invalid command, use !help for a list of commands.");
                }
            }
            else
            {
                irc.RfcNotice(user, "You are not an IRC Moderator, please contact the admin if this is a problem.");
            }
        }

        void OnNickChange(object sender, NickChangeEventArgs e)
        {
            
        }

        void OnAction(object sender, ActionEventArgs e)
        {
            RocketChat.Say("* " + e.Data.Nick + " " + e.ActionMessage);
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

            if (message == "test" && player.CharacterName == "LeeIzaZombie")
            {
                ircThread = new Thread(new ThreadStart(delegate
                {
                    Thread.Sleep(1000);
                    irc.SendMessage(SendType.Message, channel, "test passed!");
                    RocketChat.Say("test passed!");
                }));
                ircThread.Start();
            }
        }
    }

    public class IRCConfig : IRocketPluginConfiguration
    {
        public string server, channel, nick, password, shutdownMessage;
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
                    password = "changeme",
                    port = 6667, 
                    showJoinLeaveMsgs = true,
                    shutdownMessage = "Server is shutting down! Bye bye!",
                    IRCColor =  RocketChat.GetColorFromName("yellow", Color.yellow)
                };
            }
        }
    }
}
