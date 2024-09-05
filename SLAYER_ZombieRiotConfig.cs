// Decompiled with JetBrains decompiler
// Type: SLAYER_ZombieRiot.SLAYER_ZombieRiotConfig
// Assembly: SLAYER_ZombieRiot, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: EFB61C6B-D231-40D9-8DFE-B400C55CDADD
// Assembly location: C:\Users\alexr\Desktop\SLAYER_ZombieRiot.dll

using CounterStrikeSharp.API.Core;
using System.Collections.Generic;
using System.Text.Json.Serialization;

#nullable enable
namespace SLAYER_ZombieRiot
{
  public class SLAYER_ZombieRiotConfig : BasePluginConfig
  {
    [JsonPropertyName("ZRiot_PluginEnabled")]
    public bool ZRiot_PluginEnabled { get; set; } = true;

    [JsonPropertyName("ZRiot_HudText")]
    public bool ZRiot_HudText { get; set; } = true;

    [JsonPropertyName("ZRiot_NoBlock")]
    public bool ZRiot_NoBlock { get; set; } = true;

    [JsonPropertyName("ZRiot_Regression")]
    public bool ZRiot_Regression { get; set; } = true;

    [JsonPropertyName("ZRiot_Freeze")]
    public float ZRiot_Freeze { get; set; } = 10f;

    [JsonPropertyName("ZRiot_FirstRespawn")]
    public int ZRiot_FirstRespawn { get; set; } = 10;

    [JsonPropertyName("ZRiot_Respawn")]
    public int ZRiot_Respawn { get; set; } = 30;

    [JsonPropertyName("ZRiot_ZombieMax")]
    public int ZRiot_ZombieMax { get; set; } = 12;

    [JsonPropertyName("ZRriot_Cashamount")]
    public int ZRriot_Cashamount { get; set; } = 12000;

    [JsonPropertyName("ZRriot_HumanWinSoundPath")]
    public string ZRriot_HumanWinSoundPath { get; set; } = "";

    [JsonPropertyName("ZRriot_ZombieWinSoundPath")]
    public string ZRriot_ZombieWinSoundPath { get; set; } = "";

    [JsonPropertyName("ZRriot_ZombieDieSoundPath")]
    public string ZRriot_ZombieDieSoundPath { get; set; } = "";

    [JsonPropertyName("ZRriot_AdminFlagToUseCMDs")]
    public string ZRriot_AdminFlagToUseCMDs { get; set; } = "@css/root";

    [JsonPropertyName("ZRiot_Days")]
    public List<ZombieRiotDaysSettings> ZRiot_Days { get; set; } = new List<ZombieRiotDaysSettings>();

    [JsonPropertyName("ZRiot_Zombies")]
    public List<ZombieRiotZombieSettings> ZRiot_Zombies { get; set; } = new List<ZombieRiotZombieSettings>();
  }
}
