using ConnectFour.Messaging;

namespace ConnectFour.ThreadModel.PerModel
{
    /// <summary>
    /// This parallel schema provides container objects hosted by dedicated threads. Additional configuration is available
    /// to provide multiple models to each threaded container.
    /// 
    /// <para>Where large numbers of models are required, consider <see cref="Pool.ParallelPool"/>.</para>
    /// </summary>
    public class ParallelThreadPerModel : ParallelSchema
    {

        public ParallelThreadPerModel() : base()
        {
        }

        /// <summary>
        /// Creates a new model container which generates and manages a thread internally
        /// to host the model.
        /// </summary>
        /// <param name="requester"></param>
        /// <returns></returns>
        protected override ModelContainer CreateContainer(Model requester)
        {
            return new SimpleContainer(requester, Parent, this);
        }
    }
}
