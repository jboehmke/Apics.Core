﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Castle.ActiveRecord;
using System.Diagnostics;

namespace Apics.Model.Certification
{
    [ActiveRecord( "APICSCertificationMaintenanceApplicationActivity", Lazy = true )]
    [DebuggerDisplay( "Maintenance Application Activity: {Id}" )]
    public class MaintenanceApplicationActivity
    {
        [PrimaryKey]
        public virtual int Id { get; set; }

        [BelongsTo( "ApplicationID", NotNull = true, Lazy = FetchWhen.OnInvoke )]
        public virtual MaintenanceApplication Application { get; set; }

        [BelongsTo( "CategoryID", NotNull = true, Lazy = FetchWhen.OnInvoke )]
        public virtual MaintenanceCategory Category { get; set; }

        [BelongsTo( "ActivityID", NotNull = true, Lazy = FetchWhen.OnInvoke )]
        public virtual MaintenanceActivity Activity { get; set; }

        [Property( NotNull = true, Length = 2000 )]
        public virtual string Description { get; set; }

    }

}
