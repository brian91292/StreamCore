using StreamCore.YouTube;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StreamCore.Utilities
{
    public class TaskHelper
    {
        private static ConcurrentDictionary<Assembly, ConcurrentDictionary<string, CancellationTokenSource>> _cancellationTokensForAssembly = new ConcurrentDictionary<Assembly, ConcurrentDictionary<string, CancellationTokenSource>>();

        /// <summary>
        /// Cancels a task based on the unique string identifier provided.
        /// </summary>
        /// <param name="identifier"></param>
        public static void CancelTask(string identifier)
        {
            // Return if there are no cancellation tokens for the current assembly
            if (!_cancellationTokensForAssembly.TryGetValue(Assembly.GetCallingAssembly(), out var cancellationTokens)) return;

            // Return if no cancellation token with the given identifier exists
            if (!cancellationTokens.TryGetValue(identifier, out var cancellationToken)) return;

            // Invoke the cancellation token, if it exists
            try
            {
                cancellationToken?.Cancel();
                Plugin.Log($"Cancelled task with identifier {identifier}.");
            }
            catch(Exception ex)
            {
                Plugin.Log($"Error when trying to cancel task {identifier}, {ex.ToString()}");
            }
        }

        /// <summary>
        /// Cancel all tasks that are currently running
        /// </summary>
        public static void CancelAllTasks()
        {
            var assembly = Assembly.GetCallingAssembly();
            // Return if there are no cancellation tokens for the current assembly
            if (!_cancellationTokensForAssembly.TryGetValue(assembly, out var cancellationTokens)) return;
            
            lock (_cancellationTokensForAssembly[assembly])
            {
                // Iterate through each cancellation token for our assembly 
                foreach (CancellationTokenSource token in cancellationTokens.Values)
                    token?.Cancel();

                // Clear the list of cancellation tokens
                _cancellationTokensForAssembly[assembly].Clear();
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
                    TimeSpan delayTime = time - DateTime.UtcNow;
                    Plugin.Log($"Waiting for {delayTime.Minutes}m {delayTime.Seconds}s");
                    await Task.Delay(delayTime, newCancellationToken.Token);
                    if(!newCancellationToken.IsCancellationRequested)
                        action?.Invoke();
                }
                catch (ThreadAbortException) { Plugin.Log("Thread aborting!"); }
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
            // If any task already exists with this identifier, cancel it
            CancelTask(identifier);

            var assembly = Assembly.GetCallingAssembly();
            if (!_cancellationTokensForAssembly.ContainsKey(assembly))
                _cancellationTokensForAssembly[assembly] = new ConcurrentDictionary<string, CancellationTokenSource>();

            Task task = ScheduleActionAtTime(action, time, out var newCancellationToken);
            _cancellationTokensForAssembly[assembly][identifier] = newCancellationToken;
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
                    while (!newCancellationToken.IsCancellationRequested)
                    {
                        action?.Invoke();
                        await Task.Delay(delay, newCancellationToken.Token);
                    }
                }
                catch (ThreadAbortException) { Plugin.Log("Thread aborting!"); }
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
            // If any task already exists with this identifier, cancel it
            CancelTask(identifier);

            var assembly = Assembly.GetCallingAssembly();
            if (!_cancellationTokensForAssembly.ContainsKey(assembly))
                _cancellationTokensForAssembly[assembly] = new ConcurrentDictionary<string, CancellationTokenSource>();

            Task task = ScheduleRepeatingAction(action, delay, out var newCancellationToken);
            _cancellationTokensForAssembly[assembly][identifier] = newCancellationToken;
            return task;
        }
    }
}
