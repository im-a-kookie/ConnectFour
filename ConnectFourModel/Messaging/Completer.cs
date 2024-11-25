namespace ConnectFour.Messaging
{
    /// <summary>
    /// An abstract base class representing a completer, which is used to handle the completion of an asynchronous operation.
    /// </summary>
    public abstract class Completer : IDisposable
    {
        /// <summary>
        /// Completes the operation with the provided signal.
        /// </summary>
        /// <param name="s">The signal that represents the completion of the operation.</param>
        public abstract void Complete(Signal s);

        /// <summary>
        /// Waits for the operation to complete (blocking).
        /// </summary>
        public abstract void Await();

        /// <summary>
        /// Disposes of the completer, releasing any resources.
        /// </summary>
        public virtual void Dispose()
        {
        }

        /// <summary>
        /// Gets the response from the operation after completion.
        /// </summary>
        /// <typeparam name="T">The type of the response.</typeparam>
        /// <returns>The response of the operation, or null if no response is available.</returns>
        public abstract T? GetResponse<T>();
    }

    /// <summary>
    /// A concrete implementation of the <see cref="Completer"/> class that blocks the calling thread until the operation is complete.
    /// </summary>
    public class BlockingCompleter : Completer
    {
        private ManualResetEventSlim gate = new(false);  // Used to block the thread until completion
        private object? response;  // The response that will be returned after completion

        /// <summary>
        /// Completes the operation and sets the response.
        /// </summary>
        /// <param name="s">The signal representing the completion of the operation.</param>
        public override void Complete(Signal s)
        {
            gate.Set();  // Signal that the operation is complete
            response = s.Response?.GetData() ?? null;  // Store the response data, if available
        }

        /// <summary>
        /// Blocks the calling thread until the operation is complete.
        /// </summary>
        public override void Await()
        {
            gate.Wait(Timeout.Infinite);  // Block indefinitely until the operation completes
        }

        /// <summary>
        /// Disposes of the resources used by the <see cref="BlockingCompleter"/> instance.
        /// </summary>
        public override void Dispose()
        {
            gate.Dispose();  // Dispose of the ManualResetEventSlim to release resources
            GC.SuppressFinalize(this);  // Suppress finalization to prevent unnecessary cleanup
        }

        /// <summary>
        /// Retrieves the response of the operation, casting it to the specified type.
        /// </summary>
        /// <typeparam name="T">The expected type of the response.</typeparam>
        /// <returns>The response of the operation, or the default value of the type if it is not available.</returns>
        public override T? GetResponse<T>() where T : default
        {
            if (response is T t)
            {
                return t;  // Return the response if it matches the expected type
            }
            return default;  // Return the default value if no response or a different type is available
        }

        /// <summary>
        /// Finalizer to ensure resources are cleaned up if Dispose is not called.
        /// </summary>
        ~BlockingCompleter()
        {
            gate.Dispose();  // Dispose of the ManualResetEventSlim in the finalizer
        }
    }
}
