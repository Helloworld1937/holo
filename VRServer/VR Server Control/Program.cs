using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using Microsoft.Kinect;
using System.IO;

// 采集
namespace VR_Server_Control {
    class Program {
        static private KinectSensor sensor;
        static private byte[] skeletonDataBytes = new byte[sizeof(float) * 80];

        // 20个骨骼点
        static private JointType[] orderedTypes = { JointType.HipCenter, JointType.Spine, JointType.ShoulderCenter, JointType.Head,
                                         JointType.ShoulderLeft, JointType.ElbowLeft, JointType.WristLeft, JointType.HandLeft,
                                         JointType.ShoulderRight, JointType.ElbowRight, JointType.WristRight, JointType.HandRight,
                                         JointType.HipLeft, JointType.KneeLeft, JointType.AnkleLeft, JointType.FootLeft,
                                         JointType.HipRight, JointType.KneeRight, JointType.AnkleRight, JointType.FootRight};

        static void Main(string[] args) {

            /*
             * 没有Kinect调试的时候不需要这句
             */
            
            if (!InitializeKinect())
            {
                return;
            }

            Server tserver = new Server();

            tserver.Start();

            ConsoleKeyInfo info;
            do
            {
                info = Console.ReadKey();
            } while (info.KeyChar != 'Q');

            /*
             * 没有Kinect调试的时候不需要这句
             */
            if (null != sensor)
            {
                sensor.Stop();
            }
        }

        static bool InitializeKinect()
        {
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    sensor = potentialSensor;
                    break;
                }
            }

            if (null != sensor)
            {
                // Turn on the skeleton stream to receive skeleton frames
                sensor.SkeletonStream.Enable();

                // Add an event handler to be called whenever there is new color frame data
                sensor.SkeletonFrameReady += SensorSkeletonFrameReady;

                // Start the sensor!
                try
                {
                    sensor.Start();
                }
                catch (IOException)
                {
                    sensor = null;
                }
            }

            if (null == sensor)
            {
                Console.WriteLine("Cannot Find Kinect!");
                return false;
            }
            return true;
        }

        private static void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }

                ProcessFrame(skeletons);
            }
        }

        private static void ProcessFrame(Skeleton[] Skeletons)
        {
            foreach (var skeleton in Skeletons)
            {
                if (skeleton.TrackingState != SkeletonTrackingState.Tracked)
                    continue;
                byte[] tmpBytes;

                lock (skeletonDataBytes)
                {
                    // 遍历Enum
                    int index = 0;
                    foreach (JointType type in orderedTypes)
                    {
                        var x = skeleton.Joints[type].Position.X;
                        var y = skeleton.Joints[type].Position.Y;
                        var z = skeleton.Joints[type].Position.Z;

                        tmpBytes = BitConverter.GetBytes(x);
                        skeletonDataBytes[4 * index] = tmpBytes[0];
                        skeletonDataBytes[4 * index + 1] = tmpBytes[1];
                        skeletonDataBytes[4 * index + 2] = tmpBytes[2];
                        skeletonDataBytes[4 * index + 3] = tmpBytes[3];

                        ++index;

                        tmpBytes = BitConverter.GetBytes(y);
                        skeletonDataBytes[4 * index] = tmpBytes[0];
                        skeletonDataBytes[4 * index + 1] = tmpBytes[1];
                        skeletonDataBytes[4 * index + 2] = tmpBytes[2];
                        skeletonDataBytes[4 * index + 3] = tmpBytes[3];

                        ++index;

                        tmpBytes = BitConverter.GetBytes(z);
                        skeletonDataBytes[4 * index] = tmpBytes[0];
                        skeletonDataBytes[4 * index + 1] = tmpBytes[1];
                        skeletonDataBytes[4 * index + 2] = tmpBytes[2];
                        skeletonDataBytes[4 * index + 3] = tmpBytes[3];

                        ++index;

                        tmpBytes = BitConverter.GetBytes(1.0f);
                        skeletonDataBytes[4 * index] = tmpBytes[0];
                        skeletonDataBytes[4 * index + 1] = tmpBytes[1];
                        skeletonDataBytes[4 * index + 2] = tmpBytes[2];
                        skeletonDataBytes[4 * index + 3] = tmpBytes[3];

                        ++index;
                    }
                }
            }
        }

        const Int32 NEED_NEW_DATA = 0x00000001;
        const Int32 EXIT = 0x000000002;

        static void GenerateFloatData(byte[] bytes) {
            Random ran = new Random();
            byte[] tmpBytes;
            for (int i = 0; i < 80; ++i) {
                if ((i + 1) % 4 == 0)
                {
                    tmpBytes = BitConverter.GetBytes(1.0f);
                    bytes[4 * i] = tmpBytes[0];
                    bytes[4 * i + 1] = tmpBytes[1];
                    bytes[4 * i + 2] = tmpBytes[2];
                    bytes[4 * i + 3] = tmpBytes[3];
                }
                else
                {
                    tmpBytes = BitConverter.GetBytes(((float)ran.Next(-200, 200)) / 100);
                    bytes[4 * i] = tmpBytes[0];
                    bytes[4 * i + 1] = tmpBytes[1];
                    bytes[4 * i + 2] = tmpBytes[2];
                    bytes[4 * i + 3] = tmpBytes[3];
                }
            }
        }

        public class Server {

            private TcpListener m_tcplistener = null;
            private byte[] m_transData = new byte[80 * 4];

            public Server() {
                m_tcplistener = new TcpListener(IPAddress.Any, 2333);
            }

            public void Start() {

                new Thread(() => {
                    m_tcplistener.Start();
                    int count = 0;
                    byte[] readData = new byte[4];

                    while (true) {
                        Console.WriteLine("Accept a client.");
                        TcpClient client = m_tcplistener.AcceptTcpClient();
                        Console.WriteLine("Accepted.");
                        new Thread(()=> {
                            NetworkStream clientStream = client.GetStream();

                            while(true) {
                                try {
                                    // get count
                                    count = clientStream.Read(readData, 0, sizeof(Int32));
                                    Int32 inst = BitConverter.ToInt32(readData, 0);

                                    if(inst == NEED_NEW_DATA) {

                                        // 有Kinect调试的时候把下面这两句注释掉
                                        // GenerateFloatData(m_transData);
                                        // clientStream.Write(m_transData, 0, m_transData.Length);

                                        /*
                                         * 有Kinect调试的时候取消这里的注释
                                         */
                                        lock (skeletonDataBytes)
                                        {
                                            clientStream.Write(skeletonDataBytes, 0, skeletonDataBytes.Length);
                                        }

                                        clientStream.Flush();
                                    }
                                    else if(inst == EXIT) {
                                        break;
                                    }
                                }
                                catch (Exception e) {
                                    Console.Out.WriteLine(e.Message);
                                    break;
                                }

                                if (count == 0) {
                                    break;
                                }

                            }

                            client.Close();
                        }                    
                        ).Start();
                    }

                }).Start();

            }

        }

    }
}
