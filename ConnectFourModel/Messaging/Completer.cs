using ConnectFour.Messaging.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ConnectFour.Messaging
{
    public abstract class Completer : IDisposable
    {

        public abstract void Complete(Signal s);

        public abstract void Await();

        public virtual void Dispose()
        {
        }

        public abstract T? GetResponse<T>();



    }

    public class BlockingCompleter : Completer
    {
        ManualResetEventSlim gate = new(false);

        object? response;

        public override void Complete(Signal s)
        {
            gate.Set();
            response = s.Response?.GetData() ?? null;
        }

        public override void Await()
        {
            gate.Wait(Timeout.Infinite);
        }

        public override void Dispose()
        {
            gate.Dispose();
            GC.SuppressFinalize(this);
        }

        public override T? GetResponse<T>() where T : default
        {
            if(response is T t)
            {
                return t;
            }
            return default;
        }

        ~BlockingCompleter()
        {
            gate.Dispose();
        }
    }


}
