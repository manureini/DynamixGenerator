using Microsoft.EntityFrameworkCore;
using System;

namespace DynamixGenerator.EfCore
{
    public interface IDynamixDbContext
    {
        Action<ModelBuilder> OnModelBuildAction { get; set; }
    }
}
