using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Adgangskontroll.Kortleser
{
    internal class Sender
    {
        private TcpClient _client;

        // Konstruktør
        public TCPClient(string ipAddress, int port)
        {
            _client = new TcpClient();           
            _client.Connect(ipAddress, port);
            Console.WriteLine("Connected to server.");
           
        }

        // Sender melding til serveren og mottar svar
        public void SendMessage(string message)
        {
            try
            {
                NetworkStream stream = _client.GetStream();
                byte[] data = Encoding.ASCII.GetBytes(message);

                // Sender data
                stream.Write(data, 0, data.Length);
                Console.WriteLine("Sent: " + message);

                // Mottar svar
                byte[] responseData = new byte[1024];
                int bytesRead = stream.Read(responseData, 0, responseData.Length);
                string response = Encoding.ASCII.GetString(responseData, 0, bytesRead);
                Console.WriteLine("Received: " + response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
            }
        }

        // Lukk forbindelsen
        public void Close()
        {
            try
            {
                _client.Close();
                Console.WriteLine("Connection closed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
            }
        }
    }
}
