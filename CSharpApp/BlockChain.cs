using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace BlockChain
{
    public class BlockChain
    {
        private List<Transaction> _Transactions = new List<Transaction>() ;
        private List<Block> _Chains = new List<Block>();
        private List<Node> _Nodes = new List<Node>() ;
        private Block _lastBlock => _Chains.Last() ;

        public string NodeId { get; set; }

        public BlockChain()
        {
            NodeId = Guid.NewGuid().ToString().Replace("-","");
            CreateNewBlock(proof: 100, previousHash: "1") ; //genesis block
        }

        private Block CreateNewBlock(int proof, string previousHash = null)
        {
            var block = new Block() 
            {
                Index = _Chains.Count,
                Timestamp = DateTime.Now,
                Transactions = _Transactions.ToList(),
                Proof = proof,
                PreviousHash = previousHash ?? GetHash(_Chains.Last())
            } ;

            _Transactions.Clear();
            _Chains.Add(block) ;
            return block ;
        }

        private void RegisterNode(string address) 
        {
            _Nodes.Add(new Node(new Uri(address))) ;
        }

        private bool IsValidChain(List<Block> chains)
        {
            Block block = null ;
            Block lastBlock = chains.First() ;
            int currentIndex = 1;
            while (currentIndex < chains.Count)
            {
                block = chains[currentIndex] ;
                Debug.WriteLine($"{lastBlock}") ;
                Debug.WriteLine($"{block}") ;
                Debug.WriteLine("---------------------") ;

                if (block.PreviousHash != GetHash(lastBlock))
                    return false ;

                if (!IsValidProof(lastBlock.Proof, block.Proof, lastBlock.PreviousHash))
                    return false ;
                
                lastBlock = block ;
                currentIndex ++ ;
            }

            return true ;
        }

        private string GetHash(Block block)
        {
            string blockText = JsonConvert.SerializeObject(block);
            return GetSha256(blockText);
        }

        private string GetSha256(string blockText)
        {
            var sha256 = new SHA256Managed() ;
            var hashBuilder = new StringBuilder() ;

            byte[] bytes = Encoding.Unicode.GetBytes(blockText) ;
            byte[] hash = sha256.ComputeHash(bytes) ;

            foreach(var x in hash){
                hashBuilder.Append($"{x:x2}");
            }

            return hashBuilder.ToString();
        }

        private bool IsValidProof(int lastProof, int proof, string previousHash)
        {
            string guess = $"{lastProof}{proof}{previousHash}" ;
            string result = GetSha256(guess) ;
            return result.StartsWith("0000") ;
        }

        private int CreateProofOfWork(int lastProof, string previousHash)
        {
            int proof = 0;
            while(!IsValidProof(lastProof, proof, previousHash))
                proof ++;
            
            return proof;
        }

        private bool ResolveConflicts()
        {
            List<Block> newChains = null ;
            int maxLength = _Chains.Count ;

            foreach(var node in _Nodes)
            {
                var url = new Uri(node.Address, "/chain");
                var request = (HttpWebRequest)WebRequest.Create(url) ;
                var response = (HttpWebResponse)request.GetResponse();

                if(response.StatusCode != HttpStatusCode.OK)
                {
                    Debug.WriteLine("response is not okay") ;
                    continue;
                }

                var model = new  
                {
                    Chain = new List<Block>(),
                    Length = 0
                };
                
                string json = new StreamReader(response.GetResponseStream()).ReadToEnd() ;
                var data = JsonConvert.DeserializeAnonymousType(json, model) ;

                if(data.Chain.Count > _Chains.Count && IsValidChain(data.Chain))
                {
                    maxLength = data.Chain.Count;
                    newChains = data.Chain;
                }
                else
                    Debug.WriteLine("exception chain here...") ;
            }

            if(newChains != null)
            {
                _Chains = newChains ;
                return true ;
            }

            return false ;
        }

        internal string Mine()
        {
            int proof = CreateProofOfWork(_lastBlock.Proof, _lastBlock.PreviousHash);
            
            CreateTransaction(sender: "0", recipient: NodeId, amount: 1);

            Block block = CreateNewBlock(proof);

            var response = new 
            {
                Mesage = "New Block Forget",
                Index = block.Index,
                Transactions = block.Transactions.ToArray(),
                Proof = block.Proof,
                PreviousHash = block.PreviousHash
            };

            return JsonConvert.SerializeObject(response);
        }

        internal int CreateTransaction(string sender, string recipient, int amount)
        {
            var transaction = new Transaction(sender, recipient, amount);

            _Transactions.Add(transaction) ;

            return _lastBlock != null ? _lastBlock.Index + 1 : 0;
        }

        internal string GetFullChain()
        {
            var response = new 
            {
                Chain = _Chains.ToArray(),
                Length = _Chains.Count
            };

            return JsonConvert.SerializeObject(response);
        }

        internal string RegisterNodes(string http, string[] nodes)
        {
            var builder = new StringBuilder();
            foreach(var node in nodes)
            {
                string url = $"{http}://{node}";
                RegisterNode(url);
                builder.Append($"{url},");
            }

            builder.Insert(0, $"{nodes.Count()} new nodes have been added: ");
            var result = builder.ToString();
            return result.Substring(0, result.Length - 2);
        }

        internal string Consensus()
        {
            bool replaced = ResolveConflicts();
            string message = replaced ? "was replaced" : "is authoritive" ;

            var response = new 
            {
                Message = $"Our chain {message}",
                Chains = _Chains
            };

            return JsonConvert.SerializeObject(response) ;
        }
    }
}