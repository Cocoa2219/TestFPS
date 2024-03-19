using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using UnityEngine;
using UserSettings.OtherSettings;
using Exception = System.Exception;
using Random = UnityEngine.Random;

namespace TestFPS;

public class EventHandler
{
    public readonly Dictionary<Player, HashSet<Player>> pinPointedPlayers = new();
    private readonly Dictionary<Player, HashSet<Vector3>> pinPointedPositions = new();
    private readonly Dictionary<Player, HashSet<Pickup>> pinPointedItems = new();

    private readonly HashSet<Player> pingCooldown = [];

    private Room room;

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

    public HashSet<Player> _teamA;
    public HashSet<Player> _teamB;

    private HashSet<Player> _teamAPointedPlayers = [];
    private HashSet<Player> _teamBPointedPlayers = [];

    private int _timer;

    private float _pointScore;

    private bool _showCompleted;

    private int _isOccupyed;
    private int _isSteal;

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
        room = PickRandomRoom(ZoneType.LightContainment);

        room.Color = new Color32(132, 191, 133, 25);
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

        _timer = 300;

        foreach (var player in Player.List)
        {
            player.Role.Set(RoleTypeId.Spectator, SpawnReason.None, RoleSpawnFlags.All);
            player.Broadcast(10, "<size=35><b>MTF-E11-SR 부착물을 설정해 주세요.</b></size>");
        }

        yield return Timing.WaitForSeconds(1f);

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

        Map.Broadcast(3, "<size=35><b>포인트 A와 B를 점령하세요!</b></size>", Broadcast.BroadcastFlags.Normal, true);

        yield return Timing.WaitForSeconds(3f);

        var spawnableRooms = Room.List.Where(x =>
            _playerSpawnRooms[ZoneType.LightContainment].Contains(x.Type) && x != room).ToList();

        foreach (var aPlayer in _teamA)
        {
            aPlayer.Role.Set(RoleTypeId.ClassD, SpawnReason.Respawn, RoleSpawnFlags.None);
            spawnableRooms.ShuffleList();
            aPlayer.Position = spawnableRooms.First().Position + new Vector3(0, 1, 0);
            aPlayer.Broadcast(3, "<size=35><b>시작!</b></size>", Broadcast.BroadcastFlags.Normal, true);
        }

        foreach (var bPlayer in _teamB)
        {
            bPlayer.Role.Set(RoleTypeId.NtfSergeant, SpawnReason.Respawn, RoleSpawnFlags.None);
            spawnableRooms.ShuffleList();
            bPlayer.Position = spawnableRooms.First().Position + new Vector3(0, 1, 0);
            bPlayer.Broadcast(3, "<size=35><b>시작!</b></size>", Broadcast.BroadcastFlags.Normal, true);
        }

        foreach (var player in Player.List)
        {
            player.AddItem(ItemType.GunE11SR);
            player.AddItem(ItemType.Medkit);
            player.AddItem(ItemType.ArmorCombat);
            player.AddItem(ItemType.Radio);
            player.AddItem(ItemType.KeycardO5);
            player.AddAmmo(AmmoType.Nato556, 5000);
        }
        Timing.RunCoroutine(BroadcastGameStat());
        Timing.RunCoroutine(Timer(_timer));
        Player.List.ToList().ForEach(x => x.IsGodModeEnabled = true);
        yield return Timing.WaitForSeconds(2f);
        Player.List.ToList().ForEach(x => x.IsGodModeEnabled = false);
    }



    private void SendHint(string text, int duration, params Player[] players)
    {
        players.ToList().ForEach(player =>
        {
            Timing.RunCoroutine(MultiHint.AddPlayerHint(player.UserId, duration, text));
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
        Timing.RunCoroutine(StartGame());
    }

    private IEnumerator<float> Timer(int time)
    {
        _timer = time;

        while (_timer > 0 && !Round.IsEnded)
        {
            _timer--;
            yield return Timing.WaitForSeconds(1f);
        }
    }

    private IEnumerator<float> BroadcastGameStat()
    {
        while (!Round.IsEnded)
        {
            Player.List.ToList().ForEach(player =>
            {
                var degree = GetDegree(player.CameraTransform.forward.normalized, Vector3.zero);
                if (degree < 0)
                    degree += 360;

                var text = player.IsAlive ? FormatCompass(degree, player) + "\n" : "";

                text += FormatGameStats(player);

                player.Broadcast(1, text, Broadcast.BroadcastFlags.Normal, true);
            });

            CheckRoom();

            yield return Timing.WaitForSeconds(0.01f);
        }
    }

    private void CheckRoom()
    {
        try
        {
            foreach (var player in Player.List)
            {
                if (!player.IsAlive)
                {
                    _teamAPointedPlayers.Remove(player);
                    _teamBPointedPlayers.Remove(player);
                    continue;
                }

                if (Vector3.Distance(player.Position, room.Position) > 10f)
                {
                    _teamAPointedPlayers.Remove(player);
                    _teamBPointedPlayers.Remove(player);
                    continue;
                }

                if (player.CurrentRoom != room)
                {
                    _teamAPointedPlayers.Remove(player);
                    _teamBPointedPlayers.Remove(player);
                    continue;
                }

                if (_teamA.Contains(player))
                {
                    _teamAPointedPlayers.Add(player);
                }

                if (_teamB.Contains(player))
                {
                    _teamBPointedPlayers.Add(player);
                }

                if (_teamAPointedPlayers.Count > 0 && _teamBPointedPlayers.Count > 0)
                {
                    Log.Debug($"{player.Nickname} : Both teams are in the room.");
                    continue;
                }

                if (_teamA.Contains(player) && _pointScore < 100)
                {
                    Log.Debug($"{player.Nickname} : Team A is in the room.");
                    if (_isOccupyed == -1)
                    {
                        _isSteal = 1;
                        _pointScore += 0.1f;
                    }
                    else
                    {
                        _pointScore += 0.1f;
                        _isOccupyed = 0;
                        _isSteal = 0;
                    }
                }
                else if (_teamB.Contains(player) && _pointScore > -100)
                {
                    Log.Debug($"{player.Nickname} : Team B is in the room.");
                    if (_isOccupyed == 1)
                    {
                        _isSteal = -1;
                        _pointScore -= 0.1f;
                    }
                    else
                    {
                        _pointScore -= 0.1f;
                        _isOccupyed = 0;
                        _isSteal = 0;
                    }
                }
            }

            switch (_pointScore)
            {
                case > 100:
                    _showCompleted = true;
                    _isOccupyed = 1;
                    _pointScore = 100;
                    Timing.CallDelayed(3f, () =>
                    {
                        _showCompleted = false;
                    });
                    break;
                case < -100:
                    _showCompleted = true;
                    _isOccupyed = -1;
                    _pointScore = -100;
                    Timing.CallDelayed(3f, () =>
                    {
                        _showCompleted = false;
                    });
                    break;
            }

            if (_teamAPointedPlayers.Count == 0 && _pointScore is > 0 and < 100 && _isOccupyed != -1 && _isSteal != -1)
            {
                _pointScore -= 0.2f;
                if (_pointScore < 0)
                {
                    _pointScore = 0;
                }
            }

            if (_teamBPointedPlayers.Count == 0 && _pointScore is < 0 and > -100 && _isOccupyed != 1 && _isSteal != 1)
            {
                _pointScore += 0.2f;
                if (_pointScore > 0)
                {
                    _pointScore = 0;
                }
            }

            room.Color = _pointScore switch
            {
                > 0 => MixColors(new Color32(132, 191, 133, 25), new Color32(239, 121, 4, 25), _pointScore / 100),
                < 0 => MixColors(new Color32(132, 191, 133, 25), new Color32(7, 143, 243, 25), -_pointScore / 100),
                _ => new Color32(132, 191, 133, 25)
            };

            if (_pointScore == 0)
            {
                _isOccupyed = 0;
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

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

            var roomAngle = GetDegree(player.Position, room.Position);

            roomAngle -= 180;

            if (roomAngle < 0)
                roomAngle += 360;

            pinAngle.Add(new Pin
            {
                Letter = "<size=35>🏠</size>",
                Degree = Mathf.RoundToInt(roomAngle),
                Color = "#FFFF00"
            });

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

        var pointText = _pointScore switch
        {
            > 0 =>
                $"{MakeGradientText($"{_pointScore:F1}%", new Color32(239, 121, 4, 255), new Color32(85, 38, 0, 255))}",
            < 0 =>
                $"{MakeGradientText($"{-_pointScore:F1}%", new Color32(7, 143, 243, 255), new Color32(0, 46, 85, 255))}",
            _ => "0.0%"
        };

        if (_showCompleted)
        {
            pointText = _pointScore switch
            {
                >= 100 => $"<size=25>{MakeGradientText("점령되었습니다!", new Color32(239, 121, 4, 255), new Color32(85, 38, 0, 255))}</size>",
                <= -100 =>
                    $"<size=25>{MakeGradientText("점령되었습니다!", new Color32(7, 143, 243, 255), new Color32(0, 46, 85, 255))}</size>",
                _ => pointText
            };
        }

        txt.Append(
            @$"<size=35><b><align=left>ㅤㅤㅤ점령 구역 : {pointText}<line-height=0>\n<size=45><align=center>{SecondsToTime(_timer)}<line-height=0></size>\n<align=right>점령 구역까지 {RoundToFloat(Vector3.Distance(player.Position, room.Position), 2):F2}mㅤㅤㅤ<line-height=1em></b></size>");

        return txt.ToString();
    }

    public void OnServerRestarting()
    {
        pinPointedPlayers.Clear();
    }

    public void OnChangingItem(ChangingItemEventArgs ev)
    {
        if (ev.Item == null) return;
        if (!ev.Item.IsKeycard) return;

        ev.IsAllowed = false;

        Timing.RunCoroutine(Ping(ev.Player));
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

        if (Physics.Raycast(player.CameraTransform.position + player.CameraTransform.forward * 0.2f, player.CameraTransform.forward, out var hit, 15f))
        {
            var obj = hit.collider.gameObject;
            var target = Player.Get(obj.GetComponentInParent<ReferenceHub>());
            if (target == player || target == null)
            {
                if (Physics.Raycast(player.CameraTransform.position + player.CameraTransform.forward * 0.2f,
                        player.CameraTransform.forward, out var h, 15f, layerMask))
                {
                    var pickup = Pickup.Get(h.transform.root.gameObject);

                    if (pickup != null)
                    {
                        if (pinPointedItems.TryGetValue(player, out var items))
                        {
                            if (items.Add(pickup))
                            {
                                SendHint($"<size=30><align=right><b>{player.CustomName}</b> - <b>{GetPickupName(pickup.Type)}</b>이(가) 있음.</align>", 5, Player.List.ToArray());
                                Log.Debug($"{player.Nickname} pinpointed {pickup}.");
                            }
                            else
                            {
                                yield break;
                            }
                        }
                        else
                        {
                            SendHint($"<size=30><align=right><b>{player.CustomName}</b> - <b>{GetPickupName(pickup.Type)}</b>이(가) 있음.</align>", 5, Player.List.ToArray());
                            pinPointedItems.Add(player, [pickup]);
                            Log.Debug($"{player.Nickname} pinpointed {pickup}.");
                        }

                        Timing.RunCoroutine(PingCooldown(player));

                        yield return Timing.WaitForSeconds(TestFPS.Instance.Config.PingDuration);

                        pinPointedItems[player].Remove(pickup);

                        yield break;
                    }

                    if (pinPointedPositions.TryGetValue(player, out var positions))
                    {
                        if (positions.Add(h.point))
                        {
                            foreach (var p in Player.List)
                            {
                                var angle = GetDegree(p.Position, h.point);

                                angle -= 180;

                                if (angle < 0)
                                    angle += 360;

                                angle = Mathf.RoundToInt(angle);

                                SendHint($"<size=30><align=right><b>{player.Nickname}</b> - <color=#FFFFFF>{angle}°</color> 표시.</align>", 5, p);
                            }
                            Log.Debug($"{player.Nickname} pinpointed {h.point}.");
                        }
                        else
                        {
                            yield break;
                        }
                    }
                    else
                    {
                        foreach (var p in Player.List)
                        {
                            var angle = GetDegree(p.Position, h.point);

                            angle -= 180;

                            if (angle < 0)
                                angle += 360;

                            angle = Mathf.RoundToInt(angle);

                            SendHint($"<size=30><align=right><b>{player.Nickname}</b> - <color=#FFFFFF>{angle}°</color> 표시.</align>", 5, p);
                        }
                        pinPointedPositions.Add(player, [h.point]);
                        Log.Debug($"{player.Nickname} pinpointed {h.point}.");
                    }

                    Timing.RunCoroutine(PingCooldown(player));

                    yield return Timing.WaitForSeconds(TestFPS.Instance.Config.PingDuration);

                    pinPointedPositions[player].Remove(h.point);
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

                if (pinPointedPlayers.TryGetValue(player, out var players))
                {
                    if (players.Add(target))
                    {
                        SendHint($"<size=30><align=right><b>{player.CustomName}</b> - <b><color={roleColor}>{target.CustomName}</color></b> 발견.</align>", 5, Player.List.ToArray());
                        Log.Debug($"{player.Nickname} pinpointed {target.Nickname}.");
                    }
                    else
                    {
                        yield break;
                    }
                }
                else
                {
                    SendHint($"<size=30><align=right><b>{player.CustomName}</b> - <b><color={roleColor}>{target.CustomName}</color></b> 발견.</align>", 5, Player.List.ToArray());
                    pinPointedPlayers.Add(player, [target]);
                    Log.Debug($"{player.Nickname} pinpointed {target.Nickname}.");
                }

                Timing.RunCoroutine(PingCooldown(player));

                yield return Timing.WaitForSeconds(TestFPS.Instance.Config.PingDuration);

                pinPointedPlayers[player].Remove(target);
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
        if (ev.Attacker == null) return;

        Pickup.List.Where(x => x.PreviousOwner == ev.Player).ToList().ForEach(pickup => pickup.Destroy());
        if (!ev.Attacker.HasItem(ItemType.Medkit))
            ev.Attacker.AddItem(ItemType.Medkit);
        ev.Attacker.AddAhp(20, 75, 0, 1, 0, false);
        Timing.RunCoroutine(Respawn(ev.Player, 5));
    }

    private IEnumerator<float> Respawn(Player player, ushort time)
    {
        var timeLeft = time;
        while (timeLeft > 0)
        {
            timeLeft--;
            yield return Timing.WaitForSeconds(1f);
        }

        var spawnableRooms = Room.List.Where(x =>
            _playerSpawnRooms[ZoneType.LightContainment].Contains(x.Type) && x != room).ToList();

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

        player.AddItem(ItemType.GunE11SR);
        player.AddItem(ItemType.KeycardO5);
        player.AddItem(ItemType.Medkit);
        player.AddItem(ItemType.ArmorCombat);
        player.AddItem(ItemType.Radio);
        player.AddAmmo(AmmoType.Nato556, 5000);

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
        Timing.CallDelayed(0.1f, () =>
        {
            Pickup.List.Where(x => x.PreviousOwner == ev.Player).ToList().ForEach(x => x.Destroy());
        });
    }
}