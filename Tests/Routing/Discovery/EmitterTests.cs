using ConnectFour.Discovery;
using ConnectFour.Messaging;
using System.Reflection;

namespace Tests.Routing.Discovery.Emitting
{
    [TestClass]
    public class EmitterTests
    {
        private MethodInfo methodInfo;
        List<(int src, int dst)> mapping;

        [TestInitialize]
        public void Setup()
        {
            // Setup a sample method to test against
            methodInfo = typeof(SampleClass).GetMethod("TestMethod", BindingFlags.Static | BindingFlags.Public)!;
            mapping = new Creator().GetParameterMappings(methodInfo).ToList();
;
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void GenerateInvoker_NullMethodInfoException()
        {
            // Arrange
            string signal = "TestSignal";

            // Act
            var result = Emitter.GenerateInvoker(null, signal, null, mapping);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void GenerateInvoker_EmptySignalException()
        {
            // Arrange
            string signal = "";

            // Act
            var result = Emitter.GenerateInvoker(null, signal, methodInfo, mapping);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void GenerateInvoker_OutOfRangeMapping()
        {
            // Arrange
            string signal = "TestSignal";
            byte[] _mapping = { 0, 10, 1, 2 }; // 10 is out of range for method parameters
            var mapping = Emitter.MapBytesToTuple(_mapping);
            // Act
            var result = Emitter.GenerateInvoker(null, signal, methodInfo, mapping);
        }


        [TestMethod]
        public void GenerateInvoker_CreatesDelegate()
        {
            // Arrange
            string signal = "TestSignal";

            // Act
            var result = Emitter.GenerateInvoker(null, signal, methodInfo, mapping);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(Emitter.SignalDelegate));
        }


        [TestMethod]
        public void GenerateInvoker_DelegateInvocable()
        {
            // Arrange
            string signal = "TestSignal";

            // Act
            var result = Emitter.GenerateInvoker(null, signal, methodInfo, mapping);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(Emitter.SignalDelegate));

            int initialValue = 1;
            SampleData sd = new SampleData(initialValue);

            result(new SampleClass(), null, null, null, sd);

            Assert.AreEqual(initialValue + 1, sd.TestField);

        }
    }


    public class SampleData
    {
        public int TestField;
        public SampleData(int value) { TestField = value; }
    }

    // Sample class with a method to be used in the tests
    public class SampleClass
    {
        public static void TestMethod(Router router, Model model, Signal signal, object? data)
        {
            // Sample method to invoke via dynamic generation
            if (data is SampleData sd) sd.TestField += 1;
        }
    }

}