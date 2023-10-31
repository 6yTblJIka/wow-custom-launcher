using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WoWLauncher.Updater
{
    internal class ServerCheck
    {
        private readonly UpdateController m_UpdaterRef;
        private readonly MainWindow m_WndRef;

        public ServerCheck(MainWindow _wndRef, ref UpdateController _updater)
        {
            m_WndRef = _wndRef;
            m_UpdaterRef = _updater;

            DispatcherTimer timer = new DispatcherTimer()
            {
                Interval = TimeSpan.FromSeconds(60)
            };
            timer.Tick += CheckServerStatus;
            timer.Start();
            CheckServerStatus(null, null);
        }

        private async void CheckServerStatus(object? sender, EventArgs? e)
        {
            
            bool _serverAvailable = await Task.Run(() => IsServerAvailable());

            if (_serverAvailable)
            {
                m_WndRef.ServerStatusIcon.Source =
                    new BitmapImage(new Uri(@"/WoWLauncher;component/images/Indicator-Green.png", UriKind.Relative));
                m_WndRef.ServerStatus.Content = "Server online!";
            }
            else
            {
                m_WndRef.ServerStatusIcon.Source =
                    new BitmapImage(new Uri(@"/WoWLauncher;component/images/Indicator-Red.png", UriKind.Relative));
                m_WndRef.ServerStatus.Content = "Server offline.";
                m_WndRef.PlayBtn.IsEnabled = false;
            }
        }

        private bool IsServerAvailable()
        {
            try
            {
                var host = m_UpdaterRef.RealmAddress ?? "MadClownWorld.com";
                using var _tcpClient = new TcpClient();
                var _asyncConnectionResult = _tcpClient.BeginConnect(host, 8085, null, null);
                var _asyncConnectionWaitHandle = _asyncConnectionResult.AsyncWaitHandle;

                try
                {
                    if (!_asyncConnectionWaitHandle.WaitOne(TimeSpan.FromMilliseconds(5000), false))
                    {
                        _tcpClient.EndConnect(_asyncConnectionResult);
                        _tcpClient.Close();
                        throw new SocketException();
                    }

                    return true;
                }
                finally
                {
                    _asyncConnectionWaitHandle.Close();
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
