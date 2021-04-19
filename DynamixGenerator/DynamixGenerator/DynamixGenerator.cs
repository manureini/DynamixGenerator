using System.Collections.Generic;
using System.Text;

namespace DynamixGenerator
{
    public static class DynamixGenerator
    {
        public static string GenerateCode(string pAssemblyName, IEnumerable<DynamixClass> pDynamixClasses)
        {
            StringBuilder sb = new StringBuilder();

            foreach (var dynClass in pDynamixClasses)
            {
                sb.AppendLine($"namespace {dynClass.Namespace}");
                sb.AppendLine("{"); //namespace

                sb.AppendLine("[global::System.CodeDom.Compiler.GeneratedCode(\"Dynamix\", \"1.0\")]");
                sb.AppendLine($"[global::DynamixGenerator.DynamixId(\"{dynClass.Id}\")]");
                sb.AppendLine($"public class {dynClass.Name}");

                if (!string.IsNullOrEmpty(dynClass.InheritsFrom))
                {
                    sb.AppendLine($" : {dynClass.InheritsFrom}");
                }

                sb.AppendLine("{"); //class
                sb.AppendLine($@"public virtual global::System.Guid Id {{ get; set; }}");

                foreach (var property in dynClass.Properties)
                {
                    string typename = property.GetPropertyTypeName();

                    if (property.IsOneToMany)
                    {
                        typename = $"System.Collections.Generic.ICollection<{typename}>";
                    }

                    sb.AppendLine($@"public virtual global::{typename} {property.Name} {{ get; set; }}");

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
