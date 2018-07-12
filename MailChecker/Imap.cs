using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Security;
using System.Threading;
using System.Net;
using System.Net.Sockets;

namespace MailChecker
{
    class Imap
    {
            private  TcpClient _imapClient = new TcpClient();
            private  SslStream _imapSecure;
            const int Read_buffer_size = 256;


            public async Task InitializeConnection(string hostname, int port)
            {
                try
                {
                    //_imapClient.AsyncConnect(hostname, port);
                    
                    await _imapClient.ConnectAsync(hostname, port);
                    _imapSecure = new SslStream(_imapClient.GetStream());

                   // _imapSecure.AuthenticateAsClient();
                    await _imapSecure.AuthenticateAsClientAsync(hostname);

                    
                
                }
                catch (SocketException ex)
                {
                    Console.WriteLine(ex.Message);
                }
            
            }



            public async Task AuthenticateUser(string username, string password)
            {
                string toWrite = "$ LOGIN " + username + " " + password + "\r\n";
                byte[] data = Encoding.ASCII.GetBytes(toWrite);

                try
                {
                     await _imapSecure.WriteAsync(data, 0, data.Length);
                }
                catch(SocketException ex)
                {
                    Console.WriteLine("[Socket-Exception] : {0}", ex.Message);
                }
            }

            public async Task<String> Response()
            {
                byte[] data = new byte[Read_buffer_size];
                //System.IO.MemoryStream st = new System.IO.MemoryStream();

                try
                {

                    int ret = await _imapSecure.ReadAsync(data, 0, data.Length);
                
                    return Encoding.ASCII.GetString(data, 0, ret);
                }
                catch (SocketException ex)
                {
                    return "[Exception] : " + ex.Message;
                }
                
            }

            public async Task Disconnect()
            {
                try
                {
                    byte[] data = Encoding.ASCII.GetBytes("$ LOGOUT\r\n");
                    await _imapSecure.WriteAsync(data, 0, data.Length);
                    _imapSecure.Close();
                    _imapClient.Close();
                }
                catch(SocketException ex)
                {
                    Console.WriteLine("[SocketException logout error] : {0}", ex.Message);
                }
            }
    }
}
