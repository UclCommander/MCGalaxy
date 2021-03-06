﻿/*
    Copyright 2015 MCGalaxy
    
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
using MCGalaxy.BlockBehaviour;
using MCGalaxy.BlockPhysics;

namespace MCGalaxy {
    
    public sealed partial class Block {
        
        /// <summary> Returns whether this block handles the player placing a block at the given coordinates. </summary>
        /// <remarks>If this returns true, the usual 'dirt/grass below' behaviour and 'adding to the BlockDB' is skipped. </remarks>
        public delegate bool HandleDelete(Player p, byte block, ushort x, ushort y, ushort z);
        internal static HandleDelete[] deleteHandlers = new HandleDelete[256];
        
        /// <summary> Returns whether this block handles the player deleting a block at the given coordinates. </summary>
        /// <remarks>If this returns true, the usual 'checking dirt/grass below' and 'adding to the BlockDB' is skipped. </remarks>
        public delegate bool HandlePlace(Player p, byte block, ushort x, ushort y, ushort z);
        internal static HandlePlace[] placeHandlers = new Block.HandlePlace[256];
        
        /// <summary> Returns whether this block handles the player walking through this block at the given coordinates. </summary>
        /// <remarks>If this returns true, the usual 'death check' behaviour is skipped. </remarks>
        public delegate bool HandleWalkthrough(Player p, byte block, ushort x, ushort y, ushort z);
        internal static HandleWalkthrough[] walkthroughHandlers = new Block.HandleWalkthrough[256];
        
        /// <summary> Called to handle the physics for this particular block. </summary>
        /// <remarks>If this returns true, the usual 'death check' behaviour is skipped. </remarks>
        public delegate void HandlePhysics(Level lvl, Check C);
        internal static HandlePhysics[] physicsHandlers = new Block.HandlePhysics[256];
        internal static HandlePhysics[] physicsDoorsHandlers = new Block.HandlePhysics[256];
        
        static void SetupCoreHandlers() {
            deleteHandlers[Block.rocketstart] = DeleteBehaviour.RocketStart;
            deleteHandlers[Block.firework] = DeleteBehaviour.Firework;
            walkthroughHandlers[Block.checkpoint] = WalkthroughBehaviour.Checkpoint;
            deleteHandlers[Block.c4det] = DeleteBehaviour.C4Det;
            placeHandlers[Block.dirt] = PlaceBehaviour.Dirt;
            placeHandlers[Block.staircasestep] = PlaceBehaviour.Stairs;
            
            for (int i = 0; i < 256; i++) {
                if (Block.mb((byte)i)) {
                    walkthroughHandlers[i] = WalkthroughBehaviour.MessageBlock;
                    deleteHandlers[i] = WalkthroughBehaviour.MessageBlock;
                } else if (Block.portal((byte)i)) {
                    walkthroughHandlers[i] = WalkthroughBehaviour.Portal;
                    deleteHandlers[i] = WalkthroughBehaviour.Portal;
                }
                
                byte doorAir = Block.DoorAirs((byte)i); // if not 0, means i is a door block
                if (Block.tDoor((byte)i)) {
                    deleteHandlers[i] = DeleteBehaviour.RevertDoor;
                } else if (Block.odoor((byte)i) != Block.Zero) {
                    deleteHandlers[i] = DeleteBehaviour.ODoor;
                } else if (doorAir != 0) {
                    deleteHandlers[doorAir] = DeleteBehaviour.RevertDoor;
                    deleteHandlers[i] = DeleteBehaviour.Door;
                }
            }
            SetupCorePhysicsHandlers();
        }
        
        static void SetupCorePhysicsHandlers() {
            physicsHandlers[Block.birdblack] = BirdPhysics.Do;
            physicsHandlers[Block.birdwhite] = BirdPhysics.Do;
            physicsHandlers[Block.birdlava] = BirdPhysics.Do;
            physicsHandlers[Block.birdwater] = BirdPhysics.Do;
            physicsHandlers[Block.birdred] = (lvl, C) => HunterPhysics.DoKiller(lvl, C, Block.air);
            physicsHandlers[Block.birdblue] = (lvl, C) => HunterPhysics.DoKiller(lvl, C, Block.air);
            physicsHandlers[Block.birdkill] = (lvl, C) => HunterPhysics.DoKiller(lvl, C, Block.air);
            
            physicsHandlers[Block.snaketail] = SnakePhysics.DoTail;
            physicsHandlers[Block.snake] = SnakePhysics.Do;
            physicsHandlers[Block.rockethead] = RocketPhysics.Do;
            physicsHandlers[Block.firework] = FireworkPhysics.Do;
            physicsHandlers[Block.zombiebody] = ZombiePhysics.Do;
            physicsHandlers[Block.zombiehead] = ZombiePhysics.DoHead;
            physicsHandlers[Block.creeper] = ZombiePhysics.Do;
            physicsHandlers[Block.c4] = C4Physics.DoC4;
            physicsHandlers[Block.c4det] = C4Physics.DoC4Det;

            physicsHandlers[Block.fishbetta] = (lvl, C) => HunterPhysics.DoKiller(lvl, C, Block.water);
            physicsHandlers[Block.fishshark] = (lvl, C) => HunterPhysics.DoKiller(lvl, C, Block.water);
            physicsHandlers[Block.fishlavashark] = (lvl, C) => HunterPhysics.DoKiller(lvl, C, Block.lava);
            physicsHandlers[Block.fishgold] = (lvl, C) => HunterPhysics.DoFlee(lvl, C, Block.water);
            physicsHandlers[Block.fishsalmon] = (lvl, C) => HunterPhysics.DoFlee(lvl, C, Block.water);
            physicsHandlers[Block.fishsponge] = (lvl, C) => HunterPhysics.DoFlee(lvl, C, Block.water);
            
            physicsHandlers[Block.water] = SimpleLiquidPhysics.DoWater;
            physicsHandlers[Block.activedeathwater] = SimpleLiquidPhysics.DoWater;
            physicsHandlers[Block.lava] = SimpleLiquidPhysics.DoLava;
            physicsHandlers[Block.activedeathlava] = SimpleLiquidPhysics.DoLava;
            physicsHandlers[Block.WaterDown] = ExtLiquidPhysics.DoWaterfall;
            physicsHandlers[Block.LavaDown] = ExtLiquidPhysics.DoLavafall;
            physicsHandlers[Block.WaterFaucet] = (lvl, C) => 
                ExtLiquidPhysics.DoFaucet(lvl, C, Block.WaterDown);
            physicsHandlers[Block.LavaFaucet] = (lvl, C) => 
                ExtLiquidPhysics.DoFaucet(lvl, C, Block.LavaDown);
            physicsHandlers[Block.finiteWater] = FinitePhysics.DoWaterOrLava;
            physicsHandlers[Block.finiteLava] = FinitePhysics.DoWaterOrLava;
            physicsHandlers[Block.finiteFaucet] = FinitePhysics.DoFaucet;
            physicsHandlers[Block.magma] = ExtLiquidPhysics.DoMagma;
            physicsHandlers[Block.geyser] = ExtLiquidPhysics.DoGeyser;
            physicsHandlers[Block.lava_fast] = SimpleLiquidPhysics.DoFastLava;
            physicsHandlers[Block.fastdeathlava] = SimpleLiquidPhysics.DoFastLava;
            
            physicsHandlers[Block.air] = AirPhysics.DoAir;
            physicsHandlers[Block.dirt] = OtherPhysics.DoDirt;
            physicsHandlers[Block.leaf] = LeafPhysics.DoLeaf;
            physicsHandlers[Block.shrub] = OtherPhysics.DoShrub;
            physicsHandlers[Block.fire] = FirePhysics.Do;
            physicsHandlers[Block.sand] = OtherPhysics.DoFalling;
            physicsHandlers[Block.gravel] = OtherPhysics.DoFalling;
            physicsHandlers[Block.cobblestoneslab] = OtherPhysics.DoStairs;
            physicsHandlers[Block.staircasestep] = OtherPhysics.DoStairs;
            physicsHandlers[Block.wood_float] = OtherPhysics.DoFloatwood;

            physicsHandlers[Block.sponge] = (lvl, C) => OtherPhysics.DoSponge(lvl, C, false);
            physicsHandlers[Block.lava_sponge] = (lvl, C) => OtherPhysics.DoSponge(lvl, C, true);

            //Special blocks that are not saved
            physicsHandlers[Block.air_flood] = (lvl, C) => 
                AirPhysics.DoFlood(lvl, C, AirFlood.Full, Block.air_flood);
            physicsHandlers[Block.air_flood_layer] = (lvl, C) => 
                AirPhysics.DoFlood(lvl, C, AirFlood.Layer, Block.air_flood_layer);
            physicsHandlers[Block.air_flood_down] = (lvl, C) => 
                AirPhysics.DoFlood(lvl, C, AirFlood.Down, Block.air_flood_down);
            physicsHandlers[Block.air_flood_up] = (lvl, C) => 
                AirPhysics.DoFlood(lvl, C, AirFlood.Up, Block.air_flood_up);
            
            physicsHandlers[Block.smalltnt] = TntPhysics.DoSmallTnt;
            physicsHandlers[Block.bigtnt] = (lvl, C) => TntPhysics.DoLargeTnt(lvl, C, 1);
            physicsHandlers[Block.nuketnt] = (lvl, C) => TntPhysics.DoLargeTnt(lvl, C, 4);
            physicsHandlers[Block.tntexplosion] = TntPhysics.DoTntExplosion;
            physicsHandlers[Block.train] = TrainPhysics.Do;
            
            for (int i = 0; i < 256; i++) {
                //Adv physics updating anything placed next to water or lava
                if ((i >= Block.red && i <= Block.redmushroom) || i == Block.wood ||
                    i == Block.trunk || i == Block.bookcase) {
                    physicsHandlers[i] = OtherPhysics.DoOther;
                    continue;
                }
                
                byte odoor = Block.odoor((byte)i);
                byte door = Block.DoorAirs((byte)i);
                if (odoor != Block.Zero) {
                    physicsHandlers[i] = DoorPhysics.odoorPhysics;
                    physicsDoorsHandlers[i] = DoorPhysics.odoorPhysics;
                } else if (door == Block.door_tnt_air) {
                    physicsHandlers[door] = (lvl, C) => DoorPhysics.AnyDoor(lvl, C, 4);
                    physicsDoorsHandlers[door] = (lvl, C) => DoorPhysics.AnyDoor(lvl, C, 4);
                } else if (door == Block.air_switch_air || door == Block.air_door_air) {
                    physicsHandlers[door] = (lvl, C) => DoorPhysics.AnyDoor(lvl, C, 4, true);
                    physicsDoorsHandlers[door] = (lvl, C) => DoorPhysics.AnyDoor(lvl, C, 4, true);
                } else if (door != Block.air) {
                    physicsHandlers[door] = (lvl, C) => DoorPhysics.AnyDoor(lvl, C, 16);
                    physicsDoorsHandlers[door] = (lvl, C) => DoorPhysics.AnyDoor(lvl, C, 16);
                }
            }
        }
    }
}
