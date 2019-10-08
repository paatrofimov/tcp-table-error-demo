using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ReplicaExhaustingPorts
{
    public class Program
    {
        public static void Main()
        {
            Task.Factory.StartNew(
                () =>
                {
                    while (true)
                    {
                        var acceptedClients = new List<TcpClient>();
                        var clients = new List<TcpClient>();
                        try
                        {
                            var l = new TcpListener(IPAddress.Loopback, 0);
                            l.Start();
                            var listenerPort = ((IPEndPoint) l.LocalEndpoint).Port;
                            Task.Factory.StartNew(
                                () =>
                                {
                                    while (true)
                                    {
                                        var accepted = l.AcceptTcpClient();
                                        acceptedClients.Add(accepted);
                                    }
                                });

                            for (var i = 0; i < 100500; i++)
                            {
                                clients.Add(new TcpClient());
                                clients.Last().Connect(IPAddress.Loopback, listenerPort);
                            }

                            SpinWait.SpinUntil(() => false, -1);
                        }
                        catch (SocketException) // catch exception when all OS tcp ports exhausted
                        {
                        }
                    }
                });

            while (true)
            {
                try
                {
                    var result = new List<TcpRow>();
                    GetTcpConnections(result);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        private static unsafe void GetTcpConnections(List<TcpRow> rows)
        {
            var AF_INET = 2; // IP_v4
            var buffer = new byte[0];
            var buffSize = 0;
            var res = GetExtendedTcpTable(
                null,
                ref buffSize,
                true,
                AF_INET,
                TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL,
                0);

            while (res == (int) ErrorCodes.ERROR_INSUFFICIENT_BUFFER)
            {
                buffer = new byte[buffSize];
                fixed (byte* b = buffer)
                {
                    res = GetExtendedTcpTable(
                        b,
                        ref buffSize,
                        true,
                        AF_INET,
                        TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL,
                        0);
                }
            }

            if (res != (int) ErrorCodes.ERROR_SUCCESS)
            {
                Console.WriteLine(res);
                throw new Win32Exception(res);
            }

            fixed (byte* b = buffer)
            {
                var count = *(int*) b;
                var ptr = (MIB_TCPROW_OWNER_PID*) (b + 4);

                for (var i = 0; i < count; i++)
                {
                    rows.Add(
                        new TcpRow()
                        {
                            dwLocalAddr = ptr[i].dwLocalAddr
                        });
                }
            }
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        public static extern unsafe int GetExtendedTcpTable(
            [Out] byte* pTcpTable,
            [In, Out] ref int pdwSize,
            [In] bool bOrder,
            [In] int ulAf,
            [In] TCP_TABLE_CLASS TableClass,
            [In] uint Reserved
        );

        [StructLayout(LayoutKind.Sequential)]
        internal struct MIB_TCPROW_OWNER_PID
        {
            public int dwLocalAddr;
            public int dwLocalPort;
            public int dwRemoteAddr;
            public int dwRemotePort;
            public int dwOwningPid;
        }

        public class TcpRow
        {
            public int dwLocalAddr;
            public int dwLocalPort;
            public int dwRemoteAddr;
            public int dwRemotePort;
            public int dwOwningPid;
        }

        public enum ErrorCodes
        {
            ERROR_SUCCESS = 0,
            ERROR_INVALID_PARAMETER = 87,
            ERROR_BUFFER_OVERFLOW = 111,
            ERROR_INSUFFICIENT_BUFFER = 122,
            ERROR_NO_DATA = 232,
        }

        public enum TCP_TABLE_CLASS
        {
            TCP_TABLE_BASIC_LISTENER,
            TCP_TABLE_BASIC_CONNECTIONS,
            TCP_TABLE_BASIC_ALL,
            TCP_TABLE_OWNER_PID_LISTENER,
            TCP_TABLE_OWNER_PID_CONNECTIONS,
            TCP_TABLE_OWNER_PID_ALL,
            TCP_TABLE_OWNER_MODULE_LISTENER,
            TCP_TABLE_OWNER_MODULE_CONNECTIONS,
            TCP_TABLE_OWNER_MODULE_ALL
        }
    }
}