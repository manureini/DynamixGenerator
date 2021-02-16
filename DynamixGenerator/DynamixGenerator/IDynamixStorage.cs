using System.Collections.Generic;

namespace DynamixGenerator
{
    public interface IDynamixStorage
    {
        IEnumerable<DynamixClass> GetDynamixClasses();
        
        void UpdateDynamixClass(DynamixClass pDynamixClass);
    }
}