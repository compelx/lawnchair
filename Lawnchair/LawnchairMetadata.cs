using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lawnchair
{
  class LawnchairMetadata
  {
    public String Author { get; set; }
    public String Category { get; set; }
    public String Comments { get; set; }
    public String Name { get; set; }
    public String ScriptArguments { get; set; }
    public String ScriptExecutor { get; set; }
    public String ScriptRelativePath { get; set; }
    public String ScriptRootPath { get; set; }
    public String[] Tags { get; set; }
    public String Version { get; set; }
  }
}
