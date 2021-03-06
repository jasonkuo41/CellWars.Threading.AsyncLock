using System;
using Xunit;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using Xunit.Abstractions;
using CellWars.Threading;
using System.Diagnostics;

namespace CellWars.Threading.Tests {
    public class AsyncLockTests {

        public class RaceCoditionException : Exception { }

        private ITestOutputHelper Out;

        public AsyncLockTests(ITestOutputHelper output) {
            Out = output;
        }

        private static async Task CheckDuplicateThreadId(HashSet<int> ThreadId) {
            var id = Thread.CurrentThread.ManagedThreadId;
            if (ThreadId.Contains(id)) {
                throw new RaceCoditionException();
            }
            ThreadId.Add(id);
            await Task.Yield();
            ThreadId.Remove(id);
        }

        [Fact]
        public async Task PreTestRaceConditionTest() {
            HashSet<int> ThreadId = new HashSet<int>();
            List<Task> tasks = new List<Task>();

            await Assert.ThrowsAsync<RaceCoditionException>(async () => {
                for (var i = 0; i < 1000; i++) {
                    tasks.Add(CheckDuplicateThreadId(ThreadId));
                }
                await Task.WhenAll(tasks);
            });
        }

        [Fact]
        public async Task AsyncLock_BasicReq() {
            HashSet<int> ThreadId = new HashSet<int>();
            var Mutex = new AsyncLock();

            async Task PushListAsync() {
                using (await Mutex.LockAsync()) {
                    await CheckDuplicateThreadId(ThreadId);
                }
            }

            await Task.WhenAll(Enumerable.Range(0, 10000).Select(x => PushListAsync()));
            Mutex.Dispose();
        }

        [Fact]
        public async Task AsyncLock_TestReentrant() {
            HashSet<int> ThreadId = new HashSet<int>();
            var Mutex = new AsyncLock();

            async Task PushListAsync() {
                using (await Mutex.LockAsync()) {
                    using (await Mutex.LockAsync()) {
                        await CheckDuplicateThreadId(ThreadId);
                    }
                }
            }

            await Task.WhenAll(Enumerable.Range(0, 10000).Select(x => PushListAsync()));
        }

        [Fact]
        public async Task AsyncLock_MutlipleLocks() {

            HashSet<int> ThreadId = new HashSet<int>();
            HashSet<int> ThreadId1 = new HashSet<int>();
            var Mutex = new AsyncLock();
            var Mutex2 = new AsyncLock();

            async Task PushListAsync(int number) {
                if (number % 2 == 0) {
                    using (await Mutex.LockAsync()) {
                        await CheckDuplicateThreadId(ThreadId);
                    }
                }
                else {
                    using (await Mutex2.LockAsync()) {
                        await CheckDuplicateThreadId(ThreadId1);
                    }
                }
            }

            await Task.WhenAll(Enumerable.Range(0, 10000).Select(x => PushListAsync(x)));
        }

        [Fact]
        public async Task AsyncLock_TestReentrantReal() {
            HashSet<int> ThreadId = new HashSet<int>();
            var Mutex = new AsyncLock();

            async Task PushListAsync() {
                using (await Mutex.LockAsync()) {
                    await NestFuncAsync();
                }
            }

            async Task NestFuncAsync() {
                using (await Mutex.LockAsync()) {
                    await CheckDuplicateThreadId(ThreadId);
                }
            }

            await Task.WhenAll(Enumerable.Range(0, 10000).Select(x => PushListAsync()));
        }

        [Fact]
        public async Task AsyncLock_MutlipleLayerLocks() {
            HashSet<int> ThreadId = new HashSet<int>();
            HashSet<int> ThreadId1 = new HashSet<int>();
            var Mutex = new AsyncLock();
            var Mutex2 = new AsyncLock();

            async Task PushListAsync(int number) {
                using (await Mutex.LockAsync()) {
                    using (await Mutex2.LockAsync()) {
                        await CheckDuplicateThreadId(ThreadId1);
                    }
                    await CheckDuplicateThreadId(ThreadId);
                }
            }

            await Task.WhenAll(Enumerable.Range(0, 10000).Select(x => PushListAsync(x)));
        }

        [Fact]
        public async Task AsyncTask_CancellationTest() {
            var Mutex = new AsyncLock();
            await Assert.ThrowsAsync<OperationCanceledException>(async () => {

                var ct = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

                async Task ForceLock() {
                    using (await Mutex.LockAsync(TimeSpan.FromMilliseconds(1000), ct.Token)) {
                        await Task.Delay(3000);
                    }
                }

                var task = ForceLock();
                await Task.Yield();
                using (await Mutex.LockAsync(ct.Token)) {
                }
            });

        }

        [Fact]
        public async Task AsyncLock_MixLocks() {
            HashSet<int> ThreadId = new HashSet<int>();
            var Mutex = new AsyncLock();

            async Task PushListAsync() {
                using (Mutex.Lock()) {
                    using (await Mutex.LockAsync()) {
                        await CheckDuplicateThreadId(ThreadId);
                    }
                }
            }

            await Task.WhenAll(Enumerable.Range(0, 10000).Select(x => PushListAsync()));
        }

        [Fact]
        public async Task AsyncLock_MultiLockFirstSyncThenAsync() {
            HashSet<int> ThreadId = new HashSet<int>();
            var Mutex = new AsyncLock(TimeSpan.FromSeconds(10));
            var Mutex1 = new AsyncLock(TimeSpan.FromSeconds(10));

            async Task PushListAsync() {
                using (Mutex.Lock()) {
                    using (await Mutex1.LockAsync()) {
                        await CheckDuplicateThreadId(ThreadId);
                    }
                }
                using (await Mutex.LockAsync()) {
                    using (Mutex1.Lock()) {
                        await CheckDuplicateThreadId(ThreadId);
                    }
                }
            }

            await Task.WhenAll(Enumerable.Range(0, 1000).Select(x => PushListAsync()));
        }

        [Fact]
        public async Task AsyncLock_ThrowExceptionTestAsync() {
            HashSet<int> ThreadId = new HashSet<int>();
            var Mutex = new AsyncLock(TimeSpan.FromMilliseconds(100));

            await Assert.ThrowsAsync<TimeoutException>(async () => {
                async Task TimeoutMutexAsync() {
                    using (await Mutex.LockAsync()) {
                        await Task.Delay(1000);
                    }
                };
                async Task ThrowAsync() {
                    using (await Mutex.LockAsync()) {
                        await Task.Yield();
                    }
                }

                var task1 = TimeoutMutexAsync();
                var task2 = ThrowAsync();
                await Task.WhenAll(task1, task2);
            });

            async Task PushListAsync() {
                using (await Mutex.LockAsync()) {
                    await CheckDuplicateThreadId(ThreadId);
                }
            }
            await Task.WhenAll(Enumerable.Range(0, 1000).Select(x => PushListAsync()));
        }
    }
}
