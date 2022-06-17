//-----------------------------------------------------------------------
// <copyright file="GTAC_TcUI_PostgreSQL.cs" company="Beckhoff Automation GmbH & Co. KG">
//     Copyright (c) Beckhoff Automation GmbH & Co. KG. All Rights Reserved.
// </copyright>
//-----------------------------------------------------------------------
// Edits by b.lekx-toniolo to create custom Server Extension
//-----------------------------------------------------------------------

using System;
using System.Diagnostics;

using TcHmiSrv.Core;
using TcHmiSrv.Core.General;
using TcHmiSrv.Core.Listeners;
using TcHmiSrv.Core.Tools.Management;
using Npgsql;


namespace GTAC_TcUI_PostgreSQL
{
    // Represents the default type of the TwinCAT HMI server extension.
    public class GTAC_TcUI_PostgreSQL : IServerExtension
    {
        private readonly RequestListener _requestListener = new RequestListener();
        private readonly PerformanceCounter _cpuUsage = new PerformanceCounter("Processor", "% Processor Time", "_Total");

        //Create Npgsql connection object
        private NpgsqlConnection connObject;
        private bool connected;

        // Called after the TwinCAT HMI server loaded the server extension.
        public ErrorValue Init()
        {
            _requestListener.OnRequest += OnRequest;

            return ErrorValue.HMI_SUCCESS;
        }

        //-----------------------------------------------------------------
        // Custom Methods for use in this Server Extension, b.lekx-toniolo
        //-----------------------------------------------------------------
        
        //---------- Server Extension Diagnostics -------------
        private Value CollectDiagnosticsData(Command command)
        {
            //First close established connections
            if (connected)
            {
                CLOSE(command);
                connected = false;
            }
            //Re-establish new connection
            CONNECT(command);

            //Return connection diagnostic data
            return new Value
            {
                { "DBconnectionstate", command.ReadValue },
                { "DBversion", command.ResultString},
                { "cpuUsage", _cpuUsage.NextValue() }
            };
        }


        //------------ Connect to DB ----------------
        private void CONNECT(Command command)
        {
            //Retreive Server Extension parameters
            string ServerAddr = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "AddrServer");
            string DB = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "DB");
            string Port = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "Port");
            string username = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "username");
            //DEBUG temp internalize password
            string password = "badpassword";

            //------ NPGSQL connection --------

            //Build connection string using parameters from TcHmi Server Configuration
            string connectionString =
                String.Format(
                    "Server={0};Username={1};Database={2};Port={3};Password={4};SSLMode=Prefer",
                    ServerAddr,
                    username,
                    DB,
                    Port,
                    password);

            //Assemble connection
            connObject = new NpgsqlConnection(connectionString);

            //Try and Open the connection, catch exceptions and respond as required
            try
            {
                connObject.Open();
                command.ReadValue = connObject.State.ToString()+" ("+connObject.Database+")";
                command.ResultString = connObject.PostgreSqlVersion.ToString();
                command.ExtensionResult = GTAC_TcUI_PostgreSQLErrorValue.GTAC_TcUI_PostgreSQLSuccess;
                connected = true;
            }
            catch (Exception e)
            {
                command.ReadValue = "Could not connect to DB ("+ e.Message +")";
                command.ResultString = "N/A";
                //command.ExtensionResult = GTAC_TcUI_PostgreSQLErrorValue.GTAC_TcUI_PostgreSQLFail;
                connected = false;

            }

        }

        //------------ Read data from DB --------------
        private void SELECT(Command command)
        {
            //To be completed
        }

        //------------- Write data to DB -----------------
        private void INSERT(Command command)
        {
            //To be completed
        }

        //------------- Close Connection -------------
        private void CLOSE(Command command)
        {
            connObject.Close();
        }

        //------------- Get Connected Status -------------
        private Boolean CONNECTION(Command command)
        {
            return connected;
        }

        //-----------------------------------------------------------------
        //------------------------------------------------------------------------
        //------------------------------------------------------------------------


        // Called when a client requests a symbol from the domain of the TwinCAT HMI server extension.
        private void OnRequest(object sender, TcHmiSrv.Core.Listeners.RequestListenerEventArgs.OnRequestEventArgs e)
        {
            try
            {
                e.Commands.Result = GTAC_TcUI_PostgreSQLErrorValue.GTAC_TcUI_PostgreSQLSuccess;

                foreach (Command command in e.Commands)
                {
                    try
                    {
                        // Use the mapping to check which command is requested
                        switch (command.Mapping)
                        {
                            //Sever Extension Diagnostics I
                            case "Diagnostics":
                                command.ReadValue = CollectDiagnosticsData(command);
                                break;
                                
                            //Connect to DB Server
                            case "CONNECT":
                                CONNECT(command);
                                break;

                            //Read Data from Database
                            case "SELECT":
                                SELECT(command);
                                break;

                            //Write Data to Database
                            case "INSERT":
                                INSERT(command);
                                break;

                            //Close Conection
                            case "CLOSE":
                                CLOSE(command);
                                break;

                            //Get Connected Status
                            case "CONNECTION":
                                CONNECTION(command);
                                break;

                            //Default case
                            default:
                                command.ExtensionResult = GTAC_TcUI_PostgreSQLErrorValue.GTAC_TcUI_PostgreSQLFail;
                                command.ResultString = "Unknown command '" + command.Mapping + "' not handled.";
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        command.ExtensionResult = GTAC_TcUI_PostgreSQLErrorValue.GTAC_TcUI_PostgreSQLFail;
                        command.ResultString = "Calling command '" + command.Mapping + "' failed! Additional information: " + ex.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new TcHmiException(ex.ToString(), ErrorValue.HMI_E_EXTENSION);
            }
        }
    }
}
