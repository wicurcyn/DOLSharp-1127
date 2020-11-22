/*
 * DAWN OF LIGHT - The first free open source DAoC server emulator
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
 *
 */

using DOL.Database;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;

namespace DOL.GS.PacketHandler
{
    [PacketLib(1126, GameClient.eClientVersion.Version1126)]
    public class PacketLib1126 : PacketLib1125
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // <summary>
        /// Constructs a new PacketLib for Client Version 1.126
        /// </summary>
        /// <param name="client">the gameclient this lib is associated with</param>
        public PacketLib1126(GameClient client)
            : base(client)
        {
        }

        /// <summary>
        /// 1126 update - less info / shorter packet sent back
        /// </summary>        
        public override void SendDupNameCheckReply(string name, byte result)
        {
            if (GameClient == null || GameClient.Account == null)
                return;

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.DupNameCheckReply)))
            {
                pak.FillString(name, 24);
                pak.WriteByte(result);
                SendTCP(pak);
            }
        }

        /// <summary>
        /// 1126 update - new packet Id 0xFC, and a few changes but very similar to old 0xFD packet.
        /// </summary>        
        public override void SendCharacterOverview(eRealm realm)
        {
            if (realm < eRealm._FirstPlayerRealm || realm > eRealm._LastPlayerRealm)
            {
                throw new Exception("CharacterOverview requested for unknown realm " + realm);
            }

            int firstSlot = (byte)realm * 100;

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.CharacterOverview1126)))
            {
                if (GameClient.Account.Characters == null)
                {
                    pak.Fill(0x0, 26); // TEST
                }
                else
                {
                    pak.WriteByte(0x01); // No idea
                    pak.Fill(0, 15); // TEST
                    Dictionary<int, DOLCharacters> charsBySlot = new Dictionary<int, DOLCharacters>();
                    foreach (DOLCharacters c in GameClient.Account.Characters)
                    {
                        try
                        {
                            charsBySlot.Add(c.AccountSlot, c);
                        }
                        catch (Exception ex)
                        {
                            log.Error("SendCharacterOverview - Duplicate char in slot? Slot: " + c.AccountSlot + ", Account: " + c.AccountName, ex);
                        }
                    }
                    var itemsByOwnerID = new Dictionary<string, Dictionary<eInventorySlot, InventoryItem>>();

                    if (charsBySlot.Any())
                    {
                        var allItems = GameServer.Database.SelectObjects<InventoryItem>("`OwnerID` = @OwnerID AND `SlotPosition` >= @MinEquipable AND `SlotPosition` <= @MaxEquipable",
                                                                                        charsBySlot.Select(kv => new[] { new QueryParameter("@OwnerID", kv.Value.ObjectId), new QueryParameter("@MinEquipable", (int)eInventorySlot.MinEquipable), new QueryParameter("@MaxEquipable", (int)eInventorySlot.MaxEquipable) }))
                            .SelectMany(objs => objs);

                        foreach (InventoryItem item in allItems)
                        {
                            try
                            {
                                if (!itemsByOwnerID.ContainsKey(item.OwnerID))
                                {
                                    itemsByOwnerID.Add(item.OwnerID, new Dictionary<eInventorySlot, InventoryItem>());
                                }

                                itemsByOwnerID[item.OwnerID].Add((eInventorySlot)item.SlotPosition, item);
                            }
                            catch (Exception ex)
                            {
                                log.Error("SendCharacterOverview - Duplicate item on character? OwnerID: " + item.OwnerID + ", SlotPosition: " + item.SlotPosition + ", Account: " + GameClient.Account.Name, ex);
                            }
                        }
                    }

                    for (int i = firstSlot; i < (firstSlot + 10); i++)
                    {
                        if (!charsBySlot.TryGetValue(i, out DOLCharacters c))
                        {
                            pak.WriteByte(0);
                        }
                        else
                        {

                            if (!itemsByOwnerID.TryGetValue(c.ObjectId, out Dictionary<eInventorySlot, InventoryItem> charItems))
                            {
                                charItems = new Dictionary<eInventorySlot, InventoryItem>();
                            }

                            byte extensionTorso = 0;
                            byte extensionGloves = 0;
                            byte extensionBoots = 0;


                            if (charItems.TryGetValue(eInventorySlot.TorsoArmor, out InventoryItem item))
                            {
                                extensionTorso = item.Extension;
                            }

                            if (charItems.TryGetValue(eInventorySlot.HandsArmor, out item))
                            {
                                extensionGloves = item.Extension;
                            }

                            if (charItems.TryGetValue(eInventorySlot.FeetArmor, out item))
                            {
                                extensionBoots = item.Extension;
                            }

                            pak.WriteByte((byte)c.Level);
                            pak.WritePascalStringIntLowEndian(c.Name);
                            pak.WriteIntLowEndian(0x18); // no idea
                            pak.WriteByte(0x01); // no idea
                            pak.WriteByte((byte)c.EyeSize);
                            pak.WriteByte((byte)c.LipSize);
                            pak.WriteByte((byte)c.EyeColor);
                            pak.WriteByte((byte)c.HairColor);
                            pak.WriteByte((byte)c.FaceType);
                            pak.WriteByte((byte)c.HairStyle);
                            pak.WriteByte((byte)((extensionBoots << 4) | extensionGloves));
                            pak.WriteByte((byte)((extensionTorso << 4) | (c.IsCloakHoodUp ? 0x1 : 0x0)));
                            pak.WriteByte((byte)c.CustomisationStep); //1 = auto generate config, 2= config ended by player, 3= enable config to player
                            pak.WriteByte((byte)c.MoodType);

                            pak.Fill(0x0, 13);

                            string locationDescription = string.Empty;
                            Region region = WorldMgr.GetRegion((ushort)c.Region);
                            if (region != null)
                            {
                                locationDescription = region.GetTranslatedSpotDescription(GameClient, c.Xpos, c.Ypos, c.Zpos);
                            }
                            if (locationDescription.Length > 23) // zone names above 23 characters need to be truncated 
                            {
                                locationDescription = (locationDescription.Substring(0, 20)) + "...";
                            }
                            pak.WritePascalStringIntLowEndian(locationDescription);

                            string classname = "";
                            if (c.Class != 0)
                            {
                                classname = ((eCharacterClass)c.Class).ToString();
                            }
                            pak.WritePascalStringIntLowEndian(classname);

                            string racename = GameClient.RaceToTranslatedName(c.Race, c.Gender);

                            pak.WritePascalStringIntLowEndian(racename);
                            pak.WriteShortLowEndian((ushort)c.CurrentModel);
                            pak.WriteByte((byte)c.Region);

                            if (region == null || (int)GameClient.ClientType > region.Expansion)
                            {
                                pak.WriteByte(0x00);
                            }
                            else
                            {
                                pak.WriteByte((byte)(region.Expansion + 1)); //0x04-Cata zone, 0x05 - DR zone
                            }

                            charItems.TryGetValue(eInventorySlot.RightHandWeapon, out InventoryItem rightHandWeapon);
                            charItems.TryGetValue(eInventorySlot.LeftHandWeapon, out InventoryItem leftHandWeapon);
                            charItems.TryGetValue(eInventorySlot.TwoHandWeapon, out InventoryItem twoHandWeapon);
                            charItems.TryGetValue(eInventorySlot.DistanceWeapon, out InventoryItem distanceWeapon);
                            charItems.TryGetValue(eInventorySlot.HeadArmor, out InventoryItem helmet);
                            charItems.TryGetValue(eInventorySlot.HandsArmor, out InventoryItem gloves);
                            charItems.TryGetValue(eInventorySlot.FeetArmor, out InventoryItem boots);
                            charItems.TryGetValue(eInventorySlot.TorsoArmor, out InventoryItem torso);
                            charItems.TryGetValue(eInventorySlot.Cloak, out InventoryItem cloak);
                            charItems.TryGetValue(eInventorySlot.LegsArmor, out InventoryItem legs);
                            charItems.TryGetValue(eInventorySlot.ArmsArmor, out InventoryItem arms);

                            pak.WriteShortLowEndian((ushort)(helmet != null ? helmet.Model : 0));
                            pak.WriteShortLowEndian((ushort)(gloves != null ? gloves.Model : 0));
                            pak.WriteShortLowEndian((ushort)(boots != null ? boots.Model : 0));

                            ushort rightHandColor = 0;
                            if (rightHandWeapon != null)
                            {
                                rightHandColor = (ushort)(rightHandWeapon.Emblem != 0 ? rightHandWeapon.Emblem : rightHandWeapon.Color);
                            }
                            pak.WriteShortLowEndian(rightHandColor);

                            pak.WriteShortLowEndian((ushort)(torso != null ? torso.Model : 0));
                            pak.WriteShortLowEndian((ushort)(cloak != null ? cloak.Model : 0));
                            pak.WriteShortLowEndian((ushort)(legs != null ? legs.Model : 0));
                            pak.WriteShortLowEndian((ushort)(arms != null ? arms.Model : 0));

                            ushort helmetColor = 0;
                            if (helmet != null)
                            {
                                helmetColor = (ushort)(helmet.Emblem != 0 ? helmet.Emblem : helmet.Color);
                            }
                            pak.WriteShortLowEndian(helmetColor);

                            ushort glovesColor = 0;
                            if (gloves != null)
                            {
                                glovesColor = (ushort)(gloves.Emblem != 0 ? gloves.Emblem : gloves.Color);
                            }
                            pak.WriteShortLowEndian(glovesColor);

                            ushort bootsColor = 0;
                            if (boots != null)
                            {
                                bootsColor = (ushort)(boots.Emblem != 0 ? boots.Emblem : boots.Color);
                            }
                            pak.WriteShortLowEndian(bootsColor);

                            ushort leftHandWeaponColor = 0;
                            if (leftHandWeapon != null)
                            {
                                leftHandWeaponColor = (ushort)(leftHandWeapon.Emblem != 0 ? leftHandWeapon.Emblem : leftHandWeapon.Color);
                            }
                            pak.WriteShortLowEndian(leftHandWeaponColor);

                            ushort torsoColor = 0;
                            if (torso != null)
                            {
                                torsoColor = (ushort)(torso.Emblem != 0 ? torso.Emblem : torso.Color);
                            }
                            pak.WriteShortLowEndian(torsoColor);

                            ushort cloakColor = 0;
                            if (cloak != null)
                            {
                                cloakColor = (ushort)(cloak.Emblem != 0 ? cloak.Emblem : cloak.Color);
                            }
                            pak.WriteShortLowEndian(cloakColor);

                            ushort legsColor = 0;
                            if (legs != null)
                            {
                                legsColor = (ushort)(legs.Emblem != 0 ? legs.Emblem : legs.Color);
                            }
                            pak.WriteShortLowEndian(legsColor);

                            ushort armsColor = 0;
                            if (arms != null)
                            {
                                armsColor = (ushort)(arms.Emblem != 0 ? arms.Emblem : arms.Color);
                            }
                            pak.WriteShortLowEndian(armsColor);

                            pak.WriteShortLowEndian((ushort)(rightHandWeapon != null ? rightHandWeapon.Model : 0));
                            pak.WriteShortLowEndian((ushort)(leftHandWeapon != null ? leftHandWeapon.Model : 0));
                            pak.WriteShortLowEndian((ushort)(twoHandWeapon != null ? twoHandWeapon.Model : 0));
                            pak.WriteShortLowEndian((ushort)(distanceWeapon != null ? distanceWeapon.Model : 0));

                            pak.WriteByte((byte)c.Strength);
                            pak.WriteByte((byte)c.Dexterity);
                            pak.WriteByte((byte)c.Constitution);
                            pak.WriteByte((byte)c.Quickness);
                            pak.WriteByte((byte)c.Intelligence);
                            pak.WriteByte((byte)c.Piety);
                            pak.WriteByte((byte)c.Empathy);
                            pak.WriteByte((byte)c.Charisma);
                            pak.WriteByte((byte)c.Class);
                            pak.WriteByte((byte)c.Realm);
                            pak.WriteByte((byte)((((c.Race & 0x10) << 2) + (c.Race & 0x0F)) | (c.Gender << 4)));

                            if (c.ActiveWeaponSlot == (byte)GameLiving.eActiveWeaponSlot.TwoHanded)
                            {
                                pak.WriteByte(0x02);
                                pak.WriteByte(0x02);
                            }
                            else if (c.ActiveWeaponSlot == (byte)GameLiving.eActiveWeaponSlot.Distance)
                            {
                                pak.WriteByte(0x03);
                                pak.WriteByte(0x03);
                            }
                            else
                            {
                                byte righthand = 0xFF;
                                byte lefthand = 0xFF;

                                if (rightHandWeapon != null)
                                {
                                    righthand = 0x00;
                                }

                                if (leftHandWeapon != null)
                                {
                                    lefthand = 0x01;
                                }

                                pak.WriteByte(righthand);
                                pak.WriteByte(lefthand);
                            }

                            if (region == null || region.Expansion != 1)
                            {
                                pak.WriteByte(0x00);
                            }
                            else
                            {
                                pak.WriteByte(0x01); //0x01=char in SI zone, classic client can't "play"
                            }

                            pak.WriteByte((byte)c.Constitution);
                            pak.WriteByte(0); // Null terminated
                        }
                    }
                }

                SendTCP(pak);
            }
        }

        /// <summary>
        /// Gutted this packet to get 1126 connections to work. Definitely needs more research
        /// </summary>
        public override void SendRegions()
        {            
            if (!GameClient.Socket.Connected)
            {
                return;
            }

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.StartArena)))
            {                
                string ip = ip = ((IPEndPoint)GameClient.Socket.LocalEndPoint).Address.ToString();
                
                pak.WritePascalStringIntLowEndian(ip);
                pak.WriteShort(10400); // from port?
                pak.WriteByte(0); // ??
                pak.WriteByte(0);  // ??
                pak.WriteShort(10400);  // ?? to port?
                pak.WriteByte(0);  // ??
                pak.WriteByte(0);  // ??
                SendTCP(pak);
            }           
        }

        /// <summary>
        /// This packet may have been updated anywhere from 1125b-1126a - not sure
        /// </summary>
        public override void SendUpdateWeaponAndArmorStats()
        {
            if (GameClient.Player == null)
            {
                return;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.VariousUpdate)))
            {
                pak.WriteByte(0x05); //subcode
                pak.WriteByte(6); //number of entries
                pak.WriteByte(0x00); //subtype
                pak.WriteByte(0x00); //unk

                // weapondamage
                var wd = (int)(GameClient.Player.WeaponDamage(GameClient.Player.AttackWeapon) * 100.0);
                pak.WriteByte((byte)(wd / 100));
                pak.WriteByte(0x00);
                pak.WriteByte((byte)(wd % 100));
                pak.WriteByte(0x00);
                // weaponskill
                int ws = GameClient.Player.DisplayedWeaponSkill;
                pak.WriteByte((byte)(ws >> 8));
                pak.WriteByte(0x00);
                pak.WriteByte((byte)(ws & 0xff));
                pak.WriteByte(0x00);
                // overall EAF
                int eaf = GameClient.Player.EffectiveOverallAF;
                pak.WriteByte((byte)(eaf >> 8));
                pak.WriteByte(0x00);
                pak.WriteByte((byte)(eaf & 0xff));
                pak.WriteByte(0x00);
                SendTCP(pak);
            }
        }
    }
}
