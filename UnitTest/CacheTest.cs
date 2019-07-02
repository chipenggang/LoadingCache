using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace UnitTest
{
    public class CacheTest
    {
        [Fact]
        public void TestCache()
        {

            var v = 10;
            var vv = 11;

            Assert.Equal(v, vv);


        }
    }
}
