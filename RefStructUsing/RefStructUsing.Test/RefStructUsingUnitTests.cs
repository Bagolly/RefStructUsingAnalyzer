using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Threading.Tasks;
using Verifier = RefStructUsing.Test.CSharpCodeFixVerifier<RefStructUsing.RefStructUsingAnalyzer, RefStructUsing.RefStructUsingCodeFixProvider>;


namespace RefStructUsing.Test
{
    [TestClass]
    public class RefStructUsingUnitTest
    {
        // A note on adding test cases:
        // The analyzer's codefixer relies on the editor formatting the trivia according to standard C# rules.
        // This works when actually running in Visual Studio, but the tester won't do any formatting.
        // Because of this, the test and expected strings need to be *very* precise, as even a single whitespace
        // or a different line ending (usually insivisible in IDEs by default) will cause the test to fail.
        // To make this somewhat easier, test diagnostics will report a diff for failing tests.


        [TestMethod]
        public async Task Analyzer_ValidCode_ReportsNothing()
        {
            var test = @"
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.Text;
            using System.Threading.Tasks;
            using System.Diagnostics;

            namespace ConsoleApplication1
            {
                ref struct S { public void Dispose() { } }

                class Program
                {
                    public void Main()
                    {
                       using S case1 = new S();                 // typed 'using'
                       using var case2 = new S();               // inferred 'using'
                       using S case3a = new(), case3b = new();  // multiple 'using'
                       using(var case4 = new S()) { }           // 'using' with explicit scope 
                       using S case5 = default;                 // default initialized
                       S case6; using(case6 = new()){ };        // delayed instantiation (only possible with old syntax)
                    }
                }
            }";

            await Verifier.VerifyAnalyzerAsync(test);
        }


        [TestMethod]
        public async Task Analyzer_InvalidDisposeSignature_ReportsNothing()
        {   
            var test = """
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.Text;
            using System.Threading.Tasks;
            using System.Diagnostics;
            namespace ConsoleApplication1
            {           
                class Program
                {
                    static void Main() 
                    { 
                      var case1 = new S1(); 
                      var case2 = new S2(); 
                      var case3 = new S3(); 
                      var case4 = new S4();
                      var case5 = new S5();
                    }

                    ref struct S1 { public static void Dispose() { } }        // static

                    ref struct S2 { private void Dispose() { } }              // private

                    ref struct S3 { public void Dispose(bool disposing) { } } // not parameterless

                    ref struct S4 { public bool Dispose() { return true; } }  // not void

                    ref struct S5 { public void dispose() { } }               // not called Dispose
                }
            }
            """;

            await Verifier.VerifyAnalyzerAsync(test);
        }


        [TestMethod]
        public async Task Analyzer_MissingUsing_ReportsWarningAndFixesCode()
        {
            var test = @"
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.Text;
            using System.Threading.Tasks;
            using System.Diagnostics;

            namespace ConsoleApplication1
            {
                ref struct S { public void Dispose() { } }

                class Program
                {
                    public void Main(){{|#0:S instance = new S();|}}
                }
            }";

            var fixtest = @"
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.Text;
            using System.Threading.Tasks;
            using System.Diagnostics;

            namespace ConsoleApplication1
            {
                ref struct S { public void Dispose() { } }

                class Program
                {
                    public void Main(){ using S instance = new S(); }
                }
            }";

            var expected = Verifier
                .Diagnostic(RefStructUsingAnalyzer.DiagnosticId)
                .WithLocation(0)
                .WithArguments("instance");

            await Verifier.VerifyCodeFixAsync(test, expected, fixtest);
        }
    }
}
