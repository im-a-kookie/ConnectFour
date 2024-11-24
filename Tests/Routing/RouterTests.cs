using ConnectFour.Messaging;
using ConnectFour.Messaging.Packets;
using System;
using System.Reflection;


namespace Tests.Routing
{
    [TestClass]
    public class RouterTests
    {
        /// <summary>
        /// A simple data template class for sending things
        /// </summary>
        class Cookie
        {
            public int data { get; set; }
            public Cookie(int data) => this.data = data;
        }


        List<string> string_collector = [];
        List<int> int_collector = [];

        Router? router;

        [TestInitialize]
        public void Init()
        {
            router = new Router();
            string_collector = [];

            router.RegisterSignal("untyped", (a, b) => {
                string_collector.Add(b.HeaderName);
                int_collector.Add((int)b.MessageBody.GetData()!);
            }
            );

            router.RegisterSignal<int>("typed", (a, b, c) =>
            {
                string_collector.Add(b.HeaderName);
                int_collector.Add(c);
            });

            router.RegisterSignal<Cookie>("cookie", (a, b, c) =>
            {
                string_collector.Add(b.HeaderName);
                int_collector.Add(c?.data ?? -1);
            });

            //create a cookie encoder
            router.RegisterTypeEncoder((Cookie c) => BitConverter.GetBytes(c.data));

            //and a cookie decoder
            router.RegisterTypeDecoder((Type t, byte[] data) => new Cookie(BitConverter.ToInt32(data)));


        }

        /// <summary>
        /// Test that content can be created, and that the headers and data values
        /// are correctly set
        /// </summary>
        [TestMethod()]
        public void CreateContent()
        {
            var c = router?.BuildSignalContent("untyped", 1);

            var n = c.GetData();
            //check that the integer type was set correctly
            Assert.IsInstanceOfType(n, typeof(int));
            Assert.AreEqual(n, 1);

            //now check the signal
            var lists = router?.GetType().GetField("_names", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(router) as List<string>;

            var h = c.header & Router.TYPEMASK;

            //now check that the values match
            Assert.AreEqual(lists?[h], "untyped");

        }

        /// <summary>
        /// Test the retrieval and calling of the signal processor
        /// </summary>
        [TestMethod()]
        public void TestSignalProcessor()
        {
            //make the untyped signal and send it to the thing
            int n = DateTime.UtcNow.Microsecond + 1000 * DateTime.UtcNow.Millisecond;

            var c = router?.BuildSignalContent("untyped", n);
            var sp = router?.GetSignalProcessor(c);
            sp?.DynamicInvoke(router, new Signal(router!, c!));
            Assert.AreEqual(string_collector[0], "untyped");
            Assert.AreEqual(int_collector[0], n);


        }

        /// <summary>
        /// Test the retrieval and calling of the signal processor
        /// </summary>
        [TestMethod()]
        public void TestTypedSignalProcessor()
        {
            int n = DateTime.UtcNow.Microsecond + 1000 * DateTime.UtcNow.Millisecond;
            var c = router?.BuildSignalContent("typed", n);
            var sp = router?.GetSignalProcessor(c);
            sp?.DynamicInvoke(router, new Signal(router!, c!), c?.GetData());

            Assert.AreEqual(string_collector[0], "typed");
            Assert.AreEqual(int_collector[0], n);
        }

        /// <summary>
        /// Test that dynamic signal invocation is performed correctly
        /// </summary>
        [TestMethod()]
        public void TestDynamicSignalInvoker()
        {
            int n = DateTime.UtcNow.Microsecond + 1000 * DateTime.UtcNow.Millisecond;
            var c = router?.BuildSignalContent("untyped", n);
            var sp = router?.GetSignalProcessor(c);
            router.InvokeProcessorDynamic(sp, new Signal(router!, c!), c.GetData());

            Assert.AreEqual(string_collector[0], "untyped");
            Assert.AreEqual(int_collector[0], n);

            n ^= 123456;
            c = router?.BuildSignalContent("typed", n);
            sp = router?.GetSignalProcessor(c);
            router.InvokeProcessorDynamic(sp, new Signal(router!, c!), c.GetData());


            Assert.AreEqual(string_collector[1], "typed");
            Assert.AreEqual(int_collector[1], n);

        }

        [TestMethod()]
        public void TestTypeSerialization()
        {
            int n = DateTime.UtcNow.Microsecond + 1000 * DateTime.UtcNow.Millisecond;
            Content<Cookie>? c = router!.BuildSignalContent("cookie", new Cookie(n));
            Assert.IsNotNull(c);

            var packed = router!.PackContent(c);
            Assert.IsNotNull(packed);

            var unpacked = router!.UnpackContent<Cookie>(packed);

            Assert.IsInstanceOfType(unpacked, typeof(Cookie));
            Assert.AreEqual(unpacked.data, n);


        }



    }
}
