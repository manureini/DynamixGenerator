using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynamixGenerator
{
    public interface IDynamixStorage
    {
        IEnumerable<DynamixClass> GetDynamixClasses();
        
        void UpdateDynamixClass(DynamixClass pDynamixClass);
    }
}