using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{

    [Info("Rustigy", "igyVigy", "0.1")]
    [Description("Core Rustigy plugin")]
    public class Rustigy : RustPlugin
    {
        private DataFileSystem dataFile;
        private Configuration _config;
        private Admin admin;
        void Init()
        {
            _config = Config.ReadObject<Configuration>();
            admin = new Admin(_config.admin);
            if (_config == null)
            {
                Puts("Generating Default Config File.");
                LoadDefaultConfig();
            }
            EnsureConfigIntegrity();
            dataFile = new DataFileSystem($"{Interface.Oxide.DataDirectory}\\player_info");

            Puts("Rustigy initialized");
        }

        void OnServerInitialized()
        {
            Puts("Server initialized");
            ModifyItems();
        }

        object OnUserChat(IPlayer player, string message)
        {
            return null;
        }

        #region Configuration

        private class Configuration
        {
            public Dictionary<string, string> admin = new Dictionary<string, string>();
            public List<string> moderators = new List<string>();
            public List<string> notCraftableItems = new List<string>();
            public Dictionary<string, float> entityExpTable = new Dictionary<string, float>();
            public Dictionary<string, string> spawnCommandData = new Dictionary<string, string>();
            public Dictionary<string, string> createCommandData = new Dictionary<string, string>();
            public VersionNumber versionNumber;
        }

        internal class Admin
        {
            public string name;
            public string steamId;
            public Admin(Dictionary<string, string> data)
            {
                this.name = data["name"];
                this.steamId = data["steamId"];
            }
        }

        bool IsAdmin(BasePlayer player)
        {
            return player.displayName == admin.name;
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config, true);
        }

        protected override void LoadDefaultConfig()
        {
            Configuration defaultConfig = GetDefaultConfig();
            defaultConfig.versionNumber = Version;
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
            if (_config.admin == null)
            {
                _config.admin = configDefault.admin;
            }
            if (_config.moderators == null)
            {
                _config.moderators = configDefault.moderators;
            }
            if (_config.spawnCommandData == null)
            {
                _config.spawnCommandData = configDefault.spawnCommandData;
            }
            if (_config.createCommandData == null)
            {
                _config.createCommandData = configDefault.createCommandData;
            }
            _config.versionNumber = Version;
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
                case "stats":
                    return CommandStats(player, command, args);
                case "heal":
                    return CommandHeal(player, command, args);
                case "hurt":
                    return CommandHurt(player, command, args);
                case "sethome":
                    return CommandSetHome(player, command, args);
                case "home":
                    return CommandHome(player, command, args);
                case "gainexp":
                    return CommandGainExp(player, command, args);
                case "spawn":
                    return CommandSpawn(player, command, args);
                case "create":
                    return CommandCreate(player, command, args);
                default: return null;
            }
        }
        private object CommandStats(BasePlayer player, string command, string[] args)
        {
            PlayerInfo playerInfo = LoadPlayerInfo(player.UserIDString);
            var level = playerInfo.level;
            var exp = playerInfo.exp;
            var nextLevelAt = playerInfo.nextLevelAt;
            player.IPlayer.Reply("Level: " + level + ", exp: " + exp + ", next level at " + nextLevelAt + " exp");
            return true;
        }
        private object CommandSetHome(BasePlayer player, string command, string[] args)
        {
            PlayerInfo playerInfo = LoadPlayerInfo(player.UserIDString);
            playerInfo.SetHome(player.IPlayer.Position());
            SavePlayerInfo(player.UserIDString, playerInfo);
            player.IPlayer.Reply("Home set.");
            return true;
        }
        private object CommandHome(BasePlayer player, string command, string[] args)
        {
            PlayerInfo playerInfo = LoadPlayerInfo(player.UserIDString);
            if (playerInfo.HomePosition != null)
            {
                player.IPlayer.Teleport(playerInfo.HomePosition);
                player.IPlayer.Reply("Going home.");
            }
            else
            {
                player.IPlayer.Reply("Set home position first. (/sethome)");
            }
            return true;
        }
        private object CommandHeal(BasePlayer player, string command, string[] args)
        {
            if (!IsAdmin(player)) return null;
            float health = player.health;
            float maxHealth = player.MaxHealth();
            player.Heal(maxHealth - health);
            return true;
        }
        private object CommandHurt(BasePlayer player, string command, string[] args)
        {
            if (!IsAdmin(player)) return null;
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
            return true;
        }
        private object CommandGainExp(BasePlayer player, string command, string[] args)
        {
            if (!IsAdmin(player)) return null;
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
            PlayerInfo playerInfo = LoadPlayerInfo(player.UserIDString);
            playerInfo.gainExp(exp, player);
            SavePlayerInfo(player.UserIDString, playerInfo);
            return true;
        }
        private object CommandSpawn(BasePlayer player, string command, string[] args)
        {
            if (!IsAdmin(player)) return null;
            string target = "";
            if (args.Length > 0)
            {
                target = args[0];
            }
            if (target.Length == 0) return null;

            // TODO: remove next line
            _config = Config.ReadObject<Configuration>();
            if (!_config.spawnCommandData.ContainsKey(target)) return null;
            string prefab = _config.spawnCommandData[target];

            Vector3 playerPosition = player.GetNetworkPosition();
            float distance = 5f;
            if (args.Length > 1)
            {
                distance = float.Parse(args[1]);
            }
            Vector3 spawnPosition = playerPosition + player.transform.forward * distance;
            BaseEntity newEntity = GameManager.server.CreateEntity(prefab, spawnPosition, Quaternion.identity);
            newEntity.Spawn();
            player.IPlayer.Reply($"Spawned {target} at {spawnPosition}, where player is at {playerPosition}");
            return true;
        }

        private object CommandCreate(BasePlayer player, string command, string[] args)
        {
            if (!IsAdmin(player)) return null;
            string target = "";
            if (args.Length > 0)
            {
                target = args[0];
            }
            if (target.Length == 0) return null;

            int amount = 1;
            if (args.Length > 1)
            {
                amount = int.Parse(args[1]);
            }

            // TODO: remove next line
            _config = Config.ReadObject<Configuration>();
            string name = _config.createCommandData.ContainsKey(target) ? _config.createCommandData[target] : target;
            var item = ItemManager.CreateByName(name, amount);
            player.inventory.GiveItem(item);
            player.IPlayer.Reply($"Got {amount} {target}");
            return true;
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
            foreach (var name in _config.notCraftableItems)
            {
                if (bp.targetItem.shortname
                    == name)
                {
                    Puts("Tried to craft forbidden item " + bp.targetItem.shortname);
                    return false;
                }
            }
            Puts("item craft " + bp.targetItem.shortname);
            return true;
        }
        bool CanCraft(PlayerBlueprints playerBlueprints, ItemDefinition itemDefinition, int skinItemId)
        {
            foreach (var itemName in _config.notCraftableItems)
            {
                if (itemDefinition.shortname == itemName)
                {
                    Puts("Tried to craft forbidden item " + itemDefinition.shortname);
                    return false;
                }
            }
            Puts("craft " + itemDefinition.shortname);
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
                    playerInfo.gainExp(exp, info.InitiatorPlayer);
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
            public void gainExp(float exp, BasePlayer player)
            {
                this.exp += exp;
                if (this.exp >= nextLevelAt)
                {
                    player.IPlayer.Reply("You've gained " + exp + " experience points.");
                    this.levelUp(player.IPlayer);
                }
                else
                {
                    player.IPlayer.Reply("You've gained " + exp + " experience points. " + (nextLevelAt - this.exp) + " untill next level");
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

        #region Items
        void ModifyItems()
        {
            ModifyWoodenArrow();
        }

        void OnWeaponFired(BaseProjectile projectile, BasePlayer player, ItemModProjectile mod)
        {
            Puts("OnWeaponFired works!" + mod.projectileVelocity + ", " + projectile.GetLocalVelocity());
        }

        void ModifyWoodenArrow()
        {
            ItemDefinition arrow_wooden = ItemManager.FindItemDefinition("arrow.wooden");
            if (arrow_wooden == null)
            {
                Puts("no arrow wooden");
            }

            var projectile = arrow_wooden.GetComponent<ItemModProjectile>();
            if (projectile == null)
            {
                Puts("no projectile");
            }
            projectile.projectileVelocity = 2f;

            Puts("arrow velocity " + projectile.projectileVelocity);

            /*
            ItemDefinition arrow_fire = ItemManager.FindItemDefinition("arrow.fire");
            if (arrow_wooden == null)
            {
                Puts("no arrow fire");
            }
            var coocable = new ItemModCookable
            {
                becomeOnCooked = arrow_fire,
                lowTemp = -1,
                highTemp = -1
            };
            
            arrow_wooden.itemMods = new ItemMod[] { coocable };
            
            var verified = arrow_wooden.GetComponent<ItemModCookable>();
            Puts("arrow coocable verified: " + verified.becomeOnCooked.ToString() + " " + verified.lowTemp);

            

            
            var concumable = arrow_wooden.gameObject.AddComponent<ItemModConsumable>();
            if (concumable == null) return;

            Puts("arrow concumable: " + concumable.ToString());
            concumable.achievementWhenEaten = "arrow eater";
            var effect = new ItemModConsumable.ConsumableEffect();
            effect.type = MetabolismAttribute.Type.Bleeding;
            concumable.effects.Add(effect);
            Puts("arrow concumable: " + concumable.effects.Count + " " + concumable.achievementWhenEaten);
            
            */

            var arrow_wooden_new = ItemManager.FindItemDefinition("arrow.wooden");

            var mods = arrow_wooden_new.itemMods;
            Puts("mods " + mods.Length);
            foreach (var mod in mods)
            {

                Puts(mod.GetType().ToString());

            }

            Puts("done modifying wooden arrow");
        }

        #endregion
    }
}