﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using System.Data;
using ProjectLogging;

namespace ConsoleApp1
{
    /*
     * Created by Cole Lamers 
     * Date: 2021-07-11
     * 
     * == Purpose ==
     * This code is for making a class to interact with the Microsoft.Data.Sqlite nuget package
     * 
     * 
     * TODO: --3-- might need to implement a design pattern for some of my functions so they dynamically perform what is being explicitly called. I'm specifically looking at the get functions that have similar foreach loops
     * TODO: --1-- must create summaries and explaining what can be passed in to each function as an example and the purpose for them
     * TODO: --1-- need to account for SQL injection. do dictionaries. verify for other functions that this is being done as well.
     * TODO: --3-- add in documentation. very important so i can just use this and swipe code as needed and not have to try it out.
     * TODO: --3-- try to make a more consistent xml documentation including <code> tags for variables.
     */
    /// <summary>
    /// Class that simplifies access to the Microsoft.Data.Sqlite nuget package. 
    /// </summary>
    public class Sqlite_Actions
    {
        private DebugLogging _debugLogging = new DebugLogging();
        private SqliteConnection _sqlConnection;
        public string FileName { get; private set; }
        public string ActiveTable { get; private set; }
        public DataTable ActiveTableSchema { get; private set; }
        public DataTable ActiveQueryResults { get; private set; }
        public List<string> DatabaseTableList { get; private set; }

        public delegate void DataFunction(SqliteDataReader sdr);

        
        /// <summary>
        /// Default constructor. Just requires the name to the file. Not the full path or the extension, just the exact name. Get's converted into lower case so casing is not important.
        /// </summary>
        /// <param name="SqlFile">Ex: MyDatabase (will become mydatabase), portabledb, localstore</param>
        public Sqlite_Actions(string SqlFile)
        {
            this.FileName = SqlFile.ToLower();
            this.ActiveTable = "";
            this.ActiveTableSchema = new DataTable();
            this.DatabaseTableList = new List<string>();
            this.ActiveQueryResults = new DataTable();
            this._sqlConnection = new SqliteConnection($"Data Source={this.FileName}.db");
            GetDatabaseTables();
        } // constructor

        /// <summary>
        /// Creates a table asking for what you'd like the table to be named. If the table exists, it returns. If the dictionary is empty, it returns.
        /// </summary>
        /// <param name="tableName">This is the name of the table you can look up. It is enforced to lowercase so any capitalized letters will be made lowercase. Ex: cars, working.partners</param>
        /// <param name="keyIsColumnValueIsType">A dictionary passed in that contains the name of the column you'd like and the type. <code lang="CS">dict.Add("name", "text"); dict.Add("price", "integer");</code></param>
        /// <param name="primaryKey">This is a default value set for the primary key. Set to default named "ID," an Integer value that autoincrements.</param>
        public void CreateTable(string tableName, Dictionary<string, string> keyIsColumnValueIsType, string primaryKey = "ID INTEGER PRIMARY KEY AUTOINCREMENT")
        {
            try
            {
                using (this._sqlConnection)
                {
                    this._sqlConnection.Open();
                    if (keyIsColumnValueIsType == null)
                    {
                        return;
                    } // if

                    tableName = tableName.ToLower(); // tables enforced to lowercase

                    if (tableName.Equals(this.ActiveTable))
                    {
                        return;
                    } // if

                    string query = $"CREATE TABLE IF NOT EXISTS {tableName}({primaryKey}";
                    int i = 0;

                    foreach (KeyValuePair<string, string> kvp in keyIsColumnValueIsType)
                    {
                        string key = kvp.Key.ToUpper();
                        string val = kvp.Value.ToUpper();
                        query += $",{key} {val}";

                        /*
                        //TODO: --3-- don't know why i commented this out? older code?
                        this.ActiveTableSchema += $"{key}";

                        if (keyIsColumnValueIsType.Count > (i + 1))
                        {
                            this.ActiveTableSchema += ",";
                            i++;
                        } // end if; tacks on the last comma

                        */
                    } // for
                    query += ")";
                    var command = _sqlConnection.CreateCommand();
                    command.CommandText = query;
                    command.ExecuteNonQuery();
                } // using; sqlconnection
            } // try
            catch (Exception e)
            {
                this._debugLogging.LogAction($"Exception: {e}");
            } // catch
        } // function CreateTable; names the table and builds additional data

        /// <summary>
        /// Execute a query with a simple string.
        /// </summary>
        /// <param name="transaction">SQL transaction. Ex: SELECT * FROM JTABLE WHERE NCOL = 'WORDS'</param>
        public void ExecuteQuery(string transaction)
        {
            Dictionary<string, string> queryDict = new Dictionary<string, string>();
            queryDict.Add("query", transaction);
            //TODO: --1-- test this with a 'DROP TABLE;' and see if it posts it as a string or it executes the drop table command.
            try
            {
                using (this._sqlConnection)
                {
                    this._sqlConnection.Open();
                    SqliteCommand command = _sqlConnection.CreateCommand();
                    command.CommandText = queryDict["query"];
                    command.ExecuteNonQuery();

                    using (var reader = command.ExecuteReader())
                    {
                        try
                        {
                            for (int i = 0; i < reader.VisibleFieldCount; i++)
                            {
                                this.ActiveQueryResults.Columns.Add(reader.GetName(i));
                            } // for; gets column headers

                            while (reader.Read())
                            {
                                DataRow dRow = this.ActiveQueryResults.NewRow();

                                for (int i = 0; i < reader.VisibleFieldCount; i++)
                                {
                                    dRow[reader.GetName(i)] = reader.GetString(i);
                                } // for; iterate columns
                                this.ActiveQueryResults.Rows.Add(dRow);
                            } // while; iterate rows
                        } // try
                        catch (Exception e)
                        {
                            _debugLogging.LogAction($"Error: {e}");
                        } // catch
                    } // using; ExecuteReader
                } // using; sqlconnection
            } // try
            catch (Exception e)
            {
                _debugLogging.LogAction($"Error: {e}");
            } // catch
        } // function PerformCommand

        /// <summary>
        /// This function allows for executing a query with delegate function. Currently only utilized with getting a table schema so do not use this one unless you explicitly know why.
        /// </summary>
        /// <param name="whichFunction">The defined delegate function. Ex: private void del_ExecuteQuery_GetSchema(SqliteDataReader reader)...</param>
        /// <param name="transaction">SQL transaction. Ex: SELECT * FROM JTABLE WHERE NCOL = 'WORDS'</param>
        private void ExecuteQuery(DataFunction whichFunction, string transaction)
        {
            Dictionary<string, string> queryDict = new Dictionary<string, string>();
            queryDict.Add("query", transaction);
            //TODO: --1-- test this with a 'DROP TABLE;' and see if it posts it as a string or it executes the drop table command.
            using (this._sqlConnection)
            {
                this._sqlConnection.Open();
                SqliteCommand command = _sqlConnection.CreateCommand();
                command.CommandText = transaction;
                command.ExecuteNonQuery();

                using (var reader = command.ExecuteReader())
                {
                    whichFunction(reader);
                } // using; sqlitedatareader
            } // using; sqlconnection
        } // function ExecuteQuery

        /// <summary>
        /// This just returns the active tables in a SQLite database and clears out the variable so that it doesn't affect future transactions.
        /// </summary>
        public void GetDatabaseTables()
        {
            ExecuteQuery("SELECT * FROM sqlite_master WHERE type='table'");

            foreach (DataRow dRow in this.ActiveQueryResults.Rows)
            {
                this.DatabaseTableList.Add(dRow["name"].ToString());
            }
            this.ActiveQueryResults = new DataTable(); // empties the table right away
        } // func GetDatabaseTables

        /// <summary>
        /// Just sets the active table. 
        /// </summary>
        /// <param name="tableName">This is simply a string for the table name. Is enforced to lowercase. Ex: vehicles, Party.Invites.People (will become party.invites.people).</param>
        /// <returns></returns>
        public bool SetActiveTable(string tableName)
        {
            //TODO: --1-- if enforcing lowercase tablenames, need to ensure that all tablenames passed through pass the test
            string lowerTableName = tableName.ToLower();
            bool nameIsInTable = false;

            foreach (string aTable in DatabaseTableList)
            {
                if (aTable.Equals(lowerTableName))
                {
                    nameIsInTable = true;
                    break;
                } // if
            } // foreach tablename

            if (nameIsInTable) { this.ActiveTable = lowerTableName; }
            ExecuteQuery(del_ExecuteQuery_GetSchema, $"PRAGMA table_info('{this.ActiveTable}')");

            return nameIsInTable;
        } // function ChangeActiveTable

        /// <summary>
        /// Currently assumes you know the schema of the table and what specifically goes into it. 
        /// </summary>
        /// <param name="singleItemToAdd">An array of the columns in the schema in the order of which they were added to the table. Ex: Column1 = "Food", Column 2 = "Dessert", Column3 = "Price". Array contains: {"Chicken Platter", "Pudding", "12.95"}</param>
        public void InsertInto(string[] singleItemToAdd)
        {
            try
            {
                if (this.ActiveTableSchema != null)
                {
                    using (this._sqlConnection)
                    {
                        //TODO: --1-- need to verify if not all contents added to the schema incur an error. So if the schema contains 10 columns and I forget 3, will it still work?
                        this._sqlConnection.Open();
                        string query = $"INSERT INTO {this.ActiveTable}({this.ActiveTableSchema}) VALUES(";

                        for (int i = 0; i < singleItemToAdd.Length; i++)
                        {
                            query += "\'" + singleItemToAdd[i] + "\'";

                            if (singleItemToAdd.Length > (i + 1))
                            {
                                query += ",";
                            } // if; tacks on the last comma
                        } // for

                        query += ");";

                        var command = _sqlConnection.CreateCommand();
                        command.CommandText = query;
                        command.ExecuteNonQuery();
                    } // using; sqlconnection
                } // if
            } // try
            catch (Exception e)
            {
                _debugLogging.LogAction("Error:" + e);
            } // catch
        } // function InsertInto

        /// <summary>
        /// Primarily for getting the schema of the database. The reader can be any SqliteDataReader and it's just a generic instantiated reader. This is used as a delegate function
        /// </summary>
        /// <param name="reader">Utilized as such: Ex: using (var reader = command.ExecuteReader())</param>
        private void del_ExecuteQuery_GetSchema(SqliteDataReader reader)
        {
            for (int i = 0; i < reader.VisibleFieldCount; i++)
            {
                this.ActiveTableSchema.Columns.Add(reader.GetName(i));
            } // for; gets column headers

            while (reader.Read())
            {
                DataRow dRow = this.ActiveTableSchema.NewRow();

                for (int i = 0; i < reader.VisibleFieldCount; i++)
                {
                    try
                    {
                        dRow[reader.GetName(i)] = reader.GetString(i);
                    } // try
                    catch (Exception e)
                    {
                        dRow[reader.GetName(i)] = "null";
                        _debugLogging.LogAction($"Error: {e}");
                    } // Catch; for nulls in table since null cannot be in a datatable
                } // for; iterates through columns

                this.ActiveTableSchema.Rows.Add(dRow);
            } // while; iterates through rows
        } // function del_ExecuteQuery_GetSchema

        /*
         * 
         * Was redundant/excessive so I scrapped it

        private void GetTableNames(string transaction)
        {
            try
            {
                using (this._sqlConnection)
                {
                    this._sqlConnection.Open();
                    SqliteCommand command = _sqlConnection.CreateCommand();
                    command.CommandText = transaction;
                    command.ExecuteNonQuery();

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            for (int i = 0; i < reader.VisibleFieldCount; i++)
                            {
                                this.DatabaseTableList.Add(reader.GetString(i));
                            } // for; iterates through columns
                        } // while; iterates through rows
                    } // using ExecuteReader
                } // using sqlconnection
            } // try
            catch (Exception e)
            {
                _debugLogging.LogAction("Error:" + e);
            } // catch
        } // function GetTableNames

        */
    }
}
