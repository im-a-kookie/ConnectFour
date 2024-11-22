using ConnectFour.Messages;
using ConnectFour.ThreadModel;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectFour
{
    public class Core : Messages.Model
    {

       
        [Flags]
        public enum CoreFlags
        {
            TERMINATE = 1,
            FORCED = 2,
        }



        public Core(Provider parent, ModelRegistry registry) : base(parent)
        {
            OnReceiveMessage += Core_OnReceiveMessage;
        }


        private void Core_OnReceiveMessage(EventType e, Message m)
        {
            //check if the message is an exit message
            if(m.HeaderName == "exit")
            {
                //now we should retrieve all of the models in the provider
                List<Messages.Model> models = new List<Messages.Model>();
                foreach(var model in Parent.Models.models.Values)
                {
                    //now signal all of them (except for the Core, which should only be us) to exit
                    if (model is Core) continue;
                    SendSignal("exit", model); //tell it to bonk
                    models.Add(model);
                }
                //now count all of the models that are running
                int running = 0;
                while (running > 0)
                {
                    //now we're just going to wait until all of the models are terminated
                    Thread.Sleep(1);
                    running = 0;
                    foreach (var model in models)
                    {
                        if (model.Host?.IsAlive ?? false) ++running;
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
