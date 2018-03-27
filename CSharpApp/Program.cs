using System;

namespace BlockChain
{
    class Program
    {
        static void Main(string[] args)
        {
            var chain = new BlockChain();
            var server = new WebServer(chain);
            Console.WriteLine("Application is starting. Press any key will terminal the application!!!");
            Console.Read();
        }
    }
}
