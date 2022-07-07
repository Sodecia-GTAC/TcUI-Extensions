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
        private readonly string connTimeout = "2";
        private readonly string cmdTimeout = "3";
        private readonly string keepAlive = "10";


        //Create Npgsql connection object and connected flag place holders
        NpgsqlConnection connObject;
        private bool connected;

        //Some internal variables
        private string rQUERY = null;
        private string wINSERT = null;



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
                string ServerAddr = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "AddrServer");
                string Port = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "Port");
                string DB = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "DB");
                string username = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "username");
                //DEBUG must encrypt at entry point
                string password = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "userpassword");


                //------ NPGSQL connection --------

                //Build connection string using parameters from TcHmi Server Configuration
                string connectionString =
                    "Host="+ServerAddr+ ";Port = "+Port+ ";Database=" + DB + ";Username =" + username+";Password="+password+ ";Timeout="+connTimeout+";CommandTimeout="+cmdTimeout+";Keepalive="+keepAlive+";";


                //Assemble connection
                connObject = new NpgsqlConnection(connectionString);

                //Set some parameters
                
                //Try and open a connection, catch exceptions and respond as required
                try 
                {
                    connObject.Open();
                    command.ReadValue = connObject.FullState.ToString() + " (" + connObject.Database + ")";
                    command.ExtensionResult = GTAC_TcUI_PostgreSQLErrorValue.GTAC_TcUI_PostgreSQLSuccess;
                    connected = true;
                }
                catch (Exception e)
                {
                    command.ReadValue = "No Connection ("+e.Message+")";
                    command.ResultString = "N/A";
                    connected = false;
                }

            }
            //Connection is already established so simply pass data
            else
            {
                command.ReadValue = connObject.FullState.ToString() + " (" + connObject.Database + ")";
                command.ExtensionResult = GTAC_TcUI_PostgreSQLErrorValue.GTAC_TcUI_PostgreSQLSuccess;
            }

        }

        //------------ Read data from DB --------------
        private void READ(Command command)
        {

            if (connObject.FullState.ToString() == "Open")
            {
                if (rQUERY != null)
                {
                    //Create a new Npgsql command
                    var SQLreadcommand = new NpgsqlCommand(rQUERY, connObject);

                    try
                    {
                        //Create newe Data Read Object
                        using NpgsqlDataReader DBreaderObject = SQLreadcommand.ExecuteReader();
                        while (DBreaderObject.Read())
                        {
                            command.ReadValue = DBreaderObject.GetValue(0).ToString();
                        }
                      
                    }
                    catch (Exception e)
                    {
                        command.ReadValue = "Failed to read: " + e.Message;
                    }
                    
                    //Final Resource Clean-up / Garbage collection
                    SQLreadcommand.Dispose();

                }
                else
                {
                    command.ReadValue = "QUERY string null, use setQUERY method before triggering READ";
                }
            }
            else
            {
                command.ReadValue = "Not connected to DB";
            }
        }

        //------------- Write data to DB -----------------
        private void WRITE(Command command)
        {   
            if (connObject.State.ToString() == "Open")
            {
                if (wINSERT != null)
                {
                    //Create a new Npgsql Command
                    var SQLwritecommand = new NpgsqlCommand(wINSERT, connObject);

                    try
                    {
                        SQLwritecommand.ExecuteNonQuery();
                        command.ReadValue = "Wrote to DB";
                    }

                    catch (Exception e)
                    {
                        command.ReadValue = "Failed to write:" + e.Message;

                    }

                    //Final Resource Clean-up / Garbage collection
                    SQLwritecommand.Dispose();
                }
                else
                {
                    command.ReadValue = "INSERT string null, use setINSERT method before triggering WRITE";
                }
            }
            else
            {
                command.ReadValue = "Not connected to DB";
            }
        }

        //------------- Close Connection -------------
        private void CLOSE(Command command)
        {
            connObject.Close();
            connected = false;
        }

        //------------------------------------------------------
        //------------------ Get Status Methods ----------------
        //------------------------------------------------------

        //------------- Get Host -------------
        private void getHOST(Command command)
        {
            command.ReadValue = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "AddrServer");
        }

        //------------- Get Port -------------
        private void getPORT(Command command)
        {
            command.ReadValue = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "Port");
        }
        //------------- Get Host -------------
        private void getDB(Command command)
        {
            command.ReadValue = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "DB");
        }
        //------------- Get Host -------------
        private void getUSER(Command command)
        {
            command.ReadValue = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "username");
        }


        //------------- Get Connected Status -------------
        private void getCONNECTED(Command command)
        {
            
            if (connObject.FullState.ToString() == "Open")
            {
                connected = true;
                command.ReadValue = true;
            }
            else
            {
                connected = false;
                command.ReadValue = false;
            }
           
        }

        //------------- Get Current Query String-------------
        private void getQUERY(Command command)
        {
            command.ReadValue = "Current Query String: "+ rQUERY;
        }

        //------------- Get Current Query String-------------
        private void getINSERT(Command command)
        {
            command.ReadValue = "Current Insert String: " + wINSERT;
        }

        //-----------------------------------------------------------------
        //------------------------------------------------------------------------
        //------------------------------------------------------------------------


        //------------------------------------------------------
        //------------------ Set Values Methods ----------------
        //------------------------------------------------------

        //------------- Set Table Target -------------
        private void setQUERY(Command command)
        {
            rQUERY = command.WriteValue;

        }

        //------------- Set Insert Target and Values -------------
        private void setINSERT(Command command)
        {
            wINSERT = command.WriteValue;

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
                            case "READ":
                                READ(command);
                                break;

                            //Write Data to Database
                            case "WRITE":
                                WRITE(command);
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

                            //Get Current Query string
                            case "getQUERY":
                                getQUERY(command);
                                break;

                            //Get Current Insert string
                            case "getINSERT":
                                getINSERT(command);
                                break;

                            //-------- Setter Method Calls ------
                            //Set QUERY STRING for DB Reads
                            case "setQUERY":
                                setQUERY(command);
                                break;

                            //Set INSERT STRING for DB Writes
                            case "setINSERT":
                                setINSERT(command);
                                break;


                            //Default case
                            default:
                                command.ResultString = "Unknown command '" + command.Mapping + "' not handled.";
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        command.ResultString = "Calling command '" + command.Mapping + "' failed! Additional information: " + ex.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new TcHmiException("GTAC_TcUI_PostgreSQL TcHmi Error ->"+ex.Message.ToString(), ErrorValue.HMI_E_EXTENSION);
            }
        }
    }
}
