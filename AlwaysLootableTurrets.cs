using Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Always Lootable Turrets", "WhiteThunder", "1.0.2")]
    [Description("Allows players to loot auto turrets while powered.")]
    internal class AlwaysLootableTurrets : CovalencePlugin
    {
        #region Fields

        private const string PermissionOwner = "alwayslootableturrets.owner";

        private const string PrefabCodeLockDeniedEffect = "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab";

        private readonly object False = false;
        private readonly object True = true;

        private Configuration _config;

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(PermissionOwner, this);
        }

        private object CanLootEntity(BasePlayer player, AutoTurret turret)
        {
            if (!turret.IsOnline() || turret.IsAuthed(player))
                return null;

            Effect.server.Run(PrefabCodeLockDeniedEffect, turret.transform.position);
            return False;
        }

        private void OnEntitySaved(AutoTurret turret, BaseNetworkable.SaveInfo saveInfo)
        {
            if (turret.IsOnline() && IsTurretEligible(turret) && TurretHasPermission(turret))
                saveInfo.msg.baseEntity.flags = RemoveOnFlag(saveInfo.msg.baseEntity.flags);
        }

        private object OnEntityFlagsNetworkUpdate(AutoTurret turret)
        {
            if (turret.IsOnline() && IsTurretEligible(turret) && TurretHasPermission(turret))
            {
                SendFlagUpdate(turret);
                return True;
            }

            return null;
        }

        #endregion

        #region Helper Methods

        private static bool IsTurretEligible(AutoTurret turret)
        {
            return turret is not NPCAutoTurret;
        }

        private static void SendFlagUpdate(AutoTurret turret)
        {
            var subscribers = turret.GetSubscribers();

            if (subscribers is { Count: > 0 })
            {
                var write = Net.sv.StartWrite(Message.Type.EntityFlags);
                write.EntityID(turret.net.ID);
                write.Int32(RemoveOnFlag((int)turret.flags));
                write.Send(new SendInfo(subscribers));
            }

            turret.gameObject.SendOnSendNetworkUpdate(turret);
        }

        private static int RemoveOnFlag(int flags)
        {
            return flags & ~(int)BaseEntity.Flags.On;
        }

        private bool TurretHasPermission(AutoTurret turret)
        {
            if (!_config.RequireOwnerPermission)
                return true;

            if (turret.OwnerID == 0)
                return false;

            return permission.UserHasPermission(turret.OwnerID.ToString(), PermissionOwner);
        }

        #endregion

        #region Configuration

        private class Configuration : BaseConfiguration
        {
            [JsonProperty("RequireOwnerPermission")]
            public bool RequireOwnerPermission = false;
        }

        private Configuration GetDefaultConfig() => new();

        #region Configuration Helpers

        private class BaseConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        private static class JsonHelper
        {
            public static object Deserialize(string json) => ToObject(JToken.Parse(json));

            private static object ToObject(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        return token.Children<JProperty>()
                                    .ToDictionary(prop => prop.Name,
                                                  prop => ToObject(prop.Value));

                    case JTokenType.Array:
                        return token.Select(ToObject).ToList();

                    default:
                        return ((JValue)token).Value;
                }
            }
        }

        private bool MaybeUpdateConfig(BaseConfiguration config)
        {
            var currentWithDefaults = config.ToDictionary();
            var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);
            return MaybeUpdateConfigDict(currentWithDefaults, currentRaw);
        }

        private bool MaybeUpdateConfigDict(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
        {
            var changed = false;

            foreach (var key in currentWithDefaults.Keys)
            {
                if (currentRaw.TryGetValue(key, out var currentRawValue))
                {
                    var currentDictValue = currentRawValue as Dictionary<string, object>;

                    if (currentWithDefaults[key] is Dictionary<string, object> defaultDictValue)
                    {
                        if (currentDictValue == null)
                        {
                            currentRaw[key] = currentWithDefaults[key];
                            changed = true;
                        }
                        else if (MaybeUpdateConfigDict(defaultDictValue, currentDictValue))
                            changed = true;
                    }
                }
                else
                {
                    currentRaw[key] = currentWithDefaults[key];
                    changed = true;
                }
            }

            return changed;
        }

        protected override void LoadDefaultConfig() => _config = GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null)
                {
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(_config))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Log($"Configuration changes saved to {Name}.json");
            Config.WriteObject(_config, true);
        }

        #endregion

        #endregion
    }
}
