﻿using Ait.Pe04.Octopus.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Ait.Pe04.Octopus.server.wpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        Socket _socket;
        bool _serverOnline;
        int _maxConnections = 10;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            StartUpConfig();
            grpAirfield.Visibility = Visibility.Hidden;
        }

        private void StartUpConfig()
        {
            cmbIPs.ItemsSource = IP.GetActiveIPs();
            for (int port = 49200; port <= 49500; port++)
            {
                cmbPorts.Items.Add(port);
            }
            ServerConfig.GetConfig(out string savedIP, out int savedPort);
            try
            {
                cmbIPs.SelectedItem = savedIP;
            }
            catch
            {
                cmbIPs.SelectedItem = "127.0.0.1";
            }
            try
            {
                cmbPorts.SelectedItem = savedPort;
            }
            catch
            {
                cmbPorts.SelectedItem = 49200;
            }
            btnStartServer.Visibility = Visibility.Visible;
            btnStopServer.Visibility = Visibility.Hidden;
        }

        private void SaveConfig()
        {
            if (cmbIPs.SelectedItem == null) return;
            if (cmbPorts.SelectedItem == null) return;

            string ip = cmbIPs.SelectedItem.ToString();
            int port = int.Parse(cmbPorts.SelectedItem.ToString());
            ServerConfig.WriteConfig(ip, port);
        }

        private void StartServer()
        {
            _serverOnline = true;
        }

        private void CloseServer()
        {
            _serverOnline = false;
            try
            {
                _socket.Close();
            }
            catch
            { }
            _socket = null;
            InsertMessage(lstInRequest, $"Airspace closed at {DateTime.Now:G}");
        }

        public static void DoEvents()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate { }));
        }
        private void StartListening()
        {
            IPAddress ip = IPAddress.Parse(cmbIPs.SelectedItem.ToString()); // Get ip in combobox
            int port = int.Parse(cmbPorts.SelectedItem.ToString()); // Get port in combobox
            IPEndPoint endPoint = new IPEndPoint(ip, port); // Create new IpEndpoint from ip and port
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                _socket.Bind(endPoint);
                _socket.Listen(_maxConnections);
                InsertMessage(lstInRequest, $"Airspace opened at {DateTime.Now:g} \nHave a good day!");
                InsertMessage(lstInRequest, $"Maximum airplanes allowed : {_maxConnections}");

                while (_serverOnline) // While _serverOnline is true
                {
                    DoEvents();
                    if (_socket != null)
                    {
                        if (_socket.Poll(100000, SelectMode.SelectRead))
                        {
                            Socket clientSocket = _socket.Accept();
                            HandleClientCall(clientSocket);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void HandleClientCall(Socket clientCall)
        {
            byte[] clientRequest = new Byte[1024];
            string instruction = null;

            while (true)
            {
                int numByte = clientCall.Receive(clientRequest);
                instruction += Encoding.ASCII.GetString(clientRequest, 0, numByte);
                if (instruction.IndexOf("##OVER") > -1)
                    break;
            }

            List<string> serverResponseInText = HandleInstruction(instruction);

            string result;

            if (serverResponseInText.Count < 1)
            {
                result = $"{serverResponseInText} is unkown";
            }
            else
            {
                result = $"{ExecuteCommand(serverResponseInText)}";
            }

            byte[] clientResponse = Encoding.ASCII.GetBytes(result);
            clientCall.Send(clientResponse);

            clientCall.Shutdown(SocketShutdown.Both);
            clientCall.Close();
        }
        private List<string> HandleInstruction(string instruction)
        {
            InsertMessage(lstInRequest,$"Request =\n{instruction}");
            string removeEmptySpaces = instruction.ToUpper().Replace(" ", "").Trim();
            string trimmedInstruction = removeEmptySpaces.ToUpper().Replace("##OVER", "").Trim();
            if (trimmedInstruction.Contains("IDENTIFICATION")) 
            {
                List<string> data = new List<string>();
                if (trimmedInstruction.Contains("="))
                {
                    data.Insert(0, trimmedInstruction.Split("=")[0]);
                    data.Insert(1, trimmedInstruction.Split("=")[1]);
                
                
                }
                else
                {
                    data.Insert(0, trimmedInstruction);
                }
            
                return data;

            }
            else 
            {
                List<string> data = new List<string>();
                if (trimmedInstruction.Contains("="))
                {
                    data.Insert(0, trimmedInstruction.Split("=")[0]);
                    data.Insert(1, trimmedInstruction.Split("=")[1]);


                }
                if(trimmedInstruction.Contains("|"))
                {
                    data.Insert(0, trimmedInstruction.Split("|")[0]);
                    data.Insert(1, trimmedInstruction.Split("|")[1]);
                }
                else
                {
                    data.Insert(0, trimmedInstruction);
                }
                return data;
            }
        }

        private void CmbIPs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SaveConfig();
        }

        private void CmbPorts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SaveConfig();
        }

        private void BtnStartServer_Click(object sender, RoutedEventArgs e)
        {
            grpAirfield.Visibility = Visibility.Visible;
            btnStartServer.Visibility = Visibility.Hidden;
            btnStopServer.Visibility = Visibility.Visible;
            StartServer(); // Starting server and setting _serverOnline to true
            StartListening();
        }

        private void BtnStopServer_Click(object sender, RoutedEventArgs e)
        {

            InsertMessage( lstInRequest,"Airspace closing");
            btnStopServer.Visibility = Visibility.Hidden;
            btnStartServer.Visibility = Visibility.Visible;
            CloseServer();
        }

        private void InsertMessage(ListBox source, string message)
        {
            source.Items.Insert(0, $"<=============>\n{message}\n<=============>");
        }

        private string ExecuteCommand(List<string> command)
        {
            if (command[0] == "IDENTIFICATION")
            {
                InsertMessage(lstOutResponse, $"test {command[1]}");
                return "test";
            } 
            else if(command[2] == "ID" && command[1] == "ADDPASS") 
            {
                InsertMessage(lstOutResponse, $"test {command[1]}");
                return "testADDPASS";
            }
            else 
            {
                InsertMessage(lstOutResponse, "UNKNOWN INSTRUCTION");
                return "UNKNOWN INSTRUCTION";
            }
            // To add as last command
            //else if
            //{
            //    InsertMessage(lstOutResponse, "UNKNOWN INSTRUCTION");
            //    return "UNKNOWN INSTRUCTION";
            //}
        }
    }
}
