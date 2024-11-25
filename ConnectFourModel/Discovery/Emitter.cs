using ConnectFour.Messaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ConnectFour.Discovery
{
    /// <summary>
    /// Emits IL that allows exact dynamic calls to be generated, avoiding the need for 
    /// MethodInfo.DynamicInvoke.
    /// </summary>
    public class Emitter
    {
        /// <summary>
        /// The callback delegate that allows signals to be invoked
        /// </summary>
        /// <param name="caller"></param>
        /// <param name="router"></param>
        /// <param name="model"></param>
        /// <param name="signal"></param>
        /// <param name="data"></param>
        public delegate void SignalDelegate(object? caller, Router? router, Model? model, Signal? signal, object? data);


        public static void Bonker()
        {
            Console.WriteLine("Bonkers");
        }

        /// <summary>
        /// Generates the delegate with the given mapping, from an assumed input of:
        /// <para>Router, Model, Signal, Data</para>
        /// 
        /// <para>Perhaps overengineered, but it lets the parameters be remapped in the callback definition,
        /// meaning we don't need to map them manually on every function call.</para>
        /// </summary>
        /// <param name="method">The delegate whose method will be invoked.</param>
        /// <param name="mapping">A byte array containing pairs of mapping indices.</param>
        /// <returns>A dynamically generated method invoker.</returns>
        public static SignalDelegate GenerateInvoker(object? caller, string signal, MethodInfo methodInfo, List<(int src, int dst)> mapping)
        {
            mapping = OffsetSourceMapping(mapping);
            // Validate methodInfo and signal
            if (methodInfo == null)
                throw new ArgumentNullException(nameof(methodInfo), "MethodInfo cannot be null.");

            signal = SanitizeMethodName(signal);
            if (string.IsNullOrWhiteSpace(signal))
                throw new ArgumentException("Signal name must contain valid alphanumeric characters", nameof(signal));

            //Validate that the declaring type is locatable
            var declaringType = methodInfo.DeclaringType;
            if (declaringType == null)
                throw new InvalidOperationException("Method's declaring type is null.");

            var parameterTypes = methodInfo.GetParameters();
            var dynamicParams = new[] { typeof(object), typeof(Router), typeof(Model), typeof(Signal), typeof(object) };

            // Create the dynamic method
            DynamicMethod dynamicMethod;
            try
            {
                dynamicMethod = new DynamicMethod(
                    $"_S_{SanitizeMethodName(signal)}_Invoker", // Dynamic method name
                    null, // Return type (void)
                    dynamicParams, // Parameter types
                    typeof(Emitter).Module, // Module where the method will be defined
                    skipVisibility: true
                );
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to create dynamic method.", ex);
            }

            try
            {
                var il = dynamicMethod.GetILGenerator();

                // If it's a non-static method, load the caller instance as the first argument
                if (caller != null)
                {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Castclass, declaringType);
                }

                // Process the parameter mapping to load the arguments in the correct order

                // Load arguments in the correct order, based on the mapping
                foreach (var map in mapping)
                {
                    il.Emit(OpCodes.Ldarg, map.src);
                    LoadParameter(il, dynamicParams[map.src], parameterTypes[map.dst].ParameterType, map.src);
                }

                // Insert the call and a void return
                il.EmitCall(OpCodes.Call, methodInfo, null);
                il.Emit(OpCodes.Ret);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to generate IL for dynamic method.", ex);
            }

            try
            {
                // Return the created delegate
                return (SignalDelegate)dynamicMethod.CreateDelegate(typeof(SignalDelegate), caller);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to create delegate from dynamic method.", ex);
            }
        }

        /// <summary>
        /// Offsets a parameter mapping by the given offset, for aligning parameter blocks against preceding constant arguments.
        /// </summary>
        /// <param name="tuples"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static List<(int src, int dst)> OffsetSourceMapping(List<(int src, int dst)> tuples, int offset = 1)
        {
            List<(int src, int dst)> result = new();
            foreach(var t in tuples)
            {
                result.Add((t.src + offset, t.dst));
            }
            return result;
        }

        /// <summary>
        /// Gets the list of parameter mappings sorted by their order in the destination mapping.
        /// </summary>
        /// <param name="mapping"></param>
        /// <returns>A list of mappings of source parameter index, to destination parameter index. The returned list
        /// is sorted by destination parameter index, and contains an entry for each value from 0 to (parameter count) </returns>
        public static List<(int src, int dst)> MapBytesToTuple(byte[] mapping)
        {
            var maps = new List<(int src, int dst)>();
            for (int i = 0; i < mapping.Length / 2; ++i)
            {
                int src = mapping[i * 2];     // Index in (Router, Model, Signal, Object)
                int dst = mapping[i * 2 + 1]; // Index in the method's parameters
                //increment the source to account for the insertion of caller parameter
                maps.Add((src, dst));
            }
            maps.Sort((x, y) => x.dst.CompareTo(y.dst)); // Sort by output index so we can sort the parameters correctly
            return maps;
        }

        /// <summary>
        /// Appropriately loads and casts the parameter that was given
        /// </summary>
        /// <param name="il"></param>
        /// <param name="outputType"></param>
        /// <param name="srcIndex"></param>
        private static void LoadParameter(ILGenerator il, Type inputType, Type outputType, int srcIndex)
        {
            // Handle ref types like In and Out
            if (outputType.IsByRef)
            {
                il.Emit(OpCodes.Ldind_Ref); // Dereference the argument (for ref/out parameters)
            }
            else if (outputType.IsValueType) // Handle structs or value types
            {
                HandleValueType(il, outputType, srcIndex);
            }
            else
            {
                // Handle reference types
                if (outputType != inputType)
                {
                    il.Emit(OpCodes.Castclass, outputType);
                }
            }
        }

        /// <summary>
        /// Handle the emission of value type parameters
        /// </summary>
        /// <param name="il"></param>
        /// <param name="paramType"></param>
        /// <param name="srcIndex"></param>
        private static void HandleValueType(ILGenerator il, Type paramType, int srcIndex)
        {
            var underlyingType = Nullable.GetUnderlyingType(paramType);
            if (underlyingType != null)
            {
                HandleNullableType(il, underlyingType, srcIndex);
            }
            else
            {
                il.Emit(OpCodes.Box, paramType); // For normal value types, box them
            }
        }

        /// <summary>
        /// Handles the emission of nullable types. Should only be called for non-null underlying type of a Nullable{}
        /// </summary>
        /// <param name="il"></param>
        /// <param name="underlyingType"></param>
        /// <param name="srcIndex"></param>
        /// <remarks>
        /// Essentially, a nullable value type like int?, when set to null, boxes a 0 value rather than a null value.
        /// This is convenient in C#, but in IL it leads to boxing issues, especially with runtime IL generation.
        /// 
        /// <para>To solve this, we need to use a little bit of a convoluted process to retrieve the correct
        /// zero value for boxing, or otherwise ensure that we don't try to box a null into the value type.</para>
        /// </remarks>
        private static void HandleNullableType(ILGenerator il, Type underlyingType, int srcIndex)
        {
            var labelNotNull = il.DefineLabel();
            var labelDone = il.DefineLabel();

            // Check if we retrieved "null" from the data entry
            il.Emit(OpCodes.Brtrue_S, labelNotNull);

            // Load 0 for the nullable type (null case)
            LoadDefaultValueForType(il, underlyingType);

            il.Emit(OpCodes.Br_S, labelDone);

            // Non-null value
            il.MarkLabel(labelNotNull);
            il.Emit(OpCodes.Ldarg, srcIndex);

            il.MarkLabel(labelDone);
            il.Emit(OpCodes.Unbox_Any, underlyingType); // Unbox the nullable type
        }

        /// <summary>
        /// Gets the default value for a value type primitive, such that the correct boxing operation can be performend
        /// in <see cref="HandleNullableType(ILGenerator, Type, int)"/>
        /// </summary>
        /// <param name="il"></param>
        /// <param name="type"></param>
        private static void LoadDefaultValueForType(ILGenerator il, Type type)
        {
            OpCode code = OpCodes.Ldc_I4_0; // Default for most types (int, bool, etc.)
            if (type == typeof(float))
                code = OpCodes.Ldc_R4; // Default value for float
            else if (type == typeof(double))
                code = OpCodes.Ldc_R8; // Default value for double

            il.Emit(code); // Emit the default value
        }

        /// <summary>
        /// Sanitizes method names so that unexpected symbols don't blow everything up
        /// </summary>
        /// <param name="signal"></param>
        /// <returns></returns>
        public static string SanitizeMethodName(string signal)
        {
            if (string.IsNullOrEmpty(signal)) return "";

            // Ensure the first character is valid (a letter or underscore)
            var builder = new StringBuilder();
            if (char.IsLetter(signal[0]) || signal[0] == '_')
            {
                builder.Append(signal[0]);
            }
            else
            {
                builder.Append('_');
            }

            // Process remaining characters (letters, digits, underscores are valid)
            for (int i = 1; i < signal.Length; i++)
            {
                char c = signal[i];
                if (char.IsLetterOrDigit(c) || c == '_')
                {
                    builder.Append(c);
                }
                else
                {
                    builder.Append('_');
                }
            }

            return builder.ToString();


        }
    }
}
