using Model.Messages;
using Model.ThreadModel;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    public class Core : Member
    {

       
        [Flags]
        public enum CoreFlags
        {
            TERMINATE = 1,
            FORCED = 2,
        }



        public Core(Provider parent, MemberRegistry registry) : base(parent)
        {
            OnReceiveMessage += Core_OnReceiveMessage;
        }

        private void Core_OnReceiveMessage(EventType e, Message m)
        {
            if(m.HeaderName == "exit")
            {
                List<Member> members = new List<Member>();
                foreach(var member in Parent.Members.members.Values)
                {
                    if (member is Core) continue;
                    SendSignal("exit", member);
                    members.Add(member);
                }

                int running = 0;
                while (running > 0)
                {
                    Thread.Sleep(1);
                    running = 0;
                    foreach (var member in members)
                    {
                        if (member.Host?.Alive ?? false) ++running;
                    }
                }

            }
        }

        public void Shutdown()
        {
            SendSignal("exit");
        }



    }
}
