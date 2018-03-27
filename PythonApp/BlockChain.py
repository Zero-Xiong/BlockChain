import hashlib
import json

from time import time
from urllib.parse import urlparse
from uuid import uuid4

import requests
from flask import Flask, jsonify, request

class BlockChain:
    def __init__(self):
        self.Transactions = []
        self.Chain = []
        self.Nodes = set()

        self.new_block(previous_hash = "1", proof = 100)
    
    
    
    def new_block(self, previous_hash, proof):
        block = {
            "index": len(self.Chain) + 1,
            "timestamp": time(),
            "transactions": self.Transactions,
            "proof": proof,
            "previous_hash": previous_hash or self.hash(self.Chain[-1]),
        }

        self.Transactions = []
        self.Chain.append(block)

        return block
    
    
    @staticmethod
    def hash(block):
        block_string = json.dumps(block, sort_keys=True).encode()
        return hashlib.sha256(block_string).hexdigest()

    
    @property
    def last_block(self):
        return self.Chain[-1]


    def register_node(self, address):
        parsed_url = urlparse(address)
        if parsed_url.netloc:
            self.Nodes.add(parsed_url.netloc)
        elif parsed_url.path:
            self.Nodes.add(parsed_url.path)
        else:
            raise ValueError("Invalid URL")


    def valid_chain(self, chain):
        last_block = chain[0]
        current_index = 1

        while current_index < len(chain):
            block = chain[current_index]
            
            print("{last_block}".format(last_block))
            print("{block}".format(block))
            print("\n---------------------\n")

            if block["previous_hash"] != self.hash(last_block):
                return False 
            
            if not self.valid_proof(last_block['proof'], block['proof'], last_block['previous_hash']):
                return False 
            
            last_block = block
            current_index += 1

        return True


    
    @staticmethod
    def valid_proof(last_proof, proof, lash_hash):
        guess = "{last_proof}{proof}{lash_hash}".encode()
        guess_hash = hashlib.sha256(guess).hexdigest()

        return guess_hash[:4] == "0000"



    def resolve_conflicts(self, http="http"):
        neighbours = self.Nodes
        new_chain = None

        max_length = len(self.Chain)

        for node in neighbours:
            response = requests.get("{http}://{node}/chain".format(http, node))

            if response.status_code == 200:
                length = response.json()['length']
                chain = response.json()['chain']

                if length > max_length and self.valid_chain(chain):
                    max_length = length
                    new_chain = chain

        if new_chain:
            self.Chain = new_chain
            return True
        
        return False

    
    def new_transaction(self, sender, recipient, amount):
        self.Transactions.append({
            "sender": sender,
            "recipient": recipient,
            "amount": amount
        })

        return self.last_block["index"] + 1



    def proof_of_work(self, last_block):
        last_proof = last_block["proof"]
        last_hash = self.hash(last_block)

        proof = 0

        while self.valid_proof(last_proof, proof, last_hash) is False:
            proof += 1
        
        return proof


    


#Instantiate the node
app = Flask(__name__)

node_identifier = str(uuid4()).replace("-","")

blockchain = BlockChain()

@app.route("/mine", methods=["GET"])
def mine():
    last_block = blockchain.last_block
    proof = blockchain.proof_of_work(last_block)

    blockchain.new_transaction(sender="0", recipient=node_identifier, amount = 1)

    previous_hash = blockchain.hash(last_block)
    block = blockchain.new_block(previous_hash, proof)

    response = {
        "message": "new block forged",
        "index": block["index"],
        "transactions": block["transactions"],
        "proof": block["proof"],
        "previous_hash": block["previous_hash"],
    }

    return jsonify(response), 200


@app.route("/transaction/new", methods=["POST"])
def new_transaction():
    values = request.get_json()
    required = ["sender", "recipient", "amount"]
    if not all(k in values for k in required):
        return "Missing values", 400
    
    index = blockchain.new_transaction(values["sender"], values["recipient"], values["amount"])

    response = {
        "message": "Transaction will be added to Block {index}".format(index)
    }

    return jsonify(response), 201


@app.route("/chain", methods=["GET"])
def full_chain():
    response = {
        "chain": blockchain.Chain,
        "length": len(blockchain.Chain)
    }

    return jsonify(response), 200


@app.route("/nodes/register", methods=["POST"])
def register_node():
    values = request.get_json()
    nodes = values.get("nodes")

    if nodes is None:
        return "Error: please supply a valid list of nodes", 400
    
    for node in nodes:
        blockchain.register_node(node)
    
    response = {
        "message": "New nodes have been added",
        "total_nodes": list(blockchain.Nodes)
    }

    return jsonify(response), 201


@app.route("/nodes/resolve", methods=["GET"])
def consensus():
    replaced = blockchain.resolve_conflicts()
    
    if replaced:
        response = {
            "message": "Our chain was replaced",
            "new_chain": blockchain.Chain
        }
    else:
        response = {
            "message": "Our chain is authoritative",
            "chain": blockchain.Chain
        }
    
    return jsonify(response), 200



if __name__ == "__main__":
    from argparse import ArgumentParser

    parser = ArgumentParser()
    parser.add_argument("-p", "--port", default = 8000, type = int, help = "port to listen on")
    args = parser.parse_args()
    port = args.port

    app.run(host="127.0.01", port=port)