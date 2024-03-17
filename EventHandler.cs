using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Exiled.API.Features;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using PlayerRoles.Ragdolls;
using UnityEngine;

namespace TestFPS;

public class EventHandler
{
    public Dictionary<Player, HashSet<Player>> pinPointedPlayers = new();
    public Dictionary<Player, HashSet<Vector3>> pinPointedPositions = new();
    public Dictionary<Player, HashSet<Pickup>> pinPointedItems = new();

    private HashSet<Player> pingCooldown = new();

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
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };
}


    public void OnRoundStarted()
    {
        Timing.RunCoroutine(Compass());
    }

    private IEnumerator<float> Compass()
    {
        while (!Round.IsEnded)
        {
            Player.List.Where(x => x.IsAlive).ToList().ForEach(player =>
            {
                var degree = GetDegree(player.CameraTransform.forward.normalized, Vector3.zero);
                if (degree < 0)
                    degree += 360;

                player.Broadcast(2, FormatCompass(degree, player), Broadcast.BroadcastFlags.Normal, true);
            });
            yield return Timing.WaitForSeconds(0.01f);
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

    private string FormatCompass(float degree, Player player)
    {
        try
        {
            if (!IsPlaying(player)) return string.Empty;

            StringBuilder compassString = new StringBuilder($"<mspace=6px>");

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

            for (var i = 0; i < 90; i++)
            {
                switch (start)
                {
                    case 0 or 360:
                        compassString.Append("</mspace>N<mspace=6px>");
                        break;
                    case 45:
                        compassString.Append("</mspace><size=30>N E</size><mspace=6px>");
                        break;
                    case 90:
                        compassString.Append("</mspace>E<mspace=6px>");
                        break;
                    case 135:
                        compassString.Append("</mspace><size=30>S E</size><mspace=6px>");
                        break;
                    case 180:
                        compassString.Append("</mspace>S<mspace=6px>");
                        break;
                    case 225:
                        compassString.Append("</mspace><size=30>S W</size><mspace=6px>");
                        break;
                    case 270:
                        compassString.Append("</mspace>W<mspace=6px>");
                        break;
                    case 315:
                        compassString.Append("</mspace><size=30>N W</size><mspace=6px>");
                        break;
                    default:
                    {
                        var pin = pinAngle.FirstOrDefault(x => x.Degree == start);
                        compassString.Append(!pin.Equals(default(Pin))
                            ? $"</mspace><color={pin.Color}><size=20>{pin.Letter}</size></color><mspace=6px>"
                            : "<size=20> | </size>");
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
            Log.Error($"Error in FormatCompass: {e}");
            throw;
        }
    }

    public void OnServerRestarting()
    {
        pinPointedPlayers.Clear();
    }

    public void OnChangingItem(ChangingItemEventArgs ev)
    {
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
}