// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Net.Test.Common;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace System.Net.Sockets.Tests
{
    public class SocketOptionNameTest
    {
        private static bool SocketsReuseUnicastPortSupport => Capability.SocketsReuseUnicastPortSupport().HasValue;

        [OuterLoop] // TODO: Issue #11345
        [ConditionalFact(nameof(SocketsReuseUnicastPortSupport))]
        public void ReuseUnicastPort_CreateSocketGetOption()
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                if (Capability.SocketsReuseUnicastPortSupport().Value)
                {
                    Assert.Equal(0, (int)socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseUnicastPort));
                }
                else
                {
                    Assert.Throws<SocketException>(() => socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseUnicastPort));
                }
            }
        }

        [OuterLoop] // TODO: Issue #11345
        [ConditionalFact(nameof(SocketsReuseUnicastPortSupport))]
        public void ReuseUnicastPort_CreateSocketSetOption()
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                if (Capability.SocketsReuseUnicastPortSupport().Value)
                {
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseUnicastPort, 0);
                    int optionValue = (int)socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseUnicastPort);
                    Assert.Equal(0, optionValue);
                }
                else
                {
                    Assert.Throws<SocketException>(() => socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseUnicastPort, 1));
                }
            }
        }

        // TODO: Issue #4887
        // The socket option 'ReuseUnicastPost' only works on Windows 10 systems. In addition, setting the option
        // is a no-op unless specialized network settings using PowerShell configuration are first applied to the
        // machine. This is currently difficult to test in the CI environment. So, this test will be disabled for now
        [OuterLoop] // TODO: Issue #11345
        [ActiveIssue(4887)]
        public void ReuseUnicastPort_CreateSocketSetOptionToOneAndGetOption_SocketsReuseUnicastPortSupport_OptionIsOne()
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseUnicastPort, 1);
            int optionValue = (int)socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseUnicastPort);
            Assert.Equal(1, optionValue);
        }

        [OuterLoop] // TODO: Issue #11345
        [Fact]
        public void MulticastOption_CreateSocketSetGetOption_GroupAndInterfaceIndex_SetSucceeds_GetThrows()
        {
            int interfaceIndex = 0;
            IPAddress groupIp = IPAddress.Parse("239.1.2.3");

            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(groupIp, interfaceIndex));

                Assert.Throws<SocketException>(() => socket.GetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership));
            }
        }

        [OuterLoop] // TODO: Issue #11345
        [Fact]
        public async Task MulticastInterface_Set_AnyInterface_Succeeds()
        {
            // On all platforms, index 0 means "any interface"
            await MulticastInterface_Set_Helper(0);
        }

        [OuterLoop] // TODO: Issue #11345
        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)] // see comment below
        [ActiveIssue(21327, TargetFrameworkMonikers.Uap)] // UWP Apps are forbidden to send network traffic to the local Computer.
        public async Task MulticastInterface_Set_Loopback_Succeeds()
        {
            // On Windows, we can apparently assume interface 1 is "loopback." On other platforms, this is not a
            // valid assumption. We could maybe use NetworkInterface.LoopbackInterfaceIndex to get the index, but
            // this would introduce a dependency on System.Net.NetworkInformation, which depends on System.Net.Sockets,
            // which is what we're testing here....  So for now, we'll just assume "loopback == 1" and run this on
            // Windows only.
            await MulticastInterface_Set_Helper(1);
        }

        private async Task MulticastInterface_Set_Helper(int interfaceIndex)
        {
            IPAddress multicastAddress = IPAddress.Parse("239.1.2.3");
            string message = "hello";
            int port;

            using (Socket receiveSocket = CreateBoundUdpSocket(out port),
                          sendSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                receiveSocket.ReceiveTimeout = 1000;
                receiveSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(multicastAddress, interfaceIndex));

                sendSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, IPAddress.HostToNetworkOrder(interfaceIndex));

                var receiveBuffer = new byte[1024];
                var receiveTask = receiveSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), SocketFlags.None);

                for (int i = 0; i < TestSettings.UDPRedundancy; i++)
                {
                    sendSocket.SendTo(Encoding.UTF8.GetBytes(message), new IPEndPoint(multicastAddress, port));
                }

                var cts = new CancellationTokenSource();
                Assert.True(await Task.WhenAny(receiveTask, Task.Delay(20_000, cts.Token)) == receiveTask, "Waiting for received data timed out");
                cts.Cancel();

                int bytesReceived = await receiveTask;
                string receivedMessage = Encoding.UTF8.GetString(receiveBuffer, 0, bytesReceived);

                Assert.Equal(receivedMessage, message);
            }
        }

        [OuterLoop] // TODO: Issue #11345
        [Fact]
        public void MulticastInterface_Set_InvalidIndex_Throws()
        {
            int interfaceIndex = 31415;
            using (Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                Assert.Throws<SocketException>(() =>
                    s.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, IPAddress.HostToNetworkOrder(interfaceIndex)));
            }
        }

        [OuterLoop] // TODO: Issue #11345
        [ConditionalTheory(typeof(PlatformDetection), nameof(PlatformDetection.IsNotWindowsSubsystemForLinux))] // In WSL, the connect() call fails immediately.
        [InlineData(false)]
        [InlineData(true)]
        public void FailedConnect_GetSocketOption_SocketOptionNameError(bool simpleGet)
        {
            using (var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { Blocking = false })
            {
                // Fail a Connect
                using (var server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    server.Bind(new IPEndPoint(IPAddress.Loopback, 0)); // bind but don't listen
                    Assert.ThrowsAny<Exception>(() => client.Connect(server.LocalEndPoint));
                }

                // Verify via Select that there's an error
                const int FailedTimeout = 10 * 1000 * 1000; // 10 seconds
                var errorList = new List<Socket> { client };
                Socket.Select(null, null, errorList, FailedTimeout);
                Assert.Equal(1, errorList.Count);

                // Get the last error and validate it's what's expected
                int errorCode;
                if (simpleGet)
                {
                    errorCode = (int)client.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Error);
                }
                else
                {
                    byte[] optionValue = new byte[sizeof(int)];
                    client.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Error, optionValue);
                    errorCode = BitConverter.ToInt32(optionValue, 0);
                }
                Assert.Equal((int)SocketError.ConnectionRefused, errorCode);

                // Then get it again
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // The Windows implementation doesn't clear the error code after retrieved.
                    // https://github.com/dotnet/corefx/issues/8464
                    Assert.Equal(errorCode, (int)client.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Error));
                }
                else
                {
                    // The Unix implementation matches the getsockopt and MSDN docs and clears the error code as part of retrieval.
                    Assert.Equal((int)SocketError.Success, (int)client.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Error));
                }
            }
        }

        // Create an Udp Socket and binds it to an available port
        private static Socket CreateBoundUdpSocket(out int localPort)
        {
            Socket receiveSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            // sending a message will bind the socket to an available port
            string sendMessage = "dummy message";
            int port = 54320;
            IPAddress multicastAddress = IPAddress.Parse("239.1.1.1");
            receiveSocket.SendTo(Encoding.UTF8.GetBytes(sendMessage), new IPEndPoint(multicastAddress, port));

            localPort = (receiveSocket.LocalEndPoint as IPEndPoint).Port;
            return receiveSocket;
        }

        [Theory]
        [InlineData(null, null, null, true)]
        [InlineData(null, null, false, true)]
        [InlineData(null, false, false, true)]
        [InlineData(null, true, false, true)]
        [InlineData(null, true, true, false)]
        [InlineData(true, null, null, true)]
        [InlineData(true, null, false, true)]
        [InlineData(true, null, true, true)]
        [InlineData(true, false, null, true)]
        [InlineData(true, false, false, true)]
        [InlineData(true, false, true, true)]
        public void ReuseAddress(bool? exclusiveAddressUse, bool? firstSocketReuseAddress, bool? secondSocketReuseAddress, bool expectFailure)
        {
            using (Socket a = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                if (exclusiveAddressUse.HasValue)
                {
                    a.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, exclusiveAddressUse.Value);
                }
                if (firstSocketReuseAddress.HasValue)
                {
                    a.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, firstSocketReuseAddress.Value);
                }

                a.Bind(new IPEndPoint(IPAddress.Loopback, 0));

                using (Socket b = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    if (secondSocketReuseAddress.HasValue)
                    {
                        b.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, secondSocketReuseAddress.Value);
                    }

                    if (expectFailure)
                    {
                        Assert.ThrowsAny<SocketException>(() => b.Bind(a.LocalEndPoint));
                    }
                    else
                    {
                        b.Bind(a.LocalEndPoint);
                    }
                }
            }
        }

        [Theory]
        [PlatformSpecific(TestPlatforms.Windows)]  // ExclusiveAddressUse option is a Windows-specific option (when set to "true," tells Windows not to allow reuse of same local address)
        [InlineData(false, null, null, true)]
        [InlineData(false, null, false, true)]
        [InlineData(false, false, null, true)]
        [InlineData(false, false, false, true)]
        [InlineData(false, true, null, true)]
        [InlineData(false, true, false, true)]
        [InlineData(false, true, true, false)]
        public void ReuseAddress_Windows(bool? exclusiveAddressUse, bool? firstSocketReuseAddress, bool? secondSocketReuseAddress, bool expectFailure)
        {
            ReuseAddress(exclusiveAddressUse, firstSocketReuseAddress, secondSocketReuseAddress, expectFailure);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.AnyUnix)] // Windows defaults are different
        public void ExclusiveAddress_Default_Unix()
        {
            using (Socket a = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                Assert.Equal(1, (int)a.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse));
                Assert.Equal(true, a.ExclusiveAddressUse);
                Assert.Equal(0, (int)a.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress));
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(0)]
        [PlatformSpecific(TestPlatforms.AnyUnix)] // Unix does not have separate options for ExclusiveAddressUse and ReuseAddress.
        public void SettingExclusiveAddress_SetsReuseAddress(int value)
        {
            using (Socket a = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                a.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, value);

                Assert.Equal(value, (int)a.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse));
                Assert.Equal(value == 1 ? 0 : 1, (int)a.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress));
            }

            // SettingReuseAddress_SetsExclusiveAddress
            using (Socket a = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                a.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, value);

                Assert.Equal(value, (int)a.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress));
                Assert.Equal(value == 1 ? 0 : 1, (int)a.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse));
            }
        }

        [Fact]
        public void BindDuringTcpWait_Succeeds()
        {
            int port = 0;
            using (Socket a = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                a.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                port = (a.LocalEndPoint as IPEndPoint).Port;
                a.Listen(10);

                // Connect a client
                using (Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    client.Connect(new IPEndPoint(IPAddress.Loopback, port));
                    // accept socket and close it with zero linger time.
                    a.Accept().Close(0);
                }
            }

            // Bind a socket to the same address we just used.
            using (Socket b = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                b.Bind(new IPEndPoint(IPAddress.Loopback, port));
            }
        }

        [Fact]
        public void ExclusiveAddressUseTcp()
        {
            using (Socket a = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                // ExclusiveAddressUse defaults to true on Unix, on Windows it defaults to false.
                a.ExclusiveAddressUse = true;

                a.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                a.Listen(10);
                int port = (a.LocalEndPoint as IPEndPoint).Port;

                using (Socket b = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    SocketException ex = Assert.ThrowsAny<SocketException>(() => b.Bind(new IPEndPoint(IPAddress.Loopback, port)));
                    Assert.Equal(SocketError.AddressAlreadyInUse, ex.SocketErrorCode);
                }
            }
        }

        [OuterLoop] // TODO: Issue #11345
        [Theory]
        [PlatformSpecific(TestPlatforms.Windows)]  // SetIPProtectionLevel not supported on Unix
        [InlineData(IPProtectionLevel.EdgeRestricted, AddressFamily.InterNetwork, SocketOptionLevel.IP)]
        [InlineData(IPProtectionLevel.Restricted, AddressFamily.InterNetwork, SocketOptionLevel.IP)]
        [InlineData(IPProtectionLevel.Unrestricted, AddressFamily.InterNetwork, SocketOptionLevel.IP)]
        [InlineData(IPProtectionLevel.EdgeRestricted, AddressFamily.InterNetworkV6, SocketOptionLevel.IPv6)]
        [InlineData(IPProtectionLevel.Restricted, AddressFamily.InterNetworkV6, SocketOptionLevel.IPv6)]
        [InlineData(IPProtectionLevel.Unrestricted, AddressFamily.InterNetworkV6, SocketOptionLevel.IPv6)]
        public void SetIPProtectionLevel_Windows(IPProtectionLevel level, AddressFamily family, SocketOptionLevel optionLevel)
        {
            using (var socket = new Socket(family, SocketType.Stream, ProtocolType.Tcp))
            {
                socket.SetIPProtectionLevel(level);

                int result = (int)socket.GetSocketOption(optionLevel, SocketOptionName.IPProtectionLevel);
                Assert.Equal(result, (int)level);
            }
        }

        [OuterLoop] // TODO: Issue #11345
        [Theory]
        [PlatformSpecific(TestPlatforms.AnyUnix)]  // SetIPProtectionLevel not supported on Unix
        [InlineData(IPProtectionLevel.EdgeRestricted, AddressFamily.InterNetwork)]
        [InlineData(IPProtectionLevel.Restricted, AddressFamily.InterNetwork)]
        [InlineData(IPProtectionLevel.Unrestricted, AddressFamily.InterNetwork)]
        [InlineData(IPProtectionLevel.EdgeRestricted, AddressFamily.InterNetworkV6)]
        [InlineData(IPProtectionLevel.Restricted, AddressFamily.InterNetworkV6)]
        [InlineData(IPProtectionLevel.Unrestricted, AddressFamily.InterNetworkV6)]
        public void SetIPProtectionLevel_Unix(IPProtectionLevel level, AddressFamily family)
        {
            using (var socket = new Socket(family, SocketType.Stream, ProtocolType.Tcp))
            {
                Assert.Throws<PlatformNotSupportedException>(() => socket.SetIPProtectionLevel(level));
            }
        }

        [OuterLoop] // TODO: Issue #11345
        [Theory]
        [InlineData(AddressFamily.InterNetwork)]
        [InlineData(AddressFamily.InterNetworkV6)]
        public void SetIPProtectionLevel_ArgumentException(AddressFamily family)
        {
            using (var socket = new Socket(family, SocketType.Stream, ProtocolType.Tcp))
            {
                AssertExtensions.Throws<ArgumentException>("level", () => socket.SetIPProtectionLevel(IPProtectionLevel.Unspecified));
            }
        }
    }
}
