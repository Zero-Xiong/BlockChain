namespace BlockChain
{
    public class Transaction
    {
        public int Amount { get; private set; }

        public string Recipient { get; private set; }

        public string Sender { get; private set; }

        public Transaction(string sender, string recipient, int amount)
        {
            Sender = sender;
            Recipient = recipient;
            Amount = amount;
        }
    }
}