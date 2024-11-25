using ConnectFour.Messaging;
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
    public class Core : Messaging.Model
    {

       
        [Flags]
        public enum CoreFlags
        {
            TERMINATE = 1,
            FORCED = 2,
        }



        public Core(Provider parent, ModelRegistry registry) : base(parent)
        {
            OnReceiveSignal += Core_OnReceiveSignal;
        }

        private void Core_OnReceiveSignal(EventType e, string signal, object? data, Signal instance)
        {

            //check if the signal is an exit signal
            if (IsClosing) return;

            if(signal == "exit")
            {
                Console.WriteLine("Received Exit Signal!");

                IsClosing = true;
                //now we should retrieve all of the models in the provider
                List<Messaging.Model> models = new List<Messaging.Model>();
                foreach(var model in Parent.Models.models.Values)
                {
                    //now signal all of them (except for the Core, which should only be us) to exit
                    if (model == this) continue;

                    Parent.Models.SendSignal(signal: "exit", destination: model);
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
            Parent.Models.SendSignal("exit");
        }



    }
}
