using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CellWars.Threading.Tests {

    public class Content {

        public AsyncLock<Content> Mutex { get; }
        public double Field { get; set; } = Math.PI;

        public Content() {
            Mutex = new AsyncLock<Content>(this);
        }

    }

    public class AsyncLockGenericTests {
        [Fact]
        public void AsyncLock_Generic_SetField() {
            var content = new Content();

            async Task Passes() {
                var str1 = "cake";
                content.Mutex.SetField(x => x.Field, x => x = str1);
                var str = content.Mutex.AcquireField(x => x.Field);
                Assert.Equal(content.Field, str);
            }
        }
    }
}