using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using TShockAPI;
using Terraria;
using TerrariaApi.Server;
using System.IO;

namespace StatTracker {
    [ApiVersion(2, 1)]
    public class PlayerStats {
        public TSPlayer ThePlayer { get; set; }
        public int PlayerDamage { get; set; }
        public int PlayerKills { get; set; }
        public int PlayerDeaths { get; set; }
        public TSPlayer LastAttacker { get; set; }

        public PlayerStats(int playerID) {
            this.ThePlayer = TShock.Players[playerID];
            this.PlayerDamage = 0;
            this.PlayerDeaths = 0;
            this.PlayerKills = 0;
            this.LastAttacker = null;
        }
    }

    public class StatTracker : TerrariaPlugin {
        public override string Author => "Fryke";
        public override string Description => "A plugin that keeps track of pvp stats.";
        public override string Name => "StatTracker";
        public override Version Version => new Version(1, 1, 0, 3);

        private Dictionary<string, PlayerStats> playerList = new Dictionary<string, PlayerStats> { }; // A list of players set to play the game

        /// Initializes a new instance of the TestPlugin class.
        /// This is where you set the plugin's order and perfrom other constructor logic
        public StatTracker(Main game) : base(game) {

        }

        /// Handles plugin initialization. 
        /// Fired when the server is started and the plugin is being loaded.
        /// You may register hooks, perform loading procedures etc here.
        public override void Initialize() {
            ServerApi.Hooks.NetSendData.Register(this, OnSendData);
            ServerApi.Hooks.ServerJoin.Register(this, OnServerJoin);
            Commands.ChatCommands.Add(new Command(SaveStats, "savestats"));

            String path = System.IO.Directory.GetCurrentDirectory() + "/statsDB.json";
            if (File.Exists(path)) {
                // Load the data
                String fileContents = System.IO.File.ReadAllText(path);
                playerList = JsonConvert.DeserializeObject<Dictionary<string, PlayerStats>>(fileContents);
            }
        }

        /// Handles plugin disposal logic.
        /// *Supposed* to fire when the server shuts down.
        /// You should deregister hooks and free all resources here.
        protected override void Dispose(bool disposing) {
            String json = JsonConvert.SerializeObject(playerList, Newtonsoft.Json.Formatting.Indented);
            String path = System.IO.Directory.GetCurrentDirectory();
            System.IO.File.WriteAllText(@"path + /statsDB.json", json);

            if (disposing) {
                // Deregister hooks here
                ServerApi.Hooks.NetSendData.Deregister(this, OnSendData);
                ServerApi.Hooks.ServerJoin.Deregister(this, OnServerJoin);
            }
            base.Dispose(disposing);
        }

        public void SaveStats(CommandArgs args) {
            String json = JsonConvert.SerializeObject(playerList, Newtonsoft.Json.Formatting.Indented);
            String path = System.IO.Directory.GetCurrentDirectory();
            System.IO.File.WriteAllText(@"path + \statsDB.json", json);
        }

        public void OnServerJoin(JoinEventArgs args) {
            // Add them to the list of people
            TSPlayer thePlayer = TShock.Players[args.Who];
            if (playerList[thePlayer.Name] == null) {
                playerList[thePlayer.Name] = new PlayerStats(args.Who);
            }
        }

        public void OnSendData(SendDataEventArgs args) {
            if (args.Handled)
                return;
            if (args.MsgId == PacketTypes.PlayerDeathV2) {
                // TSPlayer.All.SendInfoMessage("(SendData) PlayerDeathV2 -> 1:{0} 2:{1} 3:{2} 4:{3} 5:{4} remote:{5} ignore:{6}", args.number, args.number2, args.number3, args.number4, args.number5, args.remoteClient, args.ignoreClient);
                // 1-playerID, 2-damage, 3-?, 4-direction, 5-PVP

                // Console.WriteLine("Victim = {0}", TShock.Players[args.number].Name);
                //Console.WriteLine("Damage? = {0}", args.number2);
                if (args.number2 >= 0 && args.number2 <= 2000) {
                    var deadPlayer = TShock.Players[args.number]; // Get the dead player
                    if (deadPlayer != null) { // If we correctly got the dead player
                        // Console.WriteLine("PlayerList: {0}", string.Join(",", playerList));
                        // Console.WriteLine("Dead player: {0}", deadPlayer.Name);

                        if (playerList[deadPlayer.Name] != null) { // If we have an entry for this player
                            this.playerList[deadPlayer.Name].PlayerDeaths += 1; // Increment that player's death count
                        } else { // Otherwise we don't have a record for this player yet.
                            playerList[deadPlayer.Name] = new PlayerStats(deadPlayer.Index); // So make one
                        }

                        if (playerList[deadPlayer.Name].LastAttacker != null) {
                            this.playerList[this.playerList[deadPlayer.Name].LastAttacker.Name].PlayerKills += 1; // Increment the killer's number of kills
                        }
                    }
                }

            } else if (args.MsgId == PacketTypes.PlayerHurtV2) {
                // TSPlayer.All.SendInfoMessage("(SendData) PlayerHurtV2 -> 1:{0} 2:{1} 3:{2} 4:{3} 5:{4} remote:{5} ignore:{6}", args.number, args.number2, args.number3, args.number4, args.number5, args.remoteClient, args.ignoreClient);
                // 1-pID, 2-damage, 4-direction, 5-pvp, ignore: Who
                // Console.WriteLine("Victim = {0}", TShock.Players[args.number].Name);
                // Console.WriteLine("Attacker = {0}", TShock.Players[args.ignoreClient].Name);

                if (args.number4 == 2 || args.number4 == 3) // if PvP or crit
                {
                    var attacker = TShock.Players[args.ignoreClient]; // Get the attacker
                    var victim = TShock.Players[args.number]; // Get the victim

                    if (victim != null && !victim.Dead) { // If the victim is valid and is not dead
                        if (playerList[victim.Name] != null) { // If we have a valid victim
                            this.playerList[victim.Name].LastAttacker = TShock.Players[args.ignoreClient]; // assign the attacker's object to the victim's index
                        }

                        if (playerList[attacker.Name] != null) { // If we have a valid attacker
                            this.playerList[attacker.Name].PlayerDamage += (int)args.number2; // Add the damage they did to their total
                        }
                    }
                }
            }
        }
    }
}