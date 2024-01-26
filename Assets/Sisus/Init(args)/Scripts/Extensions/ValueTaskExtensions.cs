using System.Threading.Tasks;

namespace Sisus.Init.Internal
{
    /// <summary>
    /// Extension methods for <see cref="ValueTask{TResult}"/>.
    /// </summary>
    public static class ValueTaskExtensions
    {
        /// <summary>
        /// Converts the generic <see cref="ValueTask{TResult}"/> to the non-generic <see cref="ValueTask"/>.
        /// </summary>
        public static ValueTask<object> AsObjectValueTask<TResult>(this ValueTask<TResult> valueTask)
        {
            if(valueTask.IsCompletedSuccessfully)
            {
                valueTask.GetAwaiter().GetResult();
                return default;
            }

            return new ValueTask<object>(valueTask.AsTask());
        }
    }
}