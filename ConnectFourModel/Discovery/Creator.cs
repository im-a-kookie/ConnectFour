using ConnectFour.Messaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Threading.Tasks;

namespace ConnectFour.Discovery
{
    /// <summary>
    /// The Discovery.Creator allows signals and models to be generated automatically using attributes.
    /// 
    /// <para>A Creator is instantiated and fed instances via <see cref="DiscoverFromInstance(object)"/>. This detects
    /// all static and instance <see cref="SignalDefinition"/> methods.
    /// </para><para>
    /// Static methods are invoked statically, and instance methods retain a <see cref="WeakReference{T}"/> of the provided object.
    /// These are then mapped to the Router via <see cref="RegisterCalls(Router)"/>, which invokes them dynamically.
    /// </para>
    /// 
    /// <para>
    /// For correct dynamic invocation, the marked methods must provide a parameter of type <see cref="Signal"/>, and may
    /// optionally provide <see cref="Model"/> and <see cref="Router"/> parameters. 
    /// </para>
    /// <para>
    /// A fourth parameter may provide an object or Type. If the type does not match, then <see cref="null"/> is  provided instead.
    /// </para>
    /// 
    /// </summary>
    public class Creator
    {

        Dictionary<string, (WeakReference<object>? caller, MethodInfo callback, (int input, int output)[] mapping)> _callMaps = [];

        /// <summary>
        /// Creates an array describing the parameter mapping from the expected callback signature 
        /// (router, model, signal, data) to the arbitrary parameter order in the provided method.
        /// 
        /// <para>
        /// The resulting array allows methods with arbitrary parameter orderings to be invoked via 
        /// <see cref="Creator.Call(string, Router, Model?, Signal, object?)"/>.
        /// </para>
        /// </summary>
        /// <param name="d">The method information to map parameters from.</param>
        /// <returns>An array of mappings where each tuple specifies the input and output parameter positions.</returns>
        /// <exception cref="ArgumentException">Thrown if the method cannot be mapped suitably.</exception>
        public (int input, int output)[] GetParameterMappings(MethodInfo d)
        {
            // Retrieve and validate method parameters
            var parameters = d.GetParameters();
            if (parameters.Length < 1 || parameters.Length > 4)
                throw new ArgumentException($"The provided method '{d.Name}' does not have a compatible signature. It must have between 1 and 4 parameters, including a Signal parameter.");

            // Define the expected parameter types
            var requiredParams = new List<Type> { typeof(Router), typeof(Model), typeof(Signal), typeof(object) };
            var outputParams = parameters.Select(p => p.ParameterType).ToList<Type?>();

            var parameterMappings = new Dictionary<int, int>();

            // Map required parameters to method parameters
            foreach (var t in requiredParams)
            {
                var index = outputParams.FindIndex(p => p != null && p.IsAssignableTo(t));
                if (index != -1)
                {
                    parameterMappings.TryAdd(requiredParams.IndexOf(t), index);
                    outputParams[index] = null; // Mark as mapped to avoid duplicates
                }
            }

            // Verify all required parameters are mapped
            var unmappedRequiredParams = requiredParams
                .Where((_, i) => !parameterMappings.ContainsKey(i))
                .ToList();

            //we must have a signal
            if (unmappedRequiredParams.Contains(typeof(Signal)))
                throw new ArgumentException($"The provided method '{d.Name}' does not include a parameter assignable to 'Signal'.");
            //and we must map every parameter
            if (parameterMappings.Count != parameters.Length)
                throw new ArgumentException($"The provided method '{d.Name}' contains parameters that cannot be mapped to the expected signature.");

            //Build the result array from the mappings
            var result = parameterMappings
                .Select(kvp => (input: kvp.Key, output: kvp.Value))
                //.OrderBy(mapping => mapping.input)
                .ToArray();

            return result;
        }


        ReaderWriterLock locker = new ReaderWriterLock();

        /// <summary>
        /// Calls a discovered function by the string name. The provided parameters are remapped dynamically
        /// to the method defined in the discovered target.
        /// </summary>
        /// <param name="function"></param>
        /// <param name="router"></param>
        /// <param name="model"></param>
        /// <param name="signal"></param>
        /// <param name="data"></param>
        public void Call(string function, Router router, Model? model, Signal signal, object? data)
        {
            try
            {
                //acquire reader to ensure that stale reference removal doesn't cause anything painful
                locker.AcquireReaderLock(Timeout.Infinite);

                if (_callMaps.TryGetValue(function, out var value))
                {
                    object? obj = null;

                    // Check if the target object is still valid
                    if (value.caller == null || value.caller.TryGetTarget(out obj))
                    {
                        //Prepare the parameter arrays
                        //the inputs are mapped back to the outputs
                        var inputParameters = new List<object?> { router, model, signal, data };
                        var outputParameters = new object?[value.mapping.Length];

                        //Remap the parameters correctly
                        foreach (var map in value.mapping)
                        {
                            outputParameters[map.output] = inputParameters[map.input];
                        }

                        //Invoke the callback method with mapped parameters
                        value.callback.Invoke(obj, outputParameters);
                    }
                    else
                    {
                        //Upgrade to a writer lock to modify shared state
                        try
                        {
                            locker.UpgradeToWriterLock(Timeout.Infinite);
                            _callMaps.Remove(function); //Remove stale entry
                        }
                        finally
                        {
                            locker.ReleaseWriterLock(); //release lock
                        }
                    }
                }
            }
            finally
            {
                // Always release the reader lock
                if (locker.IsReaderLockHeld)
                {
                    locker.ReleaseReaderLock();
                }
            }
        }

        /// <summary>
        /// Searches the provided instance for Instance and Static methods that are flagged with <see cref="SignalDefinition"/>. The resulting
        /// methods are loaded into the creator, and can be mapped to the router via <see cref="RegisterCalls(Router)"/>
        /// 
        /// <para>For compatibility, subscribing methods <b>MUST</b> have a Signal parameter, and may optionally provide parameters for Model, Router, and Data.</para>
        /// </summary>
        /// <param name="instance"></param>
        public void DiscoverFromInstance(object instance)
        {
            //get every static and instance method in the provided type
            //including non-public methods
            var t = instance.GetType();
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                //find the SignalDefinition attribute
                var attr = m.GetCustomAttributes(true);
                if(attr != null && attr.Length > 0)
                {
                    foreach(SignalDefinition sd in attr.Where(x => x is SignalDefinition))
                    {
                        //the name is either the signal name, or
                        //otherwise defaults to the name of the method
                        string? name = sd.SignalName;
                        if (name == null) name = m.Name;

                        try
                        {
                            //Now we need to find a suitable parameter mapping
                            object? caller = m.IsStatic ? null : instance;
                            var mapping = GetParameterMappings(m);

                            if(mapping != null)
                            {
                                _callMaps.TryAdd(name, (caller == null ? null : new WeakReference<object>(caller), m, mapping));
                            }
                        }
                        catch(Exception e)
                        {
                            //TODO log this back somewhere...
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Registers the calls loaded into this creator, into the router.
        /// </summary>
        /// <param name="router"></param>
        public void RegisterCalls(Router router)
        {
            foreach(var kv in _callMaps)
            {
                string name = kv.Key;
                router.RegisterSignal<object>(name, (router, model, signal, data) => Call(name, router, model, signal, data));
            }
        }




    }
}
