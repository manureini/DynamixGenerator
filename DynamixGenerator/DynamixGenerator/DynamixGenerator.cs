using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynamixGenerator
{
    public static class DynamixGenerator
    {
        public static string GenerateCode(IEnumerable<DynamixClass> pDynamixClasses)
        {
            StringBuilder sb = new StringBuilder();

            foreach (var dynClass in pDynamixClasses)
            {
                sb.AppendLine($"namespace {dynClass.Namespace}");
                sb.AppendLine("{"); //namespace

                sb.AppendLine("[global::System.CodeDom.Compiler.GeneratedCode(\"Dynamix\", \"1.0\")]");
                sb.AppendLine($"public class {dynClass.Name}");
                sb.AppendLine("{"); //class

                foreach (var property in dynClass.Properties)
                {
                    sb.AppendLine($@"public global::{property.Type.FullName} {property.Name} {{ get; set; }}");

                    if (property.DefaultCode != null)
                    {
                        sb.Append(" = ");
                        sb.Append(property.DefaultCode);
                        sb.Append(";");
                    }
                }

                sb.AppendLine("}"); //class
                sb.AppendLine("}"); //namespace

                sb.AppendLine();
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
