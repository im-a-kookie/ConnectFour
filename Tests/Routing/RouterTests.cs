using ConnectFour.Discovery;
using ConnectFour.Messaging;
using ConnectFour.Messaging.Packets;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Tests.Routing
{
    [TestClass]
    public class RouterTests
    {
        private class Cookie
        {
            public int Data { get; set; }
            public Cookie(int data) => Data = data;
        }

        private List<string> stringCollector = new();
        private List<int> intCollector = new();
        private Router? router;

        [TestInitialize]
        public void Init()
        {
            router = new Router();

            router.RegisterSignal("untyped", (r, model, signal) =>
            {
                stringCollector.Add(signal.HeaderName);
                intCollector.Add((int)signal.MessageBody.GetData()!);
            });

            router.RegisterSignal<int>("typed", (r, model, signal, data) =>
            {
                stringCollector.Add(signal.HeaderName);
                intCollector.Add(data);
            });

            router.RegisterSignal<Cookie>("cookie", (r, model, signal, data) =>
            {
                stringCollector.Add(signal.HeaderName);
                intCollector.Add(data?.Data ?? -1);
            });

            // Register Cookie encoder and decoder
            router.RegisterTypeEncoder((Cookie c) => BitConverter.GetBytes(c.Data));
            router.RegisterTypeDecoder((Type t, byte[] data) => new Cookie(BitConverter.ToInt32(data)));
        }

        [TestMethod]
        public void CreateContent()
        {
            var content = router?.BuildSignalContent("untyped", 1);

            var data = content!.GetData();
            Assert.IsInstanceOfType(data, typeof(int), "Content data is not of type int.");
            Assert.AreEqual(data, 1, "Content data value does not match expected value.");

            var headerList = router?.GetType()
                .GetField("_names", BindingFlags.NonPublic | BindingFlags.Instance)?
                .GetValue(router) as List<string>;

            var headerIndex = content.header & Router.TYPEMASK;
            Assert.AreEqual(headerList?[headerIndex], "untyped", "Header name does not match 'untyped'.");
        }

        [TestMethod]
        public void TestSignalProcessor()
        {
            int dataValue = DateTime.UtcNow.Microsecond + 1000 * DateTime.UtcNow.Millisecond;
            var content = router?.BuildSignalContent("untyped", dataValue);

            var processor = router?.GetSignalProcessor(content);
            processor?.DynamicInvoke(router, new Signal(router!, content!));

            Assert.AreEqual(stringCollector[0], "untyped", "Signal processor did not invoke the correct header name.");
            Assert.AreEqual(intCollector[0], dataValue, "Signal processor did not pass the correct data.");
        }

        [TestMethod]
        public void TestTypedSignalProcessor()
        {
            int dataValue = DateTime.UtcNow.Microsecond + 1000 * DateTime.UtcNow.Millisecond;
            var content = router?.BuildSignalContent("typed", dataValue);

            var processor = router?.GetSignalProcessor(content);
            processor?.DynamicInvoke(router, new Signal(router!, content!), content?.GetData());

            Assert.AreEqual(stringCollector[0], "typed", "Typed signal processor did not invoke the correct header name.");
            Assert.AreEqual(intCollector[0], dataValue, "Typed signal processor did not pass the correct data.");
        }

        [TestMethod]
        public void TestDynamicSignalInvoker()
        {
            int dataValue = DateTime.UtcNow.Microsecond + 1000 * DateTime.UtcNow.Millisecond;
            var content = router?.BuildSignalContent("untyped", dataValue);

            var processor = router?.GetSignalProcessor(content);
            router!.InvokeProcessorDynamic(processor, new Signal(router!, content!), content!.GetData());

            Assert.AreEqual(stringCollector[0], "untyped", "Dynamic signal invoker did not invoke the correct header name.");
            Assert.AreEqual(intCollector[0], dataValue, "Dynamic signal invoker did not pass the correct data.");

            // Test with "typed" signal
            dataValue ^= 123456;
            content = router.BuildSignalContent("typed", dataValue);
            processor = router.GetSignalProcessor(content);
            router.InvokeProcessorDynamic(processor, new Signal(router!, content!), content.GetData());

            Assert.AreEqual(stringCollector[1], "typed", "Dynamic signal invoker did not invoke the correct header name for 'typed'.");
            Assert.AreEqual(intCollector[1], dataValue, "Dynamic signal invoker did not pass the correct data for 'typed'.");
        }

        [TestMethod]
        public void TestTypeSerialization()
        {
            int dataValue = DateTime.UtcNow.Microsecond + 1000 * DateTime.UtcNow.Millisecond;
            var content = router!.BuildSignalContent("cookie", new Cookie(dataValue));

            Assert.IsNotNull(content, "Signal content for 'cookie' was not created.");
            var packed = router.PackContent(content);
            Assert.IsNotNull(packed, "Packing content for 'cookie' failed.");

            var unpacked = router.UnpackContent<Cookie>(packed);
            Assert.IsInstanceOfType(unpacked, typeof(Cookie), "Unpacked content is not of type Cookie.");
            Assert.AreEqual(unpacked!.Data, dataValue, "Unpacked Cookie data does not match the original value.");
        }
    }
}
