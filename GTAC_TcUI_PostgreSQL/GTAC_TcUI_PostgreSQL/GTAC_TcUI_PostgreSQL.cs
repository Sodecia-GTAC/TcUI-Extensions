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
            //Connection is already established so simply pass data
            if (connected)
            {
                command.ReadValue = connObject.State.ToString() + " (" + connObject.Database + ")";
                command.ResultString = connObject.PostgreSqlVersion.ToString();
                command.ExtensionResult = GTAC_TcUI_PostgreSQLErrorValue.GTAC_TcUI_PostgreSQLSuccess;
            }
            //Otherwise (no connection) create new connection 
            else
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
                    command.ReadValue = connObject.State.ToString() + " (" + connObject.Database + ")";
                    command.ResultString = connObject.PostgreSqlVersion.ToString();
                    command.ExtensionResult = GTAC_TcUI_PostgreSQLErrorValue.GTAC_TcUI_PostgreSQLSuccess;
                    connected = true;
                }
                catch (Exception e)
                {
                    command.ReadValue = "Could not connect to DB (" + e.Message + ")";
                    command.ResultString = "N/A";
                    //command.ExtensionResult = GTAC_TcUI_PostgreSQLErrorValue.GTAC_TcUI_PostgreSQLFail;
                    connected = false;

                }
            }
        }
        
        //------------ Read data from DB --------------
        private async void SELECT(Command command, string SQL_query)
        {
            if (connected)
            {
                //DEBUG, not complete
                await using var SQLreadcommand = new NpgsqlCommand(SQL_query, connObject);
                await using var DBreader = await SQLreadcommand.ExecuteReaderAsync();

                while (await DBreader.ReadAsync())
                {
                    command.ReadValue = DBreader.GetValue(2).ToString();

                }

                //Final Resource Clean-up / Garbage collection
                //await SQLreadcommand.DisposeAsync();
                //await DBreader.DisposeAsync();
            }
            else
            {
                command.ReadValue = "Not connected to DB";
            }
        }

        //------------- Write data to DB -----------------
        private async void INSERT(Command command)
        {
            if (connected)
            {
                //DEBUG, not complete
                await using var SQLwritecommand = new NpgsqlCommand("INSERT INTO public.brent_test_table (description) VALUES ($1), ($2)", connObject)
                {
                    Parameters =
                    {
                    new() { Value = "Brent_Value1" },
                    new() { Value = "Brent_Value2" }
                    }
                };

                try
                {
                    await SQLwritecommand.ExecuteNonQueryAsync();
                    command.ReadValue = "Wrote to DB";
                }
                catch (Exception e)
                {
                    command.ReadValue = "Could not write to DB (" + e.Message + ")";

                }
                //Final Resource Clean-up / Garbage collection
                //await SQLwritecommand.DisposeAsync();
            }
            else
            {
                //Add error handling for an INSERT request with no DB connection
            }
        }

        //------------- Close Connection -------------
        private void CLOSE(Command command)
        {
            connObject.Close();
            connObject.Dispose();
            connected = false;
        }

        //------------- Get Connected Status -------------
        private void CONNECTED(Command command)
        {
            command.ReadValue = connected;
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
                                string temp = "SELECT * FROM public.fieldbus_descr_rxx_kukatype6x_in LIMIT 1";
                                SELECT(command, temp);
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
                            case "CONNECTED":
                                CONNECTED(command);
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
