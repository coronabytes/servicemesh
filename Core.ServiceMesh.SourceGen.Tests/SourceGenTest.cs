namespace Core.ServiceMesh.SourceGen.Tests;

public class SourceGenTest
{
    [Fact]
    public Task ProxyInterface()
    {
        var source = """
                     using System.Collections.Generic;
                     using System.Threading.Tasks;
                     using System.Numerics;
                     using Core.ServiceMesh.Abstractions;

                     namespace SampleApp;

                     [ServiceMesh("someservice")]
                     public interface ISomeService
                     {
                         ValueTask A(string a);
                         Task Aa(string a);
                         ValueTask<string> B(string b);
                         Task<string> Bb(string b);
                         ValueTask<T> C<T>(T a, T b) where T : INumber<T>;
                         IAsyncEnumerable<string> D(string d);
                         IAsyncEnumerable<T> E<T>(T e) where T : INumber<T>;
                     }

                     [ServiceMesh]
                     public class SomeService : ISomeService
                     {
                         public async ValueTask A(string a)
                         {
                            
                         }
                         public async Task Aa(string a)
                         {
                            
                         }
                         public async ValueTask<string> B(string b)
                         {
                            return b + " " + b;
                         }
                         public async Task<string> Bb(string b)
                         {
                            return b + " " + b;
                         }
                         public async ValueTask<T> C<T>(T a, T b) where T : INumber<T>
                         {
                            return a + b;
                         }
                         public async IAsyncEnumerable<string> D(string d)
                         {
                            yield return "a";
                            yield return "b";
                            yield return "c";
                         }
                         public async IAsyncEnumerable<T> E<T>(T e) where T : INumber<T>
                         {
                            yield return e;
                            yield return e;
                            yield return e;
                         }
                     }
                     """;

        return TestHelper.VerifySourceGen(source);
    }
}