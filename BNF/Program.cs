using System;
using LIME;
using static LIME.BuiltInMatcher;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

/// <summary>
/// バッカス記法を解釈するパーサ
/// </summary>
namespace BNF
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            var bnf = "stringliteral   ::=  [stringprefix](shortstring | longstring)";

            var lime = BNFtoLIME(bnf);
            Debug.WriteLine(lime);
        }
        
        
    }
}
