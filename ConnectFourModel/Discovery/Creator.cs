using ConnectFour.Messaging;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Threading.Tasks;
using static ConnectFour.Discovery.Emitter;
using static System.Collections.Specialized.BitVector32;

namespace ConnectFour.Discovery
{
    /// <summary>
    /// The Discovery.Creator allows signals and models to be generated automatically using attributes.
    /// 
    /// <para>A Creator is instantiated and fed instances via <see cref="DiscoverFromInstance(object)"/>. This detects
    /// all static and instance <see cref="SignalDefinition"/> methods.
    /// </para><para>
    /// Static methods are invoked statically, and instance methods retain a live reference to the instanced object.
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

        /// <summary>
        /// Internal mapping of callers to string names for the signals
        /// </summary>
        Dictionary<string, Caller> _callbackMapping = new Dictionary<string, Caller>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Generates a new list of parameters that can be explicitly defined
        /// </summary>
        public List<Type> ExplicitParameters => new List<Type> { typeof(Router), typeof(Model), typeof(Signal) };

        /// <summary>
        /// Gets a new list of types that MUST be defined in the signature
        /// </summary>
        public List<Type> MandatoryParameters = new List<Type> { typeof(Signal) };

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
                throw new ArgumentException($"The provided method '{d.Name}' must have between 1 and 4 parameters, including a Signal parameter.");

            // Define the expected parameter types
            var requiredParams = ExplicitParameters;
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

            // Find a final parameter (likely data) that isn't a Signal, Model, or Router
            for (int i = 0; i < outputParams.Count; ++i)
            {
                if (outputParams[i] == null) continue;
                if (requiredParams.FindIndex(x => x.IsAssignableFrom(outputParams[i])) < 0)
                {
                    parameterMappings.TryAdd(requiredParams.Count, i);
                    break;
                }
            }

            // Remove empty entries
            outputParams.RemoveAll(x => x == null);

            // Verify all required parameters are mapped
            var unmappedRequiredParams = requiredParams
                .Where((_, i) => !parameterMappings.ContainsKey(i))
                .ToList();

            // Ensure a Signal is included
            foreach (var mandatory in MandatoryParameters)
            {
                if (unmappedRequiredParams.Contains(mandatory))
                    throw new ArgumentException($"The provided method '{d.Name}' does not include a parameter assignable to 'Signal'.");
            }

            // Ensure all parameters are mapped
            if (parameterMappings.Count != parameters.Length)
                throw new ArgumentException($"The provided method '{d.Name}' contains parameters that cannot be mapped to the expected signature.");

            // Build and return the result array from the mappings
            return parameterMappings
                .Select(kvp => (input: kvp.Key, output: kvp.Value))
                .ToArray();
        }



        /// <summary>
        /// Passes the given call to the stored callback
        /// </summary>
        /// <param name="function"></param>
        /// <param name="router"></param>
        /// <param name="model"></param>
        /// <param name="signal"></param>
        /// <param name="data"></param>
        /// <returns>Returns true if the call was made, and false otherwise.</returns>
        /// <remarks>This method may throw exceptions. It is expected that these will be caught
        /// by the host provider.</remarks>
        public bool Call(string function, Router router, Model? model, Signal signal, object? data)
        {
            if (_callbackMapping.TryGetValue(function, out var value))
            {
                value.Call(router, model, signal, data);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Searches the provided instance for static and instance methods flagged with <see cref="SignalDefinition"/>. 
        /// The methods are loaded into the creator and can be mapped to the router via <see cref="RegisterCalls(Router)"/>.
        /// 
        /// <para>Subscribing methods <b>MUST</b> have a Signal parameter and may optionally include parameters for Model, Router, and Data.</para>
        /// </summary>
        /// <param name="instance">The instance whose methods are searched for SignalDefinition attributes.</param>
        public void DiscoverFromInstance(object instance)
        {
            // Get all static and instance methods in the provided type, including non-public methods.
            var t = instance.GetType();
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                // Look for the SignalDefinition attribute on the method.
                var attr = m.GetCustomAttributes(true);
                if (attr != null && attr.Length > 0)
                {
                    foreach (SignalDefinition sd in attr.Where(x => x is SignalDefinition))
                    {
                        // Use the signal name from the attribute, or default to the method name
                        string? name = sd.SignalName ?? m.Name;
                        try
                        {
                            // If the method is static, no caller instance is needed;
                            // Otherwise, use the provided instance.
                            object? caller = m.IsStatic ? null : instance;

                            // Get the parameter mappings for the method.
                            var mapping = GetParameterMappings(m);

                            if (mapping != null)
                            {
                                // Determine the data type by filtering out parameters assignable to required parameters.
                                Type? dataType = m.GetParameters()
                                    .Select(p => p.ParameterType)
                                    .FirstOrDefault(t => !ExplicitParameters.Any(rp => rp.IsAssignableFrom(t)));

                                try
                                {
                                    Caller c = new Caller(caller, dataType, m, mapping);
                                    _callbackMapping.Add(name, c);
                                }
                                catch (Exception e){
                                    Console.WriteLine("Bonked: " + e);
                                }

                            }
                        }
                        catch (Exception e)
                        {
                            // TODO: Log the exception for debugging purposes.
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
            foreach(var kv in _callbackMapping)
            {
                string name = kv.Key;
                router.RegisterSignal<object>(name, (router, model, signal, data) => Call(name, router, model, signal, data));
            }
        }

        /// <summary>
        /// A container class for signal callbacks.
        /// </summary>
        public class Caller
        {
            /// <summary>
            /// The callback that is manufactured for this class
            /// </summary>
            SignalDelegate _callback;

            Type? dataType;
            object? caller;

            /// <summary>
            /// Create a new caller. The constructor will generate the callback internally. TODO: Don't do this it's bad.
            /// </summary>
            /// <param name="caller"></param>
            /// <param name="dataType"></param>
            /// <param name="method"></param>
            /// <param name="mapping"></param>
            /// <exception cref="ArgumentNullException"></exception>
            public Caller(object? caller, Type? dataType, MethodInfo method, (int i, int o)[]? mapping)
            {
                this.caller = caller;
                this.dataType = dataType;
                if (mapping != null)
                {
                    _callback = GenerateInvoker(caller, Path.GetRandomFileName().Remove(5), method, mapping.ToList());
                }
                else throw new ArgumentNullException("Parameter mapping must be non-null");
            }

            /// <summary>
            /// Calls the internal delegate with the given parameters
            /// </summary>
            /// <param name="r"></param>
            /// <param name="m"></param>
            /// <param name="s"></param>
            /// <param name="o"></param>
            public void Call(Router? r, Model? m, Signal? s, object? o)
            {
                // Null the data here if it doesn't match
                if(o != null && !o.GetType().IsAssignableTo(dataType))
                {
                    o = null;
                }

                _callback(caller, r, m, s, o);
            }
        }
    }
}
