// filepath: c:\Projekter\MyPostgresApi\Tests\NonParallelCollection.cs
using Xunit;
[CollectionDefinition("NonParallelCollection", DisableParallelization = true)]
public class NonParallelCollection { }