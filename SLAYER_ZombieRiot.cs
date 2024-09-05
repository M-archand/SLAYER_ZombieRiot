using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Commands.Targeting;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#nullable enable
namespace SLAYER_ZombieRiot
{
  [RequiredMember]
  public class SLAYER_ZombieRiot : BasePlugin, IPluginConfig<SLAYER_ZombieRiotConfig>
  {
    public int gZombiesKilled = 0;
    public int gCurrentDay = 0;
    public int[] gZombieID = new int[64];
    public int[] gRespawnTime = new int[64];
    public bool[] IsBotSpawnedByCmd = new bool[64];
    public bool[] IsPlayerZombie = new bool[64];
    public bool IsRoundEnd = false;
    public Timer? t_ZFreeze;
    public Timer[]? tRespawn = new Timer[64];
    private static readonly Vector VectorZero = new Vector(new float?(0.0f), new float?(0.0f), new float?(0.0f));
    private static readonly QAngle RotationZero = new QAngle(new float?(0.0f), new float?(0.0f), new float?(0.0f));
    public static string RespawnWindowsSig = "\\x44\\x88\\x4C\\x24\\x2A\\x55\\x57";
    public static string RespawnLinuxSig = "\\x55\\x48\\x89\\xE5\\x41\\x57\\x41\\x56\\x41\\x55\\x41\\x54\\x49\\x89\\xFC\\x53\\x48\\x89\\xF3\\x48\\x81\\xEC\\xC8\\x00\\x00\\x00";
    public static MemoryFunctionVoid<CCSPlayerController, CCSPlayerPawn, bool, bool> CBasePlayerController_SetPawnFunc = new MemoryFunctionVoid<CCSPlayerController, CCSPlayerPawn, bool, bool>(RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? SLAYER_ZombieRiot.SLAYER_ZombieRiot.RespawnLinuxSig : SLAYER_ZombieRiot.SLAYER_ZombieRiot.RespawnWindowsSig);
    public Action<IntPtr, float, RoundEndReason, IntPtr, uint> TerminateRoundWindows = new Action<IntPtr, float, RoundEndReason, IntPtr, uint>(SLAYER_ZombieRiot.SLAYER_ZombieRiot.TerminateRoundWindowsFunc.Invoke);
    public static MemoryFunctionVoid<IntPtr, float, RoundEndReason, IntPtr, uint> TerminateRoundWindowsFunc = new MemoryFunctionVoid<IntPtr, float, RoundEndReason, IntPtr, uint>(GameData.GetSignature("CCSGameRules_TerminateRound"));
    public static MemoryFunctionVoid<IntPtr, RoundEndReason, IntPtr, uint, float> TerminateRoundLinuxFunc = new MemoryFunctionVoid<IntPtr, RoundEndReason, IntPtr, uint, float>("55 48 89 E5 41 57 41 56 41 55 41 54 49 89 FC 53 48 81 EC ? ? ? ? 48 8D 05 ? ? ? ? F3 0F 11 85");
    public Action<IntPtr, RoundEndReason, IntPtr, uint, float> TerminateRoundLinux = new Action<IntPtr, RoundEndReason, IntPtr, uint, float>(SLAYER_ZombieRiot.SLAYER_ZombieRiot.TerminateRoundLinuxFunc.Invoke);
    private static readonly bool IsWindowsPlatform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public override string ModuleName => nameof (SLAYER_ZombieRiot);

    public override string ModuleVersion => "1.0";

    public override string ModuleAuthor => "SLAYER";

    public override string ModuleDescription => "Humans fight with zombies";

    [RequiredMember]
    public SLAYER_ZombieRiotConfig Config { get; set; }

    public void OnConfigParsed(SLAYER_ZombieRiotConfig config) => this.Config = config;

    public ZombieRiotDaysSettings GetDayByName(string modeName, StringComparer comparer)
    {
      return this.Config.ZRiot_Days.FirstOrDefault<ZombieRiotDaysSettings>((Func<ZombieRiotDaysSettings, bool>) (mode => comparer.Equals(mode.DayName, modeName)));
    }

    public ZombieRiotDaysSettings GetDayByIndex(int day)
    {
      return this.Config.ZRiot_Days.ElementAt<ZombieRiotDaysSettings>(day);
    }

    public ZombieRiotZombieSettings GetZombieClassByName(string ClassName, StringComparer comparer)
    {
      return this.Config.ZRiot_Zombies.FirstOrDefault<ZombieRiotZombieSettings>((Func<ZombieRiotZombieSettings, bool>) (mode => comparer.Equals(mode.ZombieClassName, ClassName)));
    }

    public ZombieRiotZombieSettings GetZombieByIndex(int Zombie)
    {
      return this.Config.ZRiot_Zombies.ElementAt<ZombieRiotZombieSettings>(Zombie);
    }

    public int GetZombieClassIndexByName(string ClassName)
    {
      int classIndexByName = 0;
      foreach (ZombieRiotZombieSettings zriotZomby in this.Config.ZRiot_Zombies)
      {
        if (zriotZomby.ZombieClassName == ClassName)
          return classIndexByName;
        ++classIndexByName;
      }
      return -1;
    }

    public override void Unload(bool hotReload) => this.ZRiotEnd();

    public override void Load(bool hotReload)
    {
      this.AddCommand("css_zriot_setday", "Sets the game to a certain day", new CommandInfo.CommandCallback(this.CMD_ZRiotSetDay));
      this.AddCommand("css_zriot_zombie", "Turns player into zombie", new CommandInfo.CommandCallback(this.CMD_ZRiotZombie));
      this.AddCommand("css_zriot_human", "Turns player into human", new CommandInfo.CommandCallback(this.CMD_ZRiotHuman));
      if (this.Config.ZRiot_PluginEnabled)
      {
        this.gCurrentDay = 0;
        Server.ExecuteCommand("bot_kick");
        Server.ExecuteCommand("bot_quota_mode normal");
        Server.ExecuteCommand("mp_autoteambalance 0");
        Server.ExecuteCommand("mp_limitteams 0");
      }
      this.RegisterListener<CounterStrikeSharp.API.Core.Listeners.OnMapStart>((CounterStrikeSharp.API.Core.Listeners.OnMapStart) (mapName =>
      {
        if (!this.Config.ZRiot_PluginEnabled)
          return;
        this.gCurrentDay = 0;
        Server.ExecuteCommand("bot_kick");
        Server.ExecuteCommand("bot_quota_mode normal");
        Server.ExecuteCommand("mp_autoteambalance 0");
        Server.ExecuteCommand("mp_limitteams 0");
      }));
      this.RegisterListener<CounterStrikeSharp.API.Core.Listeners.OnTick>((CounterStrikeSharp.API.Core.Listeners.OnTick) (() =>
      {
        if (!this.Config.ZRiot_PluginEnabled)
          return;
        foreach (CCSPlayerController playerController1 in Utilities.GetPlayers().Where<CCSPlayerController>((Func<CCSPlayerController, bool>) (player => (CEntityInstance) player != (CEntityInstance) null && player.IsValid && player.Connected == PlayerConnectedState.PlayerConnected && !player.IsHLTV && !player.IsBot && player.TeamNum > (byte) 0)))
        {
          DefaultInterpolatedStringHandler interpolatedStringHandler;
          if (this.Config.ZRiot_HudText && !this.IsRoundEnd && playerController1.Pawn.Value.LifeState == (byte) 0 || playerController1.TeamNum == (byte) 1)
          {
            CCSPlayerController playerController2 = playerController1;
            interpolatedStringHandler = new DefaultInterpolatedStringHandler(0, 4);
            interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Center.Prefix"]);
            interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Center.Day", new object[3]
            {
              (object) (this.gCurrentDay + 1),
              (object) this.Config.ZRiot_Days.Count,
              (object) this.GetDayByIndex(this.gCurrentDay).DayName
            }]);
            interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Center.Zombies", new object[1]
            {
              (object) this.GetAliveZombieCount()
            }]);
            interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Center.Humans", new object[1]
            {
              (object) this.GetAliveHumanCount()
            }]);
            string stringAndClear = interpolatedStringHandler.ToStringAndClear();
            playerController2.PrintToCenterHtml(stringAndClear);
          }
          if (this.tRespawn[playerController1.Slot] != null && playerController1.Pawn.Value.LifeState != (byte) 0 && this.gRespawnTime[playerController1.Slot] > 0)
          {
            CCSPlayerController playerController3 = playerController1;
            interpolatedStringHandler = new DefaultInterpolatedStringHandler(0, 2);
            interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Center.Prefix"]);
            interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Center.Respawn", new object[1]
            {
              (object) this.gRespawnTime[playerController1.Slot]
            }]);
            string stringAndClear = interpolatedStringHandler.ToStringAndClear();
            playerController3.PrintToCenterHtml(stringAndClear);
          }
        }
      }));
      this.RegisterEventHandler<EventRoundStart>((BasePlugin.GameEventHandler<EventRoundStart>) ((@event, info) =>
      {
        if (!this.Config.ZRiot_PluginEnabled)
          return HookResult.Continue;
        this.IsRoundEnd = false;
        this.ResetZombies();
        Server.ExecuteCommand("mp_autoteambalance 0");
        Server.ExecuteCommand("bot_quota_mode normal");
        Server.ExecuteCommand("mp_limitteams 0");
        Server.ExecuteCommand("bot_knives_only");
        DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(1, 2);
        interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Prefix"]);
        interpolatedStringHandler.AppendLiteral(" ");
        interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.GameInfo"]);
        Server.PrintToChatAll(interpolatedStringHandler.ToStringAndClear());
        if (this.GetDayByIndex(this.gCurrentDay).DayStoryLine != "")
        {
          interpolatedStringHandler = new DefaultInterpolatedStringHandler(1, 2);
          interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Prefix"]);
          interpolatedStringHandler.AppendLiteral(" ");
          interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.DayStory", new object[1]
          {
            (object) this.GetDayByIndex(this.gCurrentDay).DayStoryLine
          }]);
          Server.PrintToChatAll(interpolatedStringHandler.ToStringAndClear());
        }
        this.BeginDay();
        if (this.t_ZFreeze != null)
          this.t_ZFreeze?.Kill();
        return HookResult.Continue;
      }));
      this.RegisterEventHandler<EventRoundFreezeEnd>((BasePlugin.GameEventHandler<EventRoundFreezeEnd>) ((@event, info) =>
      {
        if (!this.Config.ZRiot_PluginEnabled)
          return HookResult.Continue;
        this.RemoveObjectives();
        if (this.t_ZFreeze != null)
          this.t_ZFreeze?.Kill();
        if ((double) this.Config.ZRiot_Freeze > 0.0)
        {
          this.FreezeZombies();
          foreach (CCSPlayerController playerController in Utilities.GetPlayers().Where<CCSPlayerController>((Func<CCSPlayerController, bool>) (player => (CEntityInstance) player != (CEntityInstance) null && player.IsValid && player.Connected == PlayerConnectedState.PlayerConnected && !player.IsHLTV && !player.IsBot && player.TeamNum == (byte) 3)))
          {
            DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(1, 2);
            interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Prefix"]);
            interpolatedStringHandler.AppendLiteral(" ");
            interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.ZFreezed", new object[1]
            {
              (object) this.Config.ZRiot_Freeze
            }]);
            string stringAndClear = interpolatedStringHandler.ToStringAndClear();
            playerController.PrintToChat(stringAndClear);
          }
          this.t_ZFreeze = this.AddTimer(this.Config.ZRiot_Freeze, new Action(this.UnFreezeZombies));
        }
        return HookResult.Continue;
      }));
      this.RegisterEventHandler<EventRoundEnd>((BasePlugin.GameEventHandler<EventRoundEnd>) ((@event, info) =>
      {
        if (!this.Config.ZRiot_PluginEnabled)
          return HookResult.Continue;
        this.ResetZombies();
        if (@event.Reason == 8)
          this.HumansWin();
        else if (@event.Reason == 9)
          this.ZombiesWin();
        if (this.t_ZFreeze != null)
          this.t_ZFreeze?.Kill();
        return HookResult.Continue;
      }));
      this.RegisterEventHandler<EventPlayerDisconnect>((BasePlugin.GameEventHandler<EventPlayerDisconnect>) ((@event, info) =>
      {
        CCSPlayerController userid = @event.Userid;
        if (!this.Config.ZRiot_PluginEnabled || this.tRespawn?[userid.Slot] == null)
          return HookResult.Continue;
        this.tRespawn?[userid.Slot]?.Kill();
        return HookResult.Continue;
      }));
      this.RegisterEventHandler<EventPlayerTeam>((BasePlugin.GameEventHandler<EventPlayerTeam>) ((@event, info) =>
      {
        CCSPlayerController player = @event.Userid;
        if (!this.Config.ZRiot_PluginEnabled || @event.Disconnect || (CEntityInstance) player == (CEntityInstance) null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected || player.IsHLTV)
          return HookResult.Continue;
        if (@event.Team != 1 && @event.Oldteam == 0 || @event.Oldteam == 1)
          this.AddTimer(0.1f, (Action) (() =>
          {
            CCSPlayerController player1 = player;
            CBasePlayerPawn cbasePlayerPawn = player.Pawn.Value;
            byte? nullable3 = cbasePlayerPawn != null ? new byte?(cbasePlayerPawn.LifeState) : new byte?();
            int? nullable4 = nullable3.HasValue ? new int?((int) nullable3.GetValueOrDefault()) : new int?();
            int num3 = 0;
            int num4 = nullable4.GetValueOrDefault() == num3 & nullable4.HasValue ? 1 : 0;
            this.AssignTeam(player1, num4 != 0);
          }));
        if (@event.Team == 3)
        {
          player.DesiredFOV = Convert.ToUInt32(90);
          Utilities.SetStateChanged((CBaseEntity) player, "CBasePlayerController", "m_iDesiredFOV");
          player.PlayerPawn.Value.GravityScale = 800f;
          CBasePlayerPawn cbasePlayerPawn = player.Pawn.Value;
          byte? nullable5 = cbasePlayerPawn != null ? new byte?(cbasePlayerPawn.LifeState) : new byte?();
          int? nullable6 = nullable5.HasValue ? new int?((int) nullable5.GetValueOrDefault()) : new int?();
          int num = 0;
          if (!(nullable6.GetValueOrDefault() == num & nullable6.HasValue))
            this.StartRespawnTimer(player, true);
        }
        return HookResult.Continue;
      }));
      this.RegisterEventHandler<EventPlayerSpawn>((BasePlugin.GameEventHandler<EventPlayerSpawn>) ((@event, info) =>
      {
        CCSPlayerController player = @event.Userid;
        if (!this.Config.ZRiot_PluginEnabled || (CEntityInstance) player == (CEntityInstance) null || !player.IsValid || player.TeamNum < (byte) 2 || player.IsHLTV)
          return HookResult.Continue;
        this.gZombieID[player.Slot] = -1;
        if (player.IsBot && !this.IsBotSpawnedByCmd[player.Slot])
          this.IsPlayerZombie[player.Slot] = true;
        this.AddTimer(0.1f, (Action) (() =>
        {
          CCSPlayerController player2 = player;
          CBasePlayerPawn cbasePlayerPawn = player.Pawn.Value;
          byte? nullable9 = cbasePlayerPawn != null ? new byte?(cbasePlayerPawn.LifeState) : new byte?();
          int? nullable10 = nullable9.HasValue ? new int?((int) nullable9.GetValueOrDefault()) : new int?();
          int num7 = 0;
          int num8 = nullable10.GetValueOrDefault() == num7 & nullable10.HasValue ? 1 : 0;
          this.AssignTeam(player2, num8 != 0);
        }));
        if (this.IsPlayerZombie[player.Slot])
        {
          CCSPlayerController_InGameMoneyServices gameMoneyServices = player.InGameMoneyServices;
          if (gameMoneyServices != null)
            gameMoneyServices.Account = 0;
          if (this.Config.ZRiot_NoBlock)
          {
            player.PlayerPawn.Value.Collision.CollisionGroup = (byte) 19;
            player.PlayerPawn.Value.Collision.CollisionAttribute.CollisionGroup = (byte) 19;
            Utilities.SetStateChanged((CBaseEntity) player, "CCollisionProperty", "m_CollisionGroup");
            Utilities.SetStateChanged((CBaseEntity) player, "VPhysicsCollisionAttribute_t", "m_nCollisionGroup");
          }
          string[] source = this.GetDayByIndex(this.gCurrentDay).ZombieOverride.Split(",");
          if (this.GetDayByIndex(this.gCurrentDay).ZombieOverride != "" && source != null && ((IEnumerable<string>) source).Count<string>() > 0)
          {
            int index = new Random().Next(0, ((IEnumerable<string>) source).Count<string>());
            this.Zombify(player, this.GetZombieClassIndexByName(source[index]));
          }
          else
          {
            int num;
            do
            {
              num = new Random().Next(0, this.Config.ZRiot_Zombies.Count);
            }
            while (!this.Config.ZRiot_Zombies[num].ZombieTypeNormal);
            this.Zombify(player, num);
          }
          player.PlayerPawn.Value.Health += this.GetDayByIndex(this.gCurrentDay).ZHealthBoost;
        }
        else
        {
          if (this.Config.ZRiot_NoBlock)
          {
            player.PlayerPawn.Value.Collision.CollisionGroup = (byte) 8;
            player.PlayerPawn.Value.Collision.CollisionAttribute.CollisionGroup = (byte) 8;
            Utilities.SetStateChanged((CBaseEntity) player, "CCollisionProperty", "m_CollisionGroup");
            Utilities.SetStateChanged((CBaseEntity) player, "VPhysicsCollisionAttribute_t", "m_nCollisionGroup");
          }
          player.PlayerPawn.Value.GravityScale = 800f;
          player.DesiredFOV = Convert.ToUInt32(90);
          if (this.Config.ZRriot_Cashamount > 0)
          {
            CCSPlayerController_InGameMoneyServices gameMoneyServices = player.InGameMoneyServices;
            if (gameMoneyServices != null)
              gameMoneyServices.Account = this.Config.ZRriot_Cashamount;
          }
        }
        Utilities.SetStateChanged((CBaseEntity) player, "CCSPlayerController_InGameMoneyServices", "m_iAccount");
        if (this.tRespawn[player.Slot] != null)
          this.tRespawn[player.Slot]?.Kill();
        return HookResult.Continue;
      }));
      this.RegisterEventHandler<EventPlayerDeath>((BasePlugin.GameEventHandler<EventPlayerDeath>) ((@event, info) =>
      {
        if (!this.Config.ZRiot_PluginEnabled)
          return HookResult.Continue;
        CCSPlayerController player = @event.Userid;
        if (this.IsPlayerZombie[player.Slot])
        {
          string[] source = this.Config.ZRriot_ZombieDieSoundPath.Split(";");
          if (this.Config.ZRriot_ZombieDieSoundPath != "" && source != null && ((IEnumerable<string>) source).Count<string>() > 0)
          {
            int index = new Random().Next(0, ((IEnumerable<string>) source).Count<string>());
            this.PlaySoundOnPlayer(player, ((IEnumerable<string>) source).ElementAt<string>(index));
          }
          if (this.GetDayByIndex(this.gCurrentDay).ZRespawn || this.GetAliveZombieCount() > this.Config.ZRiot_ZombieMax)
            this.AddTimer(0.5f, (Action) (() => player.Respawn()), new TimerFlags?(TimerFlags.STOP_ON_MAPCHANGE));
          ++this.gZombiesKilled;
          if (this.gZombiesKilled == this.GetDayByIndex(this.gCurrentDay).ZKillCount && this.GetDayByIndex(this.gCurrentDay).ZRespawn && !this.IsRoundEnd)
          {
            this.IsRoundEnd = true;
            this.TerminateRound(5f, RoundEndReason.CTsWin);
          }
        }
        else
        {
          if (this.GetDayByIndex(this.gCurrentDay).DeathsBeforeZombie > 0 && player.ActionTrackingServices.MatchStats.Deaths >= this.GetDayByIndex(this.gCurrentDay).DeathsBeforeZombie && this.GetAliveHumanCount() > 0)
          {
            CCSPlayerController playerController = player;
            DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(1, 2);
            interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Prefix"]);
            interpolatedStringHandler.AppendLiteral(" ");
            interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.SwitchToZombie"]);
            string stringAndClear = interpolatedStringHandler.ToStringAndClear();
            playerController.PrintToChat(stringAndClear);
            this.AddTimer(0.5f, (Action) (() =>
            {
              this.IsPlayerZombie[player.Slot] = true;
              this.AssignTeam(player, true);
            }));
          }
          else
            this.StartRespawnTimer(player, false);
          if (this.GetAliveHumanCount() <= 0 && this.GetDayByIndex(this.gCurrentDay).ZRespawn)
            this.TerminateRound(5f, RoundEndReason.TerroristsWin);
        }
        return HookResult.Continue;
      }));
      this.RegisterEventHandler<EventPlayerJump>((BasePlugin.GameEventHandler<EventPlayerJump>) ((@event, info) =>
      {
        CCSPlayerController userid = @event.Userid;
        if (!this.Config.ZRiot_PluginEnabled || !this.IsPlayerZombie[userid.Slot])
          return HookResult.Continue;
        userid.PlayerPawn.Value.AbsVelocity.Z = this.GetZombieByIndex(this.gZombieID[userid.Slot]).ZombieJump;
        return HookResult.Continue;
      }));
    }

    private void CMD_ZRiotSetDay(CCSPlayerController? player, CommandInfo commandInfo)
    {
      if ((CEntityInstance) player == (CEntityInstance) null)
        commandInfo.ReplyToCommand("[Zombie Riot] Cannot use command from RCON");
      else if (!this.Config.ZRiot_PluginEnabled)
      {
        CCSPlayerController playerController = player;
        DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(1, 2);
        interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Prefix"]);
        interpolatedStringHandler.AppendLiteral(" ");
        interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Cmd.PluginDisabled"]);
        string stringAndClear = interpolatedStringHandler.ToStringAndClear();
        playerController.PrintToChat(stringAndClear);
      }
      else
      {
        int num;
        if ((CEntityInstance) player != (CEntityInstance) null)
          num = !AdminManager.PlayerHasPermissions(player, this.Config.ZRriot_AdminFlagToUseCMDs) ? 1 : 0;
        else
          num = 0;
        if (num != 0)
        {
          CCSPlayerController playerController = player;
          DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(1, 2);
          interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Prefix"]);
          interpolatedStringHandler.AppendLiteral(" ");
          interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Cmd.NoPermission"]);
          string stringAndClear = interpolatedStringHandler.ToStringAndClear();
          playerController.PrintToChat(stringAndClear);
        }
        else
        {
          string sVal = commandInfo.ArgByIndex(1);
          if (sVal == "" || commandInfo.ArgCount <= 0)
          {
            CCSPlayerController playerController = player;
            DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(1, 2);
            interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Prefix"]);
            interpolatedStringHandler.AppendLiteral(" ");
            interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Cmd.InvalidD"]);
            string stringAndClear = interpolatedStringHandler.ToStringAndClear();
            playerController.PrintToChat(stringAndClear);
          }
          else if (!this.IsInt(sVal))
          {
            CCSPlayerController playerController = player;
            DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(1, 2);
            interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Prefix"]);
            interpolatedStringHandler.AppendLiteral(" ");
            interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Cmd.InvalidInt"]);
            string stringAndClear = interpolatedStringHandler.ToStringAndClear();
            playerController.PrintToChat(stringAndClear);
          }
          else if (Convert.ToInt32(sVal) < 1 || Convert.ToInt32(sVal) > this.Config.ZRiot_Days.Count<ZombieRiotDaysSettings>())
          {
            CCSPlayerController playerController = player;
            DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(1, 2);
            interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Prefix"]);
            interpolatedStringHandler.AppendLiteral(" ");
            interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Cmd.InvalidDay", new object[1]
            {
              (object) this.Config.ZRiot_Days.Count<ZombieRiotDaysSettings>()
            }]);
            string stringAndClear = interpolatedStringHandler.ToStringAndClear();
            playerController.PrintToChat(stringAndClear);
          }
          else
          {
            this.gCurrentDay = Convert.ToInt32(sVal) - 1;
            DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(2, 2);
            interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Prefix"]);
            interpolatedStringHandler.AppendLiteral("  ");
            interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Cmd.DayChanged", new object[1]
            {
              (object) sVal
            }]);
            Server.PrintToChatAll(interpolatedStringHandler.ToStringAndClear());
            this.TerminateRound(3f, RoundEndReason.RoundDraw);
          }
        }
      }
    }

    private void CMD_ZRiotZombie(CCSPlayerController? player, CommandInfo commandInfo)
    {
      if ((CEntityInstance) player == (CEntityInstance) null)
        commandInfo.ReplyToCommand("[Zombie Riot] Cannot use command from RCON");
      else if (!this.Config.ZRiot_PluginEnabled)
      {
        CCSPlayerController playerController = player;
        DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(1, 2);
        interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Prefix"]);
        interpolatedStringHandler.AppendLiteral(" ");
        interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Cmd.PluginDisabled"]);
        string stringAndClear = interpolatedStringHandler.ToStringAndClear();
        playerController.PrintToChat(stringAndClear);
      }
      else
      {
        int num;
        if ((CEntityInstance) player != (CEntityInstance) null)
          num = !AdminManager.PlayerHasPermissions(player, this.Config.ZRriot_AdminFlagToUseCMDs) ? 1 : 0;
        else
          num = 0;
        if (num != 0)
        {
          CCSPlayerController playerController = player;
          DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(1, 2);
          interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Prefix"]);
          interpolatedStringHandler.AppendLiteral(" ");
          interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Cmd.NoPermission"]);
          string stringAndClear = interpolatedStringHandler.ToStringAndClear();
          playerController.PrintToChat(stringAndClear);
        }
        else if (commandInfo.ArgString == "" || commandInfo.ArgCount <= 0)
        {
          CCSPlayerController playerController = player;
          DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(1, 2);
          interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Prefix"]);
          interpolatedStringHandler.AppendLiteral(" ");
          interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Cmd.InvalidZ"]);
          string stringAndClear = interpolatedStringHandler.ToStringAndClear();
          playerController.PrintToChat(stringAndClear);
        }
        else
        {
          TargetResult target = this.GetTarget(commandInfo);
          if (target == null)
            return;
          target.Players.Where<CCSPlayerController>((Func<CCSPlayerController, bool>) (targetplayer => SLAYER_ZombieRiot.SLAYER_ZombieRiot.CanTarget(player, targetplayer) && (CEntityInstance) targetplayer != (CEntityInstance) null && targetplayer.IsValid && targetplayer.Connected == PlayerConnectedState.PlayerConnected && !targetplayer.IsHLTV)).ToList<CCSPlayerController>().ForEach((Action<CCSPlayerController>) (targetplayer =>
          {
            this.IsPlayerZombie[targetplayer.Slot] = true;
            if (targetplayer.TeamNum != (byte) 3 || targetplayer.Pawn.Value.LifeState != (byte) 0)
              return;
            targetplayer.Respawn();
          }));
        }
      }
    }

    private void CMD_ZRiotHuman(CCSPlayerController? player, CommandInfo commandInfo)
    {
      if ((CEntityInstance) player == (CEntityInstance) null)
        commandInfo.ReplyToCommand("[Zombie Riot] Cannot use command from RCON");
      else if (!this.Config.ZRiot_PluginEnabled)
      {
        CCSPlayerController playerController = player;
        DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(1, 2);
        interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Prefix"]);
        interpolatedStringHandler.AppendLiteral(" ");
        interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Cmd.PluginDisabled"]);
        string stringAndClear = interpolatedStringHandler.ToStringAndClear();
        playerController.PrintToChat(stringAndClear);
      }
      else
      {
        int num;
        if ((CEntityInstance) player != (CEntityInstance) null)
          num = !AdminManager.PlayerHasPermissions(player, this.Config.ZRriot_AdminFlagToUseCMDs) ? 1 : 0;
        else
          num = 0;
        if (num != 0)
        {
          CCSPlayerController playerController = player;
          DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(1, 2);
          interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Prefix"]);
          interpolatedStringHandler.AppendLiteral(" ");
          interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Cmd.NoPermission"]);
          string stringAndClear = interpolatedStringHandler.ToStringAndClear();
          playerController.PrintToChat(stringAndClear);
        }
        else if (commandInfo.ArgString == "" || commandInfo.ArgCount <= 0)
        {
          CCSPlayerController playerController = player;
          DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(1, 2);
          interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Prefix"]);
          interpolatedStringHandler.AppendLiteral(" ");
          interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Cmd.InvalidH"]);
          string stringAndClear = interpolatedStringHandler.ToStringAndClear();
          playerController.PrintToChat(stringAndClear);
        }
        else
        {
          TargetResult target = this.GetTarget(commandInfo);
          if (target == null)
            return;
          target.Players.Where<CCSPlayerController>((Func<CCSPlayerController, bool>) (targetplayer => SLAYER_ZombieRiot.SLAYER_ZombieRiot.CanTarget(player, targetplayer) && (CEntityInstance) targetplayer != (CEntityInstance) null && targetplayer.IsValid && targetplayer.Connected == PlayerConnectedState.PlayerConnected && !targetplayer.IsHLTV)).ToList<CCSPlayerController>().ForEach((Action<CCSPlayerController>) (targetplayer =>
          {
            this.IsPlayerZombie[targetplayer.Slot] = false;
            if (targetplayer.TeamNum != (byte) 2 || targetplayer.Pawn.Value.LifeState != (byte) 0)
              return;
            this.IsBotSpawnedByCmd[targetplayer.Slot] = true;
            targetplayer.Respawn();
          }));
        }
      }
    }

    private void AssignTeam(CCSPlayerController? player, bool spawn)
    {
      if ((CEntityInstance) player == (CEntityInstance) null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected || player.IsHLTV)
        return;
      if (this.IsPlayerZombie[player.Slot])
      {
        if (player.TeamNum == (byte) 2)
          return;
        if (player.PawnIsAlive)
          player.SwitchTeam(CsTeam.Terrorist);
        else
          player.ChangeTeam(CsTeam.Terrorist);
        if (spawn)
          player.Respawn();
      }
      else if (player.TeamNum != (byte) 3)
      {
        if (player.PawnIsAlive)
          player.SwitchTeam(CsTeam.CounterTerrorist);
        else
          player.ChangeTeam(CsTeam.CounterTerrorist);
        if (spawn)
          player.Respawn();
      }
    }

    private void StartRespawnTimer(CCSPlayerController? player, bool firstspawn)
    {
      int num = !firstspawn ? this.Config.ZRiot_Respawn : this.Config.ZRiot_FirstRespawn;
      if (num == 0)
        return;
      if (this.tRespawn[player.Slot] != null)
        this.tRespawn[player.Slot].Kill();
      this.gRespawnTime[player.Slot] = num;
      this.tRespawn[player.Slot] = this.AddTimer(1f, (Action) (() => this.HumanRespawn(player)), new TimerFlags?(TimerFlags.REPEAT));
    }

    public static CCSGameRules GetGameRules()
    {
      return Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First<CCSGameRulesProxy>().GameRules;
    }

    public void HumanRespawn(CCSPlayerController player)
    {
      if ((CEntityInstance) player == (CEntityInstance) null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected || player.IsHLTV)
      {
        this.tRespawn[player.Slot].Kill();
      }
      else
      {
        if (player.TeamNum == (byte) 2 || player.TeamNum == (byte) 3)
          --this.gRespawnTime[player.Slot];
        if (this.gRespawnTime[player.Slot] > 0)
          return;
        this.RespawnClient(player);
        this.tRespawn[player.Slot].Kill();
      }
    }

    public void BeginDay()
    {
      this.gZombiesKilled = 0;
      int spawncount = !this.GetDayByIndex(this.gCurrentDay).ZRespawn ? (this.GetDayByIndex(this.gCurrentDay).ZKillCount < this.Config.ZRiot_ZombieMax ? this.GetDayByIndex(this.gCurrentDay).ZKillCount : this.Config.ZRiot_ZombieMax) : this.Config.ZRiot_ZombieMax;
      this.AddTimer(0.5f, (Action) (() =>
      {
        DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(10, 1);
        interpolatedStringHandler.AppendLiteral("bot_quota ");
        interpolatedStringHandler.AppendFormatted<int>(spawncount);
        Server.ExecuteCommand(interpolatedStringHandler.ToStringAndClear());
      }));
      string[] source = this.GetDayByIndex(this.gCurrentDay).ZombieOverride.Split(",");
      foreach (CCSPlayerController player in Utilities.GetPlayers().Where<CCSPlayerController>((Func<CCSPlayerController, bool>) (player => (CEntityInstance) player != (CEntityInstance) null && player.IsValid && player.Connected == PlayerConnectedState.PlayerConnected && !player.IsHLTV)))
      {
        player.ActionTrackingServices.MatchStats.Deaths = 0;
        if (this.IsPlayerZombie[player.Slot] && this.GetDayByIndex(this.gCurrentDay).ZombieOverride != "" && source != null && ((IEnumerable<string>) source).Count<string>() > 0)
        {
          int index = new Random().Next(0, ((IEnumerable<string>) source).Count<string>());
          this.Zombify(player, this.GetZombieClassIndexByName(source[index]));
        }
      }
    }

    private int GetAliveZombieCount()
    {
      return this.GetDayByIndex(this.gCurrentDay).ZKillCount - this.gZombiesKilled;
    }

    private int GetAliveHumanCount()
    {
      int aliveHumanCount = 0;
      foreach (CCSPlayerController playerController in Utilities.GetPlayers().Where<CCSPlayerController>((Func<CCSPlayerController, bool>) (player => (CEntityInstance) player != (CEntityInstance) null && player.IsValid && player.Connected == PlayerConnectedState.PlayerConnected && !player.IsHLTV && player.TeamNum > (byte) 1 && player.Pawn.Value.LifeState == (byte) 0)))
      {
        if (!this.IsPlayerZombie[playerController.Slot])
          ++aliveHumanCount;
      }
      return aliveHumanCount;
    }

    private void RemoveObjectives()
    {
      foreach (CEntityInstance centityInstance in Utilities.GetAllEntities().Where<CEntityInstance>((Func<CEntityInstance, bool>) (entity => entity != (CEntityInstance) null && entity.IsValid)))
      {
        if (centityInstance.DesignerName == "func_bomb_target" || centityInstance.DesignerName == "func_hostage_rescue" || centityInstance.DesignerName == "c4" || centityInstance.DesignerName == "hostage_entity")
          centityInstance.Remove();
      }
    }

    private void FreezeZombies()
    {
      foreach (CCSPlayerController playerController in Utilities.GetPlayers().Where<CCSPlayerController>((Func<CCSPlayerController, bool>) (player => (CEntityInstance) player != (CEntityInstance) null && player.IsValid && player.Connected == PlayerConnectedState.PlayerConnected && !player.IsHLTV && player.TeamNum > (byte) 1 && player.Pawn.Value.LifeState == (byte) 0)))
      {
        if (this.IsPlayerZombie[playerController.Slot])
        {
          playerController.PlayerPawn.Value.MoveType = MoveType_t.MOVETYPE_NONE;
          Schema.SetSchemaValue<int>(playerController.PlayerPawn.Value.Handle, "CBaseEntity", "m_nActualMoveType", 0);
          Utilities.SetStateChanged((CBaseEntity) playerController.PlayerPawn.Value, "CBaseEntity", "m_MoveType");
          playerController.PlayerPawn.Value.TakesDamage = false;
        }
      }
    }

    private void UnFreezeZombies()
    {
      foreach (CCSPlayerController playerController in Utilities.GetPlayers().Where<CCSPlayerController>((Func<CCSPlayerController, bool>) (player => (CEntityInstance) player != (CEntityInstance) null && player.IsValid && player.Connected == PlayerConnectedState.PlayerConnected && !player.IsHLTV && !player.IsBot && player.TeamNum == (byte) 3)))
      {
        DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(1, 2);
        interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Prefix"]);
        interpolatedStringHandler.AppendLiteral(" ");
        interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.ZUnFreezed"]);
        string stringAndClear = interpolatedStringHandler.ToStringAndClear();
        playerController.PrintToChat(stringAndClear);
      }
      foreach (CCSPlayerController playerController in Utilities.GetPlayers().Where<CCSPlayerController>((Func<CCSPlayerController, bool>) (player => (CEntityInstance) player != (CEntityInstance) null && player.IsValid && player.Connected == PlayerConnectedState.PlayerConnected && !player.IsHLTV && player.TeamNum > (byte) 1 && player.Pawn.Value.LifeState == (byte) 0)))
      {
        if (this.IsPlayerZombie[playerController.Slot])
        {
          playerController.PlayerPawn.Value.MoveType = MoveType_t.MOVETYPE_WALK;
          Schema.SetSchemaValue<int>(playerController.PlayerPawn.Value.Handle, "CBaseEntity", "m_nActualMoveType", 2);
          Utilities.SetStateChanged((CBaseEntity) playerController.PlayerPawn.Value, "CBaseEntity", "m_MoveType");
          playerController.PlayerPawn.Value.TakesDamage = true;
        }
      }
    }

    private void ZombiesWin()
    {
      if (this.Config.ZRriot_ZombieWinSoundPath != "")
      {
        foreach (CCSPlayerController player in Utilities.GetPlayers().Where<CCSPlayerController>((Func<CCSPlayerController, bool>) (player => (CEntityInstance) player != (CEntityInstance) null && player.IsValid && player.Connected == PlayerConnectedState.PlayerConnected && !player.IsHLTV && !player.IsBot && player.TeamNum > (byte) 0)))
          this.PlaySoundOnPlayer(player, this.Config.ZRriot_ZombieWinSoundPath);
      }
      if (this.gCurrentDay > 0 && this.Config.ZRiot_Regression)
        --this.gCurrentDay;
      this.FreezeZombies();
    }

    private void HumansWin()
    {
      if (this.Config.ZRriot_HumanWinSoundPath != "")
      {
        foreach (CCSPlayerController player in Utilities.GetPlayers().Where<CCSPlayerController>((Func<CCSPlayerController, bool>) (player => (CEntityInstance) player != (CEntityInstance) null && player.IsValid && player.Connected == PlayerConnectedState.PlayerConnected && !player.IsHLTV && !player.IsBot && player.TeamNum > (byte) 0)))
          this.PlaySoundOnPlayer(player, this.Config.ZRriot_HumanWinSoundPath);
      }
      if (this.gZombiesKilled >= this.GetDayByIndex(this.gCurrentDay).ZKillCount)
      {
        this.gZombiesKilled = 0;
        ++this.gCurrentDay;
      }
      if (this.gCurrentDay >= this.Config.ZRiot_Days.Count)
      {
        this.gCurrentDay = 0;
        DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(1, 2);
        interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Prefix"]);
        interpolatedStringHandler.AppendLiteral(" ");
        interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.HumanVictory"]);
        Server.PrintToChatAll(interpolatedStringHandler.ToStringAndClear());
        Server.ExecuteCommand("mp_timelimit 0.05");
      }
      this.FreezeZombies();
    }

    private void Zombify(CCSPlayerController? player, int zombieid)
    {
      if ((CEntityInstance) player == (CEntityInstance) null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected || player.IsHLTV || player.TeamNum < (byte) 2 || player.Pawn.Value.LifeState > (byte) 0)
        return;
      this.gZombieID[player.Slot] = zombieid;
      this.AddTimer(0.3f, (Action) (() =>
      {
        if (player.PlayerPawn.Value.WeaponServices.MyWeapons.Count != 0)
          player.RemoveWeapons();
        player.GiveNamedItem("weapon_knife");
        foreach (CHandle<CBasePlayerWeapon> chandle in player.PlayerPawn.Value.WeaponServices.MyWeapons.Where<CHandle<CBasePlayerWeapon>>((Func<CHandle<CBasePlayerWeapon>, bool>) (weapon => weapon != null && weapon.IsValid && weapon.Value.IsValid)))
        {
          if (chandle.Value.DesignerName.Contains("weapon_knife"))
          {
            chandle.Value.RenderMode = RenderMode_t.kRenderTransAlpha;
            chandle.Value.Render = Color.FromArgb(0, (int) byte.MaxValue, (int) byte.MaxValue, (int) byte.MaxValue);
          }
        }
        player.PlayerPawn.Value.Health = this.GetZombieByIndex(zombieid).ZombieHealth;
        player.PlayerPawn.Value.VelocityModifier = this.GetZombieByIndex(zombieid).ZombieSpeed;
        player.PlayerPawn.Value.GravityScale *= this.GetZombieByIndex(zombieid).ZombieGravity;
        player.DesiredFOV = Convert.ToUInt32(this.GetZombieByIndex(zombieid).ZombieFOV);
        Utilities.SetStateChanged((CBaseEntity) player, "CBasePlayerController", "m_iDesiredFOV");
        if (this.GetZombieByIndex(zombieid).ZombieModelPath != "")
        {
          Server.PrecacheModel(this.GetZombieByIndex(zombieid).ZombieModelPath);
          player.PlayerPawn.Value.SetModel(this.GetZombieByIndex(zombieid).ZombieModelPath);
        }
        if (player.IsBot)
          return;
        CCSPlayerController playerController1 = player;
        DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(55, 5);
        interpolatedStringHandler.AppendLiteral(" ");
        interpolatedStringHandler.AppendFormatted<char>(ChatColors.Green);
        interpolatedStringHandler.AppendLiteral("★ ");
        interpolatedStringHandler.AppendFormatted<char>(ChatColors.DarkRed);
        interpolatedStringHandler.AppendLiteral("-------------------------");
        interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Prefix"]);
        interpolatedStringHandler.AppendFormatted<char>(ChatColors.DarkRed);
        interpolatedStringHandler.AppendLiteral("------------------------- ");
        interpolatedStringHandler.AppendFormatted<char>(ChatColors.Green);
        interpolatedStringHandler.AppendLiteral("★");
        string stringAndClear1 = interpolatedStringHandler.ToStringAndClear();
        playerController1.PrintToChat(stringAndClear1);
        CCSPlayerController playerController2 = player;
        interpolatedStringHandler = new DefaultInterpolatedStringHandler(0, 1);
        interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Zombie", new object[1]
        {
          (object) this.GetZombieByIndex(this.gZombieID[player.Slot]).ZombieClassName
        }]);
        string stringAndClear2 = interpolatedStringHandler.ToStringAndClear();
        playerController2.PrintToChat(stringAndClear2);
        CCSPlayerController playerController3 = player;
        interpolatedStringHandler = new DefaultInterpolatedStringHandler(0, 1);
        interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.ZombieHealth", new object[1]
        {
          (object) this.GetZombieByIndex(this.gZombieID[player.Slot]).ZombieHealth
        }]);
        string stringAndClear3 = interpolatedStringHandler.ToStringAndClear();
        playerController3.PrintToChat(stringAndClear3);
        CCSPlayerController playerController4 = player;
        interpolatedStringHandler = new DefaultInterpolatedStringHandler(0, 1);
        interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.ZombieSpeed", new object[1]
        {
          (object) this.GetZombieByIndex(this.gZombieID[player.Slot]).ZombieSpeed
        }]);
        string stringAndClear4 = interpolatedStringHandler.ToStringAndClear();
        playerController4.PrintToChat(stringAndClear4);
        CCSPlayerController playerController5 = player;
        interpolatedStringHandler = new DefaultInterpolatedStringHandler(0, 1);
        interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.ZombieGravity", new object[1]
        {
          (object) this.GetZombieByIndex(this.gZombieID[player.Slot]).ZombieGravity
        }]);
        string stringAndClear5 = interpolatedStringHandler.ToStringAndClear();
        playerController5.PrintToChat(stringAndClear5);
        CCSPlayerController playerController6 = player;
        interpolatedStringHandler = new DefaultInterpolatedStringHandler(0, 1);
        interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.ZombieJump", new object[1]
        {
          (object) this.GetZombieByIndex(this.gZombieID[player.Slot]).ZombieJump
        }]);
        string stringAndClear6 = interpolatedStringHandler.ToStringAndClear();
        playerController6.PrintToChat(stringAndClear6);
        CCSPlayerController playerController7 = player;
        interpolatedStringHandler = new DefaultInterpolatedStringHandler(0, 1);
        interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.ZombieFOV", new object[1]
        {
          (object) this.GetZombieByIndex(this.gZombieID[player.Slot]).ZombieFOV
        }]);
        string stringAndClear7 = interpolatedStringHandler.ToStringAndClear();
        playerController7.PrintToChat(stringAndClear7);
        CCSPlayerController playerController8 = player;
        interpolatedStringHandler = new DefaultInterpolatedStringHandler(77, 3);
        interpolatedStringHandler.AppendLiteral(" ");
        interpolatedStringHandler.AppendFormatted<char>(ChatColors.Green);
        interpolatedStringHandler.AppendLiteral("★ ");
        interpolatedStringHandler.AppendFormatted<char>(ChatColors.DarkRed);
        interpolatedStringHandler.AppendLiteral("------------------------------------------------------------------------ ");
        interpolatedStringHandler.AppendFormatted<char>(ChatColors.Green);
        interpolatedStringHandler.AppendLiteral("★");
        string stringAndClear8 = interpolatedStringHandler.ToStringAndClear();
        playerController8.PrintToChat(stringAndClear8);
      }));
    }

    private void ResetZombies()
    {
      foreach (CCSPlayerController playerController in Utilities.GetPlayers().Where<CCSPlayerController>((Func<CCSPlayerController, bool>) (player => (CEntityInstance) player != (CEntityInstance) null && player.IsValid && player.Connected == PlayerConnectedState.PlayerConnected && !player.IsHLTV)))
      {
        this.IsBotSpawnedByCmd[playerController.Slot] = false;
        this.IsPlayerZombie[playerController.Slot] = playerController.IsBot;
      }
    }

    private void ZRiotEnd()
    {
      this.TerminateRound(3f, RoundEndReason.GameCommencing);
      Server.ExecuteCommand("bot_all_weapons");
      Server.ExecuteCommand("bot_kick");
      foreach (CCSPlayerController playerController in Utilities.GetPlayers().Where<CCSPlayerController>((Func<CCSPlayerController, bool>) (player => (CEntityInstance) player == (CEntityInstance) null || !player.IsValid || player.Connected > PlayerConnectedState.PlayerConnected)))
      {
        if (this.tRespawn[playerController.Slot] != null)
          this.tRespawn[playerController.Slot]?.Kill();
      }
    }

    private bool IsInt(string sVal)
    {
      foreach (int num in sVal)
      {
        if (num > 57 || num < 48)
          return false;
      }
      return true;
    }

    private TargetResult? GetTarget(CommandInfo command)
    {
      TargetResult argTargetResult = command.GetArgTargetResult(1);
      if (!argTargetResult.Any<CCSPlayerController>())
      {
        CommandInfo commandInfo = command;
        DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(1, 2);
        interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Prefix"]);
        interpolatedStringHandler.AppendLiteral(" ");
        interpolatedStringHandler.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Cmd.NoTarget", new object[1]
        {
          (object) command.GetArg(1)
        }]);
        string stringAndClear = interpolatedStringHandler.ToStringAndClear();
        commandInfo.ReplyToCommand(stringAndClear);
        return (TargetResult) null;
      }
      if (argTargetResult.Count<CCSPlayerController>() > 1 || command.GetArg(1).StartsWith('@') || argTargetResult.Count<CCSPlayerController>() == 1 || !command.GetArg(1).StartsWith('@'))
        return argTargetResult;
      CommandInfo commandInfo1 = command;
      DefaultInterpolatedStringHandler interpolatedStringHandler1 = new DefaultInterpolatedStringHandler(1, 2);
      interpolatedStringHandler1.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Prefix"]);
      interpolatedStringHandler1.AppendLiteral(" ");
      interpolatedStringHandler1.AppendFormatted<LocalizedString>(this.Localizer["ZR.Chat.Cmd.MultiTarget", new object[1]
      {
        (object) command.GetArg(1)
      }]);
      string stringAndClear1 = interpolatedStringHandler1.ToStringAndClear();
      commandInfo1.ReplyToCommand(stringAndClear1);
      return (TargetResult) null;
    }

    public static bool CanTarget(CCSPlayerController controller, CCSPlayerController target)
    {
      return target.IsBot || AdminManager.CanPlayerTarget(controller, target);
    }

    private void PlaySoundOnPlayer(CCSPlayerController? player, string sound)
    {
      if ((CEntityInstance) player == (CEntityInstance) null || !player.IsValid)
        return;
      player.ExecuteClientCommand("play " + sound);
    }

    public void RespawnClient(CCSPlayerController client)
    {
      if (!client.IsValid || client.PawnIsAlive)
        return;
      CCSPlayerPawn ccsPlayerPawn = client.PlayerPawn.Value;
      SLAYER_ZombieRiot.SLAYER_ZombieRiot.CBasePlayerController_SetPawnFunc.Invoke(client, ccsPlayerPawn, true, false);
      VirtualFunction.CreateVoid<CCSPlayerController>(client.Handle, GameData.GetOffset("CCSPlayerController_Respawn"))(client);
    }

    public void TerminateRound(float delay, RoundEndReason roundEndReason)
    {
      CCSGameRules gameRules = SLAYER_ZombieRiot.SLAYER_ZombieRiot.GetGameRules();
      if (SLAYER_ZombieRiot.SLAYER_ZombieRiot.IsWindowsPlatform)
        this.TerminateRoundWindows(gameRules.Handle, delay, roundEndReason, IntPtr.Zero, 0U);
      else
        this.TerminateRoundLinux(gameRules.Handle, roundEndReason, IntPtr.Zero, 0U, delay);
    }

    [Obsolete("Constructors of types with required members are not supported in this version of your compiler.", true)]
    [CompilerFeatureRequired("RequiredMembers")]
    public SLAYER_ZombieRiot()
    {
    }
  }
}
