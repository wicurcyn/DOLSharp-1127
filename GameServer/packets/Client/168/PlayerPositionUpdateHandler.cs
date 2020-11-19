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

using System;
using System.Reflection;
using System.Text;
using DOL.Database;
using DOL.Language;
using log4net;

namespace DOL.GS.PacketHandler.Client.v168
{
	[PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.PositionUpdate, "Handles player position updates for client 1.124+", eClientStatus.PlayerInGame)]
	public class PlayerPositionUpdateHandler : IPacketHandler
	{		

		/// <summary>
		/// Defines a logger for this class.
		/// </summary>
		private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		public const string LASTMOVEMENTTICK = "PLAYERPOSITION_LASTMOVEMENTTICK";
		public const string LASTCPSTICK = "PLAYERPOSITION_LASTCPSTICK";

		/// <summary>
		/// Stores the count of times the player is above speedhack tolerance!
		/// If this value reaches 10 or more, a logfile entry is written.
		/// </summary>
		public const string SPEEDHACKCOUNTER = "SPEEDHACKCOUNTER";
		public const string SHSPEEDCOUNTER = "MYSPEEDHACKCOUNTER";			
		
		public void HandlePacket(GameClient client, GSPacketIn packet)
		{
			//Tiv: in very rare cases client send 0xA9 packet before sending S<=C 0xE8 player world initialize
			if ((client.Player.ObjectState != GameObject.eObjectState.Active) ||
			    (client.ClientState != GameClient.eClientState.Playing))
				return;

			int environmentTick = Environment.TickCount;
			int oldSpeed = client.Player.CurrentSpeed;
						
			// Getting ugly in this file - supporting multiple clients
			int newPlayerX;
			int newPlayerY;
			int newPlayerZ;
			int newPlayerSpeed;
			int newPlayerZSpeed;
			ushort sessionID;			
			ushort currentZoneID;
			ushort playerState;
			ushort fallingDMG;
			ushort newHeading;
			byte playerAction;			
			byte playerHealth;			
			ushort ObjectId1127 = 0;

			if (client.Version >= GameClient.eClientVersion.Version1127)
			{
				newPlayerX = (int)packet.ReadFloatLowEndian();
				newPlayerY = (int)packet.ReadFloatLowEndian();
				newPlayerZ = (int)packet.ReadFloatLowEndian();
				newPlayerSpeed = (int)packet.ReadFloatLowEndian();
				newPlayerZSpeed = (int)packet.ReadFloatLowEndian();
				sessionID = packet.ReadShort();
				ObjectId1127 = packet.ReadShort(); // new
				currentZoneID = packet.ReadShort();
				playerState = packet.ReadShort();
				fallingDMG = packet.ReadShort();
				newHeading = packet.ReadShort();
				playerAction = (byte)packet.ReadByte();
				packet.Skip(2);
				playerHealth = (byte)packet.ReadByte();
				// four trailing bytes, no data
				packet.Skip(4); // extra 2 bytes
			}
			else
			{
				newPlayerX = (int)packet.ReadFloatLowEndian();
				newPlayerY = (int)packet.ReadFloatLowEndian();
				newPlayerZ = (int)packet.ReadFloatLowEndian();
				newPlayerSpeed = (int)packet.ReadFloatLowEndian();
				newPlayerZSpeed = (int)packet.ReadFloatLowEndian();
				sessionID = packet.ReadShort();
				currentZoneID = packet.ReadShort();
				playerState = packet.ReadShort();
				fallingDMG = packet.ReadShort();
				newHeading = packet.ReadShort();
				playerAction = (byte)packet.ReadByte();
				packet.Skip(2);
				playerHealth = (byte)packet.ReadByte();
				// two trailing bytes, no data
			}

			if (client.Player.IsMezzed || client.Player.IsStunned)
			{				
				client.Player.CurrentSpeed = 0;
			}
			else
			{
				client.Player.CurrentSpeed = (short)newPlayerSpeed;
			}
                        
            client.Player.IsJumping = ((playerAction & 0x40) != 0);
            client.Player.IsStrafing = ((playerState & 0xe000) != 0);            
           
            Zone newZone = WorldMgr.GetZone(currentZoneID);
			if (newZone == null)
			{
                if (client.Player == null)
                {
                    return;
                }
				if (!client.Player.TempProperties.getProperty("isbeingbanned", false))
				{
                    if (log.IsErrorEnabled)
                    {
                        log.Error(client.Player.Name + "'s position in unknown zone! => " + currentZoneID);
                    }
					GamePlayer player = client.Player;
					player.TempProperties.setProperty("isbeingbanned", true);
					player.MoveToBind();
				}

				return;
			}

			// move to bind if player fell through the floor
			if (newPlayerZ == 0)
			{
				client.Player.MoveTo(
					(ushort)client.Player.BindRegion,
					client.Player.BindXpos,
					client.Player.BindYpos,
					(ushort)client.Player.BindZpos,
					(ushort)client.Player.BindHeading
				);
				return;
			}
			
			bool zoneChange = newZone != client.Player.LastPositionUpdateZone;
			if (zoneChange)
			{
				//If the region changes -> make sure we don't take any falling damage
				if (client.Player.LastPositionUpdateZone != null && newZone.ZoneRegion.ID != client.Player.LastPositionUpdateZone.ZoneRegion.ID)
				{
					client.Player.MaxLastZ = int.MinValue;
				}
				// Update water level and diving flag for the new zone
				// commenting this out for now, creates a race condition when teleporting within same region, jumping player back and forth as player xyz isnt updated yet.
				//client.Out.SendPlayerPositionAndObjectID();				

				/*
				 * "You have entered Burial Tomb."
				 * "Burial Tomb"
				 * "Current area is adjusted for one level 1 player."
				 * "Current area has a 50% instance bonus."
				 */

                string description = newZone.Description;
                string screenDescription = description;

                if (client.GetTranslation(newZone) is DBLanguageZone translation)
                {
                    if (!Util.IsEmpty(translation.Description))
                    {
                        description = translation.Description;
                    }
                    if (!Util.IsEmpty(translation.ScreenDescription))
                    {
                        screenDescription = translation.ScreenDescription;
                    }
                }

                client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "PlayerPositionUpdateHandler.Entered", description),
				                       eChatType.CT_System, eChatLoc.CL_SystemWindow);
                client.Out.SendMessage(screenDescription, eChatType.CT_ScreenCenterSmaller, eChatLoc.CL_SystemWindow);

				client.Player.LastPositionUpdateZone = newZone;
			}

			int coordsPerSec = 0;
			int jumpDetect = 0;
			int timediff = Environment.TickCount - client.Player.LastPositionUpdateTick;
			int distance = 0;

			if (timediff > 0)
			{
				distance = client.Player.LastPositionUpdatePoint.GetDistanceTo(new Point3D(newPlayerX, newPlayerY, newPlayerZ));
				coordsPerSec = distance * 1000 / timediff;

				if (distance < 100 && client.Player.LastPositionUpdatePoint.Z > 0)
				{
					jumpDetect = newPlayerZ - client.Player.LastPositionUpdatePoint.Z;
				}
			}			

			client.Player.LastPositionUpdateTick = Environment.TickCount;
			client.Player.LastPositionUpdatePoint.X = newPlayerX;
			client.Player.LastPositionUpdatePoint.Y = newPlayerY;
			client.Player.LastPositionUpdatePoint.Z = newPlayerZ;

			int tolerance = ServerProperties.Properties.CPS_TOLERANCE;

			if (client.Player.Steed != null && client.Player.Steed.MaxSpeed > 0)
			{
				tolerance += client.Player.Steed.MaxSpeed;
			}
			else if (client.Player.MaxSpeed > 0)
			{
				tolerance += client.Player.MaxSpeed;
			}

			if (client.Player.IsJumping)
			{
				coordsPerSec = 0;
				jumpDetect = 0;
				client.Player.IsJumping = false;
			}

			if (!client.Player.IsAllowedToFly && (coordsPerSec > tolerance || jumpDetect > ServerProperties.Properties.JUMP_TOLERANCE))
			{
				bool isHackDetected = true;

				if (coordsPerSec > tolerance)
				{
					// check to see if CPS time tolerance is exceeded
					int lastCPSTick = client.Player.TempProperties.getProperty<int>(LASTCPSTICK, 0);

					if (environmentTick - lastCPSTick > ServerProperties.Properties.CPS_TIME_TOLERANCE)
					{
						isHackDetected = false;
					}
				}

				if (isHackDetected)
				{
					StringBuilder builder = new StringBuilder();
					builder.Append("MOVEHACK_DETECT");
					builder.Append(": CharName=");
					builder.Append(client.Player.Name);
					builder.Append(" Account=");
					builder.Append(client.Account.Name);
					builder.Append(" IP=");
					builder.Append(client.TcpEndpointAddress);
					builder.Append(" CPS:=");
					builder.Append(coordsPerSec);
					builder.Append(" JT=");
					builder.Append(jumpDetect);
					ChatUtil.SendDebugMessage(client, builder.ToString());

					if (client.Account.PrivLevel == 1)
					{
						GameServer.Instance.LogCheatAction(builder.ToString());

						if (ServerProperties.Properties.ENABLE_MOVEDETECT)
						{
							if (ServerProperties.Properties.BAN_HACKERS && false) // banning disabled until this technique is proven accurate
							{
								DBBannedAccount b = new DBBannedAccount();
								b.Author = "SERVER";
								b.Ip = client.TcpEndpointAddress;
								b.Account = client.Account.Name;
								b.DateBan = DateTime.Now;
								b.Type = "B";
								b.Reason = string.Format("Autoban MOVEHACK:(CPS:{0}, JT:{1}) on player:{2}", coordsPerSec, jumpDetect, client.Player.Name);
								GameServer.Database.AddObject(b);
								GameServer.Database.SaveObject(b);

								string message = "";
								
								message = "You have been auto kicked and banned due to movement hack detection!";
								for (int i = 0; i < 8; i++)
								{
									client.Out.SendMessage(message, eChatType.CT_Help, eChatLoc.CL_SystemWindow);
									client.Out.SendMessage(message, eChatType.CT_Help, eChatLoc.CL_ChatWindow);
								}

								client.Out.SendPlayerQuit(true);
								client.Player.SaveIntoDatabase();
								client.Player.Quit(true);
							}
							else
							{
								string message = "";
								
								message = "You have been auto kicked due to movement hack detection!";
								for (int i = 0; i < 8; i++)
								{
									client.Out.SendMessage(message, eChatType.CT_Help, eChatLoc.CL_SystemWindow);
									client.Out.SendMessage(message, eChatType.CT_Help, eChatLoc.CL_ChatWindow);
								}

								client.Out.SendPlayerQuit(true);
								client.Player.SaveIntoDatabase();
								client.Player.Quit(true);
							}
							client.Disconnect();
							return;
						}
					}
				}

				client.Player.TempProperties.setProperty(LASTCPSTICK, environmentTick);
			}			

			if (client.Player.X != newPlayerX || client.Player.Y != newPlayerY)
			{
				client.Player.TempProperties.setProperty(LASTMOVEMENTTICK, client.Player.CurrentRegion.Time);
			}            

            client.Player.SetCoords(newPlayerX, newPlayerY, newPlayerZ, (ushort)(newHeading & 0xFFF));
            
			// used to predict current position, should be before
			// any calculation (like fall damage)			

			// Begin ---------- New Area System -----------
			if (client.Player.CurrentRegion.Time > client.Player.AreaUpdateTick) // check if update is needed
			{
				var oldAreas = client.Player.CurrentAreas;

				// Because we may be in an instance we need to do the area check from the current region
				// rather than relying on the zone which is in the skinned region.  - Tolakram

				var newAreas = client.Player.CurrentRegion.GetAreasOfZone(newZone, client.Player);

				// Check for left areas
				if (oldAreas != null)
				{
					foreach (IArea area in oldAreas)
					{
						if (!newAreas.Contains(area))
						{
							area.OnPlayerLeave(client.Player);
						}
					}
				}
				// Check for entered areas
				foreach (IArea area in newAreas)
				{
					if (oldAreas == null || !oldAreas.Contains(area))
					{
						area.OnPlayerEnter(client.Player);
					}
				}
				// set current areas to new one...
				client.Player.CurrentAreas = newAreas;
				client.Player.AreaUpdateTick = client.Player.CurrentRegion.Time + 2000; // update every 2 seconds
			}
            // End ---------- New Area System -----------
                                   
            client.Player.TargetInView = ((playerAction & 0x30) != 0);
            client.Player.GroundTargetInView = ((playerAction & 0x08) != 0);            
            client.Player.IsTorchLighted = ((playerAction & 0x80) != 0);            
            // if player has a pet summoned, player action is sent by client as 0x04, but sending to other players this is skipped
            client.Player.IsDiving = ((playerAction & 0x02) != 0);

            int state = ((playerState >> 10) & 7);
            client.Player.IsClimbing = (state == 7);
            client.Player.IsSwimming = (state == 1);            
                        
            if (state == 3 && !client.Player.TempProperties.getProperty(GamePlayer.DEBUG_MODE_PROPERTY, false) && !client.Player.IsAllowedToFly) //debugFly on, but player not do /debug on (hack)
            {
                StringBuilder builder = new StringBuilder();
                builder.Append("HACK_FLY");
                builder.Append(": CharName=");
                builder.Append(client.Player.Name);
                builder.Append(" Account=");
                builder.Append(client.Account.Name);
                builder.Append(" IP=");
                builder.Append(client.TcpEndpointAddress);
                GameServer.Instance.LogCheatAction(builder.ToString());
                {
                    if (ServerProperties.Properties.BAN_HACKERS)
                    {
                        DBBannedAccount b = new DBBannedAccount();
                        b.Author = "SERVER";
                        b.Ip = client.TcpEndpointAddress;
                        b.Account = client.Account.Name;
                        b.DateBan = DateTime.Now;
                        b.Type = "B";
                        b.Reason = string.Format("Autoban flying hack: on player:{0}", client.Player.Name);
                        GameServer.Database.AddObject(b);
                        GameServer.Database.SaveObject(b);
                    }
                    string message = "";

                    message = "Client Hack Detected!";
                    for (int i = 0; i < 6; i++)
                    {
                        client.Out.SendMessage(message, eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        client.Out.SendMessage(message, eChatType.CT_System, eChatLoc.CL_ChatWindow);
                    }
                    client.Out.SendPlayerQuit(true);
                    client.Disconnect();
                    return;
                }
            }
            lock (client.Player.LastUniqueLocations)
			{
				GameLocation[] locations = client.Player.LastUniqueLocations;
				GameLocation loc = locations[0];
				if (loc.X != newPlayerX || loc.Y != newPlayerY || loc.Z != newPlayerZ || loc.RegionID != client.Player.CurrentRegionID)
				{
					loc = locations[locations.Length - 1];
					Array.Copy(locations, 0, locations, 1, locations.Length - 1);
					locations[0] = loc;
					loc.X = newPlayerX;
					loc.Y = newPlayerY;
					loc.Z = newPlayerZ;
					loc.Heading = client.Player.Heading;
					loc.RegionID = client.Player.CurrentRegionID;
				}
			}

            //FALLING DAMAGE

            if (GameServer.ServerRules.CanTakeFallDamage(client.Player) && !client.Player.IsSwimming)
            {
                try
                {
                    int maxLastZ = client.Player.MaxLastZ;

                    // Are we on the ground?
                    if ((fallingDMG >> 15) != 0)
                    {                        
                        int safeFallLevel = client.Player.GetAbilityLevel(Abilities.SafeFall);
                        
                        int fallSpeed = (newPlayerZSpeed * -1) - (100 * safeFallLevel);
                        
                        int fallDivide = 15;

                        int fallPercent = Math.Min(99, (fallSpeed - (501)) / fallDivide);
                        
                        if (fallSpeed > 500)
                        {
                            client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "PlayerPositionUpdateHandler.FallingDamage"), eChatType.CT_Damaged, eChatLoc.CL_SystemWindow);
                            client.Out.SendMessage(string.Format("You take {0}% of you max hits in damage.", fallPercent), eChatType.CT_Damaged, eChatLoc.CL_SystemWindow);
                            client.Out.SendMessage("You lose endurance", eChatType.CT_Damaged, eChatLoc.CL_SystemWindow);
                            client.Player.CalcFallDamage(fallPercent);
                        }

                        client.Player.MaxLastZ = client.Player.Z;
                    }
                    else
                    {
                        if (maxLastZ < client.Player.Z || client.Player.IsRiding || newPlayerZSpeed > -150) // is riding, for dragonflys
                        {
                            client.Player.MaxLastZ = client.Player.Z;
                        }
                    }
                }
                catch
                {
                    log.Warn("error when attempting to calculate fall damage");
                }
            }
			
			ushort steedSeatPosition = 0;
			
            if (client.Player.Steed != null && client.Player.Steed.ObjectState == GameObject.eObjectState.Active)
            {
                client.Player.Heading = client.Player.Steed.Heading;
                newHeading = (ushort)client.Player.Steed.ObjectID;
				steedSeatPosition = (ushort)client.Player.SteedSeatPosition;                
            }
            else if ((playerState >> 10) == 4)
            {
                client.Player.IsSitting = true;
            }
            
            // Build Server to Client packet
            GSUDPPacketOut outpak = new GSUDPPacketOut(client.Out.GetPacketCode(eServerPackets.PlayerPosition));                      
                        
            byte playerOutAction = 0x00;
            if (client.Player.IsDiving)
            {
                playerOutAction |= 0x04;
            }
            if (client.Player.TargetInView)
            {
                playerOutAction |= 0x30;
            }
            if (client.Player.GroundTargetInView)
            {
                playerOutAction |= 0x08;
            }
            if (client.Player.IsTorchLighted)
            {
                playerOutAction |= 0x80;
            }            
            if (client.Player.IsStealthed)
			{
				playerOutAction |= 0x02;
			}
            
            outpak.WriteFloatLowEndian(newPlayerX);
            outpak.WriteFloatLowEndian(newPlayerY);
            outpak.WriteFloatLowEndian(newPlayerZ);
            outpak.WriteFloatLowEndian(newPlayerSpeed);
            outpak.WriteFloatLowEndian(newPlayerZSpeed);
            outpak.WriteShort(sessionID);
            outpak.WriteShort(currentZoneID);
            outpak.WriteShort(playerState);
            outpak.WriteShort(steedSeatPosition); // fall damage flag coming in, steed seat position going out
            outpak.WriteShort(newHeading);
            outpak.WriteByte(playerOutAction);
            outpak.WriteByte((byte)(client.Player.RPFlag ? 1 : 0));
            outpak.WriteByte(0);
            outpak.WriteByte((byte)(client.Player.HealthPercent + (client.Player.AttackState ? 0x80 : 0)));            
            outpak.WriteByte(client.Player.ManaPercent);
            outpak.WriteByte(client.Player.EndurancePercent);            
            outpak.WritePacketLength();

			// more ugliness to support multiple clients at once...
			GSUDPPacketOut outpak1127 = new GSUDPPacketOut(client.Out.GetPacketCode(eServerPackets.PlayerPosition));
			outpak1127.WriteFloatLowEndian(newPlayerX);
			outpak1127.WriteFloatLowEndian(newPlayerY);
			outpak1127.WriteFloatLowEndian(newPlayerZ);
			outpak1127.WriteFloatLowEndian(newPlayerSpeed);
			outpak1127.WriteFloatLowEndian(newPlayerZSpeed);
			outpak1127.WriteShort(sessionID);
			outpak1127.WriteShort(ObjectId1127); // new
			outpak1127.WriteShort(currentZoneID);
			outpak1127.WriteShort(playerState);
			outpak1127.WriteShort(steedSeatPosition);
			outpak1127.WriteShort(newHeading);
			outpak1127.WriteByte(playerOutAction);
			outpak1127.WriteByte((byte)(client.Player.RPFlag ? 1 : 0));
			outpak1127.WriteByte(0);
			outpak1127.WriteByte((byte)(client.Player.HealthPercent + (client.Player.AttackState ? 0x80 : 0)));
			outpak1127.WriteByte(client.Player.ManaPercent);
			outpak1127.WriteByte(client.Player.EndurancePercent);
			outpak1127.WriteShort(0); // new
			outpak.WritePacketLength();

			foreach (GamePlayer player in client.Player.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
			{
                if (player == null)
                {
                    continue;
                }
				//No position updates for ourselves
				if (player == client.Player)
				{
					// Update Player Cache (Client sending Packet is admitting he's already having it)
					player.Client.GameObjectUpdateArray[new Tuple<ushort, ushort>(client.Player.CurrentRegionID, (ushort)client.Player.ObjectID)] = GameTimer.GetTickCount();
					continue;
				}
                //no position updates in different houses
                if ((client.Player.InHouse || player.InHouse) && player.CurrentHouse != client.Player.CurrentHouse)
                {
                    continue;
                }
                /* no minotaur logic
				if (client.Player.MinotaurRelic != null)
				{
					MinotaurRelic relic = client.Player.MinotaurRelic;
					if (!relic.Playerlist.Contains(player) && player != client.Player)
					{
						relic.Playerlist.Add(player);
						player.Out.SendMinotaurRelicWindow(client.Player, client.Player.MinotaurRelic.Effect, true);
					}
				}*/

                if (!client.Player.IsStealthed || player.CanDetect(client.Player))
                {                    
                    if (player.Client.Version == GameClient.eClientVersion.Version1127)
                    {
						player.Out.SendUDPRaw(outpak1127);
                    }
                    else
                    {
						player.Out.SendUDPRaw(outpak);
					}					
                }
                else
                {
                    player.Out.SendObjectDelete(client.Player); //remove the stealthed player from view
                }
			}		

			//handle closing of windows
			//trade window
			if (client.Player.TradeWindow != null)
			{
				if (client.Player.TradeWindow.Partner != null)
				{
                    if (!client.Player.IsWithinRadius(client.Player.TradeWindow.Partner, WorldMgr.GIVE_ITEM_DISTANCE))
                    {
                        client.Player.TradeWindow.CloseTrade();
                    }
				}
			}
		}
	}
}
