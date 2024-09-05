// Decompiled with JetBrains decompiler
// Type: SLAYER_ZombieRiot.ZombieRiotDaysSettings
// Assembly: SLAYER_ZombieRiot, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: EFB61C6B-D231-40D9-8DFE-B400C55CDADD
// Assembly location: C:\Users\alexr\Desktop\SLAYER_ZombieRiot.dll

using System.Text.Json.Serialization;

#nullable enable
namespace SLAYER_ZombieRiot
{
  public class ZombieRiotDaysSettings
  {
    [JsonPropertyName("DayName")]
    public string DayName { get; set; } = "Outbreak";

    [JsonPropertyName("ZKillCount")]
    public int ZKillCount { get; set; } = 15;

    [JsonPropertyName("ZHealthBoost")]
    public int ZHealthBoost { get; set; } = 0;

    [JsonPropertyName("ZRespawn")]
    public bool ZRespawn { get; set; } = true;

    [JsonPropertyName("DeathsBeforeZombie")]
    public int DeathsBeforeZombie { get; set; } = 2;

    [JsonPropertyName("ZombieOverride")]
    public string ZombieOverride { get; set; } = "";

    [JsonPropertyName("DayStoryLine")]
    public string DayStoryLine { get; set; } = "";
  }
}
