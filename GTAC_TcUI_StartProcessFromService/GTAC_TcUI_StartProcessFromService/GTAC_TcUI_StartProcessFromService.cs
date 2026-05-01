//-----------------------------------------------------------------------
// <copyright file="StartProcessFromService.cs" company="Beckhoff Automation GmbH & Co. KG">
//     Copyright (c) Beckhoff Automation GmbH & Co. KG. All Rights Reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Runtime.InteropServices;
using System.Threading;
using TcHmiSrv.Core;
using TcHmiSrv.Core.General;
using TcHmiSrv.Core.Listeners;
using TcHmiSrv.Core.Listeners.RequestListenerEventArgs;
using TcHmiSrv.Core.Tools.Management;
using WindowsProcesses;

namespace GTAC_TcUI_StartProcessFromService
{
    // represents the default type of the TwinCAT HMI server extension
    // ReSharper disable once UnusedType.Global
    public class GTAC_TcUI_StartProcessFromService : IServerExtension
    {
        //---------------- Added by b.lekx-toniolo of Sodecia GTAC ---------------------------------
        //Const vars for Process Window manipulation
        private const int SW_MAXIMIZE = 3;
        private const int SW_MINIMIZE = 6;
        private const int SW_RESTORE = 9;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_SHOWWINDOW = 0x0040;

        //WinAPI to change process window state (maximize, minimize, etc.)
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        //WinAPI to bring process window to front
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        //WinAPI to manipulate process window pos
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);
        //Pointer to process Window
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        //WinAPI for Finding the browser
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindBrowserWindow(string lpClassName, string lpWindowName);

        //WinAPI for manipulating browser smin/mx
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowBrowserWindow(IntPtr hBWnd, int nCmdShow);
        //-----------------------------------------------------------------------------------

        //Beckhoff created
        private readonly RequestListener _requestListener = new RequestListener();

        // initializes the TwinCAT HMI server extension
        public ErrorValue Init()
        {
            try
            {
                // add event handlers
                _requestListener.OnRequest += OnRequest;

                return ErrorValue.HMI_SUCCESS;
            }
            catch (Exception ex)
            {
                _ = TcHmiAsyncLogger.Send(Severity.Error, "errorInit", ex.ToString());
                return ErrorValue.HMI_E_EXTENSION_LOAD;
            }
        }

        // called when a client requests a symbol from the domain of the TwinCAT HMI server extension
        private void OnRequest(object sender, OnRequestEventArgs e)
        {
            // handle all commands one by one
            foreach (var command in e.Commands)
            {
                try
                {
                    // use the mapping to check which command is requested
                    switch (command.Mapping)
                    {
                        case "Launch_App":
                            StartProcess(command);
                            break;

                    }
                }
                catch
                {
                    // ignore exceptions and continue processing the other commands in the group
                    command.ExtensionResult = Convert.ToUInt32(GTAC_TcUI_ExtensionSpecificError.InternalError);
                }
            }
        }

        private static Value RetrieveOptionalValue(Value writeValue, string key)
        {
            return writeValue.TryGetValue(key, out var value) ? value : null;
        }

        private static void StartProcess(Command command)
        {
        //------------------------------------------------------------------------------
        //Changed this section to allow for a configurable interface, b.lekx-toniolo of Sodecia GTAC
        //------------------------------------------------------------------------------
        
            try
            {
                string applicationName = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "App");
                string commandLine = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "CMD_Line");
                string workingDirectory = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "Env_Dir");
                bool showWindow = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "showWindow");

                //This method allows current user context to be leveraged for starting a new process under the existing service (TcHmi)
                var process = UserProcess.Create(applicationName, commandLine, workingDirectory, showWindow);
                command.ReadValue = "Started new process -> "+process.ProcessName +" with id "+ process.Id+" \nYou may need to close this UI to access";


                //If User Config has ShowWindow set true the force new Process to foreground and maximize
                //**NOTE** may not work in all cases, especially if Browser is running in Kiosk mode 

                if (showWindow) 
                {

                    
                    if (process != null)
                    {
                       
                        IntPtr hWnd = process.MainWindowHandle;
                        //Retry loop if process window not yet ready
                        for (int i = 0; i < 20 && hWnd == IntPtr.Zero; i++)
                        {
                            Thread.Sleep(200);
                            process.Refresh();
                            //Capture Handle to new process Window
                            hWnd = process.MainWindowHandle;
                        }

                        if (hWnd != IntPtr.Zero)
                        {
                            //Maximize the window
                            ShowWindow(hWnd, SW_MAXIMIZE);

                            //Bring to front reliably (trick Windows foreground issue)
                            SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                            SetForegroundWindow(hWnd);
                            SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);

                            //Finaly, minimize browser if possible
                            IntPtr hBroswerWnd = FindBrowserWindow(null, "Sodecia GTAC - The new UI");
                            if (hBroswerWnd != IntPtr.Zero)
                            {
                                ShowBrowserWindow(hBroswerWnd, SW_MINIMIZE);
                            }
                        }
                    }
                }
            }
            catch (Exception err)
            {
                command.ReadValue = "Error starting process-> " + err;
            }
            
        }
    }
}
