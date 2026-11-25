using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace FrostySdk
{
    // [DAI fix] Frosty's own worker threads for loading and applying mods, so the user can cap how many
    // cores they use and at what priority (the .NET thread pool can't go below one thread per core)
    public static class WorkerPool
    {
        private static readonly object sync = new object();
        private static readonly Queue<Action> queue = new Queue<Action>();
        // [DAI fix] helpers of a running For go before jobs that haven't started, since the For's caller waits on them
        private static readonly Queue<Action> helpers = new Queue<Action>();
        private static int threadCount;
        private static int idleCount;
        private static int maxThreads = DefaultThreads;
        private static ThreadPriority priority = ThreadPriority.Lowest;

        public static int DefaultThreads => Math.Max(1, Environment.ProcessorCount - 2);

        public static int MaxThreads => maxThreads;

        public static ThreadPriority Priority => priority;

        public static void Configure(int threads, ThreadPriority inPriority)
        {
            lock (sync)
            {
                maxThreads = Math.Max(1, Math.Min(threads, Environment.ProcessorCount));
                priority = inPriority;
                Monitor.PulseAll(sync);
            }
        }

        public static void Post(Action job) => Post(job, false);

        private static void Post(Action job, bool helper)
        {
            lock (sync)
            {
                (helper ? helpers : queue).Enqueue(job);
                if (idleCount > 0)
                    Monitor.Pulse(sync);
                if (queue.Count + helpers.Count > idleCount && threadCount < maxThreads)
                {
                    threadCount++;
                    new Thread(Work) { IsBackground = true, Name = "Frosty Worker" }.Start();
                }
            }
        }

        public static Task<T> Run<T>(Func<T> func)
        {
            TaskCompletionSource<T> tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            Post(() =>
            {
                try { tcs.SetResult(func()); }
                catch (Exception e) { tcs.SetException(e); }
            });
            return tcs.Task;
        }

        // the calling thread takes part, so this never waits on a job that hasn't started (safe with 1 thread)
        public static void For(int count, Action<int> body)
        {
            if (count <= 0)
                return;

            int next = -1;
            int done = 0;
            Exception error = null;
            ManualResetEventSlim finished = new ManualResetEventSlim(false);

            Action loop = () =>
            {
                int i;
                while ((i = Interlocked.Increment(ref next)) < count)
                {
                    try
                    {
                        if (Volatile.Read(ref error) == null)
                            body(i);
                    }
                    catch (Exception e)
                    {
                        Interlocked.CompareExchange(ref error, e, null);
                    }

                    if (Interlocked.Increment(ref done) == count)
                        finished.Set();
                }
            };

            for (int h = Math.Min(count, maxThreads) - 1; h > 0; h--)
                Post(loop, true);

            loop();
            finished.Wait();

            if (error != null)
                ExceptionDispatchInfo.Capture(error).Throw();
        }

        public static void ForEach<T>(IList<T> items, Action<T> body) => For(items.Count, i => body(items[i]));

        private static void Work()
        {
            while (true)
            {
                Action job;
                ThreadPriority jobPriority;

                lock (sync)
                {
                    while (queue.Count == 0 && helpers.Count == 0)
                    {
                        if (threadCount > maxThreads)
                        {
                            threadCount--;
                            return;
                        }

                        idleCount++;
                        bool signalled = Monitor.Wait(sync, 30000);
                        idleCount--;

                        if (!signalled && queue.Count == 0 && helpers.Count == 0)
                        {
                            threadCount--;
                            return;
                        }
                    }

                    if (threadCount > maxThreads)
                    {
                        threadCount--;
                        return;
                    }

                    job = (helpers.Count > 0) ? helpers.Dequeue() : queue.Dequeue();
                    jobPriority = priority;
                }

                Thread.CurrentThread.Priority = jobPriority;
                job();
            }
        }
    }
}
