
using System;
using System.Configuration;
using System.IO;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;

namespace BlockChain
{
    public class WebServer
    {
        public WebServer(BlockChain chain)
        {
            var settings = ConfigurationManager.AppSettings;
            string host = settings["host"] ?? "localhost";
            string port = settings["port"] ?? "80";
            string http = "http";

            var server = new TinyWebServer.WebServer(request =>
            {
                http = request.IsSecureConnection? "https" : "http";

                string path = request.Url.PathAndQuery.ToLower();
                string query = string.Empty ;
                string json = string.Empty;
                if(path.Contains("?"))
                {
                    string[] parts = path.Split('?');
                    path = parts[0] ;
                    query = parts[2];
                }

                switch(path)
                {
                    //GET: http://localhost:8000/mine
                    case "/mine":
                        return chain.Mine() ;
                    
                    //POST: http://localhost:8000/transactions/new
                    case "/transactions/new":
                        if(request.HttpMethod != HttpMethod.Post.Method)
                            return $"{new HttpResponseMessage(HttpStatusCode.MethodNotAllowed)}" ;

                        json = new StreamReader(request.InputStream).ReadToEnd();
                        var trx = JsonConvert.DeserializeObject<Transaction>(json);
                        int blockId = chain.CreateTransaction(trx.Sender, trx.Recipient, trx.Amount) ;
                        return $"Your transaction will be included in block {blockId}";

                    //GET: http://localhost:8000/chain
                    case "/chain":
                        return chain.GetFullChain() ;

                    //POST: http://localhost:8000/nodes/register
                    case "/nodes/register":
                        if(request.HttpMethod != HttpMethod.Post.Method)
                            return $"{new HttpResponseMessage(HttpStatusCode.MethodNotAllowed)}" ;

                        json = new StreamReader(request.InputStream).ReadToEnd();
                        var urlList = new { Urls = new string[0] };
                        var obj = JsonConvert.DeserializeAnonymousType(json, urlList);
                        return chain.RegisterNodes(http, obj.Urls);

                    //GET: http://localhost:8000/nodes/resolve
                    case "/nodes/resolve":
                        return chain.Consensus();
                }

                return "" ;
            },
            $"{http}://{host}:{port}/mine/",
            $"{http}://{host}:{port}/transactions/new/",
            $"{http}://{host}:{port}/chain/",
            $"{http}://{host}:{port}/nodes/register/",
            $"{http}://{host}:{port}/nodes/resolve/"
            );

            server.Run() ;
            Console.WriteLine($"{http}://{host}:{port} is running.") ;
        }
    }
}