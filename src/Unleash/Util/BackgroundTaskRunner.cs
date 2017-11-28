﻿using System;
using System.Collections.Generic;
using System.Threading;
using Unleash.Logging;

namespace Unleash.Util
{
    internal interface IBackgroundTaskRunner : IDisposable
    {
        void Register(IBackgroundTask task, TimeSpan interval, bool executeImmediately);
    }

    internal class BackgroundTaskRunnerV2 : IBackgroundTaskRunner
    {
        private static readonly ILog Logger = LogProvider.GetLogger(typeof(BackgroundTaskRunner));
        private static readonly TimeSpan TimeSpanExecuteImmediately = TimeSpan.Zero;

        private readonly Dictionary<string, Timer> timers = new Dictionary<string, Timer>();

        private readonly CancellationToken cancellationToken;

        public BackgroundTaskRunnerV2(CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;
        }

        public void Register(IBackgroundTask task, TimeSpan interval, bool executeImmediately)
        {
            var name = task.GetType().Name;

            async void Callback(object state)
            {
                if (!(state is CallbackState localState))
                    return;
                
                if (!timers.TryGetValue(localState.Name, out Timer localTimer))
                    return;

                try
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    await task.Execute(cancellationToken);
                }
                catch (Exception ex)
                {
                    Logger.ErrorException($"UNLEASH: Unhandled exception from background task '{name}'.", ex);
                }
                finally
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        if (interval == TimeSpanExecuteImmediately)
                        {
                            SafeTimerChange(localTimer, -1, Timeout.Infinite);
                            Logger.Trace($"UNLEASH: Stopped background task '{name}'...");
                        }
                        else
                        {
                            SafeTimerChange(localTimer, interval, Timeout.InfiniteTimeSpan);
                            Logger.Trace($"UNLEASH: Scheduled background task '{name}' to run after '{interval.TotalSeconds}' seconds...");
                        }
                    }
                }
            }

            var dueTime = executeImmediately
                ? TimeSpanExecuteImmediately
                : interval;

            var callbackState = new CallbackState
            {
                Name = name,
                DueTime = dueTime
            };

            var timer = new Timer(
                callback: Callback,
                state: callbackState,
                dueTime: dueTime,
                period: Timeout.InfiniteTimeSpan);

            timers.Add(name, timer);
        }


        private void SafeTimerChange(Timer timer, TimeSpan dueTime, TimeSpan period)
        {
            try
            {
                timer?.Change(dueTime, period);
            }
            catch (ObjectDisposedException)
            {
                // race condition with Dispose can cause trigger to be called when underlying
                // timer is being disposed - and a change will fail in this case.
                // see 
                // https://msdn.microsoft.com/en-us/library/b97tkt95(v=vs.110).aspx#Anchor_2
                if (disposeEnded)
                {
                    // we still want to throw the exception in case someone really tries
                    // to change the timer after disposal has finished
                    // of course there's a slight race condition here where we might not
                    // throw even though disposal is already done.
                    // since the offending code would most likely already be "failing"
                    // unreliably i personally can live with increasing the
                    // "unreliable failure" time-window slightly
                    throw;
                }
            }
        }

        private void SafeTimerChange(Timer timer, int dueTime, int period)
        {
            try
            {
                timer?.Change(dueTime, period);
            }
            catch (ObjectDisposedException)
            {
                // race condition with Dispose can cause trigger to be called when underlying
                // timer is being disposed - and a change will fail in this case.
                // see 
                // https://msdn.microsoft.com/en-us/library/b97tkt95(v=vs.110).aspx#Anchor_2
                if (disposeEnded)
                {
                    // we still want to throw the exception in case someone really tries
                    // to change the timer after disposal has finished
                    // of course there's a slight race condition here where we might not
                    // throw even though disposal is already done.
                    // since the offending code would most likely already be "failing"
                    // unreliably i personally can live with increasing the
                    // "unreliable failure" time-window slightly
                    throw;
                }
            }
        }

        private bool disposeEnded;
        public void Dispose()
        {
            var timeout = TimeSpan.FromSeconds(1);

            using (var waitHandle = new ManualResetEvent(false))
            {
                foreach (var task in timers)
                {
                    // Returns false on second dispose
                    if (task.Value.Dispose(waitHandle))
                    {
                        if (!waitHandle.WaitOne(timeout))
                        {
                            throw new TimeoutException($"UNLEASH: Timeout waiting for task '{task.Key}' to stop..");
                        }
                    }
                }
            }

            disposeEnded = true;
            timers.Clear();
        }

        internal class CallbackState
        {
            public string Name { get; set; }
            public TimeSpan DueTime { get; set; }
        }
    }


    internal class BackgroundTaskRunner : IDisposable
    {
        private static readonly ILog Logger = LogProvider.GetLogger(typeof(BackgroundTaskRunner));
        private static readonly TimeSpan TimeSpanExecuteImmediately = TimeSpan.Zero;

        private readonly Timer timer;
        private bool disposeEnded;
        public string Name { get; }

        public BackgroundTaskRunner(IBackgroundTask task, TimeSpan interval, bool executeImmediately, CancellationToken cancellationToken)
        {
            Name = task.GetType().Name;

            async void Callback(object state)
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;
                    
                    await task.Execute(cancellationToken);
                }
                catch (Exception ex)
                {
                    Logger.ErrorException($"UNLEASH: Unhandled exception from background task '{Name}'.", ex);
                }
                finally
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        if (interval == TimeSpanExecuteImmediately)
                        {
                            
                            SafeTimerChange(-1, Timeout.Infinite);
                            Logger.Trace($"UNLEASH: Stopped background task '{Name}'...");
                        }
                        else
                        {
                            SafeTimerChange(interval, Timeout.InfiniteTimeSpan);
                            Logger.Trace($"UNLEASH: Scheduled background task '{Name}' to run after '{interval.TotalSeconds}' seconds...");
                        }
                    }
                }
            }

            var dueTime = executeImmediately 
                ? TimeSpanExecuteImmediately 
                : interval;

            timer = new Timer(
                callback: Callback, 
                state: null, 
                dueTime: dueTime, 
                period: Timeout.InfiniteTimeSpan);
        }

        private void SafeTimerChange(TimeSpan dueTime, TimeSpan period)
        {
            try
            {
                timer?.Change(dueTime, period);
            }
            catch (ObjectDisposedException)
            {
                // race condition with Dispose can cause trigger to be called when underlying
                // timer is being disposed - and a change will fail in this case.
                // see 
                // https://msdn.microsoft.com/en-us/library/b97tkt95(v=vs.110).aspx#Anchor_2
                if (disposeEnded)
                {
                    // we still want to throw the exception in case someone really tries
                    // to change the timer after disposal has finished
                    // of course there's a slight race condition here where we might not
                    // throw even though disposal is already done.
                    // since the offending code would most likely already be "failing"
                    // unreliably i personally can live with increasing the
                    // "unreliable failure" time-window slightly
                    throw;
                }
            }
        }

        private void SafeTimerChange(int dueTime, int period)
        {
            try
            {
                timer?.Change(dueTime, period);
            }
            catch (ObjectDisposedException)
            {
                // race condition with Dispose can cause trigger to be called when underlying
                // timer is being disposed - and a change will fail in this case.
                // see 
                // https://msdn.microsoft.com/en-us/library/b97tkt95(v=vs.110).aspx#Anchor_2
                if (disposeEnded)
                {
                    // we still want to throw the exception in case someone really tries
                    // to change the timer after disposal has finished
                    // of course there's a slight race condition here where we might not
                    // throw even though disposal is already done.
                    // since the offending code would most likely already be "failing"
                    // unreliably i personally can live with increasing the
                    // "unreliable failure" time-window slightly
                    throw;
                }
            }
        }

        public void Dispose()
        {
            using (var waitHandle = new ManualResetEvent(false))
            {
                // Returns false on second dispose
                if (timer.Dispose(waitHandle))
                {
                    if (!waitHandle.WaitOne(TimeSpan.FromSeconds(1)))
                    {
                        throw new TimeoutException($"UNLEASH: Timeout waiting for task '{Name}' to stop..");
                    }

                    disposeEnded = true;
                }
            }
        }
    }
}