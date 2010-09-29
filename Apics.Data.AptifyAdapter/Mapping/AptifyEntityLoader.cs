﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using Apics.Data.AptifyAdapter.Mapping.Metadata;
using Apics.Utilities.Extension;
using log4net;

namespace Apics.Data.AptifyAdapter.Mapping
{
    internal class AptifyEntityLoader
    {
        private static readonly ILog Log = LogManager.GetLogger( typeof( AptifyEntityLoader ) );
        private readonly IDbConnection connection;

        internal AptifyEntityLoader( IDbConnection connection )
        {
            this.connection = connection;
        }

        /// <summary>
        /// Loads the entity metadata for all the entities in the Aptify database
        /// </summary>
        /// <returns>A collection of the metadata from the Aptify entities</returns>
        internal EntityMetadataCollection LoadEntityMetadata( )
        {
            using( this.connection )
            {
                try
                {
                    this.connection.Open( );

                    // Load all the basic entity information
                    EntityMetadataCollection entities = LoadAptifyEntityInformation( this.connection );

                    // Load the underlying table information
                    LoadAptifyTableMetadata( this.connection, entities );

                    Log.DebugFormat( "Successfully loaded {0} entities from Aptify database", entities.Count( ) );

                    return entities;
                }
                catch( DbException ex )
                {
                    Log.Fatal( "Could not load aptify entities", ex );
                    throw;
                }
            }
        }

        private static EntityMetadataCollection LoadAptifyEntityInformation( IDbConnection connection )
        {
            var entities = new EntityMetadataCollection( );
            var parentIDs = new List<int>( );

            using( IDbCommand command = connection.CreateCommand( ) )
            {
                command.CommandText = Queries.GetAllEntities;
                command.CommandType = CommandType.Text;

                using( IDataReader reader = command.ExecuteReader( ) )
                {
                    while( reader.Read( ) )
                    {
                        var entity = new AptifyEntityMetadata(
                            ( int )reader[ "ID" ], ( string )reader[ "Name" ] );

                        entities.Add( entity );
                        parentIDs.Add( reader.GetInt32( 1 ) );
                    }
                }

                MapParents( entities, parentIDs );
            }

            return entities;
        }

        private static void MapParents( EntityMetadataCollection entities, IEnumerable<int> parentIDs )
        {
            // Define parent entities
            foreach( var item in entities.Zip( parentIDs ).Where( item => item.Second > 0 ) )
                item.First.Parent = entities.GetById( item.Second );
        }

        private static void LoadAptifyTableMetadata( IDbConnection connection, EntityMetadataCollection entities )
        {
            using( IDbCommand command = connection.CreateCommand( ) )
            {
                command.CommandText = Queries.GetAllColumns;
                command.CommandType = CommandType.Text;

                using( IDataReader reader = command.ExecuteReader( ) )
                {
                    while( reader.Read( ) )
                    {
                        // Load the required fields to simplify the latter code
                        var entityId = ( int )reader[ "EntityID" ];
                        AptifyEntityMetadata parent = entities.GetByName( reader[ "LinkedEntity" ] as string );
                        var tableName = ( string )reader[ "BaseTable" ];
                        var columnName = ( string )reader[ "Name" ];
                        var nullable = ( bool )reader[ "SQLAllowNull" ];

                        AptifyEntityMetadata entity = entities.GetById( entityId );

                        if( entity == null )
                            throw new InvalidOperationException( "No entity found with ID " + entityId );

                        AptifyTableMetadata table = entity.Tables.FirstOrDefault( t => t.Name == tableName );

                        // This is the first time the table was found
                        if( table == null )
                        {
                            // Add the table to the entity
                            table = new AptifyTableMetadata( entity, tableName );
                            entity.AddTable( table );
                        }

                        var column = new AptifyColumnMetadata( columnName, nullable, parent );

                        table.AddColumn( column );
                    }
                }
            }
        }
    }
}