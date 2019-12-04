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
