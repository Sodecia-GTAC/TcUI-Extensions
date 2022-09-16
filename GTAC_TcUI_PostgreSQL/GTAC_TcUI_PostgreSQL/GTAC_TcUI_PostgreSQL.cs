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
        private NpgsqlConnection[] DB_ConnectionArray = new NpgsqlConnection[6];
        private bool[] DB_isconnected = new bool[6];

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

        //------------------------------------------------------
        //------------ Connect to DB Base Method----------------
        //------------------------------------------------------

        private void CONNECT_Base(int DBConnectionNum, string targetDB, ref Command command)
        {
            //Check if DB name paramter is actually configured, if not then we will simply return a "Not Configured"
            if (targetDB != "" || targetDB != null)
            {

                //If no connection, create new connection and open 
                if (DB_isconnected[DBConnectionNum] != true || DB_ConnectionArray[DBConnectionNum].FullState.ToString() != "Open")
                {
                    //Retreive Server Extension parameters
                    string ServerAddr = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "AddrServer");
                    string Port = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "Port");
                    string DB = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, targetDB);
                    string username = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "username");
                    //DEBUG must encrypt at entry point
                    string password = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "userpassword");


                    //------ NPGSQL connection --------

                    //Build connection string using parameters from TcHmi Server Configuration
                    string connectionString =
                        "Host=" + ServerAddr + ";Port = " + Port + ";Database=" + DB + ";Username =" + username + ";Password=" + password + ";Timeout=" + connTimeout + ";CommandTimeout=" + cmdTimeout + ";Keepalive=" + keepAlive + ";";


                    //Assemble connection
                    DB_ConnectionArray[DBConnectionNum] = new NpgsqlConnection(connectionString);

                    //Set some parameters

                    //Try and open a connection, catch exceptions and respond as required
                    try
                    {
                        DB_ConnectionArray[DBConnectionNum].Open();
                        command.ReadValue = DB_ConnectionArray[DBConnectionNum].FullState.ToString() + " (" + DB_ConnectionArray[DBConnectionNum].Database + ")";
                        command.ExtensionResult = GTAC_TcUI_PostgreSQLErrorValue.GTAC_TcUI_PostgreSQLSuccess;
                        DB_isconnected[DBConnectionNum] = true;
                    }
                    catch (Exception e)
                    {
                        command.ReadValue = "No Connection (" + e.Message + ")";
                        command.ResultString = "N/A";
                        DB_isconnected[DBConnectionNum] = false;
                    }

                }
                //Connection is already established so simply confirm and pass data
                else
                {
                    command.ReadValue = DB_ConnectionArray[DBConnectionNum].FullState.ToString() + " (" + DB_ConnectionArray[DBConnectionNum].Database + ")";
                    command.ExtensionResult = GTAC_TcUI_PostgreSQLErrorValue.GTAC_TcUI_PostgreSQLSuccess;
                    DB_isconnected[DBConnectionNum] = true;
                }
            }
            //Connection targetDB is not configured so connection will not be used
            else
            {
                command.ReadValue = ("Not Configured");
                command.ExtensionResult = GTAC_TcUI_PostgreSQLErrorValue.GTAC_TcUI_PostgreSQLSuccess;
                DB_isconnected[DBConnectionNum] = false;
            }
        }

        //Method calls from the server extension Interface (TcHmi symbol triggers)
        //------------ Connect to Primary DB ----------------
        private void CONNECT(Command command)
        {
            CONNECT_Base(0, "DB", ref command);
        }
        //------------ Connect to DB Optional 1 ----------------
        private void CONNECT_OP1(Command command)
        {
            CONNECT_Base(1, "DB_OP1", ref command);
        }
        //------------ Connect to DB Optional 2 ----------------
        private void CONNECT_OP2(Command command)
        {
            CONNECT_Base(2, "DB_OP2", ref command);
        }

        //------------ Connect to DB Optional 3 ----------------
        private void CONNECT_OP3(Command command)
        {
            CONNECT_Base(3, "DB_OP3", ref command);
        }

        //------------ Connect to DB Optional 4 ----------------
        private void CONNECT_OP4(Command command)
        {
            CONNECT_Base(4, "DB_OP4", ref command);
        }

        //------------ Connect to DB Optional 5 ----------------
        private void CONNECT_OP5(Command command)
        {
            CONNECT_Base(5, "DB_OP5", ref command);
        }


        //------------ Read data from DB --------------
        private void READ(Command command)
        {

            if (DB_ConnectionArray[0].FullState.ToString() == "Open")
            {
                if (rQUERY != null)
                {
                    //Create a new Npgsql command
                    var SQLreadcommand = new NpgsqlCommand(rQUERY, DB_ConnectionArray[0]);

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
            if (DB_ConnectionArray[0].State.ToString() == "Open")
            {
                if (wINSERT != null)
                {
                    //Create a new Npgsql Command
                    var SQLwritecommand = new NpgsqlCommand(wINSERT, DB_ConnectionArray[0]);

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
            DB_ConnectionArray[0].Close();
            DB_ConnectionArray[0].Dispose();
            DB_isconnected[0] = false;
        }
        //------------- Close Connection Optional 1-------------
        private void CLOSE_OP1(Command command)
        {
            DB_ConnectionArray[1].Close();
            DB_ConnectionArray[1].Dispose();
            DB_isconnected[1] = false;
        }
        //------------- Close Connection Optional 2-------------
        private void CLOSE_OP2(Command command)
        {
            DB_ConnectionArray[2].Close();
            DB_ConnectionArray[2].Dispose();
            DB_isconnected[2] = false;
        }
        //------------- Close Connection Optional 3-------------
        private void CLOSE_OP3(Command command)
        {
            DB_ConnectionArray[3].Close();
            DB_ConnectionArray[3].Dispose();
            DB_isconnected[3] = false;
        }
        //------------- Close Connection Optional 4-------------
        private void CLOSE_OP4(Command command)
        {
            DB_ConnectionArray[4].Close();
            DB_ConnectionArray[4].Dispose();
            DB_isconnected[4] = false;
        }
        //------------- Close Connection Optional 5-------------
        private void CLOSE_OP5(Command command)
        {
            DB_ConnectionArray[5].Close();
            DB_ConnectionArray[5].Dispose();
            DB_isconnected[5] = false;
        }

        //------------------------------------------------------
        //------------------ Gettter Status Method -------------
        //------------------------------------------------------

 
        //------------- Get Connected Status Primary DB-------------
        private void getCONNECTED(Command command)
        {
            command.ReadValue = DB_isconnected[0];
        }
        //------------- Get Connected Status (Optional DB 1)-------------
        private void getCONNECTED_OP1(Command command)
        {
            command.ReadValue = DB_isconnected[1];
        }
        //------------- Get Connected Status (Optional DB 2)-------------
        private void getCONNECTED_OP2(Command command)
        {
            command.ReadValue = DB_isconnected[2];
        }
        //------------- Get Connected Status (Optional DB 3)-------------
        private void getCONNECTED_OP3(Command command)
        {
            command.ReadValue = DB_isconnected[3];
        }
        //------------- Get Connected Status (Optional DB 4)-------------
        private void getCONNECTED_OP4(Command command)
        {
            command.ReadValue = DB_isconnected[4];
        }
        //------------- Get Connected Status (Optional DB 5)-------------
        private void getCONNECTED_OP5(Command command)
        {
            command.ReadValue = DB_isconnected[5];
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
        //------------------ Setter Method ---------------------
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


        //------------------------------------------------------------------------
        //------------------------------------------------------------------------
        //------------------------------------------------------------------------

        //------------------------------------------------------------------------
        //------------- TcHmi Server Extension Interface -------------------------
        //---------- (commands from TcHmi that end up calling the above)----------
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
                            //Connect to DB Server
                            case "CONNECT_OP1":
                                CONNECT_OP1(command);
                                break;
                            //Connect to DB Server
                            case "CONNECT_OP2":
                                CONNECT_OP2(command);
                                break;
                            //Connect to DB Server
                            case "CONNECT_OP3":
                                CONNECT_OP3(command);
                                break;
                            //Connect to DB Server
                            case "CONNECT_OP4":
                                CONNECT_OP4(command);
                                break;
                            //Connect to DB Server
                            case "CONNECT_OP5":
                                CONNECT_OP5(command);
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
                            //Close Conection
                            case "CLOSE_OP1":
                                CLOSE(command);
                                break;
                            //Close Conection
                            case "CLOSE_OP2":
                                CLOSE(command);
                                break;
                            //Close Conection
                            case "CLOSE_OP3":
                                CLOSE(command);
                                break;
                            //Close Conection
                            case "CLOSE_OP4":
                                CLOSE(command);
                                break;
                            //Close Conection
                            case "CLOSE_OP5":
                                CLOSE(command);
                                break;


                            //-------- Getter Method Calls ------

                            //Get Connected Status Primary DB
                            case "getCONNECTED":
                                getCONNECTED(command);
                                break;
                            //Get Connected Status Optional DB 1
                            case "getCONNECTED_OP1":
                                getCONNECTED_OP1(command);
                                break;
                            //Get Connected Status Optional DB 2
                            case "getCONNECTED_OP2":
                                getCONNECTED_OP2(command);
                                break;
                            //Get Connected Status Optional DB 3
                            case "getCONNECTED_OP3":
                                getCONNECTED_OP3(command);
                                break;
                            //Get Connected Status Optional DB 4
                            case "getCONNECTED_OP4":
                                getCONNECTED_OP4(command);
                                break;
                            //Get Connected Status Optional DB 5
                            case "getCONNECTED_OP5":
                                getCONNECTED_OP5(command);
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
