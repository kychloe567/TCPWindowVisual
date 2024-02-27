namespace TCPServer
{
    internal class TCPServerMain
    {
        public static TCPServer chat;

        protected static void InterruptHandler(object sender, ConsoleCancelEventArgs args)
        {
            chat.Shutdown();
            args.Cancel = true;
        }


        static void Main(string[] args)
        {
            // Create the server
            string name = "Window Position Network Visualiser";//args[0].Trim();
            int port = 6000;//int.Parse(args[1].Trim());
            chat = new TCPServer(name, port);

            // Add a handler for a Ctrl-C press
            Console.CancelKeyPress += InterruptHandler;

            // run the chat server
            chat.Run();
        }
    }
}