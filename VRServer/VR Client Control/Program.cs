using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// 显示
namespace VR_Client_Control {
    class Program {
        static void Main(string[] args) {
            Client tclient = new Client();
            tclient.Start();
        }

        const Int32 NEED_NEW_DATA = 0x00000001;
        const Int32 EXIT = 0x000000002;

        public class Client {
            private TcpClient m_client = null;
            public Client() {
                m_client = new TcpClient();
            }

            public void Start() {
                IPEndPoint ip = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 2333);
                m_client.Connect(ip);
                System.Console.WriteLine("Access Successful.");
                NetworkStream clientStream = m_client.GetStream();
                int bytesRead = 0;
                int count = 0;
                byte[] readData = new byte[80 * 4];
                byte[] toFloat = new byte[4];
                
                while (true) {

                    try {
                        // need new data
                        Thread.Sleep(20);
                        clientStream.Write(BitConverter.GetBytes(NEED_NEW_DATA), 0, sizeof(Int32));

                        bytesRead = clientStream.Read(readData, count, readData.Length);
                        count += bytesRead;
                    }
                    catch (Exception e) {
                        Console.Out.WriteLine(e.Message);
                        break;
                    }

                    if (bytesRead == 0) {
                        break;
                    }

                    if(bytesRead == 80 * 4) {
                        for(int i = 0; i < 80; ++i) {
                            Console.Out.Write(BitConverter.ToSingle(readData, i * 4));
                            Console.Out.Write(",");
                        }
                        Console.Out.Write("\n");
                        count = 0;
                    }

                }
            }
        }
    }
}
