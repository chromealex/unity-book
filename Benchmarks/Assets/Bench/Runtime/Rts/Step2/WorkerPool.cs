using System;
using System.Threading;

// Шаг 2 (rts_2.md), без изменений.
namespace BookBench.Rts.Step2 {

    public sealed class WorkerPool : IDisposable {

        private readonly Thread[] threads;
        private readonly SemaphoreSlim[] start;
        private readonly CountdownEvent done;
        private Action<int> job;
        private volatile bool disposed;

        // Главный поток работает как worker 0
        public int WorkerCount => this.threads.Length + 1;

        public WorkerPool(int backgroundThreads) {
            this.threads = new Thread[backgroundThreads];
            this.start = new SemaphoreSlim[backgroundThreads];
            this.done = new CountdownEvent(backgroundThreads);
            for (int t = 0; t < backgroundThreads; ++t) {
                this.start[t] = new SemaphoreSlim(0);
                var workerIndex = t + 1;
                this.threads[t] = new Thread(() => this.Loop(workerIndex)) {
                    IsBackground = true,
                    Name = "RTS Worker " + workerIndex,
                };
                this.threads[t].Start();
            }
        }

        private void Loop(int workerIndex) {
            var signal = this.start[workerIndex - 1];
            while (true) {
                signal.Wait();
                if (this.disposed) return;
                try {
                    this.job(workerIndex);
                } finally {
                    this.done.Signal();
                }
            }
        }

        // Запускает job на всех воркерах и ждет завершения. Не аллоцирует.
        public void Run(Action<int> job) {
            this.job = job;
            this.done.Reset(this.threads.Length);
            for (int t = 0; t < this.start.Length; ++t) {
                this.start[t].Release();
            }
            job(0);
            this.done.Wait();
            this.job = null;
        }

        public void Dispose() {
            this.disposed = true;
            for (int t = 0; t < this.start.Length; ++t) {
                this.start[t].Release();
            }
            for (int t = 0; t < this.threads.Length; ++t) {
                this.threads[t].Join();
            }
            this.done.Dispose();
        }
    }
}
