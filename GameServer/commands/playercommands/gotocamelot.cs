
using System;
using DOL.GS;
using DOL.GS.PacketHandler;

namespace DOL.GS.Commands
{
    [Cmd(
        "&camelot",
        ePrivLevel.Player,
        "Teleports you to Camelot City (Albion only, must be out of combat for 10 seconds)",
        "/camelot")]
    public class GotoCamelotCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            GamePlayer player = client.Player;

            if (player == null || !player.IsAlive)
            {
                client.Out.SendMessage("You must be alive to use this command.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (player.Realm != eRealm.Albion)
            {
                client.Out.SendMessage("Only Albion players can use this command.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (player.InCombat)
            {
                client.Out.SendMessage("You must be out of combat for at least 10 seconds.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            long ticksSinceCombat = player.CurrentRegion.Time - player.LastAttackTick;
            if (ticksSinceCombat < 10000)
            {
                client.Out.SendMessage("Please wait 10 seconds after your last combat action.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            player.MoveTo(10, 36211, 29912, 7969, 6); // Camelot City
            client.Out.SendMessage("You are being transported to Camelot City!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }
    }
}
