
using System;
using System.Collections.Generic;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.Database;
using DOL.Events;
using DOL.AI.Brain;

namespace DOL.GS
{
    public class PushbackMonster : GameNPC
    {
        private const int PUSHBACK_RANGE = 300;
        private const int PUSHBACK_DISTANCE = 1000;
        private RegionTimer _pushTimer;

        public override bool AddToWorld()
        {
            this.Model = 408;
            _pushTimer = new RegionTimer(this, new RegionTimerCallback(CheckPlayers), 2000); // Every 2 sec
            return base.AddToWorld();
        }

        public override bool RemoveFromWorld()
        {
            if (_pushTimer != null)
            {
                _pushTimer.Stop();
                _pushTimer = null;
            }
            return base.RemoveFromWorld();
        }

        private int CheckPlayers(RegionTimer timer)
        {
            foreach (GamePlayer player in this.GetPlayersInRadius(PUSHBACK_RANGE))
            {
                if (!player.IsAlive)
                    continue;

                // Skip admins or GMs
                if (player.Client.Account.PrivLevel > 1) // 2 = GM, 3 = Admin
                    continue;

                PushPlayerAway(player);
            }

            return 2000;
        }

        private void PushPlayerAway(GamePlayer player)
        {
            double angle = Math.Atan2(player.Z - Z, player.X - X);
            int newX = player.X + (int)(Math.Cos(angle) * PUSHBACK_DISTANCE);
            int newY = player.Y + (int)(Math.Sin(angle) * PUSHBACK_DISTANCE);
            player.MoveTo(player.CurrentRegionID, newX, newY, player.Z, player.Heading);
            player.Out.SendMessage("A force hurls you backward!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }
    }
}
