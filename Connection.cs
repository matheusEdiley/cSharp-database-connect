using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Data.SqlClient;
using System.Reflection;

namespace ConsoleApp1
{
    class Connection
    {

        #region Attr&Props

        private IDbConnection _connection = null;
        private IDbCommand _command = null;
        private IDbTransaction _transaction = null;
        private IDataParameter _param = null;
        private string _catalog = "Project"; //Database name
        private string _dataSource = string.Empty; 
        private string _integratedSecurity = "True";
        private string _multipleActiveResultSets = "True";

    
        protected enum ConnMode
        {
            Dev,
            Deploy,
            Production
        }

        /// <summary>
        /// Transaction prop
        /// </summary>
        private IDbTransaction Transaction
        {
            get
            {
                if (_transaction == null)
                {
                    _transaction = this._connection.BeginTransaction();
                }

                return _transaction;
            }
            set { _transaction = value; }
        }

        /// <summary>
        /// Command prop
        /// </summary>
        private IDbCommand Command
        {
            get
            {
                this.openConnection();

                if (_command == null)
                {
                    //Neste caso somente SQL SERVER
                    _command = new SqlCommand();
                    _command.Connection = this._connection;
                }

                return _command;
            }
            set { _command = value; }
        }

        #endregion

        #region Open/Close Connection

        /// <summary>
        /// Open connection to database
        /// </summary>
        /// <param name="connMode">Mode of connection</param>
        private void openConnection(ConnMode connMode = ConnMode.Dev)
        {
            try
            {
                switch (connMode)
                {
                    case ConnMode.Dev:
                        this._dataSource = @"DESKTOP-NP990SR\SQLEXPRESS";
                        break;
                    case ConnMode.Deploy:
                        this._dataSource = @"";
                        break;
                    case ConnMode.Production:
                        this._dataSource = @"";
                        break;
                }

                string connectionString = $@"data source={this._dataSource};initial catalog={this._catalog};
                integrated security={this._integratedSecurity};MultipleActiveResultSets={this._multipleActiveResultSets};";

                if (this._connection == null || this._connection.State != ConnectionState.Open)
                {
                    this._connection = new SqlConnection(connectionString);
                    _connection.Open();

                }
                else
                {
                    if (_connection.State != ConnectionState.Open)
                        _connection.Open();
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Close connection to database
        /// </summary>
        private void closeConnection()
        {
            if (this._command != null)
            {
                this._command.Dispose();
                this._command = null;
            }

            if (this._connection.State != ConnectionState.Closed)
            {
                this._connection.Close();
                this._connection.Dispose();
            }
        }

        #endregion

        #region Query/Transactions/Procedures

        /// <summary>
        /// Starts a database query
        /// </summary>
        /// <typeparam name="T">DataTable</typeparam>
        /// <param name="query">SQL Query</param>
        /// <returns>DataTable</returns>
        public T runQuery<T>(string query) where T : DataTable
        {
            T table = null;
            try
            {
                table = this.setCommand<T>(CommandType.Text, query);
                table.Load(Command.ExecuteReader());
                return table;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                if (table != null)
                {
                    table.Dispose();
                    table = null;
                }

                this.closeConnection();
            }
        }

        /// <summary>
        /// Starts a database query
        /// </summary>
        /// <typeparam name="T">type object</typeparam>
        /// <param name="query">SQL Query</param>
        /// <param name="bindingFlag">Specifies that reflection</param>
        /// <returns>List of T objects</returns>
        public List<T> runQuery<T>(string query, BindingFlags bindingFlag) where T : class, new()
        {
            try
            {

                DataTable table = runQuery<DataTable>(query);
                List<T> list = new List<T>();

                foreach (var record in table.AsEnumerable())
                {
                    T obj = new T();

                    foreach (var f in obj.GetType().GetFields(bindingFlag | BindingFlags.Instance))
                    {
                        try
                        {
                            var value = !string.IsNullOrEmpty(record[f.Name].ToString()) ? record[f.Name] : null;
                            FieldInfo fieldInfo = obj.GetType().GetField(f.Name, bindingFlag | BindingFlags.Instance);
                            Type u = Nullable.GetUnderlyingType(fieldInfo.FieldType);
                            Type aux = fieldInfo.FieldType;

                            if (u != null)
                            {
                                aux = u;
                            }

                            fieldInfo.SetValue(obj, Convert.ChangeType((object)value, aux),
                                BindingFlags.NonPublic | BindingFlags.Instance, null, null);
                        }
                        catch
                        {
                            continue;
                        }
                    }

                    list.Add(obj);
                }

                return list;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Starts a database transaction
        /// </summary>
        /// <param name="query"> query SQL </param>
        /// <returns> Number of registers affected </returns>
        public int runTransaction(string query)
        {
            try
            {
                this.Command.Transaction = this.Transaction;

                this.setCommand(CommandType.Text, query);
                int action = this.Command.ExecuteNonQuery();

                this.Transaction.Commit();

                return action;

            }
            catch (Exception ex)
            {
                this.Transaction.Rollback();
                return -1;
            }
            finally
            {
                this.closeConnection();
            }
        }

        /// <summary>
        /// Starts a database scalar query
        /// </summary>
        /// <param name="query"> query SQL </param>
        /// <returns> object </returns>
        public object runScalar(string query)
        {
            try
            {
                this.Command.Transaction = this.Transaction;

                this.setCommand(CommandType.Text, query);
                object obj = Command.ExecuteScalar();
                this.Transaction.Commit();

                return obj;

            }
            catch (Exception ex)
            {
                this.Transaction.Rollback();
                throw ex;
            }
            finally
            {
                this.closeConnection();
            }
        }

        /// <summary>
        /// Run a database procedure
        /// </summary>
        /// <typeparam name="T"> DataTable </typeparam>
        /// <param name="nomeProcedure"> Name of Procedure </param>
        /// <param name="tabela"> Tabela que você queira que seja preenchido </param>
        public T ProcedureReader<T>(string name) where T : DataTable
        {
            T table = null;
            try
            {
                table = setCommand<T>(CommandType.StoredProcedure, name);
                table.Load(Command.ExecuteReader());
                return table;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                this.closeConnection();

                if (table != null)
                {
                    table.Dispose();
                    table = null;
                }
            }
        }

        #endregion

        #region SET Command

        /// <summary>
        /// Set type and text of command
        /// </summary>
        /// <param name="type">Type of command</param>
        /// <param name="commandText">Text of query</param>
        private void setCommand(CommandType type, string commandText)
        {
            if (!String.IsNullOrEmpty(commandText))
            {
                this.Command.CommandType = type;
                this.Command.CommandText = commandText;
            }
            else
                throw new Exception("O parâmetro Query ou o nome da Procedure está vazio.");
        }

        /// <summary>
        /// Set type and text of command
        /// </summary>
        /// <typeparam name="T">Generic type</typeparam>
        /// <param name="type">Type of command</param>
        /// <param name="commandText">Text of query</param>
        /// <returns></returns>
        private T setCommand<T>(CommandType type, string commandText) where T : DataTable
        {
            this.setCommand(type, commandText);
            return Activator.CreateInstance<T>();
        }

        #endregion

        #region Parameters

        /// <summary>
        /// Add parameters to the command
        /// </summary>
        /// <param name="name">Name of parameter</param>
        /// <param name="value">Value of parameter</param>
        /// <param name="direction">Specifies the type of a parameter within a query relative to the DataSet</param>
        public void addParams(string name, object value, ParameterDirection direction = ParameterDirection.Input)
        {
            try
            {
                this._param = new SqlParameter();
                this._param.ParameterName = $"@{name}";
                this._param.Value = (object)value ?? DBNull.Value;
                this._param.Direction = direction;
                this.Command.Parameters.Add(this._param);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                this._param = null;
            }
        }

        /// <summary>
        ///  Add obj parameters to the command
        /// </summary>
        /// <typeparam name="T">Type of class that will be added to the parameters</typeparam>
        /// <param name="obj">Object T</param>
        public void addParams<T>(T obj, BindingFlags bindingFlag = BindingFlags.NonPublic) where T : class, new()
        {
            try
            {
                foreach (var i in obj.GetType().GetFields(bindingFlag | BindingFlags.Instance))
                {
                    this.addParams(i.Name, i.GetValue(obj));
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        #endregion
    }
}
