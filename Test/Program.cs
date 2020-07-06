using System;

using LIME;

using System.Diagnostics;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            LIME.TestClass.Test();
            Console.WriteLine("Hello World!");

            //var match = ('a'._() + 'b').Search("abc");

            //Debug.WriteLine(match.ToString("abc"));

            //foreach(var subMatch in match.SubMatches)
            //{
            //    Debug.WriteLine(subMatch.ToString("abc"));
            //}

        }
    }
}
