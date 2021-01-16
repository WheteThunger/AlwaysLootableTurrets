using Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Always Lootable Turrets", "WhiteThunder", "0.1.0")]
    [Description("Allows players to loot auto turrets while powered.")]
    internal class AlwaysLootableTurrets : CovalencePlugin
    {
        #region Fields

        private const string PermissionOwner = "alwayslootableturrets.owner";

        private const string PrefabCodeLockDeniedEffect = "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab";

        private Configuration pluginConfig;

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
            return false;
        }

        private void OnEntitySaved(AutoTurret turret, BaseNetworkable.SaveInfo saveInfo)
        {
            if (IsTurretAllowed(turret) && turret.IsOnline() && TurretHasPermission(turret))
                saveInfo.msg.baseEntity.flags = RemoveOnFlag(saveInfo.msg.baseEntity.flags);
        }

        private object OnEntityFlagsNetworkUpdate(AutoTurret turret)
        {
            if (IsTurretAllowed(turret) && turret.IsOnline() && TurretHasPermission(turret))
            {
                SendFlagUpdate(turret);
                return false;
            }

            return null;
        }

        private int RemoveOnFlag(int flags)
        {
            return flags & ~(int)BaseEntity.Flags.On;
        }

        #endregion

        #region Helper Methods

        private bool IsTurretAllowed(AutoTurret turret)
        {
            return !(turret is NPCAutoTurret);
        }

        private bool TurretHasPermission(AutoTurret turret)
        {
            if (!pluginConfig.RequireOwnerPermission)
                return true;

            if (turret.OwnerID == 0)
                return false;

            return permission.UserHasPermission(turret.OwnerID.ToString(), PermissionOwner);
        }

        private void SendFlagUpdate(AutoTurret turret)
        {
            List<Connection> subscribers = turret.GetSubscribers();
            if (subscribers != null && subscribers.Count > 0 && Net.sv.write.Start())
            {
                Net.sv.write.PacketID(Message.Type.EntityFlags);
                Net.sv.write.EntityID(turret.net.ID);
                Net.sv.write.Int32(RemoveOnFlag((int)turret.flags));
                SendInfo info = new SendInfo(subscribers);
                Net.sv.write.Send(info);
            }
            turret.gameObject.SendOnSendNetworkUpdate(turret);
        }

        #endregion

        #region Configuration

        internal class Configuration : SerializableConfiguration
        {
            [JsonProperty("RequireOwnerPermission")]
            public bool RequireOwnerPermission = false;
        }

        private Configuration GetDefaultConfig() => new Configuration();

        #endregion

        #region Configuration Boilerplate

        internal class SerializableConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        internal static class JsonHelper
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

        private bool MaybeUpdateConfig(SerializableConfiguration config)
        {
            var currentWithDefaults = config.ToDictionary();
            var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);
            return MaybeUpdateConfigDict(currentWithDefaults, currentRaw);
        }

        private bool MaybeUpdateConfigDict(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
        {
            bool changed = false;

            foreach (var key in currentWithDefaults.Keys)
            {
                object currentRawValue;
                if (currentRaw.TryGetValue(key, out currentRawValue))
                {
                    var defaultDictValue = currentWithDefaults[key] as Dictionary<string, object>;
                    var currentDictValue = currentRawValue as Dictionary<string, object>;

                    if (defaultDictValue != null)
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

        protected override void LoadDefaultConfig() => pluginConfig = GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                pluginConfig = Config.ReadObject<Configuration>();
                if (pluginConfig == null)
                {
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(pluginConfig))
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
            Config.WriteObject(pluginConfig, true);
        }

        #endregion
    }
}
