using FluentAssertions;

namespace Test.CRoaring.Net
{
    // Forked from:
    // https://github.com/Auralytical/CRoaring.Net/blob/master/test/CRoaring.Net.Test/Tests.cs
    /*
        The MIT License (MIT)

        Copyright (c) 2015 RogueException

        Permission is hereby granted, free of charge, to any person obtaining a copy
        of this software and associated documentation files (the "Software"), to deal
        in the Software without restriction, including without limitation the rights
        to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
        copies of the Software, and to permit persons to whom the Software is
        furnished to do so, subject to the following conditions:

        The above copyright notice and this permission notice shall be included in all
        copies or substantial portions of the Software.

        THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
        IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
        FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
        AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
        LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
        OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
        SOFTWARE.
     */
    public class Tests
    {        
        [Fact]
        public void TestCardinality()
        {
            var values = new uint[] { 1, 2, 3, 4, 5, 100, 1000 };

            using var result = RoaringBitmap.FromValues(values);
            Assert.Equal(7U, result.Cardinality);
        }
        [Fact]
        public void TestMin()
        {
            var values = new uint[] { 1, 2, 3, 4, 5, 100, 1000 };

            using var result = RoaringBitmap.FromValues(values);
            Assert.Equal(1U, result.Min);
        }
        [Fact]
        public void TestMax()
        {
            var values = new uint[] { 1, 2, 3, 4, 5, 100, 1000 };

            using var result = RoaringBitmap.FromValues(values);
            Assert.Equal(1000U, result.Max);
        }

        [Fact]
        public void TestAdd()
        {
            var values = new uint[] { 1, 2, 3, 4, 5, 100, 1000 };
            var max = values.Max() + 1;

            using var rb1 = new RoaringBitmap();
            using var rb2 = new RoaringBitmap();
            using var rb3 = RoaringBitmap.FromValues(values);
            foreach (var t in values)
                rb1.Add(t);

            rb1.Optimize();

            rb2.AddMany(values);
            rb2.Optimize();

            rb3.Optimize();

            Assert.Equal(rb1.Cardinality, (uint)values.Length);
            Assert.Equal(rb2.Cardinality, (uint)values.Length);
            Assert.Equal(rb3.Cardinality, (uint)values.Length);

            for (uint i = 0; i < max; i++)
            {
                if (values.Contains(i))
                {
                    Assert.True(rb1.Contains(i));
                    Assert.True(rb2.Contains(i));
                    Assert.True(rb3.Contains(i));
                }
                else
                {
                    Assert.False(rb1.Contains(i));
                    Assert.False(rb2.Contains(i));
                    Assert.False(rb3.Contains(i));
                }
            }
        }

        [Fact]
        public void TestRemove()
        {
            var initialValues = new uint[] { 1, 2, 3, 4, 5, 100, 1000 };
            var removeValues = new uint[] { 2, 3 };
            var finalValues = initialValues.Except(removeValues).ToArray();
            var max = initialValues.Max() + 1;

            using var rb = RoaringBitmap.FromValues(initialValues);
            rb.RemoveMany(removeValues);
            rb.Optimize();

            Assert.Equal(rb.Cardinality, (uint)finalValues.Length);

            for (uint i = 0; i < max; i++)
            {
                if (finalValues.Contains(i))
                    Assert.True(rb.Contains(i));
                else
                    Assert.False(rb.Contains(i));
            }
        }

        [Fact]
        public void TestNot()
        {
            var values = new uint[] { 1, 2, 3, 4, 5, 100, 1000 };
            var max = values.Max() + 1;

            using var source = RoaringBitmap.FromValues(values);
            using var result = source.Not(0, max);
            for (uint i = 0; i < max; i++)
            {
                if (values.Contains(i))
                    Assert.False(result.Contains(i));
                else
                    Assert.True(result.Contains(i));
            }
        }

        [Fact]
        public void TestOr()
        {
            var values1 = new uint[] { 1, 2, 3, 4, 5, 100, 1000 };
            var values2 = new uint[] { 1, 2, 3, 4, 5, 100, 1000 };
            var values3 = new uint[] { 3, 4, 5, 7, 100, 1020 };

            using var source1 = RoaringBitmap.FromValues(values1);
            using var source2 = RoaringBitmap.FromValues(values2);
            using var source3 = RoaringBitmap.FromValues(values3);
            using var result1 = source1.Or(source2);
            using var result2 = source2.Or(source3);
            using var result3 = result1.Or(source3);
            Assert.Equal(result1.Cardinality, OrCount(values1, values2));
            Assert.Equal(result2.Cardinality, OrCount(values2, values3));
            Assert.Equal(result3.Cardinality, OrCount(values1, values2, values3));
        }
        [Fact]
        public void TestIOr()
        {
            var values1 = new uint[] { 1, 2, 3, 4, 5, 100, 1000 };
            var values2 = new uint[] { 1, 2, 3, 4, 5, 100, 1000 };
            var values3 = new uint[] { 3, 4, 5, 7, 100, 1020 };

            using var result = RoaringBitmap.FromValues(values1);
            using var source1 = RoaringBitmap.FromValues(values2);
            using var source2 = RoaringBitmap.FromValues(values3);
            result.ApplyOr(source1);
            result.ApplyOr(source2);
            Assert.Equal(result.Cardinality, OrCount(values1, values2, values3));
        }

        [Fact]
        public void TestAnd()
        {
            var values1 = new uint[] { 1, 2, 3, 4, 5, 100, 1000 };
            var values2 = new uint[] { 1, 2, 3, 4, 5, 100, 1000 };
            var values3 = new uint[] { 3, 4, 5, 7, 100, 1020 };

            using var source1 = RoaringBitmap.FromValues(values1);
            using var source2 = RoaringBitmap.FromValues(values2);
            using var source3 = RoaringBitmap.FromValues(values3);
            using var result1 = source1.And(source2);
            using var result2 = source2.And(source3);
            using var result3 = result1.And(source3);
            Assert.Equal(result1.Cardinality, AndCount(values1, values2));
            Assert.Equal(result2.Cardinality, AndCount(values2, values3));
            Assert.Equal(result3.Cardinality, AndCount(values1, values2, values3));
        }
        [Fact]
        public void TestIAnd()
        {
            var values1 = new uint[] { 1, 2, 3, 4, 5, 100, 1000 };
            var values2 = new uint[] { 1, 2, 3, 4, 5, 100, 1000 };
            var values3 = new uint[] { 3, 4, 5, 7, 100, 1020 };

            using var result = RoaringBitmap.FromValues(values1);
            using var source1 = RoaringBitmap.FromValues(values2);
            using var source2 = RoaringBitmap.FromValues(values3);
            result.ApplyAnd(source1);
            result.ApplyAnd(source2);
            Assert.Equal(result.Cardinality, AndCount(values1, values2, values3));
        }

        [Fact]
        public void TestAndNot()
        {
            var values1 = new uint[] { 1, 2, 3, 4, 5, 100, 1000 };
            var values2 = new uint[] { 1, 2, 3, 4, 5, 100, 1000 };
            var values3 = new uint[] { 3, 4, 5, 7, 100, 1020 };

            using var source1 = RoaringBitmap.FromValues(values1);
            using var source2 = RoaringBitmap.FromValues(values2);
            using var source3 = RoaringBitmap.FromValues(values3);
            using var result1 = source1.AndNot(source2);
            using var result2 = source2.AndNot(source3);
            using var result3 = result1.AndNot(source3);
            Assert.Equal(result1.Cardinality, AndNotCount(values1, values2));
            Assert.Equal(result2.Cardinality, AndNotCount(values2, values3));
            Assert.Equal(result3.Cardinality, AndNotCount(values1, values2, values3));
        }
        [Fact]
        public void TestIAndNot()
        {
            var values1 = new uint[] { 1, 2, 3, 4, 5, 100, 1000 };
            var values2 = new uint[] { 1, 2, 3, 4, 5, 100, 1000 };
            var values3 = new uint[] { 3, 4, 5, 7, 100, 1020 };

            using var result = RoaringBitmap.FromValues(values1);
            using var source1 = RoaringBitmap.FromValues(values2);
            using var source2 = RoaringBitmap.FromValues(values3);
            result.ApplyAndNot(source1);
            result.ApplyAndNot(source2);
            Assert.Equal(result.Cardinality, AndNotCount(values1, values2, values3));
        }

        [Fact]
        public void TestXor()
        {
            var values1 = new uint[] { 1, 2, 3, 4, 5, 100, 1000 };
            var values2 = new uint[] { 1, 2, 3, 4, 5, 100, 1000 };
            var values3 = new uint[] { 3, 4, 5, 7, 100, 1020 };

            using var source1 = RoaringBitmap.FromValues(values1);
            using var source2 = RoaringBitmap.FromValues(values2);
            using var source3 = RoaringBitmap.FromValues(values3);
            using var result1 = source1.Xor(source2);
            using var result2 = source2.Xor(source3);
            using var result3 = result1.Xor(source3);
            Assert.Equal(result1.Cardinality, XorCount(values1, values2));
            Assert.Equal(result2.Cardinality, XorCount(values2, values3));
            Assert.Equal(result3.Cardinality, XorCount(values1, values2, values3));
        }
        [Fact]
        public void TestIXor()
        {
            var values1 = new uint[] { 1, 2, 3, 4, 5, 100, 1000 };
            var values2 = new uint[] { 1, 2, 3, 4, 5, 100, 1000 };
            var values3 = new uint[] { 3, 4, 5, 7, 100, 1020 };

            using var result = RoaringBitmap.FromValues(values1);
            using var source1 = RoaringBitmap.FromValues(values2);
            using var source2 = RoaringBitmap.FromValues(values3);
            result.ApplyXor(source1);
            result.ApplyXor(source2);
            Assert.Equal(result.Cardinality, XorCount(values1, values2, values3));
        }

        [Fact]
        public void TestEnumerator()
        {
            var values = new uint[] { 1, 2, 3, 4, 5, 100, 1000 };

            using var result = RoaringBitmap.FromValues(values);
            result.Should().Equal(values);
            // Assert.True(result.SequenceEqual(values));
        }

        [Fact]
        public void TestSerialization()
        {
            using var rb1 = new RoaringBitmap();
            rb1.AddMany(1, 2, 3, 4, 5, 100, 1000);
            rb1.Optimize();

            var s1 = rb1.Serialize();
            var s2 = rb1.Serialize(SerializationFormat.Portable);

            using var rb2 = RoaringBitmap.Deserialize(s1);
            using var rb3 = RoaringBitmap.Deserialize(s2, SerializationFormat.Portable);
            Assert.True(rb1.Equals(rb2));
            Assert.True(rb1.Equals(rb3));
        }

        [Fact]
        public void TestStats()
        {
            var bitmap = new RoaringBitmap();
            bitmap.AddMany(1, 2, 3, 4, 6, 7);
            bitmap.AddMany(999991, 999992, 999993, 999994, 999996, 999997);
            var stats = bitmap.GetStatistics();

            Assert.Equal(bitmap.Cardinality, stats.Cardinality);
            Assert.Equal(2U, stats.ContainerCount);
            Assert.Equal(2U, stats.SortedSetContainerCount);
            Assert.Equal(0U, stats.BitsetContainerCount);
        }

        private static ulong OrCount(params IEnumerable<uint>[] values)
        {
            var set = values[0];
            for (var i = 1; i < values.Length; i++)
                set = set.Union(values[i]);
            return (ulong)set.LongCount();
        }
        private static ulong AndCount(params IEnumerable<uint>[] values)
        {
            var set = values[0];
            for (var i = 1; i < values.Length; i++)
                set = set.Intersect(values[i]);
            return (ulong)set.LongCount();
        }
        private static ulong AndNotCount(params IEnumerable<uint>[] values)
        {
            var set = values[0];
            for (var i = 1; i < values.Length; i++)
                set = set.Except(values[i]);
            return (ulong)set.LongCount();
        }
        private static ulong XorCount(params IEnumerable<uint>[] values)
        {
            var set = values[0];
            for (var i = 1; i < values.Length; i++)
            {
                var asArray = set as uint[] ?? set.ToArray();
                set = asArray.Except(values[i]).Union(values[i].Except(asArray));
            }

            return (ulong)set.LongCount();
        }
    }
}
