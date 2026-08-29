using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Foundation.Tests")]

// Граф DI — это Foundation + Features вместе, поэтому его тест живёт в Features.Tests
// и вызывает internal-перегрузки RegisterAutoTypes/RegisterConfigs с явным набором сборок.
[assembly: InternalsVisibleTo("Features.Tests")]
