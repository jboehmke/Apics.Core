﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Apics.Data.AptifyAdapter.ADO;
using Apics.Data.AptifyAdapter.Mapping.Metadata;
using Apics.Data.AptifyAdapter.Store;
using Aptify.Framework.BusinessLogic.GenericEntity;
using log4net;
using NHibernate;
using NHibernate.Engine;
using NHibernate.Proxy;
using NHibernate.Type;
using System.Reflection;

namespace Apics.Data.AptifyAdapter
{
    /// <summary>
    /// Wrapper around a single generic entity instance in the aptify API
    /// </summary>
    internal class AptifyEntity
    {
        #region [ Internal Properties ]

        internal AptifyEntity Parent
        {
            get { return this.parent; }
        }

        internal AptifyGenericEntityBase GenericEntity
        {
            get { return this.genericEntity; }
        }

        #endregion [ Internal Properties ]

        #region [ Private Members ]

        private static readonly ILog Log = LogManager.GetLogger( typeof( AptifyEntity ) );

        private readonly IEnumerable<AptifyEntity> children;
        private readonly AptifyGenericEntityBase genericEntity;
        private readonly AptifyEntity parent;
        private readonly AptifyServer server;
        private readonly EntityStore store;
        private readonly AptifyTableMetadata table;

        #endregion [ Private Members ]

        /// <summary>
        /// Creates an instance of a generic entity
        /// </summary>
        /// <param name="server">Aptify server connection</param>
        /// <param name="store">Entity storage</param>
        public AptifyEntity( AptifyServer server, EntityStore store )
            : this( ( AptifyEntity )null, server, store )
        {
        }

        /// <summary>
        /// Creates a child entity
        /// </summary>
        /// <param name="parent">Parent entity</param>
        /// <param name="server">Aptify server connection</param>
        /// <param name="store">Entity storage</param>
        private AptifyEntity( AptifyEntity parent, AptifyServer server, EntityStore store )
        {
            if ( server == null )
                throw new ArgumentNullException( "server" );

            if ( store == null )
                throw new ArgumentNullException( "store" );

            this.parent = parent;
            this.server = server;
            this.store = store;

            // Get the entity that is mapped to this model instance
            this.table = server.Tables.GetTableMetadata( store.EntityObject );

            // Get the aptify generic entity and load its contents
            this.genericEntity = GetGenericEntity( server, parent, store, this.table.Entity );
            UpdateEntityContent( );

            // Load any child items
            if ( store.Persister.HasCascades )
                this.children = LoadChildEntities( ).ToArray( );
        }

        private AptifyEntity( AptifyDataFieldBase field, AptifyServer server, EntityStore store )
        {
            if ( !field.EmbeddedObjectExists )
                throw new InvalidOperationException( field.Name + " is not an embedded object" );

            this.server = server;
            this.store = store;

            // Get the entity that is mapped to this model instance
            this.table = server.Tables.GetTableMetadata( store.EntityObject );

            this.genericEntity = field.EmbeddedObject;
            UpdateEntityContent( );

            // Load any child items
            if ( store.Persister.HasCascades )
                this.children = LoadChildEntities( ).ToArray( );

            //this.store.MarkAsPersisted( 0 );
        }

        /// <summary>
        /// Save or update the aptify entity
        /// </summary>
        public int SaveOrUpdate( )
        {
            try
            {
                int id = this.store.Id;

                // Clean objects don't need to do anything
                if ( this.store.Status != EntityStatus.Clean || this.Parent == null )
                {
                    // Save the entity to the Aptify server
                    id = this.server.SaveEntity( this.genericEntity, this.store.Transaction );

                    // Save the entity
                    this.store.MarkAsPersisted( id );

                    Log.InfoFormat( "Saved {0}[{1}]", this.table.Name, id );
                }

                if ( this.children != null )
                {
                    foreach ( AptifyEntity child in this.children )
                        child.SaveOrUpdate( );
                }

                return id;
            }
            catch ( Exception e )
            {
                Log.ErrorFormat( "Could not save entity {0}: {1}", this.table.Name, e );
                throw;
            }
        }

        /// <summary>
        /// Delete the aptify entity
        /// </summary>
        public void Delete( )
        {
            try
            {
                AptifyGenericEntityBase aptifyEntity = this.server.GetEntity( this.table.Entity, this.store );
                AptifyNHibernateTransaction transaction = this.store.Transaction;

                if ( transaction == null )
                {
                    if ( !aptifyEntity.Delete( ) )
                        throw new DataException( aptifyEntity.LastError );
                }
                else
                {
                    if ( !aptifyEntity.Delete( transaction.TransactionName ) )
                        throw new DataException( aptifyEntity.LastError );
                }

                Log.InfoFormat( "Deleted {0}[{1}]", this.table.Name, aptifyEntity.RecordID );
            }
            catch ( Exception e )
            {
                Log.ErrorFormat( "Could not delete entity {0}: {1}", this.table.Name, e );
                throw;
            }
        }

        #region [ Private Methods ]

        /// <summary>
        /// Loads an aptify entity
        /// </summary>
        private static AptifyGenericEntityBase GetGenericEntity( AptifyServer server, AptifyEntity parent, EntityStore store, 
            AptifyEntityMetadata metadata )
        {
            // Clean entities don't need to be loaded
            if ( store.Status == EntityStatus.Clean && parent != null )
                return null;

            Log.DebugFormat( "Loading entity: '{0}'", metadata.Name );

            if ( parent != null )
                return server.GetEntity( parent, metadata, store );

            return server.GetEntity( metadata, store );
       }

        private void UpdateEntityContent( )
        {
            // Update each dirty value
            foreach ( int dirtyIndex in this.store.DirtyIndices )
            {
                // Get the name of the dirty column
                string columnName = this.store.Persister.PropertyNames[ dirtyIndex ];

                // Set the value of the generic entity to what is stored in the store
                AptifyColumnMetadata column;

                bool doCascade =
                    this.store.Persister.PropertyCascadeStyles[ dirtyIndex ].DoCascade( CascadingAction.SaveUpdate );

                if ( this.table.Columns.TryGetValue( columnName, out column ) )
                    SetColumnValue( this.genericEntity, column, this.store[ dirtyIndex ], doCascade );
            }
        }

        /// <summary>
        /// Loads child entities from modified collections
        /// </summary>
        /// <returns>Entities from modified collections</returns>
        private IEnumerable<AptifyEntity> LoadChildEntities( )
        {
            // Load which properties require cascading
            CascadeStyle[ ] propertyCascades = this.store.Persister.PropertyCascadeStyles;

            for ( int i = 0; i < propertyCascades.Length; ++i )
            {
                IType type = this.store.Persister.PropertyTypes[ i ];
                string propertyName = this.store.Persister.PropertyNames[ i ];
                AptifyColumnMetadata column = null;

                // Make sure that this property requires cascade saves
                if ( !propertyCascades[ i ].DoCascade( CascadingAction.SaveUpdate ) )
                    continue;

                // Ignore embedded entities.  These are saved in the root object
                if ( this.table.Columns.TryGetValue( propertyName, out column ) )
                {
                    if ( column.IsEmbedded )
                        continue;
                }

                // Pull all the child items
                object child = this.store.Persister.GetPropertyValue( this.store.EntityObject, i, EntityMode.Poco );

                if ( IsUninitializedProxy( child ) )
                    continue;

                foreach ( var dirtyChild in LoadDirtyChild( i, type, child ).ToList( ) )
                    yield return dirtyChild;
            }
        }

        private IEnumerable<AptifyEntity> LoadDirtyChild( int index, IType type, object child )
        {
            if ( type.IsCollectionType )
            {
                // HACK: Children need loading, but not updating through NHibernate
                var children = GetChildrenFromCollection( ( IEnumerable )child ).ToList( );
                yield break;
            }

            // This child is not dirty
            if ( !this.store.DirtyIndices.Contains( index ) )
                yield break;

            /// Find the persister
            var childStore = this.store.CreateChild( child );

            // Check that the store is clean
            if ( childStore.Status == EntityStatus.Clean )
                yield break;

            yield return new AptifyEntity( this, this.server, childStore );
        }

        /// <summary>
        /// Gets the children from a collection as AptifyEntities
        /// </summary>
        /// <param name="items"></param>
        /// <param name="session"></param>
        /// <returns></returns>
        private IEnumerable<AptifyEntity> GetChildrenFromCollection( IEnumerable items )
        {
            return from object item in items
                   where !IsUninitializedProxy( item )
                   select this.store.CreateChild( item ) into childStore
                   select new AptifyEntity( this, this.server, childStore );
        }

        /// <summary>
        /// Updates the column
        /// </summary>
        /// <param name="genericEntityBase">Entity to update</param>
        /// <param name="column">Column description</param>
        /// <param name="state">Column state</param>
        /// <param name="doCascade">Should this column be persisted if it's a foreign key column</param>
        private void SetColumnValue( AptifyGenericEntityBase genericEntityBase, AptifyColumnMetadata column,
            object state, bool doCascade )
        {
            if ( genericEntityBase == null )
                throw new ArgumentNullException( "genericEntityBase" );

            if ( column == null )
                throw new ArgumentNullException( "column" );

            if ( column.IsForeignKeyColumn && state != null || state is INHibernateProxy )
            {
                if ( doCascade )
                {
                    var cascadeStore = new EntityStore( this.store.Session, state );
                    AptifyEntity cascadeEntity;

                    if ( column.IsEmbedded )
                    {
                        cascadeEntity =
                            new AptifyEntity( this.GenericEntity.Fields[ column.Name ], this.server, cascadeStore );
                    }
                    else
                    {
                        cascadeEntity = new AptifyEntity( this.server, cascadeStore );
                    }

                    state = cascadeEntity.SaveOrUpdate( );
                }
                else
                {
                    state = this.store.Session.GetIdentifier( state );
                }
            }

            genericEntityBase.SetValue( column.Name, state );
        }

        /// <summary>
        /// Check to see if an object is a proxy
        /// </summary>
        /// <param name="entity">Entity to check</param>
        /// <returns>True if the entity is a proxy and uninitialized</returns>
        private static bool IsUninitializedProxy( object entity )
        {
            var proxy = entity as INHibernateProxy;

            return ( proxy != null && proxy.HibernateLazyInitializer.IsUninitialized );

        }

        #endregion [ Private Methods ]
    }
}