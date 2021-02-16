using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynamixGenerator.Tests
{
    class MemoryDynamixStorage : IDynamixStorage
    {
        protected List<DynamixClass> mClasses = new List<DynamixClass>();

        public void Add(DynamixClass pClass)
        {
            mClasses.Add(pClass);
        }

        public IEnumerable<DynamixClass> GetDynamixClasses()
        {
            return mClasses;
        }

        public void UpdateDynamixClass(DynamixClass pDynamixClass)
        {
            if (!mClasses.Contains(pDynamixClass))
                mClasses.Add(pDynamixClass);
        }
    }
}
