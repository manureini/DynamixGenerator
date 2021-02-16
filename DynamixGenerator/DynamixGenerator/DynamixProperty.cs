using System;
using System.ComponentModel.DataAnnotations;

namespace DynamixGenerator
{
    public class DynamixProperty
    {
        [Key]
        public virtual Guid Id { get; set; }

        public virtual string Name { get; set; }

        public virtual string DefaultCode { get; set; }

        public virtual Type Type { get; set; }

        public virtual DynamixClass DynamicClass { get; set; }
    }
}
