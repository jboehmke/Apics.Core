﻿using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate.Event;

namespace Apics.Data.AptifyAdapter.Event
{
    internal class NHibernateUpdateEvent : NHibernateBaseEvent, ISaveOrUpdateEventListener
    {
        public NHibernateUpdateEvent( AptifyServer server ) :
            base( server )
        {
        }

        #region ISaveOrUpdateEventListener Members

        public void OnSaveOrUpdate( SaveOrUpdateEvent @event )
        {
            AptifyEntity entity = LoadEntity( @event.Session, @event.Entity );

            entity.SaveOrUpdate( );
        }

        #endregion

        public override void Register( EventListeners eventListeners )
        {
            IEnumerable<ISaveOrUpdateEventListener> listeners =
                eventListeners.UpdateEventListeners.Union( new[ ] { this } );
            eventListeners.UpdateEventListeners = listeners.ToArray( );
        }
    }
}