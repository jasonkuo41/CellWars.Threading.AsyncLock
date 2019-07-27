using System;
using Xunit;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using Xunit.Abstractions;

namespace CellWars.Threading.Tests {
    public class AsyncLockTests {
        private readonly ITestOutputHelper _testOutputHelper;

        public AsyncLockTests(ITestOutputHelper testOutputHelper) {
            _testOutputHelper = testOutputHelper;
        }

        private class RaceCoditionException : Exception { }

        private async Task CheckDuplicateThreadId(HashSet<int> ThreadId) {
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
                for (var i = 0; i < 1000; i++)
                    tasks.Add(CheckDuplicateThreadId(ThreadId));
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
    }
}
