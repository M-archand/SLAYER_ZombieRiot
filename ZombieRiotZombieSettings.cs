// Decompiled with JetBrains decompiler
// Type: SLAYER_ZombieRiot.ZombieRiotZombieSettings
// Assembly: SLAYER_ZombieRiot, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: EFB61C6B-D231-40D9-8DFE-B400C55CDADD
// Assembly location: C:\Users\alexr\Desktop\SLAYER_ZombieRiot.dll

using System.Text.Json.Serialization;

#nullable enable
namespace SLAYER_ZombieRiot
{
  public class ZombieRiotZombieSettings
  {
    [JsonPropertyName("ZombieClassName")]
    public string ZombieClassName { get; set; } = "NormalZombie";

    [JsonPropertyName("ZombieModelPath")]
    public string ZombieModelPath { get; set; } = "";

    [JsonPropertyName("ZombieTypeNormal")]
    public bool ZombieTypeNormal { get; set; } = true;

    [JsonPropertyName("ZombieHealth")]
    public int ZombieHealth { get; set; } = 100;

    [JsonPropertyName("ZombieSpeed")]
    public float ZombieSpeed { get; set; } = 1.1f;

    [JsonPropertyName("ZombieGravity")]
    public float ZombieGravity { get; set; } = 0.9f;

    [JsonPropertyName("ZombieJump")]
    public float ZombieJump { get; set; } = 15f;

    [JsonPropertyName("ZombieFOV")]
    public int ZombieFOV { get; set; } = 110;
  }
}
