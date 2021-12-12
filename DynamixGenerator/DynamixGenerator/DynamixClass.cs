using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DynamixGenerator
{
    public class DynamixClass
    {
        public const string DYNAMIX_DEFAULT_NAMESPACE_PREFIX = "DynamixGenerated";

        protected Type mTypeReference;

        public virtual Guid Id { get; set; }

        public virtual string Name { get; set; }

        public virtual string Namespace { get; set; } = DYNAMIX_DEFAULT_NAMESPACE_PREFIX;

        public virtual string FullName
        {
            get
            {
                return Namespace + "." + Name;
            }
            set
            {
            }
        }

        public virtual string InheritsFrom { get; set; }

        public virtual string Implements { get; set; }

        public virtual ICollection<DynamixProperty> Properties { get; set; }

        public virtual Type GetTypeReference()
        {
            if (mTypeReference != null)
                return mTypeReference;

            return new DynamixType(Id, Namespace, Name);
        }

        public virtual void UpdateTypeReference(Assembly pAssembly)
        {
            mTypeReference = pAssembly.GetTypes().First(c =>
            {
                var attr = c.GetCustomAttribute<DynamixId>();
                if (attr == null)
                    return false;

                return attr.Id == Id;
            });

            Namespace = mTypeReference.Namespace;

            foreach (var property in Properties)
            {
                property.UpdateTypeReferenceFromClass(mTypeReference);
            }
        }
    }
}
