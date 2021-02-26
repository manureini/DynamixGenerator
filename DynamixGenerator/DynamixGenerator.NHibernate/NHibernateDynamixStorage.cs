using NHibernate;
using System.Collections.Generic;

namespace DynamixGenerator.NHibernate
{
    public class NHibernateDynamixStorage : IDynamixStorage
    {
        protected ISession mSession;

        public NHibernateDynamixStorage(ISession pSession)
        {
            mSession = pSession;
        }

        public IEnumerable<DynamixClass> GetDynamixClasses()
        {
            return mSession.Query<DynamixClass>();
        }

        public void UpdateDynamixClass(DynamixClass pDynamixClass)
        {
            mSession.SaveOrUpdate(pDynamixClass);
        }
    }
}
