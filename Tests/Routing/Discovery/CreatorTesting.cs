using ConnectFour.Discovery;
using ConnectFour.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static Tests.Routing.RouterTests;

namespace Tests.Routing.Discovery.Creating
{
    [TestClass]
    public class CreatorTesting
    {


        private Router router;
        private Creator creator;
        private Sampler sampler;

        private IEnumerable<MethodInfo>? validMethods;
        private IEnumerable<MethodInfo>? invalidMethods;

        private Dictionary<string, MethodInfo> Categories = [];


        [TestInitialize]
        public void Setup()
        {

            router = new Router();
            creator = new Creator();
            sampler = new Sampler();



            // Sample valid method
            validMethods = typeof(SampleClass).GetMethods(BindingFlags.Instance | BindingFlags.Public).Where(x => x.Name == "ValidMethod");

            // Sample invalid method (missing required Signal parameter)
            invalidMethods = typeof(SampleClass).GetMethods(BindingFlags.Instance | BindingFlags.Public).Where(x => x.Name == "InvalidMethod");

            // And set up the dictionary so we can get them easily
            foreach(MethodInfo m in validMethods.Union(invalidMethods))
            {
                var attr = m.GetCustomAttribute(typeof(SampleClass.Category));
                if(attr != null && attr is SampleClass.Category c)
                {
                    Categories.TryAdd(c.Reason, m);
                }
            }
        }
        /// <summary>
        /// Gets the reason/category from the given method info
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        private string GetReason(MethodInfo m)
        {
            var attr = m.GetCustomAttribute(typeof(SampleClass.Category));
            if (attr != null && attr is SampleClass.Category c) return c.Reason;
            return "Default Reason";
        }

        public bool CompareMapping(List<(int src, int dst)> mapping, MethodInfo input)
        {
            // We also need to increment the mapping
            var adjustedMap = Emitter.OffsetSourceMapping(mapping, 1);

            // Now get the signature of the signal delegate
            var invokeMethod = typeof(Emitter.SignalDelegate).GetMethod("Invoke");
            if (invokeMethod == null)
                throw new InvalidOperationException("Unable to find the Invoke method for the provided delegate type.");

            // Get the parameter types
            var delegateInputParameters = invokeMethod.GetParameters()
                .Select(param => param.ParameterType)
                .ToList();

            var callbackInvokeParameters = input.GetParameters()
                .Select(p => p.ParameterType)
                .ToList();

            var explicitParams = creator.ExplicitParameters;

            // Ensure that every output parameter is aligned
            //Assert.AreEqual(callbackInvokeParameters.Count, adjustedMap.Count);

            // Check every mapping
            foreach(var map in adjustedMap)
            {
                Type a = delegateInputParameters[map.src];
                Type b = callbackInvokeParameters[map.dst];

                if(explicitParams.Contains(a))
                {
                    // Check that the callback has an assignable parameter
                    //Assert.IsTrue(b.IsAssignableTo(a));
                    if (!b.IsAssignableTo(a)) return false;
                }
                else
                {
                    // The additional parameter must not be a router, signal, or messagte
                    //Assert.IsTrue(!explicitParams.Contains(b));
                    if (explicitParams.Contains(b))
                        return false;
                }
            }

            return true;
        }



        [TestMethod]
        public void Test_GenerateMappings()
        {
            // Each of these is expected to pass
            Assert.IsTrue((validMethods?.Count() ?? 0) > 0, "Valid methods were not loaded into test environement!");
            foreach (var v in validMethods ?? [])
            {
                var mapping = creator.GetParameterMappings(v).ToList();
                Assert.IsNotNull(mapping, $"The mapping could not be generated from valid signature ({GetReason(v)})");
                Assert.AreEqual(mapping.Count, v.GetParameters().Length, $"Mapping provides incorrect parameter count for valid signature ({GetReason(v)})");
                Assert.IsTrue(CompareMapping(mapping, v), $"The mapping does not correctly match parameters for valid signature ({GetReason(v)})");
            }

            foreach (var v in invalidMethods ?? [])
            {
                try
                {
                    var mapping = creator.GetParameterMappings(v).ToList();
                    Assert.IsTrue(mapping == null, $"An invalid signature ({GetReason(v)} provided a non-null mapping!");
                }
                catch(Exception ex)
                {
                    Assert.IsInstanceOfType(ex, typeof(ArgumentException), $"Mapping retrieval should only throw Argument Exception. Method: {GetReason(v)}");
                }
            }
        }
                       

        [TestMethod]
        public void TestMethodDiscoveryAndSignalHandling()
        {
            //ensure the router can be configured etc
            creator.DiscoverFromInstance(sampler);
            creator.RegisterCalls(router);

            // Test for method "Brog"
            var staticSignal = CreateSignal("TestStatic");
            creator.Call("TestStatic", router, null, staticSignal, staticSignal.GetData());
            Sampler.RealStatic(staticSignal, router, null);

            // Test for method "scrub"
            var scrubSignal = CreateSignal("alternate");
            creator.Call("alternate", router, null, scrubSignal, scrubSignal.GetData());
            sampler.RealVirtual(scrubSignal, router, null);

            // Verify the results
            Assert.AreEqual(2, Sampler.realResult.Count, "Expected 2 results, from TestStatic and TestVirtual (alternate)");
            Assert.AreEqual(2, Sampler.expectedResults.Count, "Expected 2 results, from TestStatic and TestVirtual (alternate)");

            for (int i = 0; i < Sampler.realResult.Count; i++)
            {
                Assert.AreEqual(Sampler.realResult[i], Sampler.expectedResults[i], 
                    $"Mismatch at index {i}. Expected: {Sampler.expectedResults[i]}, Actual: {Sampler.realResult[i]}");
            }
        }

        private Signal CreateSignal(string methodName)
        {
            // Helper method to create signals for different methods
            int n = DateTime.UtcNow.Millisecond + 1000 * DateTime.UtcNow.Millisecond;
            var signalContent = router.BuildSignalContent(methodName, n);
            Assert.IsNotNull(signalContent, $"Signal content for {methodName} should not be null.");
            return new Signal(router, signalContent);
        }


    }

    public class Sampler
    {

        public static List<string> realResult = [];
        public static List<string> expectedResults = [];

        public Sampler()
        {
            realResult = [];
            expectedResults = [];
        }

        [SignalDefinition]
        public static void TestStatic(Signal s, Router r, Model? m)
        {
            realResult.Add(s.HeaderName);
        }

        public static void RealStatic(Signal s, Router r, Model? m)
        {
            expectedResults.Add("TestStatic");
        }


        [SignalDefinition(signalName: "alternate")]
        public void TestVirtual(Signal s)
        {
            realResult.Add(s.HeaderName);
        }

        public void RealVirtual(Signal s, Router r, Model? m)
        {
            expectedResults.Add("alternate");
        }

    }


    // Sample class with methods for testing
    public class SampleClass
    {
        public class Category : Attribute
        {
            public string Reason { get; private set; }
            public Category(string Reason) { this.Reason = Reason; }
        }

        [Category("Complete Method")]
        public void ValidMethod(Router router, Model model, Signal signal, object data)
        {
        }

        //defining overloads with the same signature lets us get them all :D
        [Category("Absent Parameters")]
        public void ValidMethod(Signal signal, object data)
        {
        }

        [Category("Disordered Parameters")]
        public void ValidMethod(Signal signal, Router model, object data)
        {
        }

        [Category("Custom Data")]
        public void ValidMethod(Router router, Model model, Signal signal, string customData)
        {
        }

        [Category("No Parameters")]
        public void InvalidMethod()
        {
        }

        [Category("Too Many Parameters")]
        public void InvalidMethod(Router router, Model model, Signal signal, object data, string extra)
        {
        }

        [Category("Lacks Signal")]
        public void InvalidMethod(Router router, Model model, object data)
        {
        }

    }


}
