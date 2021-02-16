using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DynamixGenerator
{
    public class DynamixClass
    {
        protected Type mTypeReference;

        public virtual Guid Id { get; set; }

        public virtual string Name { get; set; }

        public virtual string Namespace { get; set; }

        public virtual ICollection<DynamixProperty> Properties { get; set; }

        public Type GetTypeReference()
        {
            if (mTypeReference != null)
                return mTypeReference;

            return new DynamixType(Id, Namespace, Name);
        }

        internal void UpdateTypeReference(Assembly pAssembly)
        {
            mTypeReference = pAssembly.GetTypes().First(c =>
            {
                var attr = c.GetCustomAttribute<DynamixId>();
                if (attr == null)
                    return false;

                return attr.Id == Id;
            });
        }
    }
}
