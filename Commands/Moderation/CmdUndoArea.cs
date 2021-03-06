﻿/*
    Copyright 2011 MCForge
        
    Dual-licensed under the Educational Community License, Version 2.0 and
    the GNU General Public License, Version 3 (the "Licenses"); you may
    not use this file except in compliance with the Licenses. You may
    obtain a copy of the Licenses at
    
    http://www.opensource.org/licenses/ecl2.php
    http://www.gnu.org/licenses/gpl-3.0.html
    
    Unless required by applicable law or agreed to in writing,
    software distributed under the Licenses are distributed on an "AS IS"
    BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express
    or implied. See the Licenses for the specific language governing
    permissions and limitations under the Licenses.
 */
using System;
using System.Collections.Generic;
using MCGalaxy.Drawing;
using MCGalaxy.Drawing.Ops;
using MCGalaxy.Util;

namespace MCGalaxy.Commands {
    
    public sealed class CmdUndoArea : Command {
        public override string name { get { return "undoarea"; } }
        public override string shortcut { get { return "ua"; } }
        public override string type { get { return CommandTypes.Building; } }
        public override bool museumUsable { get { return true; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Operator; } }
        
        public override void Use(Player p, string message) {
            CatchPos cpos = default(CatchPos);
            if (p == null) { MessageInGameOnly(p); return; }
            if (message == "") { Player.SendMessage(p, "You need to provide a player name."); return; }
            
            string[] parts = message.Split(' ');
            cpos.message = parts[0];
            cpos.seconds = CmdUndo.GetSeconds(p, null, parts.Length > 1 ? parts[1] : "30");
            
            Player.SendMessage(p, "Place two blocks to determine the edges.");           
            p.ClearBlockchange();
            p.blockchangeObject = cpos;
            p.Blockchange += new Player.BlockchangeEventHandler(Blockchange1);
        }
        
        void Blockchange1(Player p, ushort x, ushort y, ushort z, byte type, byte extType) {
            RevertAndClearState(p, x, y, z);
            CatchPos cpos = (CatchPos)p.blockchangeObject;
            cpos.x = x; cpos.y = y; cpos.z = z;
            p.blockchangeObject = cpos;
            p.Blockchange += new Player.BlockchangeEventHandler(Blockchange2);
        }
        
        void Blockchange2(Player p, ushort x, ushort y, ushort z, byte type, byte extType) {
            RevertAndClearState(p, x, y, z);
            CatchPos cpos = (CatchPos)p.blockchangeObject;           
            Player who = PlayerInfo.Find(cpos.message);

            if (who != null)
                UndoOnlinePlayer(p, who, cpos, x, y, z);
            else
                UndoOfflinePlayer(p, cpos.message, cpos, x, y, z);
        }
        
        void UndoOnlinePlayer(Player p, Player who, CatchPos cpos, ushort x, ushort y, ushort z) {
            if (p != who && who.group.Permission >= p.group.Permission) {
                Player.SendMessage(p, "Cannot undo a user of higher or equal rank"); return;
            }
            
            UndoOnlineDrawOp op = new UndoOnlineDrawOp();
            op.Start = DateTime.UtcNow.AddTicks(-cpos.seconds * TimeSpan.TicksPerSecond);
            op.who = who;
            DrawOp.DoDrawOp(op, null, p, cpos.x, cpos.y, cpos.z, x, y, z);
            
            Level saveLevel = op.saveLevel;
            Player.SendChatFrom(who, who.color + who.DisplayName + 
                                "%S's actions for the past &b" + cpos.seconds + " seconds were undone.", false);
            Server.s.Log(who.name + "'s actions for the past " + cpos.seconds + " seconds were undone.");
            if (saveLevel != null) saveLevel.Save(true);
        }
        
        void UndoOfflinePlayer(Player p, string whoName, CatchPos cpos, ushort x, ushort y, ushort z) {
            Group group = Group.findPlayerGroup(whoName);
            if (group.Permission >= p.group.Permission) {
                Player.SendMessage(p, "Cannot undo a user of higher or equal rank"); return;
            }
            
            UndoOfflineDrawOp op = new UndoOfflineDrawOp();
            op.Start = DateTime.UtcNow.AddTicks(-cpos.seconds * TimeSpan.TicksPerSecond);
            op.whoName = whoName;
            DrawOp.DoDrawOp(op, null, p, cpos.x, cpos.y, cpos.z, x, y, z);

            if (op.foundUser) {
                Player.GlobalMessage(group.color + whoName + 
                                     "%S's actions for the past &b" + cpos.seconds + " %Sseconds were undone.");
                Server.s.Log(whoName + "'s actions for the past " + cpos.seconds + " seconds were undone.");
                p.level.Save(true);
            } else {
                Player.SendMessage(p, "Could not find player specified.");
            }
        }
        
        struct CatchPos { public ushort x, y, z; public string message; public long seconds; }

        public override void Help(Player p) {
            Player.SendMessage(p, "/undoarea [player] [seconds] - Undoes the blockchanges made by [player] in the previous [seconds].");
            if (p == null || (p.group.maxUndo <= 500000 || p.group.maxUndo == 0))
                Player.SendMessage(p, "/undoarea [player] all - &cWill undo 68 years, 18 days, 15 hours, 28 minutes, 31 seconds for [player]");
        }
    }
}
