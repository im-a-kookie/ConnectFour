﻿using ConnectFour.Messaging.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectFour.Messaging
{
    public class Signal
    {
        /// <summary>
        /// The registry that provided this message
        /// </summary>
        public Router Registry;

        /// <summary>
        /// The string identifier of the sender
        /// </summary>
        public Model? Sender;

        /// <summary>
        /// The destination identifier for the message
        /// </summary>
        public Model Destination;

        /// <summary>
        /// The string body of the message
        /// </summary>
        public Content MessageBody;

        /// <summary>
        /// Whether this message has been handled by the receiver
        /// </summary>
        public bool Handled = false;

        /// <summary>
        /// The message expiration lifetime
        /// </summary>
        public DateTime Expiration = DateTime.MaxValue;

        /// <summary>
        /// A callback provided to the message that can be handled
        /// after the message is completed
        /// </summary>
        public Action<Signal>? CompletionCallback;


        /// <summary>
        /// Create a message with the given sender/receiver and content
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="destination"></param>
        /// <param name="message"></param>
        public Signal(Router registry, Model? sender, Model destination, Content message)
        {
            this.Registry = registry;
            this.Sender = sender; 
            this.Destination = destination; 
            this.MessageBody = message;
        }

        /// <summary>
        /// Gets the name of the header in this message
        /// </summary>
        public string HeaderName => Registry.GetHeaderName(MessageBody);


        public object? UnpackData()
        {
            if(MessageBody is Content<PackedData> data)
            {
                return Registry.UnpackContent(data);
            }
            return null;
        }

        public T? UnpackData<T>()
        {
            if (MessageBody is Content<PackedData> data)
            {
                return Registry.UnpackContent<T>(data);
            }
            return default;
        }


    }
}