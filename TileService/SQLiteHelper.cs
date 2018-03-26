using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Web;

namespace WCFService
{
    /// <summary>
    /// The SQLiteHelper class is intended to encapsulate high performance, scalable best practices for 
    /// common uses of SQLiteClient.
    /// </summary>
    public sealed class SQLiteHelper
    {
        #region save connect string
        private static string _connectionString;
        public static string SetConnectionString { set { _connectionString = value; SQLiteHelperParameterCache.SetConnectionString = value; } }
        #endregion

        #region private utility methods & constructors

        //Since this class provides only static methods, make the default constructor private to prevent 
        //instances from being created with "new SQLiteHelper()".
        private SQLiteHelper() { }

        /// <summary>
        /// This method is used to attach array's of SQLiteParameters to an SQLiteCommand.
        /// 
        /// This method will assign a value of DbNull to any parameter with a direction of
        /// InputOutput and a value of null.  
        /// 
        /// This behavior will prevent default values from being used, but
        /// this will be the less common case than an intended pure output parameter (derived as InputOutput)
        /// where the user provided no input value.
        /// </summary>
        /// <param name="command">The command to which the parameters will be added</param>
        /// <param name="commandParameters">an array of SQLiteParameters tho be added to command</param>
        private static void AttachParameters(SQLiteCommand command, SQLiteParameter[] commandParameters)
        {
            foreach (SQLiteParameter p in commandParameters)
            {
                //check for derived output value with no value assigned
                if ((p.Direction == ParameterDirection.InputOutput) && (p.Value == null))
                {
                    p.Value = DBNull.Value;
                }

                command.Parameters.Add(p);
            }
        }

        /// <summary>
        /// This method assigns an array of values to an array of SQLiteParameters.
        /// </summary>
        /// <param name="commandParameters">array of SQLiteParameters to be assigned values</param>
        /// <param name="parameterValues">array of objects holding the values to be assigned</param>
        private static void AssignParameterValues(SQLiteParameter[] commandParameters, object[] parameterValues)
        {
            if ((commandParameters == null) || (parameterValues == null))
            {
                //do nothing if we get no data
                return;
            }

            // we must have the same number of values as we pave parameters to put them in
            if (commandParameters.Length != parameterValues.Length)
            {
                throw new ArgumentException("Parameter count does not match Parameter Value count.");
            }

            //iterate through the SQLiteParameters, assigning the values from the corresponding position in the 
            //value array
            for (int i = 0, j = commandParameters.Length; i < j; i++)
            {
                commandParameters[i].Value = parameterValues[i];
            }
        }

        /// <summary>
        /// This method opens (if necessary) and assigns a connection, transaction, command type and parameters 
        /// to the provided command.
        /// </summary>
        /// <param name="command">the SQLiteCommand to be prepared</param>
        /// <param name="connection">a valid SQLiteConnection, on which to execute this command</param>
        /// <param name="transaction">a valid SQLiteTransaction, or 'null'</param>
        /// <param name="commandType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">the stored procedure name or PL/SQL command</param> 
        /// <param name="commandParameters">an array of SQLiteParameters to be associated with the command or 'null' if no parameters are required</param>
        private static void PrepareCommand(SQLiteCommand command, SQLiteConnection connection, SQLiteTransaction transaction, CommandType commandType, string commandText, SQLiteParameter[] commandParameters)
        {
            //if the provided connection is not open, we will open it
            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
            }

            //associate the connection with the command
            command.Connection = connection;

            //set the command text (stored procedure name or SQLite statement)
            command.CommandText = commandText;

            //if we were provided a transaction, assign it.
            if (transaction != null)
            {
                command.Transaction = transaction;
            }

            //set the command type
            command.CommandType = commandType;

            //attach the command parameters if they are provided
            if (commandParameters != null)
            {
                AttachParameters(command, commandParameters);
            }

            return;
        }


        #endregion private utility methods & constructors

        #region ExecuteNonQuery

        /// <summary>
        /// Execute an SQLiteCommand (that returns no resultset and takes no parameters) against the database specified in 
        /// the connection string. 
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  int result = ExecuteNonQuery(connString, CommandType.StoredProcedure, "PublishOrders");
        /// </remarks>
        /// <param name="connectionString">a valid connection string for an SQLiteConnection</param>
        /// <param name="commandType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">the stored procedure name or PL/SQL command</param>  
        /// <returns>an int representing the number of rows affected by the command</returns>
        public static int ExecuteNonQuery(string connectionString, CommandType commandType, string commandText)
        {
            //pass through the call providing null for the set of SQLiteParameters
            return ExecuteNonQuery(connectionString, commandType, commandText, (SQLiteParameter[])null);
        }
        public static int ExecuteNonQuery(CommandType commandType, string commandText) { return ExecuteNonQuery(_connectionString, commandType, commandText); }

        /// <summary>
        /// Execute an SQLiteCommand (that returns no resultset) against the database specified in the connection string 
        /// using the provided parameters.
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  int result = ExecuteNonQuery(connString, CommandType.StoredProcedure, "PublishOrders", new SQLiteParameter("@prodid", 24));
        /// </remarks>
        /// <param name="connectionString">a valid connection string for an SQLiteConnection</param>
        /// <param name="commandType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">the stored procedure name or PL/SQL command</param>  
        /// <param name="commandParameters">an array of SQLiteParameters used to execute the command</param>
        /// <returns>an int representing the number of rows affected by the command</returns>
        public static int ExecuteNonQuery(string connectionString, CommandType commandType, string commandText, params SQLiteParameter[] commandParameters)
        {
            //create & open an SQLiteConnection, and dispose of it after we are done.
            using (SQLiteConnection cn = new SQLiteConnection(connectionString))
            {
                cn.Open();

                //call the overload that takes a connection in place of the connection string
                return ExecuteNonQuery(cn, commandType, commandText, commandParameters);
            }
        }
        public static int ExecuteNonQuery(CommandType commandType, string commandText, params SQLiteParameter[] commandParameters) { return ExecuteNonQuery(_connectionString, commandType, commandText, commandParameters); }

        /// <summary>
        /// Execute a stored procedure via an SQLiteCommand (that returns no resultset) against the database specified in 
        /// the connection string using the provided parameter values.  This method will query the database to discover the parameters for the 
        /// stored procedure (the first time each stored procedure is called), and assign the values based on parameter order.
        /// </summary>
        /// <remarks>
        /// This method provides no access to output parameters or the stored procedure's return value parameter.
        /// 
        /// e.g.:  
        ///  int result = ExecuteNonQuery(connString, "PublishOrders", 24, 36);
        /// </remarks>
        /// <param name="connectionString">a valid connection string for an SQLiteConnection</param>
        /// <param name="spName">the name of the stored procedure</param>
        /// <param name="parameterValues">an array of objects to be assigned as the input values of the stored procedure</param>
        /// <returns>an int representing the number of rows affected by the command</returns>
        public static int ExecuteNonQuery(string connectionString, string spName, params object[] parameterValues)
        {
            //if we got parameter values, we need to figure out where they go
            if ((parameterValues != null) && (parameterValues.Length > 0))
            {
                //pull the parameters for this stored procedure from the parameter cache (or discover them & populate the cache)
                SQLiteParameter[] commandParameters = SQLiteHelperParameterCache.GetSpParameterSet(connectionString, spName);

                //assign the provided values to these parameters based on parameter order
                AssignParameterValues(commandParameters, parameterValues);

                //call the overload that takes an array of SQLiteParameters
                return ExecuteNonQuery(connectionString, CommandType.StoredProcedure, spName, commandParameters);
            }
            //otherwise we can just call the SP without params
            else
            {
                return ExecuteNonQuery(connectionString, CommandType.StoredProcedure, spName);
            }
        }
        public static int ExecuteNonQuery(string spName, params object[] parameterValues) { return ExecuteNonQuery(_connectionString, spName, parameterValues); }

        /// <summary>
        /// Execute an SQLiteCommand (that returns no resultset and takes no parameters) against the provided SQLiteConnection. 
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  int result = ExecuteNonQuery(conn, CommandType.StoredProcedure, "PublishOrders");
        /// </remarks>
        /// <param name="connection">a valid SQLiteConnection</param>
        /// <param name="commandType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">the stored procedure name or PL/SQL command</param>
        /// <returns>an int representing the number of rows affected by the command</returns>
        public static int ExecuteNonQuery(SQLiteConnection connection, CommandType commandType, string commandText)
        {
            //pass through the call providing null for the set of SQLiteParameters
            return ExecuteNonQuery(connection, commandType, commandText, (SQLiteParameter[])null);
        }

        /// <summary>
        /// Execute an SQLiteCommand (that returns no resultset) against the specified SQLiteConnection 
        /// using the provided parameters.
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  int result = ExecuteNonQuery(conn, CommandType.StoredProcedure, "PublishOrders", new SQLiteParameter("@prodid", 24));
        /// </remarks>
        /// <param name="connection">a valid SQLiteConnection</param>
        /// <param name="commandType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">the stored procedure name or PL/SQL command</param>  
        /// <param name="commandParameters">an array of SQLiteParameters used to execute the command</param>
        /// <returns>an int representing the number of rows affected by the command</returns>
        public static int ExecuteNonQuery(SQLiteConnection connection, CommandType commandType, string commandText, params SQLiteParameter[] commandParameters)
        {
            //create a command and prepare it for execution
            SQLiteCommand cmd = new SQLiteCommand();
            PrepareCommand(cmd, connection, (SQLiteTransaction)null, commandType, commandText, commandParameters);

            //finally, execute the command.
            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Execute a stored procedure via an SQLiteCommand (that returns no resultset) against the specified SQLiteConnection 
        /// using the provided parameter values.  This method will query the database to discover the parameters for the 
        /// stored procedure (the first time each stored procedure is called), and assign the values based on parameter order.
        /// </summary>
        /// <remarks>
        /// This method provides no access to output parameters or the stored procedure's return value parameter.
        /// 
        /// e.g.:  
        ///  int result = ExecuteNonQuery(conn, "PublishOrders", 24, 36);
        /// </remarks>
        /// <param name="connection">a valid SQLiteConnection</param>
        /// <param name="spName">the name of the stored procedure</param>
        /// <param name="parameterValues">an array of objects to be assigned as the input values of the stored procedure</param>
        /// <returns>an int representing the number of rows affected by the command</returns>
        public static int ExecuteNonQuery(SQLiteConnection connection, string spName, params object[] parameterValues)
        {
            //if we got parameter values, we need to figure out where they go
            if ((parameterValues != null) && (parameterValues.Length > 0))
            {
                //pull the parameters for this stored procedure from the parameter cache (or discover them & populate the cache)
                SQLiteParameter[] commandParameters = SQLiteHelperParameterCache.GetSpParameterSet(connection.ConnectionString, spName);

                //assign the provided values to these parameters based on parameter order
                AssignParameterValues(commandParameters, parameterValues);

                //call the overload that takes an array of SQLiteParameters
                return ExecuteNonQuery(connection, CommandType.StoredProcedure, spName, commandParameters);
            }
            //otherwise we can just call the SP without params
            else
            {
                return ExecuteNonQuery(connection, CommandType.StoredProcedure, spName);
            }
        }

        /// <summary>
        /// Execute an SQLiteCommand (that returns no resultset and takes no parameters) against the provided SQLiteTransaction. 
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  int result = ExecuteNonQuery(trans, CommandType.StoredProcedure, "PublishOrders");
        /// </remarks>
        /// <param name="transaction">a valid SQLiteTransaction</param>
        /// <param name="commandType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">the stored procedure name or PL/SQL command</param>  
        /// <returns>an int representing the number of rows affected by the command</returns>
        public static int ExecuteNonQuery(SQLiteTransaction transaction, CommandType commandType, string commandText)
        {
            //pass through the call providing null for the set of SQLiteParameters
            return ExecuteNonQuery(transaction, commandType, commandText, (SQLiteParameter[])null);
        }

        /// <summary>
        /// Execute an SQLiteCommand (that returns no resultset) against the specified SQLiteTransaction
        /// using the provided parameters.
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  int result = ExecuteNonQuery(trans, CommandType.StoredProcedure, "GetOrders", new SQLiteParameter("@prodid", 24));
        /// </remarks>
        /// <param name="transaction">a valid SQLiteTransaction</param>
        /// <param name="commandType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">the stored procedure name or PL/SQL command</param>  
        /// <param name="commandParameters">an array of SQLiteParameters used to execute the command</param>
        /// <returns>an int representing the number of rows affected by the command</returns>
        public static int ExecuteNonQuery(SQLiteTransaction transaction, CommandType commandType, string commandText, params SQLiteParameter[] commandParameters)
        {
            //create a command and prepare it for execution
            SQLiteCommand cmd = new SQLiteCommand();
            PrepareCommand(cmd, transaction.Connection, transaction, commandType, commandText, commandParameters);

            //finally, execute the command.
            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Execute a stored procedure via an SQLiteCommand (that returns no resultset) against the specified 
        /// SQLiteTransaction using the provided parameter values.  This method will query the database to discover the parameters for the 
        /// stored procedure (the first time each stored procedure is called), and assign the values based on parameter order.
        /// </summary>
        /// <remarks>
        /// This method provides no access to output parameters or the stored procedure's return value parameter.
        /// 
        /// e.g.:  
        ///  int result = ExecuteNonQuery(conn, trans, "PublishOrders", 24, 36);
        /// </remarks>
        /// <param name="transaction">a valid SQLiteTransaction</param>
        /// <param name="spName">the name of the stored procedure</param>
        /// <param name="parameterValues">an array of objects to be assigned as the input values of the stored procedure</param>
        /// <returns>an int representing the number of rows affected by the command</returns>
        public static int ExecuteNonQuery(SQLiteTransaction transaction, string spName, params object[] parameterValues)
        {
            //if we got parameter values, we need to figure out where they go
            if ((parameterValues != null) && (parameterValues.Length > 0))
            {
                //pull the parameters for this stored procedure from the parameter cache (or discover them & populet the cache)
                SQLiteParameter[] commandParameters = SQLiteHelperParameterCache.GetSpParameterSet(transaction.Connection.ConnectionString, spName);

                //assign the provided values to these parameters based on parameter order
                AssignParameterValues(commandParameters, parameterValues);

                //call the overload that takes an array of SQLiteParameters
                return ExecuteNonQuery(transaction, CommandType.StoredProcedure, spName, commandParameters);
            }
            //otherwise we can just call the SP without params
            else
            {
                return ExecuteNonQuery(transaction, CommandType.StoredProcedure, spName);
            }
        }

        #endregion ExecuteNonQuery

        #region ExecuteDataSet

        /// <summary>
        /// Execute an SQLiteCommand (that returns a resultset and takes no parameters) against the database specified in 
        /// the connection string. 
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  DataSet ds = ExecuteDataset(connString, CommandType.StoredProcedure, "GetOrders");
        /// </remarks>
        /// <param name="connectionString">a valid connection string for an SQLiteConnection</param>
        /// <param name="commandType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">the stored procedure name or PL/SQL command</param>  
        /// <returns>a dataset containing the resultset generated by the command</returns>
        public static DataSet ExecuteDataset(string connectionString, CommandType commandType, string commandText)
        {
            //pass through the call providing null for the set of SQLiteParameters
            return ExecuteDataset(connectionString, commandType, commandText, (SQLiteParameter[])null);
        }
        public static DataSet ExecuteDataset(CommandType commandType, string commandText) { return ExecuteDataset(_connectionString, commandType, commandText); }

        /// <summary>
        /// Execute an SQLiteCommand (that returns a resultset) against the database specified in the connection string 
        /// using the provided parameters.
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  DataSet ds = ExecuteDataset(connString, CommandType.StoredProcedure, "GetOrders", new SQLiteParameter("@prodid", 24));
        /// </remarks>
        /// <param name="connectionString">a valid connection string for an SQLiteConnection</param>
        /// <param name="commandType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">the stored procedure name or PL/SQL command</param> 
        /// <param name="commandParameters">an array of SQLiteParameters used to execute the command</param>
        /// <returns>a dataset containing the resultset generated by the command</returns>
        public static DataSet ExecuteDataset(string connectionString, CommandType commandType, string commandText, params SQLiteParameter[] commandParameters)
        {
            //create & open an SQLiteConnection, and dispose of it after we are done.
            using (SQLiteConnection cn = new SQLiteConnection(connectionString))
            {
                cn.Open();

                //call the overload that takes a connection in place of the connection string
                return ExecuteDataset(cn, commandType, commandText, commandParameters);
            }
        }
        public static DataSet ExecuteDataset(CommandType commandType, string commandText, params SQLiteParameter[] commandParameters) { return ExecuteDataset(_connectionString, commandType, commandText, commandParameters); }

        /// <summary>
        /// Execute a stored procedure via an SQLiteCommand (that returns a resultset) against the database specified in 
        /// the conneciton string using the provided parameter values.  This method will query the database to discover the parameters for the 
        /// stored procedure (the first time each stored procedure is called), and assign the values based on parameter order.
        /// </summary>
        /// <remarks>
        /// This method provides no access to output parameters or the stored procedure's return value parameter.
        /// 
        /// e.g.:  
        ///  DataSet ds = ExecuteDataset(connString, "GetOrders", 24, 36);
        /// </remarks>
        /// <param name="connectionString">a valid connection string for an SQLiteConnection</param>
        /// <param name="spName">the name of the stored procedure</param>
        /// <param name="parameterValues">an array of objects to be assigned as the input values of the stored procedure</param>
        /// <returns>a dataset containing the resultset generated by the command</returns>
        public static DataSet ExecuteDataset(string connectionString, string spName, params object[] parameterValues)
        {
            //if we got parameter values, we need to figure out where they go
            if ((parameterValues != null) && (parameterValues.Length > 0))
            {
                //pull the parameters for this stored procedure from the parameter cache (or discover them & populet the cache)
                SQLiteParameter[] commandParameters = SQLiteHelperParameterCache.GetSpParameterSet(connectionString, spName);

                //assign the provided values to these parameters based on parameter order
                AssignParameterValues(commandParameters, parameterValues);

                //call the overload that takes an array of SQLiteParameters
                return ExecuteDataset(connectionString, CommandType.StoredProcedure, spName, commandParameters);
            }
            //otherwise we can just call the SP without params
            else
            {
                return ExecuteDataset(connectionString, CommandType.StoredProcedure, spName);
            }
        }
        public static DataSet ExecuteDataset(string spName, params object[] parameterValues) { return ExecuteDataset(_connectionString, spName, parameterValues); }

        /// <summary>
        /// Execute an SQLiteCommand (that returns a resultset and takes no parameters) against the provided SQLiteConnection. 
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  DataSet ds = ExecuteDataset(conn, CommandType.StoredProcedure, "GetOrders");
        /// </remarks>
        /// <param name="connection">a valid SQLiteConnection</param>
        /// <param name="commandType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">the stored procedure name or PL/SQL command</param>
        /// <returns>a dataset containing the resultset generated by the command</returns>
        public static DataSet ExecuteDataset(SQLiteConnection connection, CommandType commandType, string commandText)
        {
            //pass through the call providing null for the set of SQLiteParameters
            return ExecuteDataset(connection, commandType, commandText, (SQLiteParameter[])null);
        }

        /// <summary>
        /// Execute an SQLiteCommand (that returns a resultset) against the specified SQLiteConnection 
        /// using the provided parameters.
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  DataSet ds = ExecuteDataset(conn, CommandType.StoredProcedure, "GetOrders", new SQLiteParameter("@prodid", 24));
        /// </remarks>
        /// <param name="connection">a valid SQLiteConnection</param>
        /// <param name="commandType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">the stored procedure name or PL/SQL command</param> 
        /// <param name="commandParameters">an array of SQLiteParameters used to execute the command</param>
        /// <returns>a dataset containing the resultset generated by the command</returns>
        public static DataSet ExecuteDataset(SQLiteConnection connection, CommandType commandType, string commandText, params SQLiteParameter[] commandParameters)
        {
            //create a command and prepare it for execution
            SQLiteCommand cmd = new SQLiteCommand();
            PrepareCommand(cmd, connection, (SQLiteTransaction)null, commandType, commandText, commandParameters);

            //create the DataAdapter & DataSet
            SQLiteDataAdapter da = new SQLiteDataAdapter(cmd);
            DataSet ds = new DataSet();
            ds.Locale = System.Globalization.CultureInfo.InvariantCulture;
            //fill the DataSet using default values for DataTable names, etc.
            da.Fill(ds);

            //return the dataset
            return ds;
        }

        /// <summary>
        /// Execute a stored procedure via an SQLiteCommand (that returns a resultset) against the specified SQLiteConnection 
        /// using the provided parameter values.  This method will query the database to discover the parameters for the 
        /// stored procedure (the first time each stored procedure is called), and assign the values based on parameter order.
        /// </summary>
        /// <remarks>
        /// This method provides no access to output parameters or the stored procedure's return value parameter.
        /// 
        /// e.g.:  
        ///  DataSet ds = ExecuteDataset(conn, "GetOrders", 24, 36);
        /// </remarks>
        /// <param name="connection">a valid SQLiteConnection</param>
        /// <param name="spName">the name of the stored procedure</param>
        /// <param name="parameterValues">an array of objects to be assigned as the input values of the stored procedure</param>
        /// <returns>a dataset containing the resultset generated by the command</returns>
        public static DataSet ExecuteDataset(SQLiteConnection connection, string spName, params object[] parameterValues)
        {
            //if we got parameter values, we need to figure out where they go
            if ((parameterValues != null) && (parameterValues.Length > 0))
            {
                //pull the parameters for this stored procedure from the parameter cache (or discover them & populate the cache)
                SQLiteParameter[] commandParameters = SQLiteHelperParameterCache.GetSpParameterSet(connection.ConnectionString, spName);

                //assign the provided values to these parameters based on parameter order
                AssignParameterValues(commandParameters, parameterValues);

                //call the overload that takes an array of SQLiteParameters
                return ExecuteDataset(connection, CommandType.StoredProcedure, spName, commandParameters);
            }
            //otherwise we can just call the SP without params
            else
            {
                return ExecuteDataset(connection, CommandType.StoredProcedure, spName);
            }
        }

        /// <summary>
        /// Execute an SQLiteCommand (that returns a resultset and takes no parameters) against the provided SQLiteTransaction. 
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  DataSet ds = ExecuteDataset(trans, CommandType.StoredProcedure, "GetOrders");
        /// </remarks>
        /// <param name="transaction">a valid SQLiteTransaction</param>
        /// <param name="commandType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">the stored procedure name or PL/SQL command</param> 
        /// <returns>a dataset containing the resultset generated by the command</returns>
        public static DataSet ExecuteDataset(SQLiteTransaction transaction, CommandType commandType, string commandText)
        {
            //pass through the call providing null for the set of SQLiteParameters
            return ExecuteDataset(transaction, commandType, commandText, (SQLiteParameter[])null);
        }

        /// <summary>
        /// Execute an SQLiteCommand (that returns a resultset) against the specified SQLiteTransaction
        /// using the provided parameters.
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  DataSet ds = ExecuteDataset(trans, CommandType.StoredProcedure, "GetOrders", new SQLiteParameter("@prodid", 24));
        /// </remarks>
        /// <param name="transaction">a valid SQLiteTransaction</param>
        /// <param name="commandType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">the stored procedure name or PL/SQL command</param> 
        /// <param name="commandParameters">an array of SQLiteParameters used to execute the command</param>
        /// <returns>a dataset containing the resultset generated by the command</returns>
        public static DataSet ExecuteDataset(SQLiteTransaction transaction, CommandType commandType, string commandText, params SQLiteParameter[] commandParameters)
        {
            //create a command and prepare it for execution
            SQLiteCommand cmd = new SQLiteCommand();
            PrepareCommand(cmd, transaction.Connection, transaction, commandType, commandText, commandParameters);

            //create the DataAdapter & DataSet
            SQLiteDataAdapter da = new SQLiteDataAdapter(cmd);
            DataSet ds = new DataSet();
            ds.Locale = System.Globalization.CultureInfo.InvariantCulture;
            //fill the DataSet using default values for DataTable names, etc.
            da.Fill(ds);

            //return the dataset
            return ds;
        }

        /// <summary>
        /// Execute a stored procedure via an SQLiteCommand (that returns a resultset) against the specified 
        /// SQLiteTransaction using the provided parameter values.  This method will query the database to discover the parameters for the 
        /// stored procedure (the first time each stored procedure is called), and assign the values based on parameter order.
        /// </summary>
        /// <remarks>
        /// This method provides no access to output parameters or the stored procedure's return value parameter.
        /// 
        /// e.g.:  
        ///  DataSet ds = ExecuteDataset(trans, "GetOrders", 24, 36);
        /// </remarks>
        /// <param name="transaction">a valid SQLiteTransaction</param>
        /// <param name="spName">the name of the stored procedure</param>
        /// <param name="parameterValues">an array of objects to be assigned as the input values of the stored procedure</param>
        /// <returns>a dataset containing the resultset generated by the command</returns>
        public static DataSet ExecuteDataset(SQLiteTransaction transaction, string spName, params object[] parameterValues)
        {
            //if we got parameter values, we need to figure out where they go
            if ((parameterValues != null) && (parameterValues.Length > 0))
            {
                //pull the parameters for this stored procedure from the parameter cache (or discover them & populate the cache)
                SQLiteParameter[] commandParameters = SQLiteHelperParameterCache.GetSpParameterSet(transaction.Connection.ConnectionString, spName);

                //assign the provided values to these parameters based on parameter order
                AssignParameterValues(commandParameters, parameterValues);

                //call the overload that takes an array of SQLiteParameters
                return ExecuteDataset(transaction, CommandType.StoredProcedure, spName, commandParameters);
            }
            //otherwise we can just call the SP without params
            else
            {
                return ExecuteDataset(transaction, CommandType.StoredProcedure, spName);
            }
        }

        #endregion ExecuteDataSet

        #region ExecuteReader

        /// <summary>
        /// this enum is used to indicate weather the connection was provided by the caller, or created by SQLiteHelper, so that
        /// we can set the appropriate CommandBehavior when calling ExecuteReader()
        /// </summary>
        private enum SQLiteConnectionOwnership
        {
            /// <summary>Connection is owned and managed by SQLiteHelper</summary>
            Internal,
            /// <summary>Connection is owned and managed by the caller</summary>
            External
        }

        /// <summary>
        /// Create and prepare an SQLiteCommand, and call ExecuteReader with the appropriate CommandBehavior.
        /// </summary>
        /// <remarks>
        /// If we created and opened the connection, we want the connection to be closed when the DataReader is closed.
        /// 
        /// If the caller provided the connection, we want to leave it to them to manage.
        /// </remarks>
        /// <param name="connection">a valid SQLiteConnection, on which to execute this command</param>
        /// <param name="transaction">a valid SQLiteTransaction, or 'null'</param>
        /// <param name="commandType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">the stored procedure name or PL/SQL command</param> 
        /// <param name="commandParameters">an array of SQLiteParameters to be associated with the command or 'null' if no parameters are required</param>
        /// <param name="connectionOwnership">indicates whether the connection parameter was provided by the caller, or created by SQLiteHelper</param>
        /// <returns>SQLiteDataReader containing the results of the command</returns>
        private static SQLiteDataReader ExecuteReader(SQLiteConnection connection, SQLiteTransaction transaction, CommandType commandType, string commandText, SQLiteParameter[] commandParameters, SQLiteConnectionOwnership connectionOwnership)
        {
            //create a command and prepare it for execution
            SQLiteCommand cmd = new SQLiteCommand();
            PrepareCommand(cmd, connection, transaction, commandType, commandText, commandParameters);

            //create a reader
            SQLiteDataReader dr;

            // call ExecuteReader with the appropriate CommandBehavior
            if (connectionOwnership == SQLiteConnectionOwnership.External)
            {
                dr = cmd.ExecuteReader();
            }
            else
            {
                dr = cmd.ExecuteReader((CommandBehavior)((int)CommandBehavior.CloseConnection));
            }

            return (SQLiteDataReader)dr;
        }

        /// <summary>
        /// Execute an SQLiteCommand (that returns a resultset and takes no parameters) against the database specified in 
        /// the connection string. 
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  SQLiteDataReader dr = ExecuteReader(connString, CommandType.StoredProcedure, "GetOrders");
        /// </remarks>
        /// <param name="connectionString">a valid connection string for an SQLiteConnection</param>
        /// <param name="commandType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">the stored procedure name or PL/SQL command</param>  
        /// <returns>an SQLiteDataReader containing the resultset generated by the command</returns>
        public static SQLiteDataReader ExecuteReader(string connectionString, CommandType commandType, string commandText)
        {
            //pass through the call providing null for the set of SQLiteParameters
            return ExecuteReader(connectionString, commandType, commandText, (SQLiteParameter[])null);
        }
        public static SQLiteDataReader ExecuteReader(CommandType commandType, string commandText) { return ExecuteReader(_connectionString, commandType, commandText); }

        /// <summary>
        /// Execute an SQLiteCommand (that returns a resultset) against the database specified in the connection string 
        /// using the provided parameters.
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  SQLiteDataReader dr = ExecuteReader(connString, CommandType.StoredProcedure, "GetOrders", new SQLiteParameter("@prodid", 24));
        /// </remarks>
        /// <param name="connectionString">a valid connection string for an SQLiteConnection</param>
        /// <param name="commandType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">the stored procedure name or PL/SQL command</param>  
        /// <param name="commandParameters">an array of SQLiteParameters used to execute the command</param>
        /// <returns>an SQLiteDataReader containing the resultset generated by the command</returns>
        public static SQLiteDataReader ExecuteReader(string connectionString, CommandType commandType, string commandText, params SQLiteParameter[] commandParameters)
        {
            //create & open an SQLitebConnection
            SQLiteConnection cn = new SQLiteConnection(connectionString);
            cn.Open();

            try
            {
                //call the private overload that takes an internally owned connection in place of the connection string
                return ExecuteReader(cn, null, commandType, commandText, commandParameters, SQLiteConnectionOwnership.Internal);
            }
            catch
            {
                //if we fail to return the SQLiteDataReader, we need to close the connection ourselves
                cn.Close();
                throw;
            }
        }
        public static SQLiteDataReader ExecuteReader(CommandType commandType, string commandText, params SQLiteParameter[] commandParameters) { return ExecuteReader(_connectionString, commandType, commandText, commandParameters); }

        /// <summary>
        /// Execute a stored procedure via an SQLiteCommand (that returns a resultset) against the database specified in 
        /// the connection string using the provided parameter values.  This method will query the database to discover the parameters for the 
        /// stored procedure (the first time each stored procedure is called), and assign the values based on parameter order.
        /// </summary>
        /// <remarks>
        /// This method provides no access to output parameters or the stored procedure's return value parameter.
        /// 
        /// e.g.:  
        ///  SQLiteDataReader dr = ExecuteReader(connString, "GetOrders", 24, 36);
        /// </remarks>
        /// <param name="connectionString">a valid connection string for an SQLiteConnection</param>
        /// <param name="spName">the name of the stored procedure</param>
        /// <param name="parameterValues">an array of objects to be assigned as the input values of the stored procedure</param>
        /// <returns>an SQLiteDataReader containing the resultset generated by the command</returns>
        public static SQLiteDataReader ExecuteReader(string connectionString, string spName, params object[] parameterValues)
        {
            //if we got parameter values, we need to figure out where they go
            if ((parameterValues != null) && (parameterValues.Length > 0))
            {
                //pull the parameters for this stored procedure from the parameter cache (or discover them & populate the cache)
                SQLiteParameter[] commandParameters = SQLiteHelperParameterCache.GetSpParameterSet(connectionString, spName);

                //assign the provided values to these parameters based on parameter order
                AssignParameterValues(commandParameters, parameterValues);

                //call the overload that takes an array of SQLiteParameters
                return ExecuteReader(connectionString, CommandType.StoredProcedure, spName, commandParameters);
            }
            //otherwise we can just call the SP without params
            else
            {
                return ExecuteReader(connectionString, CommandType.StoredProcedure, spName);
            }
        }
        public static SQLiteDataReader ExecuteReader(string spName, params object[] parameterValues) { return ExecuteReader(_connectionString, spName, parameterValues); }

        /// <summary>
        /// Execute an SQLiteCommand (that returns a resultset and takes no parameters) against the provided SQLiteConnection. 
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  SQLiteDataReader dr = ExecuteReader(conn, CommandType.StoredProcedure, "GetOrders");
        /// </remarks>
        /// <param name="connection">a valid SQLiteConnection</param>
        /// <param name="commandType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">the stored procedure name or PL/SQL command</param>
        /// <returns>an SQLiteDataReader containing the resultset generated by the command</returns>
        public static SQLiteDataReader ExecuteReader(SQLiteConnection connection, CommandType commandType, string commandText)
        {
            //pass through the call providing null for the set of SQLiteParameters
            return ExecuteReader(connection, commandType, commandText, (SQLiteParameter[])null);
        }

        /// <summary>
        /// Execute an SQLiteCommand (that returns a resultset) against the specified SQLiteConnection 
        /// using the provided parameters.
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  SQLiteDataReader dr = ExecuteReader(conn, CommandType.StoredProcedure, "GetOrders", new SQLiteParameter("@prodid", 24));
        /// </remarks>
        /// <param name="connection">a valid SQLiteConnection</param>
        /// <param name="commandType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">the stored procedure name or PL/SQL command</param>  
        /// <param name="commandParameters">an array of SQLiteParameters used to execute the command</param>
        /// <returns>an SQLiteDataReader containing the resultset generated by the command</returns>
        public static SQLiteDataReader ExecuteReader(SQLiteConnection connection, CommandType commandType, string commandText, params SQLiteParameter[] commandParameters)
        {
            //pass through the call to the private overload using a null transaction value and an externally owned connection
            return ExecuteReader(connection, (SQLiteTransaction)null, commandType, commandText, commandParameters, SQLiteConnectionOwnership.External);
        }

        /// <summary>
        /// Execute a stored procedure via an SQLiteCommand (that returns a resultset) against the specified SQLiteConnection 
        /// using the provided parameter values.  This method will query the database to discover the parameters for the 
        /// stored procedure (the first time each stored procedure is called), and assign the values based on parameter order.
        /// </summary>
        /// <remarks>
        /// This method provides no access to output parameters or the stored procedure's return value parameter.
        /// 
        /// e.g.:  
        ///  SQLiteDataReader dr = ExecuteReader(conn, "GetOrders", 24, 36);
        /// </remarks>
        /// <param name="connection">a valid SQLiteConnection</param>
        /// <param name="spName">the name of the stored procedure</param>
        /// <param name="parameterValues">an array of objects to be assigned as the input values of the stored procedure</param>
        /// <returns>an SQLiteDataReader containing the resultset generated by the command</returns>
        public static SQLiteDataReader ExecuteReader(SQLiteConnection connection, string spName, params object[] parameterValues)
        {
            //if we got parameter values, we need to figure out where they go
            if ((parameterValues != null) && (parameterValues.Length > 0))
            {
                SQLiteParameter[] commandParameters = SQLiteHelperParameterCache.GetSpParameterSet(connection.ConnectionString, spName);

                AssignParameterValues(commandParameters, parameterValues);

                return ExecuteReader(connection, CommandType.StoredProcedure, spName, commandParameters);
            }
            //otherwise we can just call the SP without params
            else
            {
                return ExecuteReader(connection, CommandType.StoredProcedure, spName);
            }
        }

        /// <summary>
        /// Execute an SQLiteCommand (that returns a resultset and takes no parameters) against the provided SQLiteTransaction. 
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  SQLiteDataReader dr = ExecuteReader(trans, CommandType.StoredProcedure, "GetOrders");
        /// </remarks>
        /// <param name="transaction">a valid SQLiteTransaction</param>
        /// <param name="commandType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">the stored procedure name or PL/SQL command</param>  
        /// <returns>an SQLiteDataReader containing the resultset generated by the command</returns>
        public static SQLiteDataReader ExecuteReader(SQLiteTransaction transaction, CommandType commandType, string commandText)
        {
            //pass through the call providing null for the set of SQLiteParameters
            return ExecuteReader(transaction, commandType, commandText, (SQLiteParameter[])null);
        }

        /// <summary>
        /// Execute an SQLiteCommand (that returns a resultset) against the specified SQLiteTransaction
        /// using the provided parameters.
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///   SQLiteDataReader dr = ExecuteReader(trans, CommandType.StoredProcedure, "GetOrders", new SQLiteParameter("@prodid", 24));
        /// </remarks>
        /// <param name="transaction">a valid SQLiteTransaction</param>
        /// <param name="commandType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">the stored procedure name or PL/SQL command</param> 
        /// <param name="commandParameters">an array of SQLiteParameters used to execute the command</param>
        /// <returns>an SQLiteDataReader containing the resultset generated by the command</returns>
        public static SQLiteDataReader ExecuteReader(SQLiteTransaction transaction, CommandType commandType, string commandText, params SQLiteParameter[] commandParameters)
        {
            //pass through to private overload, indicating that the connection is owned by the caller
            return ExecuteReader(transaction.Connection, transaction, commandType, commandText, commandParameters, SQLiteConnectionOwnership.External);
        }

        /// <summary>
        /// Execute a stored procedure via an SQLiteCommand (that returns a resultset) against the specified
        /// SQLiteTransaction using the provided parameter values.  This method will query the database to discover the parameters for the 
        /// stored procedure (the first time each stored procedure is called), and assign the values based on parameter order.
        /// </summary>
        /// <remarks>
        /// This method provides no access to output parameters or the stored procedure's return value parameter.
        /// 
        /// e.g.:  
        ///  SQLiteDataReader dr = ExecuteReader(trans, "GetOrders", 24, 36);
        /// </remarks>
        /// <param name="transaction">a valid SQLiteTransaction</param>
        /// <param name="spName">the name of the stored procedure</param>
        /// <param name="parameterValues">an array of objects to be assigned as the input values of the stored procedure</param>
        /// <returns>an SQLiteDataReader containing the resultset generated by the command</returns>
        public static SQLiteDataReader ExecuteReader(SQLiteTransaction transaction, string spName, params object[] parameterValues)
        {
            //if we got parameter values, we need to figure out where they go
            if ((parameterValues != null) && (parameterValues.Length > 0))
            {
                SQLiteParameter[] commandParameters = SQLiteHelperParameterCache.GetSpParameterSet(transaction.Connection.ConnectionString, spName);

                AssignParameterValues(commandParameters, parameterValues);

                return ExecuteReader(transaction, CommandType.StoredProcedure, spName, commandParameters);
            }
            //otherwise we can just call the SP without params
            else
            {
                return ExecuteReader(transaction, CommandType.StoredProcedure, spName);
            }
        }

        #endregion ExecuteReader

        #region ExecuteScalar

        /// <summary>
        /// Execute an SQLiteCommand (that returns a 1x1 resultset and takes no parameters) against the database specified in 
        /// the connection string. 
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  int orderCount = (int)ExecuteScalar(connString, CommandType.StoredProcedure, "GetOrderCount");
        /// </remarks>
        /// <param name="connectionString">a valid connection string for an SQLiteConnection</param>
        /// <param name="commandType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">the stored procedure name or T-SQLite command</param>
        /// <returns>an object containing the value in the 1x1 resultset generated by the command</returns>
        public static object ExecuteScalar(string connectionString, CommandType commandType, string commandText)
        {
            //pass through the call providing null for the set of SQLiteParameters
            return ExecuteScalar(connectionString, commandType, commandText, (SQLiteParameter[])null);
        }
        public static object ExecuteScalar(CommandType commandType, string commandText) { return ExecuteScalar(_connectionString, commandType, commandText); }

        /// <summary>
        /// Execute an SQLiteCommand (that returns a 1x1 resultset) against the database specified in the connection string 
        /// using the provided parameters.
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  int orderCount = (int)ExecuteScalar(connString, CommandType.StoredProcedure, "GetOrderCount", new SQLiteParameter("@prodid", 24));
        /// </remarks>
        /// <param name="connectionString">a valid connection string for an SQLiteConnection</param>
        /// <param name="commandType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">the stored procedure name or T-SQLite command</param>
        /// <param name="commandParameters">an array of SQLiteParameters used to execute the command</param>
        /// <returns>an object containing the value in the 1x1 resultset generated by the command</returns>
        public static object ExecuteScalar(string connectionString, CommandType commandType, string commandText, params SQLiteParameter[] commandParameters)
        {
            //create & open an SQLiteConnection, and dispose of it after we are done.
            using (SQLiteConnection cn = new SQLiteConnection(connectionString))
            {
                cn.Open();

                //call the overload that takes a connection in place of the connection string
                return ExecuteScalar(cn, commandType, commandText, commandParameters);
            }
        }
        public static object ExecuteScalar(CommandType commandType, string commandText, params SQLiteParameter[] commandParameters) { return ExecuteScalar(_connectionString, commandType, commandText, commandParameters); }

        /// <summary>
        /// Execute a stored procedure via an SQLiteCommand (that returns a 1x1 resultset) against the database specified in 
        /// the conneciton string using the provided parameter values.  This method will query the database to discover the parameters for the 
        /// stored procedure (the first time each stored procedure is called), and assign the values based on parameter order.
        /// </summary>
        /// <remarks>
        /// This method provides no access to output parameters or the stored procedure's return value parameter.
        /// 
        /// e.g.:  
        ///  int orderCount = (int)ExecuteScalar(connString, "GetOrderCount", 24, 36);
        /// </remarks>
        /// <param name="connectionString">a valid connection string for an SQLiteConnection</param>
        /// <param name="spName">the name of the stored procedure</param>
        /// <param name="parameterValues">an array of objects to be assigned as the input values of the stored procedure</param>
        /// <returns>an object containing the value in the 1x1 resultset generated by the command</returns>
        public static object ExecuteScalar(string connectionString, string spName, params object[] parameterValues)
        {
            //if we got parameter values, we need to figure out where they go
            if ((parameterValues != null) && (parameterValues.Length > 0))
            {
                //pull the parameters for this stored procedure from the parameter cache (or discover them & populet the cache)
                SQLiteParameter[] commandParameters = SQLiteHelperParameterCache.GetSpParameterSet(connectionString, spName);

                //assign the provided values to these parameters based on parameter order
                AssignParameterValues(commandParameters, parameterValues);

                //call the overload that takes an array of SQLiteParameters
                return ExecuteScalar(connectionString, CommandType.StoredProcedure, spName, commandParameters);
            }
            //otherwise we can just call the SP without params
            else
            {
                return ExecuteScalar(connectionString, CommandType.StoredProcedure, spName);
            }
        }
        public static object ExecuteScalar(string spName, params object[] parameterValues) { return ExecuteScalar(_connectionString, spName, parameterValues); }

        /// <summary>
        /// Execute an SQLiteCommand (that returns a 1x1 resultset and takes no parameters) against the provided SQLiteConnection. 
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  int orderCount = (int)ExecuteScalar(conn, CommandType.StoredProcedure, "GetOrderCount");
        /// </remarks>
        /// <param name="connection">a valid SQLiteConnection</param>
        /// <param name="commandType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">the stored procedure name or T-SQLite command</param>
        /// <returns>an object containing the value in the 1x1 resultset generated by the command</returns>
        public static object ExecuteScalar(SQLiteConnection connection, CommandType commandType, string commandText)
        {
            //pass through the call providing null for the set of SQLiteParameters
            return ExecuteScalar(connection, commandType, commandText, (SQLiteParameter[])null);
        }

        /// <summary>
        /// Execute an SQLiteCommand (that returns a 1x1 resultset) against the specified SQLiteConnection 
        /// using the provided parameters.
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  int orderCount = (int)ExecuteScalar(conn, CommandType.StoredProcedure, "GetOrderCount", new SQLiteParameter("@prodid", 24));
        /// </remarks>
        /// <param name="connection">a valid SQLiteConnection</param>
        /// <param name="commandType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">the stored procedure name or T-OleDb command</param>
        /// <param name="commandParameters">an array of SQLiteParameters used to execute the command</param>
        /// <returns>an object containing the value in the 1x1 resultset generated by the command</returns>
        public static object ExecuteScalar(SQLiteConnection connection, CommandType commandType, string commandText, params SQLiteParameter[] commandParameters)
        {
            //create a command and prepare it for execution
            SQLiteCommand cmd = new SQLiteCommand();
            PrepareCommand(cmd, connection, (SQLiteTransaction)null, commandType, commandText, commandParameters);

            //execute the command & return the results
            return cmd.ExecuteScalar();
        }

        /// <summary>
        /// Execute a stored procedure via an SQLiteCommand (that returns a 1x1 resultset) against the specified SQLiteConnection 
        /// using the provided parameter values.  This method will query the database to discover the parameters for the 
        /// stored procedure (the first time each stored procedure is called), and assign the values based on parameter order.
        /// </summary>
        /// <remarks>
        /// This method provides no access to output parameters or the stored procedure's return value parameter.
        /// 
        /// e.g.:  
        ///  int orderCount = (int)ExecuteScalar(conn, "GetOrderCount", 24, 36);
        /// </remarks>
        /// <param name="connection">a valid SQLiteConnection</param>
        /// <param name="spName">the name of the stored procedure</param>
        /// <param name="parameterValues">an array of objects to be assigned as the input values of the stored procedure</param>
        /// <returns>an object containing the value in the 1x1 resultset generated by the command</returns>
        public static object ExecuteScalar(SQLiteConnection connection, string spName, params object[] parameterValues)
        {
            //if we got parameter values, we need to figure out where they go
            if ((parameterValues != null) && (parameterValues.Length > 0))
            {
                //pull the parameters for this stored procedure from the parameter cache (or discover them & populet the cache)
                SQLiteParameter[] commandParameters = SQLiteHelperParameterCache.GetSpParameterSet(connection.ConnectionString, spName);

                //assign the provided values to these parameters based on parameter order
                AssignParameterValues(commandParameters, parameterValues);

                //call the overload that takes an array of SQLiteParameters
                return ExecuteScalar(connection, CommandType.StoredProcedure, spName, commandParameters);
            }
            //otherwise we can just call the SP without params
            else
            {
                return ExecuteScalar(connection, CommandType.StoredProcedure, spName);
            }
        }

        /// <summary>
        /// Execute an SQLiteCommand (that returns a 1x1 resultset and takes no parameters) against the provided SQLiteTransaction. 
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  int orderCount = (int)ExecuteScalar(trans, CommandType.StoredProcedure, "GetOrderCount");
        /// </remarks>
        /// <param name="transaction">a valid SQLiteTransaction</param>
        /// <param name="commandType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">the stored procedure name or T-OleDb command</param>
        /// <returns>an object containing the value in the 1x1 resultset generated by the command</returns>
        public static object ExecuteScalar(SQLiteTransaction transaction, CommandType commandType, string commandText)
        {
            //pass through the call providing null for the set of SQLiteParameters
            return ExecuteScalar(transaction, commandType, commandText, (SQLiteParameter[])null);
        }

        /// <summary>
        /// Execute an SQLiteCommand (that returns a 1x1 resultset) against the specified SQLiteTransaction
        /// using the provided parameters.
        /// </summary>
        /// <remarks>
        /// e.g.:  
        ///  int orderCount = (int)ExecuteScalar(trans, CommandType.StoredProcedure, "GetOrderCount", new SQLiteParameter("@prodid", 24));
        /// </remarks>
        /// <param name="transaction">a valid SQLiteTransaction</param>
        /// <param name="commandType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="commandText">the stored procedure name or T-OleDb command</param>
        /// <param name="commandParameters">an array of SQLiteParameters used to execute the command</param>
        /// <returns>an object containing the value in the 1x1 resultset generated by the command</returns>
        public static object ExecuteScalar(SQLiteTransaction transaction, CommandType commandType, string commandText, params SQLiteParameter[] commandParameters)
        {
            //create a command and prepare it for execution
            SQLiteCommand cmd = new SQLiteCommand();
            PrepareCommand(cmd, transaction.Connection, transaction, commandType, commandText, commandParameters);

            //execute the command & return the results
            return cmd.ExecuteScalar();

        }

        /// <summary>
        /// Execute a stored procedure via an SQLiteCommand (that returns a 1x1 resultset) against the specified
        /// SQLiteTransaction using the provided parameter values.  This method will query the database to discover the parameters for the 
        /// stored procedure (the first time each stored procedure is called), and assign the values based on parameter order.
        /// </summary>
        /// <remarks>
        /// This method provides no access to output parameters or the stored procedure's return value parameter.
        /// 
        /// e.g.:  
        ///  int orderCount = (int)ExecuteScalar(trans, "GetOrderCount", 24, 36);
        /// </remarks>
        /// <param name="transaction">a valid SQLiteTransaction</param>
        /// <param name="spName">the name of the stored procedure</param>
        /// <param name="parameterValues">an array of objects to be assigned as the input values of the stored procedure</param>
        /// <returns>an object containing the value in the 1x1 resultset generated by the command</returns>
        public static object ExecuteScalar(SQLiteTransaction transaction, string spName, params object[] parameterValues)
        {
            //if we got parameter values, we need to figure out where they go
            if ((parameterValues != null) && (parameterValues.Length > 0))
            {
                //pull the parameters for this stored procedure from the parameter cache (or discover them & populet the cache)
                SQLiteParameter[] commandParameters = SQLiteHelperParameterCache.GetSpParameterSet(transaction.Connection.ConnectionString, spName);

                //assign the provided values to these parameters based on parameter order
                AssignParameterValues(commandParameters, parameterValues);

                //call the overload that takes an array of SQLiteParameters
                return ExecuteScalar(transaction, CommandType.StoredProcedure, spName, commandParameters);
            }
            //otherwise we can just call the SP without params
            else
            {
                return ExecuteScalar(transaction, CommandType.StoredProcedure, spName);
            }
        }

        #endregion ExecuteScalar
    }

    /// <summary>
    /// SQLiteHelperParameterCache provides functions to leverage a static cache of procedure parameters, and the
    /// ability to discover parameters for stored procedures at run-time.
    /// </summary>
    public sealed class SQLiteHelperParameterCache
    {
        #region save connect string
        private static string _connectionString;
        public static string SetConnectionString { set { _connectionString = value; } }
        #endregion

        #region private methods, variables, and constructors

        //Since this class provides only static methods, make the default constructor private to prevent 
        //instances from being created with "new SQLiteHelperParameterCache()".
        private SQLiteHelperParameterCache() { }

        private static Hashtable paramCache = Hashtable.Synchronized(new Hashtable());

        /// <summary>
        /// resolve at run-time the appropriate set of SQLiteParameters for a stored procedure
        /// </summary>
        /// <param name="connectionString">a valid connection string for an SQLiteConnection</param>
        /// <param name="spName">the name of the stored procedure</param>
        /// <param name="includeReturnValueParameter">whether or not to include ther return value parameter</param>
        /// <returns></returns>
        private static SQLiteParameter[] DiscoverSpParameterSet(string connectionString, string spName, bool includeReturnValueParameter)
        {
            using (SQLiteConnection cn = new SQLiteConnection(connectionString))
            using (SQLiteCommand cmd = new SQLiteCommand(spName, cn))
            {
                cn.Open();
                cmd.CommandType = CommandType.StoredProcedure;
                //SQLiteCommandBuilder.DeriveParameters(cmd);
                if (!includeReturnValueParameter)
                {
                    if (ParameterDirection.ReturnValue == cmd.Parameters[0].Direction)
                        cmd.Parameters.RemoveAt(0);
                }
                SQLiteParameter[] discoveredParameters = new SQLiteParameter[cmd.Parameters.Count];
                cmd.Parameters.CopyTo(discoveredParameters, 0);
                return discoveredParameters;
            }
        }
        private static SQLiteParameter[] DiscoverSpParameterSet(string spName, bool includeReturnValueParameter) { return DiscoverSpParameterSet(_connectionString, spName, includeReturnValueParameter); }

        //deep copy of cached SQLiteParameter array
        private static SQLiteParameter[] CloneParameters(SQLiteParameter[] originalParameters)
        {
            SQLiteParameter[] clonedParameters = new SQLiteParameter[originalParameters.Length];
            for (int i = 0, j = originalParameters.Length; i < j; i++)
            {
                clonedParameters[i] = (SQLiteParameter)((ICloneable)originalParameters[i]).Clone();
            }
            return clonedParameters;
        }

        #endregion private methods, variables, and constructors

        #region caching functions

        /// <summary>
        /// add parameter array to the cache
        /// </summary>
        /// <param name="connectionString">a valid connection string for an SQLiteConnection</param>
        /// <param name="commandText">the stored procedure name or T-OleDb command</param>
        /// <param name="commandParameters">an array of SQLiteParameters to be cached</param>
        public static void CacheParameterSet(string connectionString, string commandText, params SQLiteParameter[] commandParameters)
        {
            string hashKey = connectionString + ":" + commandText;
            paramCache[hashKey] = commandParameters;
        }
        public static void CacheParameterSet(string commandText, params SQLiteParameter[] commandParameters) { CacheParameterSet(_connectionString, commandText, commandParameters); }

        /// <summary>
        /// retrieve a parameter array from the cache
        /// </summary>
        /// <param name="connectionString">a valid connection string for an SQLiteConnection</param>
        /// <param name="commandText">the stored procedure name or T-OleDb command</param>
        /// <returns>an array of SQLiteParameters</returns>
        public static SQLiteParameter[] GetCachedParameterSet(string connectionString, string commandText)
        {
            string hashKey = connectionString + ":" + commandText;
            SQLiteParameter[] cachedParameters = (SQLiteParameter[])paramCache[hashKey];

            if (cachedParameters == null)
            {
                return null;
            }
            else
            {
                return CloneParameters(cachedParameters);
            }
        }
        public static SQLiteParameter[] GetCachedParameterSet(string commandText) { return GetCachedParameterSet(_connectionString, commandText); }

        #endregion caching functions

        #region Parameter Discovery Functions

        /// <summary>
        /// Retrieves the set of SQLiteParameters appropriate for the stored procedure
        /// </summary>
        /// <remarks>
        /// This method will query the database for this information, and then store it in a cache for future requests.
        /// </remarks>
        /// <param name="connectionString">a valid connection string for an SQLiteConnection</param>
        /// <param name="spName">the name of the stored procedure</param>
        /// <returns>an array of SQLiteParameters</returns>
        public static SQLiteParameter[] GetSpParameterSet(string connectionString, string spName)
        {
            return GetSpParameterSet(connectionString, spName, false);
        }
        public static SQLiteParameter[] GetSpParameterSet(string spName) { return GetSpParameterSet(_connectionString, spName); }

        /// <summary>
        /// Retrieves the set of SQLiteParameters appropriate for the stored procedure
        /// </summary>
        /// <remarks>
        /// This method will query the database for this information, and then store it in a cache for future requests.
        /// </remarks>
        /// <param name="connectionString">a valid connection string for an SQLiteConnection</param>
        /// <param name="spName">the name of the stored procedure</param>
        /// <param name="includeReturnValueParameter">a bool value indicating whether the return value parameter should be included in the results</param>
        /// <returns>an array of SQLiteParameters</returns>
        public static SQLiteParameter[] GetSpParameterSet(string connectionString, string spName, bool includeReturnValueParameter)
        {
            string hashKey = connectionString + ":" + spName + (includeReturnValueParameter ? ":include ReturnValue Parameter" : "");

            SQLiteParameter[] cachedParameters;
            cachedParameters = (SQLiteParameter[])paramCache[hashKey];
            if (cachedParameters == null)
            {
                cachedParameters = (SQLiteParameter[])(paramCache[hashKey] = DiscoverSpParameterSet(connectionString, spName, includeReturnValueParameter));
            }
            return CloneParameters(cachedParameters);
        }
        public static SQLiteParameter[] GetSpParameterSet(string spName, bool includeReturnValueParameter) { return GetSpParameterSet(_connectionString, spName, includeReturnValueParameter); }

        #endregion Parameter Discovery Functions
    }
}