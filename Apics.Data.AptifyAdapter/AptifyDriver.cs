﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Apics.Data.AptifyAdapter.ADO;
using NHibernate.Driver;
using Environment = NHibernate.Cfg.Environment;
using Aptify.Framework.DataServices;

namespace Apics.Data.AptifyAdapter
{
    /// <summary>
    /// NHibernate driver to connect through the Aptify API
    /// </summary>
    public class AptifyDriver : DriverBase
    {
        private string connectionString;

        #region [ DriverBase Overrides ]

        public override string NamedPrefix
        {
            get { return "@"; }
        }

        public override bool UseNamedPrefixInParameter
        {
            get { return true; }
        }

        public override bool UseNamedPrefixInSql
        {
            get { return true; }
        }

        public override void Configure( IDictionary<string, string> settings )
        {
            // Pull in the connection string from the settings
            this.connectionString = settings[ Environment.ConnectionString ];
            base.Configure( settings );
        }

        public override IDbCommand CreateCommand( )
        {
            return new AptifyCommand( );
        }

        public override IDbConnection CreateConnection( )
        {
            var builder = new AptifyConnectionStringBuilder( connectionString );
            return new AptifyConnection( new DataAction( builder.Credentials ) );
        }

        #endregion [ DriverBase Overrides ]
    }
}