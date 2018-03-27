using System;

namespace BlockChain
{
    public class Node
    {
        public Uri Address { get; private set; }

        public Node(Uri _address)
        {
            Address = _address ;
        }
    }
}