using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.DamageHandlers;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using MEC;
using PlayerRoles;
using PlayerStatsSystem;
using UnityEngine;
using Utils.NonAllocLINQ;
using Exception = System.Exception;
using FirearmDamageHandler = PlayerStatsSystem.FirearmDamageHandler;
using Random = UnityEngine.Random;

namespace TestFPS;

public class EventHandler
{
    public readonly Dictionary<Player, HashSet<Player>> pinPointedPlayers = new();
    private readonly Dictionary<Player, HashSet<Vector3>> pinPointedPositions = new();
    private readonly Dictionary<Player, HashSet<Pickup>> pinPointedItems = new();

    private readonly HashSet<Player> pingCooldown = [];

    // private Room room;

    private readonly Dictionary<ZoneType, RoomType[]> _takeoverPointSpawns = new()
    {
        {
            ZoneType.LightContainment,
            [
                RoomType.LczCrossing, RoomType.LczCurve, RoomType.LczStraight, RoomType.LczTCross, RoomType.LczPlants,
                RoomType.LczToilets
            ]
        },
        {
            ZoneType.HeavyContainment,
            [
                RoomType.HczCrossing, RoomType.HczArmory, RoomType.HczCurve, RoomType.HczHid, RoomType.HczStraight,
                RoomType.HczTCross
            ]
        }
    };

    private readonly Dictionary<ZoneType, RoomType[]> _playerSpawnRooms = new()
    {
        {
            ZoneType.LightContainment,
            [
                RoomType.LczAirlock, RoomType.LczCafe, RoomType.LczCrossing, RoomType.LczCurve, RoomType.LczPlants,
                RoomType.LczStraight, RoomType.LczToilets, RoomType.LczTCross
            ]
        },
        {
            ZoneType.HeavyContainment,
            [RoomType.HczCrossing, RoomType.HczCurve, RoomType.HczHid, RoomType.HczStraight, RoomType.HczTCross]
        },
        {
            ZoneType.Entrance,
            [
                RoomType.EzConference, RoomType.EzCafeteria, RoomType.EzCurve, RoomType.EzStraight, RoomType.EzTCross,
                RoomType.EzCrossing
            ]
        }
    };

    public HashSet<Player> _teamA = [];
    public HashSet<Player> _teamB = [];

    private HashSet<Player> _teamAPointedPlayers = [];
    private HashSet<Player> _teamBPointedPlayers = [];

    private Dictionary<Player, int> _playerPointsDict = [];
    private List<KeyValuePair<Player, int>> _playerPointsList = [];

    private Dictionary<Player, HashSet<Player>> _assistList = new();

    private List<CoroutineHandle> _coroutines = new();


    private int _timer;

    private float _pointScore;

    private bool _showCompleted;

    private int _isOccupyed;
    private int _isSteal;

    private int _teamAPoint;
    private int _teamBPoint;

    private bool _roundEnded;

    private byte _1853EffectCount = 0;

    private string _show1853EffectMessage;

    private Dictionary<Player, Player> _revengeList = new();

    private Color MixColors(Color color1, Color color2, float ratio)
    {
        if (ratio is < 0 or > 1)
        {
            throw new System.ArgumentException("Ratio must be between 0 and 1 inclusive.");
        }

        var mixedRed = Mathf.Lerp(color1.r, color2.r, ratio);
        var mixedGreen = Mathf.Lerp(color1.g, color2.g, ratio);
        var mixedBlue = Mathf.Lerp(color1.b, color2.b, ratio);
        var mixedAlpha = Mathf.Lerp(color1.a, color2.a, ratio);

        return new Color(mixedRed, mixedGreen, mixedBlue, mixedAlpha);
    }

    private string MakeGradientText(string text, Color colorA, Color colorB)
    {
        var gradientText = "";
        for (var i = 0; i < text.Length; i++)
        {
            var ratio = i / (float)text.Length;
            var color = MixColors(colorA, colorB, ratio);
            gradientText += $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text[i]}</color>";
        }

        return gradientText;
    }

    public void OnMapGenerated()
    {
        // room = PickRandomRoom(ZoneType.LightContainment);

        // room.Color = new Color32(132, 191, 133, 25);
    }

    private Room PickRandomRoom(ZoneType type)
    {
        var rooms = Room.List.Where(x => x.Zone == type && _takeoverPointSpawns[type].ToList().Contains(x.Type))
            .ToList();

        var _room = rooms[Random.Range(0, rooms.Count)];

        Log.Debug($"Picked {_room.Type} in {_room.Zone}.");

        return _room;
    }

    private string SecondsToTime(int seconds)
    {
        var minutes = seconds / 60;
        var remainingSeconds = seconds % 60;

        return $"{minutes:D2} : {remainingSeconds:D2}";
    }

    private IEnumerator<float> StartGame()
    {
        yield return Timing.WaitForSeconds(0.1f);
        Round.IsLocked = true;

        _timer = 360;

        foreach (var player in Player.List)
        {
            player.Role.Set(RoleTypeId.Spectator, SpawnReason.None, RoleSpawnFlags.All);
            player.Broadcast(10, "<size=35><b>부착물을 설정해 주세요.</b></size>");
        }

        yield return Timing.WaitForSeconds(10f);

        Pickup.List.ToList().ForEach(x => x.Destroy());

        var players = Player.List.ToList();
        _teamA = players.Take(players.Count / 2).ToHashSet();
        _teamB = players.Skip(players.Count / 2).ToHashSet();

        foreach (var teamAPlayer in _teamA)
        {
            teamAPlayer.Broadcast(6, $"<size=35><b>당신은 {MakeGradientText("Class-D", new Color32(239,121,4, 255), new Color32(85,38,0,255))} 팀 입니다.</b></size>",Broadcast.BroadcastFlags.Normal, true);
        }

        foreach (var teamBPlayer in _teamB)
        {
            teamBPlayer.Broadcast(6, $"<size=35><b>당신은 {MakeGradientText("Nine-Tailed-Fox", new Color32(7,143,243,255), new Color32(0,46,85,255))} 팀 입니다.</b></size>", Broadcast.BroadcastFlags.Normal, true);
        }

        yield return Timing.WaitForSeconds(5f);

        Map.Broadcast(3, "<size=35><b>데스매치에 준비하세요...</b></size>", Broadcast.BroadcastFlags.Normal, true);

        yield return Timing.WaitForSeconds(3f);

        var spawnableRooms = Room.List.Where(x =>
            _playerSpawnRooms[ZoneType.Entrance].Contains(x.Type)).ToList();

        Log.Debug(spawnableRooms.Count);

        foreach (var aPlayer in _teamA)
        {
            aPlayer.Role.Set(RoleTypeId.ClassD, SpawnReason.Respawn, RoleSpawnFlags.None);
            spawnableRooms.ShuffleList();
            var selRoom = spawnableRooms.Count > 0 ? spawnableRooms.First() : null;

            if (selRoom is null)
            {
                spawnableRooms = Room.List.Where(x =>
                    _playerSpawnRooms[ZoneType.Entrance].Contains(x.Type)).ToList();
                spawnableRooms.ShuffleList();
                selRoom = spawnableRooms.First();
            }

            aPlayer.Position = selRoom.Position + new Vector3(0, 1, 0);
            spawnableRooms.Remove(selRoom);
            aPlayer.Broadcast(3, "<size=35><b>시작!</b></size>", Broadcast.BroadcastFlags.Normal, true);
        }

        foreach (var bPlayer in _teamB)
        {
            bPlayer.Role.Set(RoleTypeId.NtfSergeant, SpawnReason.Respawn, RoleSpawnFlags.None);
            spawnableRooms.ShuffleList();
            var selRoom = spawnableRooms.Count > 0 ? spawnableRooms.First() : null;

            if (selRoom is null)
            {
                spawnableRooms = Room.List.Where(x =>
                    _playerSpawnRooms[ZoneType.Entrance].Contains(x.Type)).ToList();
                spawnableRooms.ShuffleList();
                selRoom = spawnableRooms.First();
            }

            bPlayer.Position = selRoom.Position + new Vector3(0, 1, 0);
            spawnableRooms.Remove(selRoom);
            bPlayer.Broadcast(3, "<size=35><b>시작!</b></size>", Broadcast.BroadcastFlags.Normal, true);
        }

        foreach (var player in Player.List)
        {
            var randomWeaponType = Random.Range(0, 3);


            player.AddItem(GetRandomGun(WeaponType.Pistol));
            switch (randomWeaponType)
            {
                case 0:
                    player.AddItem(GetRandomGun(WeaponType.Rifle));
                    break;
                case 1:
                    player.AddItem(GetRandomGun(WeaponType.SMG));
                    break;
                case 2:
                    player.AddItem(GetRandomGun(WeaponType.Shotgun));
                    break;
                default:
                    player.AddItem(GetRandomGun(WeaponType.Rifle));
                    break;
            }
            player.AddItem(ItemType.KeycardO5);
            player.AddItem(ItemType.Medkit);
            player.AddItem(ItemType.ArmorCombat);
            player.AddItem(ItemType.Radio);
            player.AddAmmo(AmmoType.Ammo44Cal, 120);
            player.AddAmmo(AmmoType.Nato556, 120);
            player.AddAmmo(AmmoType.Nato762, 120);
            player.AddAmmo(AmmoType.Nato9, 120);
            player.SetAmmo(AmmoType.Ammo12Gauge, 54);
        }

        _coroutines.Add(Timing.RunCoroutine(BroadcastGameStat()));
        _coroutines.Add(Timing.RunCoroutine(Timer(_timer)));
        Player.List.ToList().ForEach(x => x.IsGodModeEnabled = true);
        yield return Timing.WaitForSeconds(2f);
        Player.List.ToList().ForEach(x => x.IsGodModeEnabled = false);
    }



    private void SendHint(string text, int duration, params Player[] players)
    {
        players.ToList().ForEach(player =>
        {
            _coroutines.Add(Timing.RunCoroutine(MultiHint.AddPlayerHint(player.UserId, duration, text)));
        });
    }

    private string GetPickupName(ItemType type)
    {
        return type switch
        {
            ItemType.KeycardJanitor => "<color=#bcb1e4>잡역부 키카드</color>",
            ItemType.KeycardScientist => "<color=#e7d678>과학자 키카드</color>",
            ItemType.KeycardResearchCoordinator => "<color=#ddab20>연구 감독관 키카드</color>",
            ItemType.KeycardZoneManager => "<color=#217778>구역 관리자 키카드</color>",
            ItemType.KeycardGuard => "<color=#606770>경비 키카드</color>",
            ItemType.KeycardMTFPrivate => "<color=#a2cade>구미호 이등병 키카드</color>",
            ItemType.KeycardContainmentEngineer => "<color=#b6887f>격리 정비사 키카드</color>",
            ItemType.KeycardMTFOperative => "<color=#5180f7>구미호 대원 키카드</color>",
            ItemType.KeycardMTFCaptain => "<color=#1841c8>구미호 대위 키카드</color>",
            ItemType.KeycardFacilityManager => "<color=#ba1846>시설 관리자 키카드</color>",
            ItemType.KeycardChaosInsurgency => "<color=#35493e>혼돈의 반란 키카드</color>",
            ItemType.KeycardO5 => "<color=#ffffff>O5 등급 키카드</color>",
            ItemType.Radio => "<color=#606770>무전기</color>",
            ItemType.GunCOM15 => "<color=#e7d678>COM-15</color>",
            ItemType.Medkit => "<color=#eb4034>구급 상자</color>",
            ItemType.Flashlight => "<color=#606770>손전등</color>",
            ItemType.MicroHID => "<color=#1841c8>Micro H.I.D.</color>",
            ItemType.SCP500 => "SCP-500",
            ItemType.SCP207 => "SCP-207",
            ItemType.Ammo12gauge => "<color=#606770>탄약 12게이지</color>",
            ItemType.GunE11SR => "<color=#5180f7>MTF-E11-SR</color>",
            ItemType.GunCrossvec => "<color=#a2cade>Crossvec</color>",
            ItemType.Ammo556x45 => "<color=#606770>탄약 5.56x45mm</color>",
            ItemType.GunFSP9 => "<color=#606770>FSP-9</color>",
            ItemType.GunLogicer => "<color=#35493e>Logicer</color>",
            ItemType.GrenadeHE => "<color=#32556E>세열 수류탄</color>",
            ItemType.GrenadeFlash => "<color=#606770>섬광탄</color>",
            ItemType.Ammo44cal => "<color=#606770>탄약 .44 매그넘</color>",
            ItemType.Ammo762x39 => "<color=#606770>탄약 7.62x39mm</color>",
            ItemType.Ammo9x19 => "<color=#606770>탄약 9x19mm</color>",
            ItemType.GunCOM18 => "<color=#606770>COM-18</color>",
            ItemType.SCP018 => "SCP-018",
            ItemType.SCP268 => "SCP-268",
            ItemType.Adrenaline => "<color=#04b018>아드레날린</color>",
            ItemType.Painkillers => "<color=#04b018>진통제</color>",
            ItemType.Coin => "동전",
            ItemType.ArmorLight => "<color=#606770>경량 방탄복</color>",
            ItemType.ArmorCombat => "<color=#5180f7>전투 방탄복</color>",
            ItemType.ArmorHeavy => "<color=#1841c8>고강도 방탄복</color>",
            ItemType.GunRevolver => "<color=#35493e>리볼버</color>",
            ItemType.GunAK => "<color=#35493e>AK</color>",
            ItemType.GunShotgun => "<color=#35493e>산탄총</color>",
            ItemType.SCP330 => "SCP-330",
            ItemType.SCP2176 => "SCP-2176",
            ItemType.SCP244a => "SCP-244-A",
            ItemType.SCP244b => "SCP-244-B",
            ItemType.SCP1853 => "SCP-1853",
            ItemType.ParticleDisruptor => "<color=#1841c8>3-X 입자 분열기</color>",
            ItemType.GunCom45 => "<color=#ff0000>COM-45</color>",
            ItemType.SCP1576 => "SCP-1576",
            ItemType.Jailbird => "<color=#037ffc>Jailbird</color>",
            ItemType.AntiSCP207 => "SCP-207?",
            ItemType.GunFRMG0 => "<color=#003DCA>FR-MG-0</color>",
            ItemType.GunA7 => "<color=#ff0000>A7</color>",
            ItemType.Lantern => "<color=#967969>랜턴</color>",
            ItemType.None => $"{MakeGradientText("아무것도 없는", new Color32(255, 0, 0, 255), new Color32(102, 0, 0, 255))}",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }


    public void OnRoundStarted()
    {
        _coroutines.Add(Timing.RunCoroutine(StartGame()));
    }

    private IEnumerator<float> Timer(int time)
    {
        _timer = time;

        while (!Round.IsEnded)
        {
            _timer--;

            switch (_timer)
            {
                case 180:
                    Player.List.ToList().ForEach(x => x.SyncEffect(new Effect(EffectType.Scp1853, 0, 1, false, true)));
                    _1853EffectCount = 1;
                    _show1853EffectMessage = "<b><size=30>💊 SCP - 1853 (x1)</size></b>";
                    break;
                case 175:
                    _show1853EffectMessage = string.Empty;
                    break;
                case 120:
                    Player.List.ToList().ForEach(x => x.SyncEffect(new Effect(EffectType.Scp1853, 0, 2, false, true)));
                    _1853EffectCount = 2;
                    _show1853EffectMessage = "<b><size=30>💊 SCP - 1853 (x2)</size></b>";
                    break;
                case 115:
                    _show1853EffectMessage = string.Empty;
                    break;
                case 60:
                    Player.List.ToList().ForEach(x => x.SyncEffect(new Effect(EffectType.Scp1853, 0, 3, false, true)));
                    _1853EffectCount = 3;
                    _show1853EffectMessage = "<b><size=30>💊 SCP - 1853 (x3)</size></b>";
                    break;
                case 55:
                    _show1853EffectMessage = string.Empty;
                    break;
            }

            if (_timer <= 0)
            {
                _roundEnded = true;
                break;
            }
            yield return Timing.WaitForSeconds(1f);
        }

        Player.List.ToList().ForEach(p =>
        {
            p.ClearInventory();
            p.Role.Set(RoleTypeId.Spectator, SpawnReason.ForceClass, RoleSpawnFlags.All);
        });

        yield return Timing.WaitForSeconds(1f);

        Map.Broadcast(10, "<size=35><b>게임이 종료되었습니다!\n과연 승자는 누구일까요...</b></size>", Broadcast.BroadcastFlags.Normal, true);

        yield return Timing.WaitForSeconds(3f);

        if (_teamAPoint > _teamBPoint)
        {
            Map.Broadcast(10, "<size=35><b>Class-D 팀이 승리했습니다!\n<size=15>와아아아ㅏㅏㅏ</size></b></size>", Broadcast.BroadcastFlags.Normal, true);
        }
        else if (_teamAPoint < _teamBPoint)
        {
            Map.Broadcast(10, "<size=35><b>Nine-Tailed-Fox 팀이 승리했습니다!\n<size=15>와아아아ㅏㅏㅏ</size></b></size>", Broadcast.BroadcastFlags.Normal, true);
        }
        else
        {
            Map.Broadcast(10, "<size=35><b>안타깝지만... 무승부입니다!</b></size>", Broadcast.BroadcastFlags.Normal, true);
        }

        yield return Timing.WaitForSeconds(1f);

        var hintText = "<align=left><b>";

        var hintCount = 0;

        foreach (var p in _playerPointsList)
        {
            if (hintCount >= 10) break;
            var index = _playerPointsList.IndexOf(p) + 1;
            var color = index switch
            {
                1 => "#FFD700",
                2 => "#C0C0C0",
                3 => "#CD7F32",
                _ => "#FFFFFF"
            };

            hintText += $"<color={color}><size=30>#{index} : {p.Key.Nickname} - {p.Value}pt</size></color>\n";
            hintCount++;
        }

        hintText += "</b></align>\n\n\n\n\n";

        Player.List.ToList().ForEach(x => x.ShowHint(hintText, 120));
    }

    private IEnumerator<float> BroadcastGameStat()
    {
        while (!_roundEnded)
        {
            Player.List.ToList().ForEach(player =>
            {
                var degree = GetDegree(player.CameraTransform.forward.normalized, Vector3.zero);
                if (degree < 0)
                    degree += 360;

                var text = player.IsAlive ? FormatCompass(degree, player) + "<line-height=115%>\n</line-height>" : "";

                text += FormatGameStats(player);

                player.Broadcast(1, text, Broadcast.BroadcastFlags.Normal, true);
            });

            // CheckRoom();

            yield return Timing.WaitForSeconds(0.01f);
        }
    }

    // private void CheckRoom()
    // {
    //     try
    //     {
    //         foreach (var player in Player.List)
    //         {
    //             if (!player.IsAlive)
    //             {
    //                 _teamAPointedPlayers.Remove(player);
    //                 _teamBPointedPlayers.Remove(player);
    //                 continue;
    //             }
    //
    //             if (Vector3.Distance(player.Position, room.Position) > 10f)
    //             {
    //                 _teamAPointedPlayers.Remove(player);
    //                 _teamBPointedPlayers.Remove(player);
    //                 continue;
    //             }
    //
    //             if (player.CurrentRoom != room)
    //             {
    //                 _teamAPointedPlayers.Remove(player);
    //                 _teamBPointedPlayers.Remove(player);
    //                 continue;
    //             }
    //
    //             if (_teamA.Contains(player))
    //             {
    //                 _teamAPointedPlayers.Add(player);
    //             }
    //
    //             if (_teamB.Contains(player))
    //             {
    //                 _teamBPointedPlayers.Add(player);
    //             }
    //
    //             if (_teamAPointedPlayers.Count > 0 && _teamBPointedPlayers.Count > 0)
    //             {
    //                 Log.Debug($"{player.Nickname} : Both teams are in the room.");
    //                 continue;
    //             }
    //
    //             if (_teamA.Contains(player) && _pointScore < 100)
    //             {
    //                 Log.Debug($"{player.Nickname} : Team A is in the room.");
    //                 if (_isOccupyed == -1)
    //                 {
    //                     _isSteal = 1;
    //                     _pointScore += 0.1f;
    //                 }
    //                 else
    //                 {
    //                     _pointScore += 0.1f;
    //                     _isOccupyed = 0;
    //                     _isSteal = 0;
    //                 }
    //             }
    //             else if (_teamB.Contains(player) && _pointScore > -100)
    //             {
    //                 Log.Debug($"{player.Nickname} : Team B is in the room.");
    //                 if (_isOccupyed == 1)
    //                 {
    //                     _isSteal = -1;
    //                     _pointScore -= 0.1f;
    //                 }
    //                 else
    //                 {
    //                     _pointScore -= 0.1f;
    //                     _isOccupyed = 0;
    //                     _isSteal = 0;
    //                 }
    //             }
    //         }
    //
    //         switch (_pointScore)
    //         {
    //             case > 100:
    //                 _showCompleted = true;
    //                 _isOccupyed = 1;
    //                 _pointScore = 100;
    //                 Timing.CallDelayed(3f, () =>
    //                 {
    //                     _showCompleted = false;
    //                 });
    //                 break;
    //             case < -100:
    //                 _showCompleted = true;
    //                 _isOccupyed = -1;
    //                 _pointScore = -100;
    //                 Timing.CallDelayed(3f, () =>
    //                 {
    //                     _showCompleted = false;
    //                 });
    //                 break;
    //         }
    //
    //         if (_teamAPointedPlayers.Count == 0 && _pointScore is > 0 and < 100 && _isOccupyed != -1 && _isSteal != -1)
    //         {
    //             _pointScore -= 0.2f;
    //             if (_pointScore < 0)
    //             {
    //                 _pointScore = 0;
    //             }
    //         }
    //
    //         if (_teamBPointedPlayers.Count == 0 && _pointScore is < 0 and > -100 && _isOccupyed != 1 && _isSteal != 1)
    //         {
    //             _pointScore += 0.2f;
    //             if (_pointScore > 0)
    //             {
    //                 _pointScore = 0;
    //             }
    //         }
    //
    //         room.Color = _pointScore switch
    //         {
    //             > 0 => MixColors(new Color32(132, 191, 133, 25), new Color32(239, 121, 4, 25), _pointScore / 100),
    //             < 0 => MixColors(new Color32(132, 191, 133, 25), new Color32(7, 143, 243, 25), -_pointScore / 100),
    //             _ => new Color32(132, 191, 133, 25)
    //         };
    //
    //         if (_pointScore == 0)
    //         {
    //             _isOccupyed = 0;
    //         }
    //     }
    //     catch (Exception e)
    //     {
    //         Log.Error(e);
    //     }
    // }

    private float GetDegree(Vector3 from, Vector3 to)
    {
        var dir = to - from;
        var angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        return angle;
    }

    private bool IsPlaying(Player player)
    {
        if (!player.IsConnected || !player.IsVerified) return false;

        if (!player.Role.IsAlive) return false;

        return true;
    }

    private float RoundToFloat(float value, int digits)
    {
        var mult = Mathf.Pow(10.0f, digits);
        return Mathf.Round(value * mult) / mult;
    }

    private string FormatCompass(float degree, Player player)
    {
        try
        {
            if (!IsPlaying(player)) return string.Empty;

            StringBuilder compassString = new StringBuilder($"<mspace=5.8px>");

            int start = Mathf.RoundToInt(degree - 45);

            switch (start)
            {
                case < 0:
                    start += 360;
                    break;
                case > 360:
                    start -= 360;
                    break;
            }

            var pinAngle = new List<Pin>();

            if (pinPointedPlayers.TryGetValue(player, out var pointedPlayer))
            {
                pointedPlayer.ToList().ForEach(p =>
                {
                    if (!IsPlaying(p)) return;

                    if (p.GetEffect(EffectType.Invisible).IsEnabled) return;

                    var pinDegree = GetDegree(player.Position, p.Position);

                    pinDegree -= 180;

                    if (pinDegree < 0)
                        pinDegree += 360;

                    var roleColor = p.Role.Type switch
                    {
                        RoleTypeId.ClassD => "#FF8000",
                        RoleTypeId.NtfSpecialist => "#0096FF",
                        RoleTypeId.Scientist => "#FFFF7C",
                        RoleTypeId.ChaosConscript => "#008F1C",
                        RoleTypeId.NtfSergeant => "#0096FF",
                        RoleTypeId.NtfCaptain => "#003DCA",
                        RoleTypeId.NtfPrivate => "#70C3FF",
                        RoleTypeId.Tutorial => "#F408A9",
                        RoleTypeId.FacilityGuard => "#535F75",
                        RoleTypeId.ChaosRifleman => "#008F1C",
                        RoleTypeId.ChaosMarauder => "#008F1C",
                        RoleTypeId.ChaosRepressor => "#008F1C",
                        _ => "#FFFFFF"
                    };

                    pinAngle.Add(new Pin
                    {
                        Degree = Mathf.RoundToInt(pinDegree),
                        Color = roleColor,
                        Letter = "👤"
                    });
                });
            }

            if (pinPointedPositions.TryGetValue(player, out var pointedPosition))
            {
                pointedPosition.ToList().ForEach(p =>
                {
                    var pinDegree = GetDegree(player.Position, p);

                    pinDegree -= 180;

                    if (pinDegree < 0)
                        pinDegree += 360;

                    pinAngle.Add(new Pin
                    {
                        Letter = "⚠",
                        Degree = Mathf.RoundToInt(pinDegree),
                        Color = "#FFFFFF"
                    });
                });
            }

            if (pinPointedItems.TryGetValue(player, out var pointedItem))
            {
                pointedItem.ToList().ForEach(p =>
                {
                    if (p == null) return;

                    var pinDegree = GetDegree(player.Position, p.Position);

                    pinDegree -= 180;

                    if (pinDegree < 0)
                        pinDegree += 360;

                    pinAngle.Add(new Pin
                    {
                        Letter = "📦",
                        Degree = Mathf.RoundToInt(pinDegree),
                        Color = "#FFFFFF"
                    });
                });
            }

            // var roomAngle = GetDegree(player.Position, room.Position);
            //
            // roomAngle -= 180;
            //
            // if (roomAngle < 0)
            //     roomAngle += 360;
            //
            // pinAngle.Add(new Pin
            // {
            //     Letter = "<size=35>🏠</size>",
            //     Degree = Mathf.RoundToInt(roomAngle),
            //     Color = "#FFFF00"
            // });

            for (var i = 0; i < 90; i++)
            {
                switch (start)
                {
                    case 0 or 360:
                        compassString.Append("</mspace>N<mspace=5.8px>");
                        break;
                    case 45:
                        compassString.Append("</mspace><size=30>N E</size><mspace=5.8px>");
                        break;
                    case 90:
                        compassString.Append("</mspace>E<mspace=5.8px>");
                        break;
                    case 135:
                        compassString.Append("</mspace><size=30>S E</size><mspace=5.8px>");
                        break;
                    case 180:
                        compassString.Append("</mspace>S<mspace=5.8px>");
                        break;
                    case 225:
                        compassString.Append("</mspace><size=30>S W</size><mspace=5.8px>");
                        break;
                    case 270:
                        compassString.Append("</mspace>W<mspace=5.8px>");
                        break;
                    case 315:
                        compassString.Append("</mspace><size=30>N W</size><mspace=5.8px>");
                        break;
                    default:
                    {
                        var pin = pinAngle.FirstOrDefault(x => x.Degree == start);
                        compassString.Append(!pin.Equals(default(Pin))
                            ? $"</mspace><color={pin.Color}><size=30>{pin.Letter}</size></color><mspace=5.8px>"
                            : "<size=18> | </size>");
                        break;
                    }
                }

                start += 1;

                if (start > 360)
                {
                    start -= 360;
                }
            }

            var direction = degree switch
            {
                < 45 => "N",
                < 135 => "E",
                < 225 => "S",
                < 315 => "W",
                _ => "N"
            };

            compassString.Append($"</mspace>\n<size=35><b>{Mathf.RoundToInt(degree * 10) / 10}° {direction}</b></size>");
            return compassString.ToString();
        }
        catch (Exception e)
        {
            Log.Error(e);
            return string.Empty;
        }
    }

    private string FormatGameStats(Player player)
    {
        var txt = new StringBuilder();

        // var pointText = _pointScore switch
        // {
        //     > 0 =>
        //         $"{MakeGradientText($"{_pointScore:F1}%", new Color32(239, 121, 4, 255), new Color32(85, 38, 0, 255))}",
        //     < 0 =>
        //         $"{MakeGradientText($"{-_pointScore:F1}%", new Color32(7, 143, 243, 255), new Color32(0, 46, 85, 255))}",
        //     _ => "0.0%"
        // };
        //
        // if (_showCompleted)
        // {
        //     pointText = _pointScore switch
        //     {
        //         >= 100 => $"<size=25>{MakeGradientText("점령되었습니다!", new Color32(239, 121, 4, 255), new Color32(85, 38, 0, 255))}</size>",
        //         <= -100 =>
        //             $"<size=25>{MakeGradientText("점령되었습니다!", new Color32(7, 143, 243, 255), new Color32(0, 46, 85, 255))}</size>",
        //         _ => pointText
        //     };
        // }

        var time = SecondsToTime(_timer);

        var timeColor = _timer switch
        {
            < 10 => "#FF0000",
            < 60 => "#ff8400",
            < 120 => "#d6cf40",
            < 180 => "#94de2c",
            _ => "#FFFFFF"
        };

        switch (_teamAPoint - _teamBPoint)
        {
            case < 0:
                txt.Append(
                    @$"<size=35><b><align=left>ㅤㅤTEAM A : {MakeGradientText($"{_teamAPoint:N0}pt", new Color32(239, 121, 4, 255), new Color32(85, 38, 0, 255))}<line-height=0>\n<size=52><align=center><color={timeColor}>{time}</color><line-height=0></size>\n<align=right><size=43pt>TEAM B : {MakeGradientText($"{_teamBPoint:N0}pt", new Color32(7, 143, 243, 255), new Color32(0, 46, 85, 255))}</size>ㅤㅤ<line-height=200%></b></size>");
                break;
            case > 0:
                txt.Append(
                    @$"<size=35><b><align=left>ㅤㅤ<size=43pt>TEAM A : {MakeGradientText($"{_teamAPoint:N0}pt", new Color32(239, 121, 4, 255), new Color32(85, 38, 0, 255))}</size><line-height=0>\n<size=52><align=center><color={timeColor}>{time}</color><line-height=0></size>\n<align=right>TEAM B : {MakeGradientText($"{_teamBPoint:N0}pt", new Color32(7, 143, 243, 255), new Color32(0, 46, 85, 255))}ㅤㅤ<line-height=200%></b></size>");
                break;
            default:
                txt.Append(
                    @$"<size=35><b><align=left>ㅤㅤTEAM A : {MakeGradientText($"{_teamAPoint:N0}pt", new Color32(239, 121, 4, 255), new Color32(85, 38, 0, 255))}<line-height=0>\n<size=52><align=center><color={timeColor}>{time}</color><line-height=0></size>\n<align=right>TEAM B : {MakeGradientText($"{_teamBPoint:N0}pt", new Color32(7, 143, 243, 255), new Color32(0, 46, 85, 255))}ㅤㅤ<line-height=200%></b></size>");
                break;
        }

        _playerPointsDict.TryAdd(player, 0);

        _playerPointsList = _playerPointsDict.OrderByDescending(x => x.Value).ToList();

        var rankIndex = _playerPointsList.FindIndex(x => x.Key == player) + 1;

        var rankColor = rankIndex switch
        {
            1 => "#ffd700",
            2 => "#c0c0c0",
            3 => "#cd7f32",
            _ => "#ffffff"
        };

        // player.Vaporize();

        txt.Append(
            $"\n<size=35pt><align=left><color={rankColor}><b>ㅤㅤ</b>🏆<b> #{rankIndex}</color> - <size=35pt>{_playerPointsDict[player]:N0}pt</size></b></size>");

        // txt.Append($"</align></align></align></align></align><line-height=150%>\n{_show1853EffectMessage}");

        return txt.ToString();
    }

    public void OnServerRestarting()
    {
        pinPointedPlayers.Clear();
        pinPointedItems.Clear();
        pinPointedPositions.Clear();

        _teamA.Clear();
        _teamB.Clear();

        _teamAPoint = 0;
        _teamBPoint = 0;

        _playerPointsDict.Clear();
        _playerPointsList.Clear();

        _roundEnded = false;

        _coroutines.ForEach(x =>
        {
            if (x is { IsRunning: true, IsValid: true })
                Timing.KillCoroutines(x);
        });

        _coroutines.Clear();

        _pointScore = 0;
        _isOccupyed = 0;
        _isSteal = 0;

        _showCompleted = false;

        _1853EffectCount = 0;

        _show1853EffectMessage = string.Empty;

        _timer = 0;

        _teamAPointedPlayers.Clear();
        _teamBPointedPlayers.Clear();

        _assistList.Clear();
    }

    public void OnChangingItem(ChangingItemEventArgs ev)
    {
        if (ev.Item == null) return;
        if (!ev.Item.IsKeycard) return;

        ev.IsAllowed = false;

        _coroutines.Add(Timing.RunCoroutine(Ping(ev.Player)));
    }

    private ItemType GetRandomGun()
    {
        var guns = new[]
        {
            ItemType.GunCOM15,
            ItemType.GunE11SR,
            ItemType.GunCrossvec,
            ItemType.GunFSP9,
            ItemType.GunLogicer,
            ItemType.GunCOM18,
            ItemType.GunRevolver,
            ItemType.GunAK,
            ItemType.GunShotgun,
            ItemType.GunCom45,
            ItemType.GunFRMG0,
            ItemType.GunA7
        };

        return guns[Random.Range(0, guns.Length)];
    }

    private ItemType GetRandomGun(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.Pistol:
                return Random.Range(0, 4) switch { 0 => ItemType.GunCOM15, 1 => ItemType.GunCOM18, 2 => ItemType.GunRevolver, 3 => ItemType.GunCom45, _ => ItemType.GunCOM15 };
            case WeaponType.SMG:
                return Random.Range(0, 2) == 0 ? ItemType.GunCrossvec : ItemType.GunFSP9;
            case WeaponType.Shotgun:
                return ItemType.GunShotgun;
            case WeaponType.Rifle:
                return Random.Range(0, 5) switch
                {
                    0 => ItemType.GunLogicer,
                    1 => ItemType.GunE11SR,
                    2 => ItemType.GunFRMG0,
                    3 => ItemType.GunAK,
                    4 => ItemType.GunA7,
                    _ => ItemType.GunLogicer
                };
            case WeaponType.Unknown:
                return ItemType.None;
            default:
                return ItemType.None;
        }
    }

    private int GetScoreForWeapon(ItemType type)
    {
        return type switch
        {
            ItemType.GunCOM15 => 40,
            ItemType.GunE11SR => 20,
            ItemType.GunCrossvec => 20,
            ItemType.GunFSP9 => 30,
            ItemType.GunLogicer => 15,
            ItemType.GunCOM18 => 35,
            ItemType.GunRevolver => 25,
            ItemType.GunAK => 20,
            ItemType.GunShotgun => 30,
            ItemType.GunCom45 => Random.Range(0, 51),
            ItemType.GunFRMG0 => 10,
            ItemType.GunA7 => 30,
            _ => 0
        };
    }

    private IEnumerator<float> Ping(Player player)
    {
        if (pingCooldown.Contains(player))
        {
            Log.Debug($"{player.Nickname} is on ping cooldown.");
            yield break;
        }

        var pingCount = 0;

        if (pinPointedItems.TryGetValue(player, out var item))
            pingCount += item.Count;

        if (pinPointedPlayers.TryGetValue(player, value: out var pointedPlayer))
            pingCount += pointedPlayer.Count;

        if (pinPointedPositions.TryGetValue(player, out var position))
            pingCount += position.Count;

        if (pingCount >= TestFPS.Instance.Config.PingLimit)
        {
            Log.Debug($"{player.Nickname} reached the ping limit.");
            yield break;
        }

        var layerMask = ~LayerMask.GetMask("Ignore Raycast", "Hitbox");

        if (Physics.Raycast(player.CameraTransform.position + player.CameraTransform.forward * 0.2f, player.CameraTransform.forward, out var hit, 50f))
        {
            var obj = hit.collider.gameObject;
            var target = Player.Get(obj.GetComponentInParent<ReferenceHub>());

            if (target == player || target == null)
            {
                if (Physics.Raycast(player.CameraTransform.position + player.CameraTransform.forward * 0.2f,
                        player.CameraTransform.forward, out var h, 50f, layerMask))
                {
                    var pickup = Pickup.Get(h.transform.root.gameObject);

                    if (pickup == null && Pickup.List.Any(p => Vector3.Distance(p.Position, h.point) < 2))
                    {
                        pickup = Pickup.List.Where(p => Vector3.Distance(p.Position, h.point) < 5).OrderBy(x => Vector3.Distance(x.Position, h.point)).First();
                    }

                    if (pickup != null)
                    {
                        if (_teamA.Contains(player))
                        {
                            foreach (var p in _teamA.Where(p => p.IsAlive))
                            {
                                if (pinPointedItems.TryGetValue(p, out var items))
                                {
                                    if (items.Add(pickup))
                                    {
                                        SendHint($"<size=30><align=right><b>{player.CustomName}</b> - <b>{GetPickupName(pickup.Type)}</b>이(가) 있음.</align>", 5, p);
                                        Log.Debug($"{player.Nickname} pinpointed {pickup}.");
                                    }
                                    else
                                    {
                                        yield break;
                                    }
                                }
                                else
                                {
                                    SendHint($"<size=30><align=right><b>{player.CustomName}</b> - <b>{GetPickupName(pickup.Type)}</b>이(가) 있음.</align>", 5, p);
                                    pinPointedItems.Add(p, [pickup]);
                                    Log.Debug($"{player.Nickname} pinpointed {pickup}.");
                                }
                            }


                            _coroutines.Add(Timing.RunCoroutine(PingCooldown(player)));

                            yield return Timing.WaitForSeconds(TestFPS.Instance.Config.PingDuration);

                            foreach (var p in _teamA)
                            {
                                pinPointedItems[p].Remove(pickup);
                            }
                        } else if (_teamB.Contains(player))
                        {
                            foreach (var p in _teamB.Where(p => p.IsAlive))
                            {
                                if (pinPointedItems.TryGetValue(p, out var items))
                                {
                                    if (items.Add(pickup))
                                    {
                                        SendHint($"<size=30><align=right><b>{player.CustomName}</b> - <b>{GetPickupName(pickup.Type)}</b>이(가) 있음.</align>", 5, p);
                                        Log.Debug($"{player.Nickname} pinpointed {pickup}.");
                                    }
                                    else
                                    {
                                        yield break;
                                    }
                                }
                                else
                                {
                                    SendHint($"<size=30><align=right><b>{player.CustomName}</b> - <b>{GetPickupName(pickup.Type)}</b>이(가) 있음.</align>", 5, p);
                                    pinPointedItems.Add(p, [pickup]);
                                    Log.Debug($"{player.Nickname} pinpointed {pickup}.");
                                }
                            }

                            _coroutines.Add(Timing.RunCoroutine(PingCooldown(player)));

                            yield return Timing.WaitForSeconds(TestFPS.Instance.Config.PingDuration);

                            foreach (var p in _teamB)
                            {
                                pinPointedItems[p].Remove(pickup);
                            }
                        }

                        _coroutines.Add(Timing.RunCoroutine(PingCooldown(player)));

                        yield break;
                    }

                    if (_teamA.Contains(player))
                    {
                        foreach (var aPlayer in _teamA.Where(x => x.IsAlive))
                        {
                            var angle = GetDegree(aPlayer.Position, h.point);

                            angle -= 180;

                            if (angle < 0)
                                angle += 360;

                            angle = Mathf.RoundToInt(angle);

                            SendHint($"<size=30><align=right><b>{player.Nickname}</b> - <color=#FFFFFF>{angle}°</color> 표시.</align>", 5, aPlayer);

                            if (pinPointedPositions.TryGetValue(aPlayer, out var posList))
                            {
                                if (posList.Add(h.point))
                                {
                                    Log.Debug($"{player.Nickname} pinpointed {h.point}.");
                                }
                                else
                                {
                                    yield break;
                                }
                            }
                            else
                            {
                                SendHint($"<size=30><align=right><b>{player.Nickname}</b> - <color=#FFFFFF>{angle}°</color> 표시.</align>", 5, aPlayer);
                                pinPointedPositions.Add(aPlayer, [h.point]);
                                Log.Debug($"{player.Nickname} pinpointed {h.point}.");
                            }
                        }

                        _coroutines.Add(Timing.RunCoroutine(PingCooldown(player)));

                        yield return Timing.WaitForSeconds(TestFPS.Instance.Config.PingDuration);

                        foreach (var p in _teamA)
                        {
                            pinPointedPositions[p].Remove(h.point);
                        }
                    }
                    else if (_teamB.Contains(player))
                    {
                        foreach (var bPlayer in _teamB.Where(x => x.IsAlive))
                        {
                            var angle = GetDegree(bPlayer.Position, h.point);

                            angle -= 180;

                            if (angle < 0)
                                angle += 360;

                            angle = Mathf.RoundToInt(angle);

                            SendHint($"<size=30><align=right><b>{player.Nickname}</b> - <color=#FFFFFF>{angle}°</color> 표시.</align>", 5, bPlayer);

                            if (pinPointedPositions.TryGetValue(bPlayer, out var posList))
                            {
                                if (posList.Add(h.point))
                                {
                                    Log.Debug($"{player.Nickname} pinpointed {h.point}.");
                                }
                                else
                                {
                                    yield break;
                                }
                            }
                            else
                            {
                                SendHint($"<size=30><align=right><b>{player.Nickname}</b> - <color=#FFFFFF>{angle}°</color> 표시.</align>", 5, bPlayer);
                                pinPointedPositions.Add(bPlayer, [h.point]);
                                Log.Debug($"{player.Nickname} pinpointed {h.point}.");
                            }
                        }

                        _coroutines.Add(Timing.RunCoroutine(PingCooldown(player)));

                        yield return Timing.WaitForSeconds(TestFPS.Instance.Config.PingDuration);

                        foreach (var p in _teamB)
                        {
                            pinPointedPositions[p].Remove(h.point);
                        }
                    }
                }
            }
            else
            {
                if (target == player) yield break;

                var roleColor = target.Role.Type switch
                {
                    RoleTypeId.ClassD => "#FF8000",
                    RoleTypeId.NtfSpecialist => "#0096FF",
                    RoleTypeId.Scientist => "#FFFF7C",
                    RoleTypeId.ChaosConscript => "#008F1C",
                    RoleTypeId.NtfSergeant => "#0096FF",
                    RoleTypeId.NtfCaptain => "#003DCA",
                    RoleTypeId.NtfPrivate => "#70C3FF",
                    RoleTypeId.Tutorial => "#F408A9",
                    RoleTypeId.FacilityGuard => "#535F75",
                    RoleTypeId.ChaosRifleman => "#008F1C",
                    RoleTypeId.ChaosMarauder => "#008F1C",
                    RoleTypeId.ChaosRepressor => "#008F1C",
                    _ => "#FFFFFF"
                };

                if (_teamA.Contains(player))
                {
                    foreach (var p in _teamA.Where(x => x.IsAlive))
                    {
                        if (pinPointedPlayers.TryGetValue(p, out var players))
                        {
                            if (players.Add(target))
                            {
                                SendHint($"<size=30><align=right><b>{player.Nickname}</b> - <color={roleColor}>{target.Nickname}</color> 표시.</align>", 5, p);
                                Log.Debug($"{player.Nickname} pinpointed {target.Nickname}.");
                            }
                            else
                            {
                                yield break;
                            }
                        }
                        else
                        {
                            SendHint($"<size=30><align=right><b>{player.Nickname}</b> - <color={roleColor}>{target.Nickname}</color> 표시.</align>", 5, p);
                            pinPointedPlayers.Add(p, [target]);
                            Log.Debug($"{player.Nickname} pinpointed {target.Nickname}.");
                        }
                    }

                    _coroutines.Add(Timing.RunCoroutine(PingCooldown(player)));

                    yield return Timing.WaitForSeconds(TestFPS.Instance.Config.PingDuration);

                    foreach (var p in _teamA)
                    {
                        pinPointedPlayers[p].Remove(target);
                    }
                }
                else if (_teamB.Contains(player))
                {
                    foreach (var p in _teamB.Where(x => x.IsAlive))
                    {
                        if (pinPointedPlayers.TryGetValue(p, out var players))
                        {
                            if (players.Add(target))
                            {
                                SendHint($"<size=30><align=right><b>{player.Nickname}</b> - <color={roleColor}>{target.Nickname}</color> 표시.</align>", 5, p);
                                Log.Debug($"{player.Nickname} pinpointed {target.Nickname}.");
                            }
                            else
                            {
                                yield break;
                            }
                        }
                        else
                        {
                            SendHint($"<size=30><align=right><b>{player.Nickname}</b> - <color={roleColor}>{target.Nickname}</color> 표시.</align>", 5, p);
                            pinPointedPlayers.Add(p, [target]);
                            Log.Debug($"{player.Nickname} pinpointed {target.Nickname}.");
                        }
                    }

                    _coroutines.Add(Timing.RunCoroutine(PingCooldown(player)));

                    yield return Timing.WaitForSeconds(TestFPS.Instance.Config.PingDuration);

                    foreach (var p in _teamB)
                    {
                        pinPointedPlayers[p].Remove(target);
                    }
                }
            }
        }
    }

    private IEnumerator<float> PingCooldown(Player player)
    {
        pingCooldown.Add(player);
        yield return Timing.WaitForSeconds(TestFPS.Instance.Config.PingCooldown);
        pingCooldown.Remove(player);
    }

    public void OnDying(DyingEventArgs ev)
    {
        SumUpScore(ev.Player, ev.Attacker, ev.DamageHandler);
        Timing.CallDelayed(3f, () =>
        {
            if (Ragdoll.List.Count(x => x.Owner == ev.Player) != 0)
            {
                Ragdoll.List.Where(x => x.Owner == ev.Player).ToList().ForEach(ragdoll =>
                {
                    ragdoll.Destroy();
                });
            }
        });
    }

    public void OnDied(DiedEventArgs ev)
    {
        if (ev.Attacker == null)
        {
            Pickup.List.Where(x => x.PreviousOwner == ev.Player).ToList().ForEach(pickup => pickup.Destroy());
            _coroutines.Add(Timing.RunCoroutine(Respawn(ev.Player, 5)));
            return;
        }

        Pickup.List.Where(x => x.PreviousOwner == ev.Player).ToList().ForEach(pickup => pickup.Destroy());
        if (!ev.Attacker.HasItem(ItemType.Medkit))
            ev.Attacker.AddItem(ItemType.Medkit);

        var weaponType = GetTypeOfWeapon(ev.DamageHandler.As<FirearmDamageHandler>().WeaponType);

        ev.Attacker.RemoveHeldItem();
        if (weaponType == WeaponType.Pistol)
            ev.Attacker.AddItem(GetRandomGun(WeaponType.Pistol));
        else
        {
            var randomType = Random.Range(0, 3) switch
            {
                0 => WeaponType.Rifle,
                1 => WeaponType.SMG,
                2 => WeaponType.Shotgun,
                _ => WeaponType.Rifle
            };

            ev.Attacker.AddItem(GetRandomGun(randomType));
        }

        ev.Attacker.AddAmmo(AmmoType.Ammo44Cal, 20);
        ev.Attacker.AddAmmo(AmmoType.Nato556, 60);
        ev.Attacker.AddAmmo(AmmoType.Nato762, 60);
        ev.Attacker.AddAmmo(AmmoType.Nato9, 120);
        ev.Attacker.SetAmmo(AmmoType.Ammo12Gauge, 54);

        ev.Attacker.AddAhp(10, 75, 0, 1, 0, false);
        _coroutines.Add(Timing.RunCoroutine(Respawn(ev.Player, 5)));
    }

    private string GetGunName(ItemType type)
    {
        return type switch {
            ItemType.GunCOM15 => "COM-15",
            ItemType.GunE11SR => "E-11-SR",
            ItemType.GunCrossvec => "Crossvec",
            ItemType.GunFSP9 => "FSP-9",
            ItemType.GunLogicer => "Logicer",
            ItemType.GunCOM18 => "COM-18",
            ItemType.GunRevolver => "Revolver",
            ItemType.GunAK => "AK-47",
            ItemType.GunShotgun => "Shotgun",
            ItemType.GunCom45 => "Com-45",
            ItemType.GunFRMG0 => "FR-MG-0",
            ItemType.GunA7 => "A-7",
            _ => "Unknown"
        };
    }

    private WeaponType GetTypeOfWeapon(ItemType type)
    {
        return type switch {
            ItemType.GunCOM15 => WeaponType.Pistol,
            ItemType.GunE11SR => WeaponType.Rifle,
            ItemType.GunCrossvec => WeaponType.SMG,
            ItemType.GunFSP9 => WeaponType.SMG,
            ItemType.GunLogicer => WeaponType.Rifle,
            ItemType.GunCOM18 => WeaponType.Pistol,
            ItemType.GunRevolver => WeaponType.Pistol,
            ItemType.GunAK => WeaponType.Rifle,
            ItemType.GunShotgun => WeaponType.Shotgun,
            ItemType.GunCom45 => WeaponType.Pistol,
            ItemType.GunFRMG0 => WeaponType.Rifle,
            ItemType.GunA7 => WeaponType.Rifle,
            _ => WeaponType.Unknown
        };
    }

    private void SumUpScore(Player victim, Player attacker, DamageHandler damageHandler)
    {
        if (victim == null || attacker == null) return;

        if (_teamA.Contains(attacker))
        {
            _teamAPoint += 50;
            _playerPointsDict[attacker] += 50;
            SendHint($"<size=25><align=right><color=#FF0000><b>{victim.Nickname}</color> - 처치! <color=#f7cb39>+50pt</color></b></align>", 5, attacker);

            var score = GetScoreForWeapon(damageHandler.As<FirearmDamageHandler>().WeaponType);
            _teamAPoint += score;
            _playerPointsDict[attacker] += score;
            SendHint($"<size=25><align=right><color=#FF0000><b>{victim.Nickname}</color> - {GetGunName(damageHandler.As<FirearmDamageHandler>().WeaponType)}로 처치! <color=#f7cb39>+{GetScoreForWeapon(damageHandler.As<FirearmDamageHandler>().WeaponType)}pt</color></b></align>", 5, attacker);

            if (damageHandler.As<StandardDamageHandler>().Hitbox == HitboxType.Headshot)
            {
                _teamAPoint += 50;
                _playerPointsDict[attacker] += 50;
                SendHint($"<size=25><align=right><color=#FF0000><b>{victim.Nickname}</color> - 헤드샷! <color=#f7cb39>+50pt</color></b></align>", 5, attacker);
            }

            if (Vector3.Distance(victim.Position, attacker.Position) >= 15f)
            {
                _teamAPoint += 20;
                _playerPointsDict[attacker] += 20;
                SendHint($"<size=25><align=right><color=#FF0000><b>{victim.Nickname}</color> - 원거리 처치! <color=#f7cb39>+20pt</color></b></align>", 5, attacker);
            }
            else if (Vector3.Distance(victim.Position, attacker.Position) >= 10f)
            {
                _teamAPoint += 10;
                _playerPointsDict[attacker] += 10;
                SendHint($"<size=25><align=right><color=#FF0000><b>{victim.Nickname}</color> - 중거리 처치! <color=#f7cb39>+10pt</color></b></align>", 5, attacker);
            }

            if (attacker.Health < 20)
            {
                _teamAPoint += 20;
                _playerPointsDict[attacker] += 20;
                SendHint($"<size=25><align=right><color=#FF0000><b>{victim.Nickname}</color> - 아슬아슬! <color=#f7cb39>+20pt</color></b></align>", 5, attacker);
            }

            _assistList[victim].Where(x => x != attacker).ToList().ForEach(x =>
            {
                _teamAPoint += 25;
                _playerPointsDict[x] += 25;

                SendHint($"<size=25><align=right><color=#FF0000><b>{victim.Nickname}</color> - 어시스트! <color=#f7cb39>+25pt</color></b></align>", 5, x);
            });

            _assistList[victim].Clear();
        }
        else if (_teamB.Contains(attacker))
        {
            _teamBPoint += 50;
            _playerPointsDict[attacker] += 50;
            SendHint($"<size=25><align=right><color=#FF0000><b>{victim.Nickname}</color> - 처치! <color=#f7cb39>+50pt</color></b></align>", 5, attacker);

            var score = GetScoreForWeapon(damageHandler.As<FirearmDamageHandler>().WeaponType);
            _teamBPoint += score;
            _playerPointsDict[attacker] += score;
            SendHint($"<size=25><align=right><color=#FF0000><b>{victim.Nickname}</color> - {GetGunName(damageHandler.As<FirearmDamageHandler>().WeaponType)}로 처치! <color=#f7cb39>+{GetScoreForWeapon(damageHandler.As<FirearmDamageHandler>().WeaponType)}pt</color></b></align>", 5, attacker);

            if (damageHandler.As<StandardDamageHandler>().Hitbox == HitboxType.Headshot)
            {
                _teamBPoint += 50;
                _playerPointsDict[attacker] += 50;
                SendHint($"<size=25><align=right><color=#FF0000><b>{victim.Nickname}</color> - 헤드샷! <color=#f7cb39>+50pt</color></b></align>", 5, attacker);
            }

            if (Vector3.Distance(victim.Position, attacker.Position) >= 15f)
            {
                _teamBPoint += 20;
                _playerPointsDict[attacker] += 20;
                SendHint($"<size=25><align=right><color=#FF0000><b>{victim.Nickname}</color> - 원거리 처치! <color=#f7cb39>+20pt</color></b></align>", 5, attacker);
            }
            else if (Vector3.Distance(victim.Position, attacker.Position) >= 10f)
            {
                _teamBPoint += 10;
                _playerPointsDict[attacker] += 10;
                SendHint($"<size=25><align=right><color=#FF0000><b>{victim.Nickname}</color> - 중거리 처치! <color=#f7cb39>+10pt</color></b></align>", 5, attacker);
            }

            if (attacker.Health < 20)
            {
                _teamBPoint += 20;
                _playerPointsDict[attacker] += 20;
                SendHint($"<size=25><align=right><color=#FF0000><b>{victim.Nickname}</color> - 아슬아슬! <color=#f7cb39>+20pt</color></b></align>", 5, attacker);
            }

            _assistList[victim].Where(x => x != attacker).ToList().ForEach(x =>
            {
                _teamBPoint += 25;
                _playerPointsDict[x] += 25;

                SendHint($"<size=25><align=right><color=#FF0000><b>{victim.Nickname}</color> - 어시스트! <color=#f7cb39>+25pt</color></b></align>", 5, x);
            });
        }
    }

    private IEnumerator<float> Respawn(Player player, ushort time)
    {
        var timeLeft = time;
        while (timeLeft > 0)
        {
            timeLeft--;
            yield return Timing.WaitForSeconds(1f);
        }

        if (_roundEnded) yield break;

        var spawnableRooms = Room.List.Where(x =>
            _playerSpawnRooms[ZoneType.Entrance].Contains(x.Type) && !x.Players.Any()).ToList();

        if (_teamA.Contains(player))
        {
            player.Role.Set(RoleTypeId.ClassD, SpawnReason.Respawn, RoleSpawnFlags.None);
        }
        else if (_teamB.Contains(player))
        {
            player.Role.Set(RoleTypeId.NtfSergeant, SpawnReason.Respawn, RoleSpawnFlags.None);
        }

        spawnableRooms.ShuffleList();
        player.Position = spawnableRooms.First().Position + new Vector3(0, 1, 0);

        if (IsPlaying(player))
        {
            var randomWeaponType = Random.Range(0, 3) switch
            {
                0 => WeaponType.SMG,
                1 => WeaponType.Rifle,
                2 => WeaponType.Shotgun,
                _ => WeaponType.Rifle
            };
            player.AddItem(GetRandomGun(WeaponType.Pistol));
            player.AddItem(GetRandomGun(randomWeaponType));
            player.AddItem(ItemType.KeycardO5);
            player.AddItem(ItemType.Medkit);
            player.AddItem(ItemType.ArmorCombat);
            player.AddItem(ItemType.Radio);
            player.AddAmmo(AmmoType.Ammo44Cal, 120);
            player.AddAmmo(AmmoType.Nato556, 120);
            player.AddAmmo(AmmoType.Nato762, 120);
            player.AddAmmo(AmmoType.Nato9, 120);
            player.SetAmmo(AmmoType.Ammo12Gauge, 54);
        }

        // Log.Info(_1853EffectCount);

        if (_1853EffectCount != 0)
            player.SyncEffect(new Effect(EffectType.Scp1853, 0, _1853EffectCount, false, true));

        player.IsGodModeEnabled = true;
        yield return Timing.WaitForSeconds(2f);
        player.IsGodModeEnabled = false;
    }

    public void OnPickingUpItem(PickingUpItemEventArgs ev)
    {
        if (ev.Pickup == null) return;

        if (pinPointedItems.TryGetValue(ev.Player, out var items))
        {
            if (items.Contains(ev.Pickup))
            {
                items.Remove(ev.Pickup);
            }
        }
    }

    public void OnDroppingItem(DroppingItemEventArgs ev)
    {
        if (ev.Item == null) return;
        if (ev.Item.IsKeycard) ev.IsAllowed = false;
    }

    public void OnChangingRole(ChangingRoleEventArgs ev)
    {
        ev.Player.ClearInventory();
        ev.Player.ClearAmmo();
        Timing.CallDelayed(0.1f, () =>
        {
            Pickup.List.Where(x => x.PreviousOwner == ev.Player).ToList().ForEach(x => x.Destroy());
        });
    }

    public void OnRespawningTeam(RespawningTeamEventArgs ev)
    {
        ev.IsAllowed = false;
    }

    public void OnUsingRadioBattery(UsingRadioBatteryEventArgs ev)
    {
        ev.Drain = 0;
    }

    public void OnPlayerLeft(LeftEventArgs ev)
    {
        if (pinPointedPlayers.ContainsKey(ev.Player))
            pinPointedPlayers.Remove(ev.Player);

        if (pinPointedItems.ContainsKey(ev.Player))
            pinPointedItems.Remove(ev.Player);

        if (pinPointedPositions.ContainsKey(ev.Player))
            pinPointedPositions.Remove(ev.Player);

        if (_teamA.Contains(ev.Player))
        {
            _teamA.Remove(ev.Player);
        }

        if (_teamB.Contains(ev.Player))
        {
            _teamB.Remove(ev.Player);
        }

        if (_playerPointsDict.ContainsKey(ev.Player))
            _playerPointsDict.Remove(ev.Player);
    }

    public void OnPlayerVerified(VerifiedEventArgs ev)
    {
        _playerPointsDict.Add(ev.Player, 0);
    }

    public void OnPlayerHurt(HurtEventArgs ev)
    {
        if (ev.Attacker == null) return;

        _coroutines.Add(Timing.RunCoroutine(Assist(ev.Player, ev.Attacker)));
    }

    private IEnumerator<float> Assist(Player victim, Player attacker)
    {
        Log.Debug($"{victim.Nickname} : {attacker.Nickname} (AssistList.Add)");

        _assistList.TryAdd(victim, []);

        _assistList[victim].Add(attacker);
        yield return Timing.WaitForSeconds(3f);
        _assistList[victim].Remove(attacker);

        Log.Debug($"{victim.Nickname} : {attacker.Nickname} (AssistList.Remove)");
    }

    public void OnHandcuffing(HandcuffingEventArgs ev)
    {
        ev.IsAllowed = false;
    }
}