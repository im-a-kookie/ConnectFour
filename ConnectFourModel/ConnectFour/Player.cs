using System.Collections.Concurrent;
using System.Drawing;

namespace Model.ConnectFour
{
    /// <summary>
    /// An internal player reference. The Player reference abides by a 
    /// multiton pattern using a static registry. This is threadsafe, and allows
    /// Players to do things like, reconnect to a game after disconnecting.
    /// </summary>
    public class Player
    {

        static ConcurrentDictionary<string, Player> PlayerRegistry = [];


        static void RegistryManager()
        {

        }


        internal string playerId = "<default>";

        /// <summary>
        /// The player can only be instantiated by the Player class itself,
        /// via Player.GetPlayer.
        /// </summary>
        private Player()
        {


        }






    }
}
