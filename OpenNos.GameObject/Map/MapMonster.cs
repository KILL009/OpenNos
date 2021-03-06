﻿/*
 * This file is part of the OpenNos Emulator Project. See AUTHORS file for Copyright information
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 */

using OpenNos.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OpenNos.GameObject
{
    public class MapMonster : MapMonsterDTO
    {
        #region Instantiation

        public MapMonster(Map parent, short VNum)
        {
            LastEffect = LastMove = DateTime.Now;
            Target = -1;
            Path = new List<MapCell>();
            Map = parent;
            MonsterVNum = VNum;
            Monster = ServerManager.GetNpc(MonsterVNum);
            Skills = Monster.Skills.ToList();
            DamageList = new Dictionary<long, long>();
        }

        #endregion

        #region Properties

        public bool Alive { get; set; }
        public int CurrentHp { get; set; }
        public int CurrentMp { get; set; }
        public DateTime Death { get; set; }
        public short firstX { get; set; }
        public short firstY { get; set; }
        public DateTime LastEffect { get; set; }
        public DateTime LastMove { get; set; }
        public Map Map { get; set; }
        public NpcMonster Monster { get; set; }
        public List<MapCell> Path { get; set; }
        public List<NpcMonsterSkill> Skills { get; set; }
        public long Target { get; set; }
        public bool inWaiting { get; set; }
        public IDictionary<long, long> DamageList { get; set; }

        #endregion

        #region Methods

        public static int GenerateMapMonsterId()
        {
            Random rnd = new Random();
            List<int> monsterIds = new List<int>();

            for (int i = ServerManager.Monsters.Count - 1; i >= 0; i--)
            {
                monsterIds.Add(ServerManager.Monsters[i].MapMonsterId);
            }

            for (int i = 20000; i < int.MaxValue; i++)
                if (!monsterIds.Contains(i))
                    return i;
            return -1;
        }

        public string GenerateEff(int effect)
        {
            return $"eff 3 {MapMonsterId} {effect}";
        }

        public string GenerateIn3()
        {
            if (Alive && !IsDisabled)
                return $"in 3 {MonsterVNum} {MapMonsterId} {MapX} {MapY} {Position} {(int)(((float)CurrentHp / (float)Monster.MaxHP) * 100)} {(int)(((float)CurrentMp / (float)Monster.MaxMP) * 100)} 0 0 0 -1 1 0 -1 - 0 -1 0 0 0 0 0 0 0 0";
            else return string.Empty;
        }

        internal void MonsterLife()
        {
            //Respawn
            if (!Alive)
            {
                double timeDeath = (DateTime.Now - Death).TotalSeconds;
                if (timeDeath >= Monster.RespawnTime / 10)
                {
                    DamageList = new Dictionary<long, long>();
                    Alive = true;
                    Target = -1;
                    CurrentHp = Monster.MaxHP;
                    CurrentMp = Monster.MaxMP;
                    MapX = firstX;
                    MapY = firstY;
                    Path = new List<MapCell>();
                    Map.Broadcast(GenerateIn3());
                    Map.Broadcast(GenerateEff(7));
                }
                return;
            }
            else if (Target == -1)
            {
                //Normal Move Mode
                if (Alive == false)
                {
                    return;
                }
                Random random = new Random((int)DateTime.Now.Ticks & 0x0000FFFF);
                double time = (DateTime.Now - LastMove).TotalSeconds;
                int MoveFrequent = 5 - (int)Math.Round((double)(Monster.Speed / 5));
                if (MoveFrequent < 1)
                    MoveFrequent = 1;
                if (IsMoving)
                {
                    if (Path.Where(s => s != null).ToList().Count > 0)//fix a path problem
                    {
                        if ((DateTime.Now - LastMove).TotalSeconds > 1.0 / Monster.Speed)
                        {
                            short mapX = Path.ElementAt(0).X;
                            short mapY = Path.ElementAt(0).Y;
                            Path.RemoveAt(0);
                            LastMove = DateTime.Now;
                            Map.Broadcast($"mv 3 {this.MapMonsterId} {this.MapX} {this.MapY} {Monster.Speed}");

                            Task.Factory.StartNew(async () =>
                            {
                                await Task.Delay(500);
                                this.MapX = mapX;
                                this.MapY = mapY;
                            });
                            return;
                        }
                    }
                    else if (time > random.Next(1, MoveFrequent) + 1)
                    {
                        int moveDistance = (int)Math.Round((double)Monster.Speed / 2);
                        byte xpoint = (byte)(random.Next(1, moveDistance + 1));
                        byte ypoint = (byte)(random.Next(1, moveDistance + 1));

                        short mapX = firstX;
                        short mapY = firstY;
                        if (ServerManager.GetMap(MapId).GetFreePosition(ref mapX, ref mapY, xpoint, ypoint))
                        {
                            LastMove = DateTime.Now;

                            string movePacket = $"mv 3 {this.MapMonsterId} {mapX} {mapY} {Monster.Speed}";
                            Map.Broadcast(movePacket);

                            Task.Factory.StartNew(async () =>
                            {
                                await Task.Delay(500);
                                this.MapX = mapX;
                                this.MapY = mapY;
                            });
                        }
                    }
                }
                if (Monster.IsHostile)
                {
                    Character character = ServerManager.Instance.Sessions.Where(s => s.Character != null && s.Character.Hp > 0).OrderBy(s => Map.GetDistance(new MapCell() { X = MapX, Y = MapY }, new MapCell() { X = s.Character.MapX, Y = s.Character.MapY })).FirstOrDefault(s => s.Character != null && !s.Character.Invisible && s.Character.MapId == MapId)?.Character;
                    if (character != null)
                    {
                        if (Map.GetDistance(new MapCell() { X = character.MapX, Y = character.MapY }, new MapCell() { X = MapX, Y = MapY }) < 7)
                        {
                            Target = character.CharacterId;
                            if (!Monster.NoAggresiveIcon)
                                character.Session.Client.SendPacket(GenerateEff(5000));
                        }
                    }
                }
            }
            else
            {
                ClientSession targetSession = Map.Sessions.SingleOrDefault(s => s.Character.CharacterId == Target);

                if (targetSession == null || targetSession.Character.Invisible)
                {
                    Target = -1;
                    return;
                }
                Random random = new Random((int)DateTime.Now.Ticks & 0x0000FFFF);
                NpcMonsterSkill npcMonsterSkill = null;
                if (random.Next(10) > 8 || inWaiting)
                {
                    inWaiting = false;
                    if ((DateTime.Now - LastEffect).TotalMilliseconds < Monster.BasicCooldown * 200)
                    {
                        inWaiting = true;
                    }
                    npcMonsterSkill = Skills.Where(s => (DateTime.Now - s.LastUse).TotalMilliseconds >= 100 * s.Skill.Cooldown).OrderBy(rnd => random.Next()).FirstOrDefault();
                }

                int damage = 100;
                if (targetSession != null && ((npcMonsterSkill != null && Map.GetDistance(new MapCell() { X = this.MapX, Y = this.MapY }, new MapCell() { X = targetSession.Character.MapX, Y = targetSession.Character.MapY }) < npcMonsterSkill.Skill.Range) || (Map.GetDistance(new MapCell() { X = this.MapX, Y = this.MapY }, new MapCell() { X = targetSession.Character.MapX, Y = targetSession.Character.MapY }) <= Monster.BasicRange)))
                {
                    if ((DateTime.Now - LastEffect).TotalMilliseconds >= Monster.BasicCooldown * 200 && !inWaiting)
                    {
                        if (npcMonsterSkill != null)
                        {
                            npcMonsterSkill.LastUse = DateTime.Now;
                            Map.Broadcast($"ct 3 {MapMonsterId} 1 {Target} {npcMonsterSkill.Skill.CastAnimation} -1 {npcMonsterSkill.Skill.SkillVNum}");
                        }
                        LastMove = DateTime.Now;

                        // deal 0 damage to GM with GodMode
                        damage = targetSession.Character.HasGodMode ? 0 : 100;
                        if (targetSession.Character.IsSitting)
                        {
                            targetSession.Character.IsSitting = false;
                            Map.Broadcast(null, targetSession.Character.GenerateRest(), ReceiverType.OnlySomeone, "", targetSession.Character.CharacterId);
                        }
                        if (npcMonsterSkill != null && npcMonsterSkill.Skill.CastEffect != 0)
                        {
                            Map.Broadcast(GenerateEff(npcMonsterSkill.Skill.CastEffect));
                            Thread.Sleep(npcMonsterSkill.Skill.CastTime * 100);
                        }
                        Path = new List<MapCell>();
                        targetSession.Character.LastDefence = DateTime.Now;
                        targetSession.Character.GetDamage(damage);

                        Map.Broadcast(null, ServerManager.Instance.GetUserMethod<string>(Target, "GenerateStat"), ReceiverType.OnlySomeone, "", Target);

                        if (npcMonsterSkill != null)
                            Map.Broadcast($"su 3 {MapMonsterId} 1 {Target} {npcMonsterSkill.SkillVNum} {npcMonsterSkill.Skill.Cooldown} {npcMonsterSkill.Skill.AttackAnimation} {npcMonsterSkill.Skill.Effect} {this.MapX} {this.MapY} {(targetSession.Character.Hp > 0 ? 1 : 0)} {(int)((double)targetSession.Character.Hp / ServerManager.Instance.GetUserMethod<double>(Target, "HPLoad"))} {damage} 0 0");
                        else
                            Map.Broadcast($"su 3 {MapMonsterId} 1 {Target} 0 {Monster.BasicCooldown} 11 {Monster.BasicSkill} 0 0 {(targetSession.Character.Hp > 0 ? 1 : 0)} {(int)((double)targetSession.Character.Hp / ServerManager.Instance.GetUserMethod<double>(Target, "HPLoad"))} {damage} 0 0");


                        LastEffect = DateTime.Now;
                        if (targetSession.Character.Hp <= 0)
                        {
                            Thread.Sleep(1000);
                            ServerManager.Instance.AskRevive(targetSession.Character.CharacterId);
                            Target = -1;
                        }
                        if (npcMonsterSkill != null && (npcMonsterSkill.Skill.Range > 0 || npcMonsterSkill.Skill.TargetRange > 0))
                        {
                            foreach (Character chara in ServerManager.GetMap(MapId).GetListPeopleInRange(npcMonsterSkill.Skill.TargetRange == 0 ? this.MapX : targetSession.Character.MapX, npcMonsterSkill.Skill.TargetRange == 0 ? this.MapY : targetSession.Character.MapY, (byte)(npcMonsterSkill.Skill.TargetRange + npcMonsterSkill.Skill.Range)).Where(s => s.CharacterId != Target))
                            {
                                if (chara.IsSitting)
                                {
                                    chara.IsSitting = false;
                                    Map.Broadcast(null, chara.GenerateRest(), ReceiverType.OnlySomeone, "", chara.CharacterId);
                                }
                                damage = chara.HasGodMode ? 0 : 100;
                                bool AlreadyDead2 = chara.Hp <= 0;
                                chara.GetDamage(damage);
                                chara.LastDefence = DateTime.Now;
                                Map.Broadcast(null, chara.GenerateStat(), ReceiverType.OnlySomeone, "", chara.CharacterId);
                                Map.Broadcast($"su 3 {MapMonsterId} 1 {chara.CharacterId} 0 {Monster.BasicCooldown} 11 {Monster.BasicSkill} 0 0 {(chara.Hp > 0 ? 1 : 0)} {(int)((double)chara.Hp / chara.HPLoad())} {damage} 0 0");
                                if (chara.Hp <= 0 && !AlreadyDead2)
                                {
                                    Thread.Sleep(1000);
                                    ServerManager.Instance.AskRevive(chara.CharacterId);
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (IsMoving == true)
                    {
                        short maxDistance = 22;

                        if (Path.Count() == 0 && targetSession != null && (Map.GetDistance(new MapCell() { X = this.MapX, Y = this.MapY }, new MapCell() { X = targetSession.Character.MapX, Y = targetSession.Character.MapY }) < maxDistance))
                        {

                            Path = ServerManager.GetMap(MapId).StraightPath(new MapCell() { X = this.MapX, Y = this.MapY, MapId = this.MapId }, new MapCell() { X = targetSession.Character.MapX, Y = targetSession.Character.MapY, MapId = this.MapId });
                            if (!Path.Any())
                            {
                                Path = ServerManager.GetMap(MapId).AStar(new MapCell() { X = this.MapX, Y = this.MapY, MapId = this.MapId }, new MapCell() { X = targetSession.Character.MapX, Y = targetSession.Character.MapY, MapId = this.MapId });
                            }
                        }
                        if (Path.Count > 0 && Map.GetDistance(new MapCell() { X = this.MapX, Y = this.MapY, MapId = this.MapId }, new MapCell() { X = targetSession.Character.MapX, Y = targetSession.Character.MapY, MapId = this.MapId }) > 1)
                        {
                            this.MapX = Path.ElementAt(0).X;
                            this.MapY = Path.ElementAt(0).Y;
                            Path.RemoveAt(0);
                        }
                        if (targetSession == null || MapId != targetSession.Character.MapId || (Map.GetDistance(new MapCell() { X = this.MapX, Y = this.MapY }, new MapCell() { X = targetSession.Character.MapX, Y = targetSession.Character.MapY }) > maxDistance))
                        {
                            Path = ServerManager.GetMap(MapId).AStar(new MapCell() { X = this.MapX, Y = this.MapY, MapId = this.MapId }, new MapCell() { X = firstX, Y = firstY, MapId = this.MapId });
                            Target = -1;
                        }
                        else
                        {
                            if ((DateTime.Now - LastMove).TotalSeconds > 1.0 / Monster.Speed)
                            {
                                LastMove = DateTime.Now;
                                Map.Broadcast($"mv 3 {this.MapMonsterId} {this.MapX} {this.MapY} {Monster.Speed}");
                            }
                        }
                    }
                }
            }
        }

        #endregion
    }
}