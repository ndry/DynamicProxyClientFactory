using System;
using System.Threading.Tasks;

namespace DynamicProxyClientFactory
{
    class Program
    {
        static void Main(string[] args)
        {
            var createClient = DynamicProxyClientFactory.Create(new Uri(@"http://www.dneonline.com/calculator.asmx?wsdl"));
            var client = createClient("CalculatorSoap");

            var a = 5;
            var b = 17;
            
            ((Task<int>) client.AddAsync(a, b))
                .ContinueWith(
                    t => Console.WriteLine("{0} + {1} = {2}", a, b, t.Result),
                    TaskContinuationOptions.OnlyOnRanToCompletion);

            Console.ReadKey();
        }

    }
}
