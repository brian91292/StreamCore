using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StreamCore.Utilities
{
    public class TaskHelper
    {
        private static Dictionary<string, CancellationTokenSource> _cancellationTokens = new Dictionary<string, CancellationTokenSource>();

        /// <summary>
        /// Cancels a task based on the unique string identifier provided.
        /// </summary>
        /// <param name="identifier"></param>
        public static void CancelTask(string identifier)
        {
            if (_cancellationTokens.TryGetValue(identifier, out var cancellationToken))
            {
                cancellationToken?.Cancel();
                Plugin.Log($"Cancelling task with identifier {identifier}.");
            }
        }

        /// <summary>
        /// Schedules a task to be run at the specified time.
        /// </summary>
        /// <param name="action">The action to be executed.</param>
        /// <param name="time">The time (UTC) the event should be executed.</param>
        /// <param name="ct">The cancellation token associated with this task.</param>
        /// <returns>The task generated as a result of this function call.</returns>
        public static Task ScheduleActionAtTime(Action action, DateTime time, out CancellationTokenSource ct)
        {
            var newCancellationToken = new CancellationTokenSource();
            ct = newCancellationToken;
            return Task.Run(async delegate
            {
                try
                {
                    await Task.Delay(time - DateTime.UtcNow, newCancellationToken.Token);
                    if(!newCancellationToken.IsCancellationRequested)
                        action?.Invoke();
                }
                catch (ThreadAbortException) { Plugin.Log("Thread aborting!"); }
                Plugin.Log("Task completed!");
            }, newCancellationToken.Token);
        }

        /// <summary>
        /// Schedules a task to be run at the specified time, cancelling the existing task if required.
        /// </summary>
        /// <param name="identifier">The unique identifier which defines the task.</param>
        /// <param name="action">The action to be executed.</param>
        /// <param name="time">The time (UTC) the event should be executed.</param>
        /// <returns></returns>
        public static Task ScheduleUniqueActionAtTime(string identifier, Action action, DateTime time)
        {
            if (_cancellationTokens.TryGetValue(identifier, out var oldCancellationToken))
            {
                Plugin.Log($"Cancelling old instance of {identifier}!");
                oldCancellationToken?.Cancel();
            }

            Task task = ScheduleActionAtTime(action, time, out var cancellationToken);
            _cancellationTokens[identifier] = cancellationToken;
            return task;
        }

        /// <summary>
        /// Schedules an action to be repeated at a set rate.
        /// </summary>
        /// <param name="action">The action to be executed.</param>
        /// <param name="delay">The delay in milliseconds.</param>
        /// <param name="ct">The cancellation token associated with this task.</param>
        /// <returns>The task generated as a result of this function call.</returns>
        public static Task ScheduleRepeatingAction(Action action, int delay, out CancellationTokenSource ct)
        {
            var newCancellationToken = new CancellationTokenSource();
            ct = newCancellationToken;
            return Task.Run(async delegate
            {
                try
                {
                    while (!Globals.IsApplicationExiting && !newCancellationToken.IsCancellationRequested)
                    {
                        action?.Invoke();
                        await Task.Delay(delay, newCancellationToken.Token);
                    }
                }
                catch (ThreadAbortException) { Plugin.Log("Thread aborting!"); }
                Plugin.Log("Task completed!");
            }, newCancellationToken.Token);
        }

        /// <summary>
        /// Schedules an action to be repeated at a set rate, cancelling the existing task if required.
        /// </summary>
        /// <param name="identifier">The unique identifier which defines the task.</param>
        /// <param name="action">The action to be executed.</param>
        /// <param name="delay">The delay in milliseconds.</param>
        /// <returns>The task generated as a result of this function call.</returns>
        public static Task ScheduleUniqueRepeatingAction(string identifier, Action action, int delay)
        {
            if (_cancellationTokens.TryGetValue(identifier, out var oldCancellationToken))
            {
                Plugin.Log($"Cancelling old instance of {identifier}!");
                oldCancellationToken?.Cancel();
            }

            Task task = ScheduleRepeatingAction(action, delay, out var newCancellationToken);
            _cancellationTokens[identifier] = newCancellationToken;
            return task;
        }
    }
}
