
using System;
using System.Collections.Generic;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.Events;
using DOL.AI.Brain;

namespace DOL.GS
{
    public class HibTeleportingMob : GameNPC
    {
        private const int TELEPORT_RADIUS = 250;
        private RegionTimer _checkTimer;

        // Target teleport coordinates
        private const ushort DEST_REGION = 163;
        private const ushort DEST_ZONE = 173;
        private const int DEST_X = 396571;
        private const int DEST_Y = 618545;
        private const int DEST_Z = 9825;
        private const ushort DEST_HEADING = 1947;

        public override bool AddToWorld()
        {
            this.Model = 1;
            _checkTimer = new RegionTimer(this, new RegionTimerCallback(CheckPlayers), 2000);
            return base.AddToWorld();
        }

        public override bool RemoveFromWorld()
        {
            if (_checkTimer != null)
            {
                _checkTimer.Stop();
                _checkTimer = null;
            }
            return base.RemoveFromWorld();
        }

        private int CheckPlayers(RegionTimer timer)
        {
            foreach (GamePlayer player in this.GetPlayersInRadius(TELEPORT_RADIUS))
            {
                if (!player.IsAlive)
                    continue;

                // Ignore GMs and Admins
                if (player.Client.Account.PrivLevel > 1)
                    continue;

                // Only teleport players from Albion (Realm 1)
                if (player.Realm != eRealm.Hibernia)
                    continue;

                TeleportPlayer(player);
            }

            return 2000; // run again in 2 sec
        }

        private void TeleportPlayer(GamePlayer player)
        {
            player.MoveTo(DEST_REGION, DEST_X, DEST_Y, DEST_Z, DEST_HEADING);
            player.Out.SendMessage("You have been magically transported!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }
    }
}
