using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Skip Night Vote", "k1lly0u", "0.1.11", ResourceId = 1866)]
    class SkipNight : HideHoldOutPlugin
    {
        bool Changed;

        private List<string> receivedYes;
        private List<string> receivedNo;
        ServerManager sm = GameObject.Find("ServerManager").GetComponent<ServerManager>();
        CONSOLE con = GameObject.Find("CONSOLE").GetComponent<CONSOLE>();        

        private static readonly FieldInfo ChatNetViewField = typeof(ChatManager).GetField("Chat_NetView", BindingFlags.NonPublic | BindingFlags.Instance);
        public static uLink.NetworkView ChatNetView = ChatNetViewField.GetValue(NetworkController.NetManager_.chatManager) as uLink.NetworkView;

        private bool voteOpen;
        private Timer voteTimer;
        private Timer timeCheck;

        #region oxide hooks
        //////////////////////////////////////////////////////////////////////////////////////
        // Oxide Hooks ///////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////
        void Loaded()
        {
            permission.RegisterPermission("skipnight.admin", this);
            lang.RegisterMessages(messages, this);
            LoadVariables();
        }
        void OnServerInitialized()
        {           
            voteOpen = false;           
            receivedYes = new List<string>();
            receivedNo = new List<string>();
            timeCheck = timer.Repeat(45, 0, () => CheckTime());
        }
        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new config file");
            Config.Clear();
            LoadVariables();
        }
        void Unload()
        {
            receivedNo.Clear();
            receivedYes.Clear();            
        }        
        #endregion

        #region methods
        //////////////////////////////////////////////////////////////////////////////////////
        // Vote Methods //////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////

        private bool alreadyVoted(PlayerInfos player)
        {
            if (receivedNo.Contains(player.account_id) || receivedYes.Contains(player.account_id))
                return true;
            return false;
        }
        private bool TallyVotes()
        {
            var Yes = receivedYes.Count;
            var No = receivedNo.Count;
            float requiredVotes = GetOnlinePlayers().Count * requiredVotesPercentage;

            if (useMajorityRules)
                if (Yes >= No)
                    return true;
            if (Yes > No && Yes >= requiredVotes) return true;
            return false;
        }
        private void OpenVote(PlayerInfos player = null)
        {
            msgAll(string.Format(lang.GetMessage("opened", this)));

            float required = GetOnlinePlayers().Count * requiredVotesPercentage;
            if (required < 1) required = 1;

            msgAll(string.Format(lang.GetMessage("required", this), (int)required));
            voteOpen = true;
            if (player != null)
                receivedYes.Add(player.account_id);
            VoteTimer();
            return;
        }
        private void voteEnd()
        {
            bool success = TallyVotes();
            if (success)
            {
                msgAll(string.Format(lang.GetMessage("voteSuccess", this), setTime / 60));
                con.TimeManager.SetTime(setTime);
                voteTimer.Destroy();
            }
            else
            {
                msgAll(lang.GetMessage("voteFail", this));
            }
            voteOpen = false;
            clearData();
        }
        private void clearData()
        {
            receivedYes.Clear();
            receivedNo.Clear();
        }       
        private void VoteTimer()
        {
            var time = voteOpenTimer * 60;
            voteTimer = timer.Repeat(1, time, () =>
            {
                time--;
                if (time == 0)
                {
                    voteEnd();
                    timeCheck = timer.Repeat(45, 0, () => CheckTime());
                    return;
                }
                if (time == 180)
                {
                    msgAll(string.Format(lang.GetMessage("timeLeft", this), 3, "Minutes"));
                }
                if (time == 120)
                {
                    msgAll(string.Format(lang.GetMessage("timeLeft", this), 2, "Minutes"));
                }
                if (time == 60)
                {
                    msgAll(string.Format(lang.GetMessage("timeLeft", this), 1, "Minute"));
                }
                if (time == 30)
                {
                    msgAll(string.Format(lang.GetMessage("timeLeft", this), 30, "Seconds"));
                }
                if (time == 10)
                {
                    msgAll(string.Format(lang.GetMessage("timeLeft", this), 10, "Seconds"));
                }
            });
        }
        private void msgAll(string left)
        {
            foreach (var player in GetOnlinePlayers())            
                if (player != null)
                    SendReply(player, lang.GetMessage("title", this, player.account_id) + left);
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
        private void CheckTime()
        {
            if (!voteOpen)
                if (con.TimeManager.TIME_display >= changeTime || con.TimeManager.TIME_display < setTime)
                {
                    OpenVote();                
                }
        }
        #endregion

        #region chat/console commands

        [ChatCommand("skipnight")]
        private void cmdSkipNightVote(PlayerInfos player, string command, string[] args)
        {            
            if (args.Length == 0)
            {
                SendReply(player, lang.GetMessage("title", this, player.account_id.ToString()) + lang.GetMessage("badSyn", this, player.account_id.ToString()));
                return;
            }

            if (args.Length >= 1)
            {
                if (canVote(player))
                {
                    if (args[0].ToLower() == "open")
                    {
                        if (voteOpen)
                        {
                            SendReply(player, lang.GetMessage("title", this, player.account_id.ToString()) + lang.GetMessage("voteOpen", this, player.account_id.ToString()));
                            return;
                        }
                        OpenVote(player);
                    }
                }
                else if (args[0].ToLower() == "yes")
                {
                    if (!voteOpen)
                    {
                        SendReply(player, lang.GetMessage("title", this, player.account_id.ToString()) + lang.GetMessage("noOpen", this, player.account_id.ToString()));
                        return;
                    }
                    if (!alreadyVoted(player))
                    {
                        receivedYes.Add(player.account_id);
                        SendReply(player, lang.GetMessage("title", this, player.account_id.ToString()) + lang.GetMessage("yesVote", this, player.account_id.ToString()));
                        float requiredVotes = GetOnlinePlayers().Count * requiredVotesPercentage;
                        if (receivedYes.Count >= requiredVotes)
                        {
                            voteEnd();
                            return;
                        }
                        if (displayProgress)
                            msgAll(string.Format(lang.GetMessage("totalVotes", this, player.account_id.ToString()), receivedYes.Count, receivedNo.Count));
                        return;
                    }
                    SendReply(player, lang.GetMessage("title", this, player.account_id.ToString()) + lang.GetMessage("alreadyVoted", this, player.account_id.ToString()));
                    return;
                }
                else if (args[0].ToLower() == "no")
                {
                    if (!voteOpen)
                    {
                        SendReply(player, lang.GetMessage("title", this, player.account_id.ToString()) + lang.GetMessage("noOpen", this, player.account_id.ToString()));
                        return;
                    }
                    if (!alreadyVoted(player))
                    {
                        receivedNo.Add(player.account_id);
                        SendReply(player, lang.GetMessage("title", this, player.account_id.ToString()) + lang.GetMessage("noVote", this, player.account_id.ToString()));
                        if (displayProgress)
                            msgAll(string.Format(lang.GetMessage("totalVotes", this, player.account_id.ToString()), receivedYes.Count, receivedNo.Count));
                        return;
                    }
                    SendReply(player, lang.GetMessage("title", this, player.account_id.ToString()) + lang.GetMessage("alreadyVoted", this, player.account_id.ToString()));
                    return;
                }
            }
        }

        private bool canVote(PlayerInfos player)
        {
            if (permission.UserHasPermission(player.account_id.ToString(), "skipnight.admin")) return true;            
            SendReply(player, lang.GetMessage("title", this, player.account_id.ToString()) + lang.GetMessage("noPerms", this, player.account_id.ToString()));
            return false;
        }       

        #endregion

        #region config
        //////////////////////////////////////////////////////////////////////////////////////
        // Configuration /////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////

        static float requiredVotesPercentage = 0.5f;
        static bool useMajorityRules = true;
        static int voteOpenTimer = 3;
        static bool displayProgress = true;
        static int auth = 1;
        static int setTime = 420;
        static int changeTime = 1110;

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }
        private void LoadConfigVariables()
        {
            CheckCfgFloat("Options - Required yes vote percentage", ref requiredVotesPercentage);
            CheckCfg("Options - Open vote timer (minutes)", ref voteOpenTimer);
            CheckCfg("Options - Display vote progress", ref displayProgress);
            CheckCfg("Options - Use majority rules", ref useMajorityRules);
            CheckCfg("Options - Time to change to", ref setTime);
            CheckCfg("Options - Time to open vote", ref changeTime);
        }
        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }
        private void CheckCfgFloat(string Key, ref float var)
        {

            if (Config[Key] != null)
                var = Convert.ToSingle(Config[Key]);
            else
                Config[Key] = var;
        }
        object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }
        #endregion

        #region messages
        private void SendReply(PlayerInfos player, string msg)
        {
            if (player.NetPlayer != null) ChatNetView.RPC("NET_Receive_msg", player.NetPlayer, new object[] { "\r\n" + msg, chat_msg_type.standard, player.account_id });
        }
        //////////////////////////////////////////////////////////////////////////////////////
        // Messages //////////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////

        Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {"title", "<color=#C4FF00>SkipNight</color> : " },
            {"noPerms", "You do not have permission to use this command" },
            {"badSyn", "<color=#C4FF00>/skipnight open</color> - Open a vote to change the time" },
            {"voteOpen", "There is already a vote open" },
            {"noOpen", "There isn't a vote open right now" },
            {"yesVote", "You have voted yes"},
            {"noVote", "You have voted no" },
            {"alreadyVoted", "You have already voted" },
            {"opened", "A vote to change the time is now open! Use <color=#C4FF00>/skipnight yes</color> or <color=#C4FF00>/skipnight no</color>" },
            {"required", "Minimum yes votes required is <color=#C4FF00>{0}</color>" },
            {"invAmount", "You have entered a invalid number" },
            {"timeLeft", "Voting ends in {0} {1}, use <color=#C4FF00>/skipnight yes</color> or <color=#C4FF00>/skipnight no</color>" },
            {"cooldown", "You must wait for the cooldown period to end before opening a vote" },
            {"voteSuccess", "The vote was successful, changing time to {0}" },
            {"voteFail", "The vote failed to meet the requirements, try again in <color=#C4FF00>{0}</color> minutes" },
            {"totalVotes", "<color=#C4FF00>{0}</color> vote(s) for Yes, <color=#C4FF00>{1}</color> vote(s) for No" }
        };      
        #endregion

    
}
}
