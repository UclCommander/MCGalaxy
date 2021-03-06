﻿/*
    Copyright 2010 MCLawl Team -
    Created by Snowl (David D.) and Cazzar (Cayde D.)

    Dual-licensed under the Educational Community License, Version 2.0 and
    the GNU General Public License, Version 3 (the "Licenses"); you may
    not use this file except in compliance with the Licenses. You may
    obtain a copy of the Licenses at
    
    http://www.osedu.org/licenses/ECL-2.0
    http://www.gnu.org/licenses/gpl-3.0.html
    
    Unless required by applicable law or agreed to in writing,
    software distributed under the Licenses are distributed on an "AS IS"
    BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express
    or implied. See the Licenses for the specific language governing
    permissions and limitations under the Licenses.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Timers;

namespace MCGalaxy.Games {
    
    public sealed partial class ZombieGame {
        
        void MainLoop() {
            if (Status == ZombieGameStatus.NotStarted) return;
            if (!initialChangeLevel) {
                ChooseNextLevel();
                initialChangeLevel = true;
            }

            while (true) {
                RoundInProgress = false;
                RoundsDone++;
                
                if (!Running) {
                    return;
                } else if (Status == ZombieGameStatus.InfiniteRounds) {
                    DoRound();
                    if (ChangeLevels) ChooseNextLevel();
                } else if (Status == ZombieGameStatus.SingleRound) {
                    DoRound();
                    ResetState(); return;
                } else if (Status == ZombieGameStatus.VariableRounds) {
                    if (RoundsDone == MaxRounds) {
                        ResetState(); return;
                    } else {
                        DoRound();
                        if (ChangeLevels) ChooseNextLevel();
                    }
                } else if (Status == ZombieGameStatus.LastRound) {
                    ResetState(); return;
                }
            }
        }

        void DoRound() {
            if (!Running) return;
            List<Player> players = DoRoundCountdown();
            RoundInProgress = true;
            Random random = new Random();
            Player first = PickFirstZombie(random, players);

            CurLevel.ChatLevel(first.color + first.name + " %Sstarted the infection!");
            first.Game.Infected = true;
            PlayerMoneyChanged(first);
            UpdatePlayerColor(first, InfectCol);

            RoundInProgress = true;
            int roundMins = random.Next(CurLevel.MinRoundTime, CurLevel.MaxRoundTime);
            string suffix = roundMins == 1 ? " %Sminute!" : " %Sminutes!";
            CurLevel.ChatLevel("The round will last for &a" + roundMins + suffix);
            RoundEnd = DateTime.UtcNow.AddMinutes(roundMins);
            timer = new System.Timers.Timer(roundMins * 60 * 1000);
            timer.Elapsed += new ElapsedEventHandler(EndRound);
            timer.Enabled = true;

            Player[] online = PlayerInfo.Online.Items;
            foreach (Player p in online) {
                if (p.level == null || p.level != CurLevel || p.Game.Referee) continue;
                if (p != first) Alive.Add(p);
            }

            Infected.Clear();
            Infected.Add(first);
            UpdateAllPlayerStatus();
            DoCoreGame(random);
            
            if (!Running) {
                Status = ZombieGameStatus.LastRound; return;
            } else {
                HandOutRewards();
            }
        }
        
        Player PickFirstZombie(Random random, List<Player> players) {
            Player first = null;
            do {
                first = QueuedZombie != null ?
                    PlayerInfo.FindExact(QueuedZombie) : players[random.Next(players.Count)];
                QueuedZombie = null;
            } while (first == null || !first.level.name.CaselessEq(CurLevelName));
            return first;
        }
        
        List<Player> DoRoundCountdown() {
            while (true) {
                RoundStart = DateTime.UtcNow.AddSeconds(30);
                CurLevel.ChatLevel("%4Round Start:%f 30...");
                Thread.Sleep(20000); if (!Running) return null;
                CurLevel.ChatLevel("%4Round Start:%f 10...");
                Thread.Sleep(10000); if (!Running) return null;
                CurLevel.ChatLevel("%4Round Start:%f 5...");
                Thread.Sleep(1000); if (!Running) return null;
                CurLevel.ChatLevel("%4Round Start:%f 4...");
                Thread.Sleep(1000); if (!Running) return null;
                CurLevel.ChatLevel("%4Round Start:%f 3...");
                Thread.Sleep(1000); if (!Running) return null;
                CurLevel.ChatLevel("%4Round Start:%f 2...");
                Thread.Sleep(1000); if (!Running) return null;
                CurLevel.ChatLevel("%4Round Start:%f 1...");
                Thread.Sleep(1000); if (!Running) return null;
                int nonRefPlayers = 0;
                List<Player> players = new List<Player>();
                
                Player[] online = PlayerInfo.Online.Items;
                foreach (Player p in online) {
                    if (!p.Game.Referee && p.level.name.CaselessEq(CurLevelName)) {
                        players.Add(p);
                        nonRefPlayers++;
                    }
                }
                
                if (nonRefPlayers >= 2) return players;
                CurLevel.ChatLevel(Colors.red + "ERROR: Need 2 or more players to play");
            }
        }
        
        void DoCoreGame(Random random) {
            Player[] alive = null;
            string lastTimespan = null;
            while ((alive = Alive.Items).Length > 0) {
                Player[] infected = Infected.Items;
                // Update the round time left shown in the top right
                int seconds = (int)(RoundEnd - DateTime.UtcNow).TotalSeconds;
                string timespan = GetTimespan(seconds);
                if (lastTimespan != timespan) {
                    UpdateAllPlayerStatus(timespan);
                    lastTimespan = timespan;
                }
                
                foreach (Player pKiller in infected) {
                    pKiller.Game.Infected = true;
                    UpdatePlayerColor(pKiller, InfectCol);
                    bool aliveChanged = false;
                    foreach (Player pAlive in alive) {
                        UpdatePlayerColor(pAlive, pAlive.color);
                        if (Math.Abs(pAlive.pos[0] - pKiller.pos[0]) > HitboxPrecision
                            || Math.Abs(pAlive.pos[1] - pKiller.pos[1]) > HitboxPrecision
                            || Math.Abs(pAlive.pos[2] - pKiller.pos[2]) > HitboxPrecision)
                            continue;
                        
                        if (!pAlive.Game.Infected && pKiller.Game.Infected && !pAlive.Game.Referee && !pKiller.Game.Referee && pKiller != pAlive
                            && pKiller.level.name.CaselessEq(CurLevelName) && pAlive.level.name.CaselessEq(CurLevelName))
                        {
                            InfectPlayer(pAlive);
                            aliveChanged = true;
                            pAlive.Game.BlocksLeft = 25;
                            
                            if (lastPlayerToInfect == pKiller.name) {
                                infectCombo++;
                                if (infectCombo >= 2) {
                                    pKiller.SendMessage("You gained " + (2 + infectCombo) + " " + Server.moneys);
                                    pKiller.money += 2 + infectCombo;
                                    pKiller.OnMoneyChanged();
                                    CurLevel.ChatLevel(pKiller.FullName + " is on a rampage! " + (infectCombo + 1) + " infections in a row!");
                                }
                            } else {
                                infectCombo = 0;
                            }
                            
                            lastPlayerToInfect = pKiller.name;
                            pKiller.Game.NumInfected++;
                            ShowInfectMessage(random, pAlive, pKiller);
                            CheckHumanPledge(pAlive);
                            CheckBounty(pAlive, pKiller);
                            UpdatePlayerColor(pAlive, InfectCol);
                        }
                    }
                    if (aliveChanged) alive = Alive.Items;
                }
                Thread.Sleep(25);
            }
        }
        
        void CheckHumanPledge(Player pAlive) {
            if (!pAlive.Game.PledgeSurvive) return;
            pAlive.Game.PledgeSurvive = false;
            CurLevel.ChatLevel(pAlive.FullName + "%Sbroke their pledge of not being infected.");
            pAlive.money = Math.Max(pAlive.money - 2, 0);
            pAlive.OnMoneyChanged();
        }
        
        void ShowInfectMessage(Random random, Player pAlive, Player pKiller) {
            string text = null;
            List<string> infectMsgs = pKiller.Game.InfectMessages;
            if (infectMsgs != null && random.Next(0, 10) < 5)
                text = infectMsgs[random.Next(infectMsgs.Count)];
            else
                text = messages[random.Next(messages.Length)];
            
            CurLevel.ChatLevel(String.Format(text,
                                             Colors.red + pKiller.DisplayName + Colors.yellow,
                                             Colors.red + pAlive.DisplayName + Colors.yellow));
        }
        
        void CheckBounty(Player pAlive, Player pKiller) {
            BountyData bounty;
            if (Bounties.TryGetValue(pAlive.name, out bounty))
                Bounties.Remove(pAlive.name);
            if (bounty != null) {
                CurLevel.ChatLevel(pKiller.FullName + " %Scollected the bounty of &a" +
                                   bounty.Amount + " %S" + Server.moneys + " on " + pAlive.FullName + "%S.");
                bounty.Origin.money = Math.Max(0, bounty.Origin.money - bounty.Amount);
                bounty.Origin.OnMoneyChanged();
                pKiller.money += bounty.Amount;
                pKiller.OnMoneyChanged();
            }
        }

        static void UpdatePlayerColor(Player p, string color) {
            if (p.Game.lastSpawnColor == color) return;
            p.Game.lastSpawnColor = color;
            Player.GlobalDespawn(p, false);
            Player.GlobalSpawn(p, p.pos[0], p.pos[1], p.pos[2], p.rot[0], p.rot[1], false);
        }
        
        void EndRound(object sender, ElapsedEventArgs e) {
            if (!Running) return;
            CurLevel.ChatLevel("%4Round End:%f 5"); Thread.Sleep(1000);
            CurLevel.ChatLevel("%4Round End:%f 4"); Thread.Sleep(1000);
            CurLevel.ChatLevel("%4Round End:%f 3"); Thread.Sleep(1000);
            CurLevel.ChatLevel("%4Round End:%f 2"); Thread.Sleep(1000);
            CurLevel.ChatLevel("%4Round End:%f 1"); Thread.Sleep(1000);
            HandOutRewards();
        }

        public void HandOutRewards() {
            if (!RoundInProgress) return;
            RoundInProgress = false;
            RoundStart = DateTime.MinValue;
            RoundEnd = DateTime.MinValue;
            Bounties.Clear();
            if (!Running) return;
            
            Player[] alive = Alive.Items;
            CurLevel.ChatLevel(Colors.lime + "The game has ended!");
            if (alive.Length == 0) CurLevel.ChatLevel(Colors.maroon + "Zombies have won this round.");
            else if (alive.Length == 1) CurLevel.ChatLevel(Colors.green + "Congratulations to the sole survivor:");
            else CurLevel.ChatLevel(Colors.green + "Congratulations to the survivors:");
            
            timer.Enabled = false;
            string playersString = "";
            Player[] online = null;
            
            if (alive.Length == 0) {
                online = PlayerInfo.Online.Items;
                foreach (Player pl in online)
                    ResetPlayer(pl, ref playersString);
            } else {
                foreach (Player pl in alive) {
                    if (pl.Game.PledgeSurvive) {
                        pl.SendMessage("You received &a5 %3" + Server.moneys +
                                       "%s for successfully pledging that you would survive.");
                        pl.money += 5;
                        pl.OnMoneyChanged();
                    }
                    ResetPlayer(pl, ref playersString);
                }
            }
            
            CurLevel.ChatLevel(playersString);
            online = PlayerInfo.Online.Items;
            Random rand = new Random();
            foreach (Player pl in online) {
                if (!pl.level.name.CaselessEq(CurLevelName)) continue;
                int money = GetMoney(pl, alive, rand);
                
                Player.GlobalDespawn(pl, false);
                Player.GlobalSpawn(pl, pl.pos[0], pl.pos[1], pl.pos[2], pl.rot[0], pl.rot[1], false);
                if (money == -1) {
                    pl.SendMessage("You may not hide inside a block! No " + Server.moneys + " for you."); money = 0;
                } else if (money > 0) {
                    pl.SendMessage( Colors.gold + "You gained " + money + " " + Server.moneys);
                }
                
                pl.Game.BlocksLeft = 50;
                pl.Game.NumInfected = 0;
                pl.money += money;
                pl.Game.Infected = false;
                if (pl.Game.Referee) {
                    pl.SendMessage("You gained one " + Server.moneys + " because you're a ref. Would you like a medal as well?");
                    pl.money++;
                }
                pl.OnMoneyChanged();
            }
            UpdateAllPlayerStatus();
            Alive.Clear();
            Infected.Clear();
        }
        
        int GetMoney(Player pl, Player[] alive, Random rand) {
            if (pl.CheckIfInsideBlock()) return -1;
            
            if (alive.Length == 0) {
                return rand.Next(1 + pl.Game.NumInfected, 5 + pl.Game.NumInfected);
            } else if (alive.Length == 1 && !pl.Game.Infected) {
                return rand.Next(5, 10);
            } else if (alive.Length > 1 && !pl.Game.Infected) {
                return rand.Next(2, 6);
            }
            return 0;
        }

        void ResetPlayer(Player p, ref string playersString) {
            p.Game.BlocksLeft = 50;
            p.Game.Infected = false;
            p.Game.NumInfected = 0;
            
            if (p.level.name.CaselessEq(CurLevelName))
                playersString += p.color + p.DisplayName + Colors.white + ", ";
        }
        
        void ChooseNextLevel() {
            if (QueuedLevel != null) { ChangeLevel(QueuedLevel); return; }
            if (!ChangeLevels) return;
            
            try
            {
                List<string> levels = GetCandidateLevels();
                if (levels.Count <= 2 && !UseLevelList) { Server.s.Log("You must have more than 2 levels to change levels in Zombie Survival"); return; }

                if (levels.Count <= 2 && UseLevelList) { Server.s.Log("You must have more than 2 levels in your level list to change levels in Zombie Survival"); return; }

                string picked1 = "", picked2 = "";
                Random r = new Random();

            LevelChoice:
                string level = levels[r.Next(0, levels.Count)];
                string level2 = levels[r.Next(0, levels.Count)];

                if (level == lastLevel1 || level == lastLevel2 || level == CurLevelName ||
                    level2 == lastLevel1 || level2 == lastLevel2 || level2 == CurLevelName ||
                    level == picked1) {
                    goto LevelChoice;
                } else if (picked1 == "") {
                    picked1 = level; goto LevelChoice;
                } else {
                    picked2 = level2;
                }

                Level1Vote = 0; Level2Vote = 0; Level3Vote = 0;
                lastLevel1 = picked1; lastLevel2 = picked2;
                if (!Running || Status == ZombieGameStatus.LastRound) return;

                if (initialChangeLevel) {
                    Server.votingforlevel = true;
                    Player[] players = PlayerInfo.Online.Items;
                    foreach (Player pl in players) {
                        if (pl.level != CurLevel) continue;
                        SendVoteMessage(pl, picked1, picked2);
                    }
                    System.Threading.Thread.Sleep(15000);
                    Server.votingforlevel = false;
                } else { Level1Vote = 1; Level2Vote = 0; Level3Vote = 0; }

                if (!Running || Status == ZombieGameStatus.LastRound) return;
                MoveToNextLevel(r, levels, picked1, picked2);
            } catch (Exception ex) {
                Server.ErrorLog(ex);
            }
        }
        
        void MoveToNextLevel(Random r, List<string> levels, string picked1, string picked2) {
            if (Level1Vote >= Level2Vote) {
                if (Level3Vote > Level1Vote && Level3Vote > Level2Vote) {
                    ChangeLevel(levels[r.Next(0, levels.Count)]);
                } else {
                    ChangeLevel(picked1);
                }
            } else {
                if (Level3Vote > Level1Vote && Level3Vote > Level2Vote) {
                    ChangeLevel(levels[r.Next(0, levels.Count)]);
                } else {
                    ChangeLevel(picked2);
                }
            }
            Player[] online = PlayerInfo.Online.Items;
            foreach (Player pl in online)
                pl.voted = false;
        }
        
        List<string> GetCandidateLevels() {
            if (UseLevelList) return LevelList;
            
            List<string> maps = new List<string>();
            DirectoryInfo di = new DirectoryInfo("levels/");
            FileInfo[] fi = di.GetFiles("*.lvl");
            foreach (FileInfo fil in fi)
                maps.Add(fil.Name.Split('.')[0]);
            return maps;
        }
        
        void SendVoteMessage(Player p, string lvl1, string lvl2) {
            const string line1 = "&eLevel vote - type &a1&e, &c2&e or &93";
            string line2 = "&eLevels: &a" + lvl1 + "&e, &c" + lvl2 + "&e, &9random";
            if (p.HasCpeExt(CpeExt.MessageTypes)) {
                p.SendCpeMessage(CpeMessageType.BottomRight2, line1, true);
                p.SendCpeMessage(CpeMessageType.BottomRight1, line2, true);
            } else {
                p.SendMessage(line1, true);
                p.SendMessage(line2, true);
            }
        }
    }
}
