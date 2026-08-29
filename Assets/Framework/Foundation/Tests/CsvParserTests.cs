using Framework.Foundation.File;
using NUnit.Framework;

namespace Framework.Foundation.Tests
{
    public class CsvParserTests
    {
        [Test]
        public void Read_ParsesRowsAndColumns()
        {
            var rows = CsvParser.Read("a,b,c\n1,2,3");

            Assert.AreEqual(2, rows.Length);
            Assert.AreEqual(new[] { "a", "b", "c" }, rows[0]);
            Assert.AreEqual(new[] { "1", "2", "3" }, rows[1]);
        }

        [Test]
        public void Read_SkipsEmptyLines()
        {
            var rows = CsvParser.Read("a,b\n\n   \nc,d\n");

            Assert.AreEqual(2, rows.Length);
            Assert.AreEqual(new[] { "c", "d" }, rows[1]);
        }

        [Test]
        public void Read_ReturnsEmpty_ForEmptyText()
        {
            Assert.AreEqual(0, CsvParser.Read(string.Empty).Length);
        }
    }
}
