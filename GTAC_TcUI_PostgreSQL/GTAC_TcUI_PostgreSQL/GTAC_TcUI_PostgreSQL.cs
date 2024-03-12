//-----------------------------------------------------------------------
// <copyright file="GTAC_TcUI_PostgreSQL.cs" company="Beckhoff Automation GmbH & Co. KG">
//     Copyright (c) Beckhoff Automation GmbH & Co. KG. All Rights Reserved.
// </copyright>
//-----------------------------------------------------------------------
//Template by Beckhoff, functional code by: b.lekx-toniolo to create custom Server Extension
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
        private string[] rQUERY = new string[6];
        private string[] wINSERT = new string[6];



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

        //--------------------------------------------------------
        //------------ Read data from DB Base Method--------------
        //--------------------------------------------------------

        private void READ_Base(int DBConnectionNum, ref Command command)
        {

            //Ensure connection state is open, otherwise inform caller
            if (DB_ConnectionArray[DBConnectionNum].State.ToString() == "Open")
            {

                //If SQL command is sent via the Read Trigger then capture and overwrite the manually set rQUERY variable
                if (command.WriteValue != null)
                {
                    rQUERY[DBConnectionNum] = command.WriteValue;
                }

                //Ensure rQUERY has something (either sent from command i/f (above) or from manually set via setQUERY)       
                if (rQUERY != null)
                {
                    //Create a new Npgsql command
                    var SQLreadcommand = new NpgsqlCommand(rQUERY[DBConnectionNum], DB_ConnectionArray[DBConnectionNum]);
                    //Create temp variable for DB read processing
                    string TempString = null;

                    try
                    {
                        //Create new Data Reader Object
                        using NpgsqlDataReader DBreaderObject = SQLreadcommand.ExecuteReader();
                        while (DBreaderObject.Read())
                        {
                            //Return mulitple columns from SELECT command, each column value seperated by ~ marker)
                            if (DBreaderObject.FieldCount >1)
                            {
                                //Build String from mulitple columns of data
                                for(var col = 0; col < DBreaderObject.FieldCount; col++)
                                {
                                    if (col < (DBreaderObject.FieldCount-1))
                                    {
                                        //Seperate Columns by tilde (~) marker
                                        TempString = TempString + DBreaderObject.GetValue(col).ToString() + "~";
                                    }
                                    else
                                    {
                                        //Seperate Rows by asterisk (*) marker
                                        TempString = TempString + DBreaderObject.GetValue(col).ToString() + "*";
                                    }
                                }
                            }
                            //If only a single column is detected, then simply return column zero of the SELECT command, seperated by * marker for mulitple rows
                            else
                            {
                                TempString = TempString + DBreaderObject.GetValue(0).ToString() + "*";
                            }
                            //Clear out previous Query string
                            rQUERY[DBConnectionNum] = "";
                        }
                        //Remove final ~ or * marker(s) from tail end of string, leave only internal markers
                        command.ReadValue = TempString.Substring(0, TempString.Length -1);

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
                    command.ReadValue = "QUERY string null, either set READ symbol upon trigger or use setQUERY method before triggering READ";
                }
            }
            else
            {
                command.ReadValue = "Connection to DB not Open, State = "+ DB_ConnectionArray[DBConnectionNum].State.ToString();
            }
        }

        //Method calls from the server extension Interface (TcHmi symbol triggers)
        //------------- Read from Primary DB ------------
        private void READ(Command command)
        {
            READ_Base(0, ref command);
        }
        //------------- Read from Optional DB 1 ------------
        private void READ_OP1(Command command)
        {
            READ_Base(1, ref command);
        }
        //------------- Read from Optional DB 2----------
        private void READ_OP2(Command command)
        {
            READ_Base(2, ref command);
        }
        //------------- Read from Optional DB 3------------
        private void READ_OP3(Command command)
        {
            READ_Base(3, ref command);
        }
        //------------- Read from Optional DB 4------------
        private void READ_OP4(Command command)
        {
            READ_Base(4, ref command);
        }
        //------------- Read from Optional DB 5------------
        private void READ_OP5(Command command)
        {
            READ_Base(5, ref command);
        }



        //------------------------------------------------------------
        //------------- Write command to DB Base Method -----------------
        //------------------------------------------------------------
        private void WRITE_Base(int DBConnectionNum, ref Command command)
        {
            //Ensure connection state is open, otherwise inform caller
            if (DB_ConnectionArray[DBConnectionNum].State.ToString() == "Open")
            {
                //If SQL command is sent via the WRITETrigger then capture and overwrite the manually set wINSERT variable
                if (command.WriteValue != null)
                {
                    wINSERT[DBConnectionNum] = command.WriteValue;
                }

                //Ensure wINSERT has something (either sent from command i/f or from manually set via setINSERT        
                if (wINSERT != null)
                {
                    //Create a new Npgsql Command
                    var SQLwritecommand = new NpgsqlCommand(wINSERT[DBConnectionNum], DB_ConnectionArray[DBConnectionNum]);

                    try
                    {
                        SQLwritecommand.ExecuteNonQuery();
                        command.ReadValue = "Command Executed";
                        //Clear out previous Insert / Command string
                        wINSERT[DBConnectionNum] = "";
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
                    command.ReadValue = "INSERT string null, either set WRITE symbol upon trigger or use setINSERT method before triggering WRITE";
                }
            }
            else
            {
                command.ReadValue = "Connection to DB not Open, State = " + DB_ConnectionArray[DBConnectionNum].State.ToString();
            }
        }
        //Method calls from the server extension Interface (TcHmi symbol triggers)
        //------------- Write to Primary DB ------------
        private void WRITE(Command command)
        {
            WRITE_Base(0, ref command);
        }
        //------------- Write to Optional DB 1 ------------
        private void WRITE_OP1(Command command)
        {
            WRITE_Base(1, ref command);
        }
        //------------- Write to Optional DB 2 ------------
        private void WRITE_OP2(Command command)
        {
            WRITE_Base(2, ref command);
        }
        //------------- Write to Optional DB 3 ------------
        private void WRITE_OP3(Command command)
        {
            WRITE_Base(3, ref command);
        }
        //------------- Write to Optional DB 4 ------------
        private void WRITE_OP4(Command command)
        {
            WRITE_Base(4, ref command);
        }
        //------------- Write to Optional DB 5 ------------
        private void WRITE_OP5(Command command)
        {
            WRITE_Base(5, ref command);
        }


        //--------------------------------------------
        //------------- Close Connection -------------
        //--------------------------------------------
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


        //------------- Get Current Query String, Primary DB -------------
        private void getQUERY(Command command)
        {
            command.ReadValue = "Current Query String: "+ rQUERY[0];
        }
        //------------- Get Current Query String, Optional DB 1 -------------
        private void getQUERY_OP1(Command command)
        {
            command.ReadValue = "Current Query String: " + rQUERY[1];
        }
        //------------- Get Current Query String, Optional DB 2 -------------
        private void getQUERY_OP2(Command command)
        {
            command.ReadValue = "Current Query String: " + rQUERY[2];
        }
        //------------- Get Current Query String, Optional DB 3 -------------
        private void getQUERY_OP3(Command command)
        {
            command.ReadValue = "Current Query String: " + rQUERY[3];
        }
        //------------- Get Current Query String, Optional DB 4 -------------
        private void getQUERY_OP4(Command command)
        {
            command.ReadValue = "Current Query String: " + rQUERY[4];
        }
        //------------- Get Current Query String, Optional DB 5 -------------
        private void getQUERY_OP5(Command command)
        {
            command.ReadValue = "Current Query String: " + rQUERY[5];
        }



        //------------- Get Current Query String, Primary DB -------------
        private void getINSERT(Command command)
        {
            command.ReadValue = "Current Insert String: " + wINSERT[0];
        }
        //------------- Get Current Query String, Optional DB 1 -------------
        private void getINSERT_OP1(Command command)
        {
            command.ReadValue = "Current Insert String: " + wINSERT[1];
        }
        //------------- Get Current Query String, Optional DB 2 -------------
        private void getINSERT_OP2(Command command)
        {
            command.ReadValue = "Current Insert String: " + wINSERT[2];
        }
        //------------- Get Current Query String, Optional DB 3 -------------
        private void getINSERT_OP3(Command command)
        {
            command.ReadValue = "Current Insert String: " + wINSERT[3];
        }
        //------------- Get Current Query String, Optional DB 4 -------------
        private void getINSERT_OP4(Command command)
        {
            command.ReadValue = "Current Insert String: " + wINSERT[4];
        }
        //------------- Get Current Query String, Optional DB 5 -------------
        private void getINSERT_OP5(Command command)
        {
            command.ReadValue = "Current Insert String: " + wINSERT[5];
        }

        //-----------------------------------------------------------------
        //------------------------------------------------------------------------
        //------------------------------------------------------------------------


        //------------------------------------------------------
        //------------------ Setter Method ---------------------
        //------------------------------------------------------

        //------------- Set Table Target, Primary DB -------------
        private void setQUERY(Command command)
        {
            rQUERY[0] = command.WriteValue;
        }
        //------------- Set Table Target, Optional DB 1 -------------
        private void setQUERY_OP1(Command command)
        {
            rQUERY[1] = command.WriteValue;
        }
        //------------- Set Table Target, Optional DB 2 -------------
        private void setQUERY_OP2(Command command)
        {
            rQUERY[2] = command.WriteValue;
        }
        //------------- Set Table Target, Optional DB 3 -------------
        private void setQUERY_OP3(Command command)
        {
            rQUERY[3] = command.WriteValue;
        }
        //------------- Set Table Target, Optional DB 4 -------------
        private void setQUERY_OP4(Command command)
        {
            rQUERY[4] = command.WriteValue;
        }
        //------------- Set Table Target, Optional DB 5 -------------
        private void setQUERY_OP5(Command command)
        {
            rQUERY[5] = command.WriteValue;
        }

 
        
        //------------- Set Insert Target and Values, Primary DB -------------
        private void setINSERT(Command command)
        {
            wINSERT[0] = command.WriteValue;
        }
        //------------- Set Insert Target and Values, Optional DB 1 -------------
        private void setINSERT_OP1(Command command)
        {
            wINSERT[1] = command.WriteValue;
        }
        //------------- Set Insert Target and Values, Optional DB 2 -------------
        private void setINSERT_OP2(Command command)
        {
            wINSERT[2] = command.WriteValue;
        }
        //------------- Set Insert Target and Values, Optional DB 3 -------------
        private void setINSERT_OP3(Command command)
        {
            wINSERT[3] = command.WriteValue;
        }
        //------------- Set Insert Target and Values, Optional DB 4 -------------
        private void setINSERT_OP4(Command command)
        {
            wINSERT[4] = command.WriteValue;
        }
        //------------- Set Insert Target and Values, Optional DB 5 -------------
        private void setINSERT_OP5(Command command)
        {
            wINSERT[5] = command.WriteValue;
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
                            case "READ_OP1":
                                READ_OP1(command);
                                break;
                            case "READ_OP2":
                                READ_OP2(command);
                                break;
                            case "READ_OP3":
                                READ_OP3(command);
                                break;
                            case "READ_OP4":
                                READ_OP4(command);
                                break;
                            case "READ_OP5":
                                READ_OP5(command);
                                break;

                            //Write Data to Database
                            case "WRITE":
                                WRITE(command);
                                break;
                            //Write Data to Database
                            case "WRITE_OP1":
                                WRITE_OP1(command);
                                break;
                            //Write Data to Database
                            case "WRITE_OP2":
                                WRITE_OP2(command);
                                break;
                            //Write Data to Database
                            case "WRITE_OP3":
                                WRITE_OP3(command);
                                break;
                            //Write Data to Database
                            case "WRITE_OP4":
                                WRITE_OP4(command);
                                break;
                            //Write Data to Database
                            case "WRITE_OP5":
                                WRITE_OP5(command);
                                break;

                            //Close Conection
                            case "CLOSE":
                                CLOSE(command);
                                break;
                            //Close Conection
                            case "CLOSE_OP1":
                                CLOSE_OP1(command);
                                break;
                            //Close Conection
                            case "CLOSE_OP2":
                                CLOSE_OP2(command);
                                break;
                            //Close Conection
                            case "CLOSE_OP3":
                                CLOSE_OP3(command);
                                break;
                            //Close Conection
                            case "CLOSE_OP4":
                                CLOSE_OP4(command);
                                break;
                            //Close Conection
                            case "CLOSE_OP5":
                                CLOSE_OP5(command);
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

                            //Get Current Query string, Primary DB
                            case "getQUERY":
                                getQUERY(command);
                                break;
                            //Get Current Query string, Optional DB 1
                            case "getQUERY_OP1":
                                getQUERY_OP1(command);
                                break;
                            //Get Current Query string, Optional DB 2
                            case "getQUERY_OP2":
                                getQUERY_OP2(command);
                                break;
                            //Get Current Query string, Optional DB 3
                            case "getQUERY_OP3":
                                getQUERY_OP2(command);
                                break;
                            //Get Current Query string, Optional DB 4
                            case "getQUERY_OP4":
                                getQUERY_OP4(command);
                                break;
                            //Get Current Query string, Optional DB 5
                            case "getQUERY_OP5":
                                getQUERY_OP5(command);
                                break;

                            //Get Current Insert string, Primary DB
                            case "getINSERT":
                                getINSERT(command);
                                break;
                            //Get Current Insert string, Optional DB 1
                            case "getINSERT_OP1":
                                getINSERT_OP1(command);
                                break;
                            //Get Current Insert string, Optional DB 2
                            case "getINSERT_OP2":
                                getINSERT_OP2(command);
                                break;
                            //Get Current Insert string, Optional DB 3
                            case "getINSERT_OP3":
                                getINSERT_OP3(command);
                                break;
                            //Get Current Insert string, Optional DB 4
                            case "getINSERT_OP4":
                                getINSERT_OP4(command);
                                break;
                            //Get Current Insert string, Optional DB 5
                            case "getINSERT_OP5":
                                getINSERT_OP5(command);
                                break;

                            //-------- Setter Method Calls ------

                            //Set QUERY STRING for DB Reads
                            //Primary DB
                            case "setQUERY":
                                setQUERY(command);
                                break;
                            //Optional DB 1
                            case "setQUERY_OP1":
                                setQUERY_OP1(command);
                                break;
                            //Optional DB 2
                            case "setQUERY_OP2":
                                setQUERY_OP2(command);
                                break;
                            //Optional DB 3
                            case "setQUERY_OP3":
                                setQUERY_OP3(command);
                                break;
                            //Optional DB 4
                            case "setQUERY_OP4":
                                setQUERY_OP4(command);
                                break;
                            //Optional DB 5
                            case "setQUERY_OP5":
                                setQUERY_OP5(command);
                                break;

                            //Set INSERT STRING for DB Writes
                            //Primary DB
                            case "setINSERT":
                                setINSERT(command);
                                break;
                            //Optional DB 1
                            case "setINSERT_OP1":
                                setINSERT_OP1(command);
                                break;
                            //Optional DB 2
                            case "setINSERT_OP2":
                                setINSERT_OP2(command);
                                break;
                            //Optional DB 3
                            case "setINSERT_OP3":
                                setINSERT_OP3(command);
                                break;
                            //Optional DB 4
                            case "setINSERT_OP4":
                                setINSERT_OP4(command);
                                break;
                            //Optional DB 5
                            case "setINSERT_OP5":
                                setINSERT_OP5(command);
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
