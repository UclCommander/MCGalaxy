/*
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
using MCGalaxy.Drawing;
using MCGalaxy.Drawing.Brushes;
using MCGalaxy.Drawing.Ops;

namespace MCGalaxy.Commands {
    
    public sealed class CmdTree : Command {
        public override string name { get { return "tree"; } }
        public override string shortcut { get { return ""; } }
        public override string type { get { return CommandTypes.Building; } }
        public override bool museumUsable { get { return false; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Builder; } }
        static char[] trimChars = {' '};

        public override void Use(Player p, string message) {
            if (p == null) { MessageInGameOnly(p); return; }
            int mode = TreeDrawOp.T_Tree;
            string[] parts = message.Split(trimChars, 2);
            string brushMsg = parts.Length >= 2 ? parts[1] : "";
            
            switch (parts[0].ToLower()) {
                case "1":
                    case "fern": mode = TreeDrawOp.T_Tree; break;
                case "2":
                    case "cactus": mode = TreeDrawOp.T_Cactus; break;
                case "3":
                    case "notch": mode = TreeDrawOp.T_NotchTree; break;
                case "4":
                    case "swamp": mode = TreeDrawOp.T_NotchSwamp; break;
                    default: brushMsg = message; break;
            }
            
            CatchPos cpos = default(CatchPos);
            cpos.mode = mode;
            cpos.brushMsg = brushMsg;
            p.ClearBlockchange();
            p.blockchangeObject = cpos;
            p.Blockchange += new Player.BlockchangeEventHandler(PlaceBlock1);
            Player.SendMessage(p, "Select where you wish your tree to grow");
        }

        void PlaceBlock1(Player p, ushort x, ushort y, ushort z, byte type, byte extType) {
            RevertAndClearState(p, x, y, z);
            CatchPos cpos = (CatchPos)p.blockchangeObject;
            type = type < 128 ? p.bindings[type] : type;
            
            TreeDrawOp op = new TreeDrawOp();
            op.Type = cpos.mode;
            op.random = p.random;
            op.overwrite = true;
            Brush brush = null;
            
            if (cpos.brushMsg != "") {
                if (!p.group.CanExecute("brush")) {
                    Player.SendMessage(p, "You cannot use /brush, so therefore cannot use /tree with a brush."); return;
                }
                brush = ParseBrush(cpos.brushMsg, p, type, extType);
                if (brush == null) return;
            }
            
            Vec3U16[] marks = { new Vec3U16(x, y, z) };
            if (!DrawOp.DoDrawOp(op, brush, p, marks))
                return;
            if (p.staticCommands)
                p.Blockchange += new Player.BlockchangeEventHandler(PlaceBlock1);
        }
        
        static Brush ParseBrush(string brushMsg, Player p, byte type, byte extType) {
            string[] parts = brushMsg.Split(trimChars, 2);
            string brushName = CmdBrush.FindBrush(parts[0]);
            if (brushName == null) {
                Player.SendMessage(p, "No brush found with name \"" + parts[0] + "\".");
                Player.SendMessage(p, "Available brushes: " + CmdBrush.AvailableBrushes);
                return null;
            }

            string brushMessage = parts.Length >= 2 ? parts[1].ToLower() : "";
            BrushArgs args = new BrushArgs(p, brushMessage, type, extType);
            return Brush.Brushes[brushName](args);
        }
        
        struct CatchPos { public int mode; public string brushMsg; }

        public override void Help(Player p) {
            Player.SendMessage(p, "%T/tree [type] %H- Draws a tree.");
            Player.SendMessage(p, "%HTypes - &fFern/1, Cactus/2, Notch/3, Swamp/4");
            Player.SendMessage(p, "%T/tree [type] [brush name] <brush args>");
			Player.SendMessage(p, "   %HFor help about brushes, type %T/help brush%H.");
        }
    }
}
