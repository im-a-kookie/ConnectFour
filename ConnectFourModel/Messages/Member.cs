using Microsoft.Win32;
using Model.ThreadModel;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Messages
{
    public abstract class Member : IDisposable
    {

        /// <summary>
        /// An enumeration of the valid message event types
        /// </summary>
        public enum EventType
        {
            RECEIVE
        }

        /// <summary>
        /// References the MemberRegistry to which this Member belongs
        /// </summary>
        public readonly Provider Parent;

        /// <summary>
        /// Signature definition for all message events
        /// </summary>
        /// <param name="e"></param>
        /// <param name="m"></param>
        public delegate void MessageEvent(EventType e, Message m);

        /// <summary>
        /// Signature definition for signal events
        /// </summary>
        /// <param name="e"></param>
        /// <param name="signal"></param>
        /// <param name="data"></param>
        /// <param name="sender"></param>
        public delegate void SignalEvent(EventType e, string signal, object? data, Member sender);

        /// <summary>
        /// Signature definition for messages received as data objects
        /// </summary>
        /// <param name="e"></param>
        /// <param name="data">The object received</param>
        /// <param name="sender"></param>
        public delegate void DataEvent(EventType e, object? data, Member? sender);

        /// <summary>
        /// Event called when this messageable instance receives a message event
        /// </summary>
        public event MessageEvent? OnReceiveMessage;

        /// <summary>
        /// Called specifically when the member receives a signal
        /// </summary>
        public event SignalEvent? OnReceiveSignal;

        /// <summary>
        /// Called when a data object is received by the member
        /// </summary>
        public event DataEvent? OnReceiveData;

        /// <summary>
        /// The ID value representing the arrival address of this messageable object
        /// </summary>
        public readonly Identifier ID;

        /// <summary>
        /// the internal message queue of messages received (but not yet processed) by
        /// this messageable object.
        /// </summary>
        public BlockingCollection<Message> Queue = [];

        /// <summary>
        /// The Thread container that will host this member's message loop
        /// </summary>
        public ThreadContainer? Host;

        /// <summary>
        /// Initialize a new member
        /// </summary>
        public Member(Provider provider)
        {
            ID = new();
            this.Parent = provider;
            provider.Members.Register(this);
            Host = new(Parent);
            Host.OnLoop += Host_OnLoop;
        }

        private void Host_OnLoop()
        {
            DateTime t = DateTime.UtcNow;
            while (Queue.TryTake(out var m, -1))
            {
                if (t > m.Expiration) continue;
                Member sender = m.Sender ?? Parent.Instance!;

                //we received a message, yay
                OnReceiveMessage?.Invoke(EventType.RECEIVE, m);

                //It wasn't handled yet
                if (!m.Handled)
                {
                    //So let's see if there's a signal handler for this header
                    var p = Parent.Router.GetSignalProcessor(m.MessageBody);
                    if (p != null) p?.callback(this, m.MessageBody.header, p?.flag);
                }

                //The message wasn't handled yet
                if(!m.Handled)
                {
                    //Now let's check if the message was actually a data object
                    if((m.MessageBody.header & Router.TYPEFLAG) != 0)
                    {
                        try
                        {
                            //unpack the data and receive it
                            var data = Parent.Router.UnpackContent(m.MessageBody);
                            OnReceiveData?.Invoke(EventType.RECEIVE, data, sender);
                        }
                        catch { }
                    }
                }
            }
            
        }

        /// <summary>
        /// Initialize a new messageable with the given string identifier
        /// </summary>
        /// <param name="id"></param>
        public Member(Provider registry, string id)
        {
            ID = new(id);
            this.Parent = registry;
            Parent.Members.Register(this);
        }

        ~Member()
        {
            //Ensure that garbage collected members are deregistered
            Parent.Members.Deregister(this);
        }

        public void Dispose()
        {
            //Clean us up, but we only need do it once
            Parent.Members.Deregister(this);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Called when a message is received by the underlying class. Can be
        /// overridden, but preferentially, subscribe to <see cref="OnReceiveMessage"/>
        /// </summary>
        /// <param name="m"></param>
        public virtual void ReceiveMessage(Message m)
        {
            //don't queue expired messages
            //TODO discarded messages should be logged
            if (m.Expiration < DateTime.UtcNow) return;
            Queue.Add(m);
            OnReceiveMessage?.Invoke(EventType.RECEIVE, m);
        }

        /// <summary>
        /// Causes this messageable instance to send a message to the given identifier
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="message"></param>
        public void SendMessage(Identifier destination, Content message)
        {
            Parent.Members.SendMessage(new (Parent.Router, this, destination, message));
        }

        /// <summary>
        /// Sends a signal to the given target
        /// </summary>
        /// <param name="signal"></param>
        /// <param name="destination"></param>
        public void SendSignal(string signal, Identifier? destination = null)
        {
            Parent.Members.SendSignal(signal, destination, this);
        }

        /// <summary>
        /// Sends a signal to the given target
        /// </summary>
        /// <param name="signal"></param>
        /// <param name="destination"></param>
        public void SendSignal(string signal, Member destination)
        {
            Parent.Members.SendSignal(signal, destination.ID, this);
        }


    }
}
