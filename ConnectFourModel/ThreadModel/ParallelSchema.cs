using ConnectFour.Messaging;
using System.Collections.Concurrent;

namespace ConnectFour.ThreadModel
{
    /// <summary>
    /// The parallel schema defines the parallel framework that will be used.
    /// 
    /// <para>Each model is provided a container by the schematic, which allows abstraction
    /// of the relationship between the model and the underlying threads. Different schemas
    /// present different advantages and disadvantages, and optimal schema design may be
    /// workload dependent.</para>
    /// 
    /// <para>By default, the library ships with 2 main schemas, these being a threadpool
    /// model via Task API, and a thread-per-container model by which each container is
    /// allocated a dedicated thread.</para>
    /// 
    /// <para>The threadpool scheme is recommended for workloads with a large number of
    /// models with low computational requirements per model, while the per-container 
    /// scheme is recommended for workloads that require a smaller number of higher
    /// performance models.</para>
    /// </summary>
    public abstract class ParallelSchema
    {
        /// <summary>
        /// The main interaction point of the Parallel Schema. Used internally to generate the model continers
        /// via <see cref="ProvideHost(Model)"/>
        /// </summary>
        /// <param name="requester"></param>
        /// <returns></returns>
        protected abstract ModelContainer CreateContainer(Model requester);

        /// <summary>
        /// Internal registry of all models contained in this registry. This is reliant on
        /// correct implementation of <see cref="ModelContainer.OnClose"/> event.
        /// </summary>
        protected ConcurrentDictionary<ModelContainer, bool> _ContainerRegistry = [];

        private bool _running = false;
        /// <summary>
        /// Gets whether the schematic us currently running
        /// </summary>
        public bool Running => _running;

        /// <summary>
        /// The parent provider instance for this schema
        /// </summary>
        public Provider? Parent { get; private set; }

        /// <summary>
        /// Base constructor of parallel schema.
        /// </summary>
        /// <param name="parent"></param>
        public ParallelSchema()
        {

        }

        internal void SetParent(Provider p)
        {
            Parent = p;
        }

        /// <summary>
        /// Provides a thread host container (Dependency Injection). This container provides the interaction
        /// layer between the Model and the ParallelSchema.
        /// </summary>
        /// <returns></returns>
        public ModelContainer ProvideHost(Model requester)
        {
            var container = CreateContainer(requester);
            //ensure we can track it
            _ContainerRegistry.TryAdd(container, true);
            container.OnClose += (m) => _ContainerRegistry.TryRemove(m, out _);
            container.StartHost();
            return container;
        }





    }
}
