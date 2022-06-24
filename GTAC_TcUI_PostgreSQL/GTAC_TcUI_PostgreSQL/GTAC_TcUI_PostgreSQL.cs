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

        //Some connection paramters
        private string connTimeout = "2";
        private string cmdTimeout = "3";


        //Create Npgsql connection object and connected flag place holders
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
        
  
        //------------ Connect to DB ----------------
        private async void CONNECT(Command command)
        {
            //If no connection, create new connection and open 
            if (connected != true)
            {
                //Retreive Server Extension parameters
                string Host = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "Host");
                string Port = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "Port");
                string DB = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "DB");
                string username = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "username");
                //DEBUG must encrypt at entry point
                string password = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "userpassword");


                //------ NPGSQL connection --------

                //Build connection string using parameters from TcHmi Server Configuration
                string connectionString =
                    "Host="+Host+ ";Port = "+Port+ ";Database=" + DB + ";Username =" + username+";Password="+password+ ";Timeout="+connTimeout+";CommandTimeout="+cmdTimeout+";";


                //Assemble connection
                connObject = new NpgsqlConnection(connectionString);

                //Set some parameters
                
                //Try and open a connection, catch exceptions and respond as required
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
                    command.ReadValue = "No Connection ("+e.Message+")";
                    command.ResultString = "N/A";
                    //command.ExtensionResult = GTAC_TcUI_PostgreSQLErrorValue.GTAC_TcUI_PostgreSQLFail;
                    connected = false;
                }
                
            }
            //Connection is already established so simply pass data
            else
            {
                command.ReadValue = connObject.State.ToString() + " (" + connObject.Database + ")";
                command.ResultString = connObject.PostgreSqlVersion.ToString();
                command.ExtensionResult = GTAC_TcUI_PostgreSQLErrorValue.GTAC_TcUI_PostgreSQLSuccess;
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
                await SQLreadcommand.DisposeAsync();
                await DBreader.DisposeAsync();
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
                await SQLwritecommand.DisposeAsync();
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

        //------------------------------------------------------
        //------------------ Get Status Methods ----------------
        //------------------------------------------------------

        //------------- Get Host -------------
        private void getHOST(Command command)
        {
            command.ReadValue = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "Host");
        }

        //------------- Get Port -------------
        private void getPORT(Command command)
        {
            //command.ReadValue = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "Port");
            command.ReadValue = "HI1";
        }
        //------------- Get Host -------------
        private void getDB(Command command)
        {
            //command.ReadValue = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "DB");
            command.ReadValue = "HI2";
        }
        //------------- Get Host -------------
        private void getUSER(Command command)
        {
            //command.ReadValue = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "username");
            command.ReadValue = "HI3";
        }

        //------------- Get Connected Status -------------
        private void getCONNECTED(Command command)
        {
            
            if (connObject.State.ToString() == "Open")
            {
                command.ReadValue = true;

            }
            else
            {
                command.ReadValue = false;
            }

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
                            
                            //-------- Functional Method calls ---------    
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


                            //-------- Getter Method Calls ------
                            //Get Host Value
                            case "getHOST":
                                getHOST(command);
                                break;
 
                            //Get Port Value
                            case "getPORT":
                                getPORT(command);
                                break;
                            
                            //Get DB Value
                            case "getDB":
                                getDB(command);
                                break;
                            
                            //Get User Value
                            case "getUSER":
                                getUSER(command);
                                break;

                            //Get Connected Status
                            case "getCONNECTED":
                                getCONNECTED(command);
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
