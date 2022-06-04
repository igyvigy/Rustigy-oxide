using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{

    [Info("Rustigy", "igyVigy", "0.1")]
    [Description("Core Rustigy plugin")]
    public class Rustigy : RustPlugin
    {
        private DataFileSystem dataFile;
        private Configuration _config;
        void Init()
        {
            _config = Config.ReadObject<Configuration>();
            if (_config == null)
            {
                Puts("Generating Default Config File.");
                LoadDefaultConfig();
            }
            EnsureConfigIntegrity();
            dataFile = new DataFileSystem($"{Interface.Oxide.DataDirectory}\\player_info");
            Puts("Rustigy initialized");
        }

        object OnUserChat(IPlayer player, string message)
        {
            return null;
        }

        #region Configuration

        private class Configuration
        {
            public List<int> notCraftableItems = new List<int>();
            public Dictionary<string, float> entityExpTable = new Dictionary<string, float>();
            public VersionNumber VersionNumber;
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config, true);
        }

        protected override void LoadDefaultConfig()
        {
            Configuration defaultConfig = GetDefaultConfig();
            defaultConfig.VersionNumber = Version;
            Config.WriteObject(defaultConfig, true);
            _config = Config.ReadObject<Configuration>();
        }

        private void EnsureConfigIntegrity()
        {
            Configuration configDefault = new Configuration();
            if (_config.notCraftableItems == null)
            {
                _config.notCraftableItems = configDefault.notCraftableItems;
            }
            _config.VersionNumber = Version;
            SaveConfig();
        }

        private Configuration GetDefaultConfig()
        {
            return new Configuration();
        }

        #endregion

        #region Commands
        object OnPlayerCommand(BasePlayer player, string command, string[] args)
        {
            switch (command)
            {
                case "heal":
                    CommandHeal(player.IPlayer, command, args);
                    return true;
                case "hurt":
                    CommandHurt(player.IPlayer, command, args);
                    return true;
                case "sethome":
                    CommandSetHome(player.IPlayer, command, args);
                    return true;
                case "home":
                    CommandHome(player.IPlayer, command, args);
                    return true;
                case "gainexp":
                    CommandGainExp(player.IPlayer, command, args);
                    return true;
                case "spawn":
                    CommandSpawn(player.IPlayer, command, args);
                    return true;
                default: return null;
            }
        }

        private void CommandSetHome(IPlayer player, string command, string[] args)
        {
            PlayerInfo playerInfo = LoadPlayerInfo(player.Id);
            playerInfo.SetHome(player.Position());
            SavePlayerInfo(player.Id, playerInfo);
            player.Reply("Home set.");
        }

        private void CommandHome(IPlayer player, string command, string[] args)
        {
            PlayerInfo playerInfo = LoadPlayerInfo(player.Id);
            if (playerInfo.HomePosition != null)
            {
                player.Teleport(playerInfo.HomePosition);
                player.Reply("Going home.");
            }
            else
            {
                player.Reply("Set home position first. (/sethome)");
            }
        }

        private void CommandHeal(IPlayer player, string command, string[] args)
        {
            float health = player.Health;
            float maxHealth = player.MaxHealth;
            player.Heal(maxHealth - health);
        }

        private void CommandHurt(IPlayer player, string command, string[] args)
        {
            float damage = 10;
            if (args.Length > 0)
            {
                try
                {
                    damage = float.Parse(args[0]);
                }
                catch (System.FormatException e)
                {

                }
            }
            player.Hurt(damage);
        }
        private void CommandGainExp(IPlayer player, string command, string[] args)
        {
            float exp = 10;
            if (args.Length > 0)
            {
                try
                {
                    exp = float.Parse(args[0]);
                }
                catch (System.FormatException e)
                {

                }
            }
            PlayerInfo playerInfo = LoadPlayerInfo(player.Id);
            playerInfo.gainExp(exp, player);
            SavePlayerInfo(player.Id, playerInfo);
        }
        private void CommandSpawn(IPlayer player, string command, string[] args)
        {
            string carPrefab = "assets/content/vehicles/modularcar/2module_car_spawned.entity.prefab";
            string boatPrefab = "assets/content/vehicles/boats/rowboat/rowboat.prefab";
            string boarPrefab = "assets/rust.ai/agents/boar/boar.prefab";
            string stagPrefab = "assets/rust.ai/agents/stag/stag.prefab";
            string pr1 = "assets/prefabs/deployable/hot air balloon/hotairballoon.prefab";
            string chickenPrefab = "assets/rust.ai/agents/chicken/chicken.prefab";

            Vector3 pos = new Vector3(player.Position().X, player.Position().Y, player.Position().Z);

            BaseEntity newEntity = GameManager.server.CreateEntity(boarPrefab, pos, new Quaternion());
            newEntity.Spawn();
        }
        #endregion

        #region Player

        void OnPlayerConnected(BasePlayer player)
        {
            Puts("OnPlayerConnected works!" + player._name);
            player.IPlayer.Reply("Wellcome to Rustigy");
        }
        void OnPlayerMetabolize()
        {

        }
        private PlayerInfo LoadPlayerInfo(string playerId)
        {
            return dataFile.ReadObject<PlayerInfo>($"playerInfo_{playerId}");
        }

        private void SavePlayerInfo(string playerId, PlayerInfo playerInfo)
        {
            dataFile.WriteObject($"playerInfo_{playerId}", playerInfo);
        }
        object OnPlayerSpawn(BasePlayer player)
        {
            Puts("OnPlayerSpawn works!");
            return null;
        }

        object OnPlayerRespawn(BasePlayer player, BasePlayer.SpawnPoint spawnPoint)
        {
            PlayerInfo playerInfo = LoadPlayerInfo(player.IPlayer.Id);
            if (playerInfo.HomePosition != null)
            {
                player.IPlayer.Teleport(playerInfo.HomePosition);
                var sp = new BasePlayer.SpawnPoint();
                sp.pos = new Vector3(playerInfo.HomePosition.X, playerInfo.HomePosition.Y, playerInfo.HomePosition.Z);
                sp.rot = Quaternion.identity;
                return sp;
            }
            return spawnPoint;
        }
        bool CanCraft(ItemCrafter itemCrafter, ItemBlueprint bp, int amount)
        {
            foreach (var itemId in _config.notCraftableItems)
            {
                if (bp.targetItem.itemid == itemId)
                {
                    Puts("Tried to craft forbidden item " + bp.targetItem.itemid);
                    return false;
                }
            }
            Puts("item craft " + bp.targetItem.itemid);
            return true;
        }
        bool CanCraft(PlayerBlueprints playerBlueprints, ItemDefinition itemDefinition, int skinItemId)
        {
            foreach (var itemId in _config.notCraftableItems)
            {
                if (itemDefinition.itemid == itemId)
                {
                    Puts("Tried to craft forbidden item " + itemDefinition.itemid);
                    return false;
                }
            }
            Puts("craft " + itemDefinition.itemid);
            return true;
        }

        bool CanDropActiveItem(BasePlayer player)
        {
            Puts("CanDropActiveItem works!");
            return true;
        }
        object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            Puts("CanBuild works!");
            return null;
        }
        #endregion

        #region Entity
        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            Puts("OnEntityDeath name: " + entity.ShortPrefabName + ", prefab: " + entity.PrefabName);

            if (info.InitiatorPlayer != null)
            {
                Puts("attacked by player: " + info.InitiatorPlayer.IPlayer.Name);
                if (_config.entityExpTable.ContainsKey(entity.ShortPrefabName))
                {
                    // TODO: remove next line
                    _config = Config.ReadObject<Configuration>();
                    float exp = _config.entityExpTable[entity.ShortPrefabName];
                    PlayerInfo playerInfo = LoadPlayerInfo(info.InitiatorPlayer.IPlayer.Id);
                    playerInfo.gainExp(exp, info.InitiatorPlayer.IPlayer);
                    SavePlayerInfo(info.InitiatorPlayer.IPlayer.Id, playerInfo);
                }
            }
        }
        #endregion

        #region PlayerInfo

        internal class PlayerInfo
        {
            public GenericPosition HomePosition;
            public int level = 1;
            public float exp = 0f;
            public float nextLevelAt;
            public PlayerInfo()
            {
                this.nextLevelAt = GetNextExpForLevel(level);
            }

            public void SetHome(GenericPosition position)
            {
                HomePosition = position;
            }
            public void gainExp(float exp, IPlayer player)
            {
                this.exp += exp;
                if (this.exp >= nextLevelAt)
                {
                    player.Reply("You've gained " + exp + " experience points.");
                    this.levelUp(player);
                }
                else
                {
                    player.Reply("You've gained " + exp + " experience points. " + (nextLevelAt - this.exp) + " untill next level");
                }
            }

            public void levelUp(IPlayer player)
            {
                level += 1;
                nextLevelAt = GetNextExpForLevel(level);
                player.Reply("Level up! Reached level " + level);
                if (exp >= nextLevelAt)
                {
                    levelUp(player);
                }
            }

            private float GetNextExpForLevel(int level)
            {
                return ((float)Math.Pow(level, 3) + 20 * level);
            }
        }

        #endregion
    }
}