using System.Collections.Generic;
using Oxide.Core;
using System.Linq;
using Oxide.Core.Plugins;
using UnityEngine;
using System;
using System.Reflection;
using Oxide.Game.HideHoldOut;
using Oxide.Game.HideHoldOut.Libraries;

namespace Oxide.Plugins
{
    [Info("ProximityAlert", "k1lly0u", "0.1.0", ResourceId = 0)]
    class ProximityAlert : HideHoldOutPlugin
    {
        #region Fields       
        ServerManager sm = GameObject.Find("ServerManager").GetComponent<ServerManager>();
        CONSOLE con = GameObject.Find("CONSOLE").GetComponent<CONSOLE>();

        private readonly Command cmdlib = Interface.Oxide.GetLibrary<Command>();
        private static readonly FieldInfo ChatNetViewField = typeof(ChatManager).GetField("Chat_NetView", BindingFlags.NonPublic | BindingFlags.Instance);
        public static uLink.NetworkView ChatNetView = ChatNetViewField.GetValue(NetworkController.NetManager_.chatManager) as uLink.NetworkView;

        private static int playerLayer = UnityEngine.LayerMask.GetMask("Player (Server)");
        List<ProximityPlayer> playerList = new List<ProximityPlayer>();

        #endregion

        #region Functions
        void OnServerInitialized() => InitializePlugin();
        void Unload()
        {
            var objects = UnityEngine.Object.FindObjectsOfType<ProximityPlayer>();
            if (objects != null)
                foreach (var gameObj in objects)
                    UnityEngine.Object.Destroy(gameObj);
            playerList.Clear();
        }
        void OnPlayerInit(PlayerInfos player) => InitializePlayer(player);
        void OnPlayerDisconnected(PlayerInfos player)
        {
            if (player.PManager.GetComponent<ProximityPlayer>())
                DestroyPlayer(player);
        }
        private void DestroyPlayer(PlayerInfos player)
        {
            playerList.Remove(player.PManager.GetComponent<ProximityPlayer>());
            UnityEngine.Object.Destroy(player.PManager.GetComponent<ProximityPlayer>());
        }
        private void InitializePlugin()
        {
            RegisterMessages();
            permission.RegisterPermission("proximityalert.use", this);
            LoadVariables();           
            foreach (var player in GetOnlinePlayers())
                OnPlayerInit(player);
        }
        private void InitializePlayer(PlayerInfos player)
        {
            if (!permission.UserHasPermission(player.account_id, "proximityalert.use")) return;
            if (player.PManager.GetComponent<ProximityPlayer>())
                DestroyPlayer(player);
            GetPlayer(player);
        }
       
        private void ProxCollisionEnter(PlayerInfos player) => SendReply(player, lang.GetMessage("warning", this, player.account_id));
        private void ProxCollisionLeave(PlayerInfos player) => SendReply(player, lang.GetMessage("clear", this, player.account_id));        
        private ProximityPlayer GetPlayer(PlayerInfos player)
        {
            if (!player.PManager.GetComponent<ProximityPlayer>())
            {
                playerList.Add(player.PManager.gameObject.AddComponent<ProximityPlayer>());
                player.PManager.GetComponent<ProximityPlayer>().SetRadius(configData.TriggerRadius);
            }
            return player.PManager.GetComponent<ProximityPlayer>();
        }
        private void SendReply(PlayerInfos player, string msg)
        {
            if (player.NetPlayer != null) ChatNetView.RPC("NET_Receive_msg", player.NetPlayer, new object[] { "\r\n" + msg, chat_msg_type.standard, player.account_id });
        }
        List<PlayerInfos> GetOnlinePlayers()
        {
            List<PlayerInfos> players = new List<PlayerInfos>();
            foreach (var entry in sm.Connected_Players)
                if (entry != null)
                    if (entry.connected)
                        players.Add(entry);
            return players;
        }
        #endregion

        #region Chat Command
        [ChatCommand("prox")]
        private void cmdProx(PlayerInfos player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.account_id, "proximityalert.use")) return;
            if (GetPlayer(player).Activated)
            {
                GetPlayer(player).Activated = false;
                SendReply(player, lang.GetMessage("deactive", this, player.account_id));
                return;
            }
            else
            {
                GetPlayer(player).Activated = true;
                SendReply(player, lang.GetMessage("active", this, player.account_id));
            }
        }
        #endregion

        #region Player Class
        class ProximityPlayer : MonoBehaviour
        {
            private PlayerInfos player;
            private List<string> inProximity = new List<string>();
            private float collisionRadius;
            public bool GUIDestroyed = true;
            public bool Activated = true;

            private void Awake()
            {
                player = GetComponent<PlayerInfos>();
                InvokeRepeating("UpdateTrigger", 2f, 2f);
            }
            public void SetRadius(float radius) => collisionRadius = radius;
            private void OnDestroy() => CancelInvoke("UpdateTrigger");
            private void UpdateTrigger()
            {
                if (!Activated) return;
                var colliderArray = Physics.OverlapSphere(player.Transfo.position, collisionRadius, playerLayer);
                var collidePlayers = new List<string>();
                var outProximity = new List<string>();

                var existingCount = inProximity.Count();

                foreach (Collider collider in colliderArray)
                {
                    var col = collider.GetComponentInParent<PlayerInfos>();
                    if (col != null && col != player && !col.isADMIN)
                        collidePlayers.Add(col.account_id);

                    if (!inProximity.Contains(col.account_id))
                        inProximity.Add(col.account_id);
                }

                if (inProximity.Count > existingCount)
                    EnterTrigger();

                foreach (var entry in inProximity)
                    if (!collidePlayers.Contains(entry))
                        outProximity.Add(entry);

                foreach (var entry in outProximity)
                {
                    inProximity.Remove(entry);
                    if (inProximity.Count == 0)
                        LeaveTrigger();
                }
            }            
            void EnterTrigger() => Interface.CallHook("ProxCollisionEnter", player);
            void LeaveTrigger() => Interface.CallHook("ProxCollisionLeave", player);           
        }
        #endregion

        #region Config
        private ConfigData configData;
        class ConfigData
        {           
            public float TriggerRadius { get; set; }
        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {               
                TriggerRadius = 50f
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        private void RegisterMessages() => lang.RegisterMessages(messages, this);
        #endregion

        #region Localization
        Dictionary<string, string> messages = new Dictionary<string, string>
        {
            {"warning", "<color=#cc0000>Caution!</color> There are players nearby!" },
            {"clear", "<color=#ffdb19>Clear!</color>" },
            {"active", "You have activated ProximityAlert" },
            {"deactive", "You have deactivated ProximityAlert" }
        };
        #endregion
    }
}
