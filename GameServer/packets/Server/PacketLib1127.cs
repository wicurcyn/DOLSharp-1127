
namespace DOL.GS.PacketHandler
{
    [PacketLib(1127, GameClient.eClientVersion.Version1127)]
    public class PacketLib1127 : PacketLib1126
    {
        /// <summary>
        /// Constructs a new PacketLib for Client Version 1.127
        /// </summary>        
        public PacketLib1127(GameClient client)
            : base(client)
        {
        }

        public override void SendMessage(string msg, eChatType type, eChatLoc loc)
        {
            if (GameClient.ClientState == GameClient.eClientState.CharScreen)
            {
                return;
            }

            GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.Message));
            {                
                pak.WriteByte((byte)type);                

                string str;
                if (loc == eChatLoc.CL_ChatWindow)
                {
                    str = "@@";
                }
                else if (loc == eChatLoc.CL_PopupWindow)
                {
                    str = "##";
                }
                else
                {
                    str = "";
                }

                pak.WriteString(str + msg);
                SendTCP(pak);
            }
        }
    }
}
