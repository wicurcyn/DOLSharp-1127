using System;
using DOL.GS;
using DOL.GS.PacketHandler;

namespace DOL.GS.Commands
{
    [Cmd(
        "&instaq",
        ePrivLevel.Player,
        "Logs you out instantly.",
        "/instaq")]
    public class InstaQCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            GamePlayer player = client.Player;

            if (player == null || !player.IsAlive)
            {
                client.Out.SendMessage("You must be alive to use this command.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            client.Out.SendMessage("You are being logged out instantly.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            client.Disconnect(); // Immediately disconnect the client
        }
    }
}